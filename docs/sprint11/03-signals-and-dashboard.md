# Сигналы и дашборд

## 1. HTTP-метрики

ASP.NET Core instrumentation публикует стандартные метрики HTTP-сервера. Основные серии, используемые дашбордом:

| Метрика | Что показывает |
|---|---|
| `http_server_request_duration_seconds_bucket` | распределение длительности запросов |
| `http_server_request_duration_seconds_count` | количество завершенных запросов |
| `http_server_active_requests` | число запросов, выполняющихся сейчас |

Статус ответа находится в label `http_response_status_code`, а Prometheus добавляет label `job` из scrape-конфигурации. Дашборд фильтрует все выражения по `job=~"$service"`.

## 2. Runtime-метрики .NET

`AddRuntimeInstrumentation()` добавляет метрики CLR. На дашборд вынесены наиболее полезные для первичной диагностики:

| Метрика | Назначение |
|---|---|
| `dotnet_gc_collections_total` | интенсивность сборок мусора по поколениям |
| `dotnet_thread_pool_thread_count_total` | текущее число потоков Thread Pool |
| `dotnet_thread_pool_queue_length_total` | длина очереди работ Thread Pool |

GC-панель помогает увидеть рост давления на память, а сочетание количества потоков и очереди — признаки исчерпания Thread Pool или блокирующих операций.

## 3. Трейсы

Tracing pipeline включает четыре источника:

- ASP.NET Core — входящие HTTP-запросы;
- HttpClient — исходящие HTTP-запросы;
- EF Core — обращения к PostgreSQL;
- Resource — стабильное имя сервиса.

Для типичного запроса к API Jaeger показывает корневой HTTP-спан и вложенные SQL-спаны. EF Core instrumentation позволяет видеть продолжительность доступа к данным без ручного добавления `Activity` в репозитории.

Текущая коммуникация Bookings → Events через Kafka не переносит trace context. Поэтому HTTP- и SQL-операции каждого сервиса видны, но асинхронные части одного бизнес-сценария могут отображаться как разные трейсы. Распространение контекста через Kafka остается отдельным улучшением.

## 4. Структурированные логи

`CompactJsonFormatter` формирует одну JSON-запись на строку. Помимо сообщения и уровня, запись содержит свойства контекста, в том числе:

- `service.name` — источник события;
- `RequestMethod` и `RequestPath` — HTTP-запрос;
- `StatusCode` и `Elapsed` — результат request log;
- `TraceId` и `SpanId` — связь с Jaeger;
- exception — диагностическая информация при ошибке.

Логи не должны содержать пароли, JWT, connection strings или значения SQL-параметров. Наблюдаемость помогает расследованию, но не отменяет требования по минимизации чувствительных данных.

## 5. Исключение `/metrics`

Prometheus обращается к каждому API каждые 15 секунд. Если учитывать эти запросы как пользовательский трафик, они будут:

- увеличивать throughput;
- искажать latency;
- создавать постоянные трейсы;
- заполнять request logs техническими событиями.

Поэтому `/metrics` исключен одновременно из HTTP-метрик, tracing, request logging и HTTPS redirection. Сам endpoint остается доступен анонимно, чтобы Prometheus мог собирать данные без application JWT.

## 6. Prometheus jobs

В [prometheus.yml](../../prometheus.yml) объявлены три статических job:

```yaml
scrape_configs:
  - job_name: users-service
    static_configs:
      - targets: [users-api:8080]

  - job_name: events-service
    static_configs:
      - targets: [events-api:8080]

  - job_name: bookings-service
    static_configs:
      - targets: [bookings-api:8080]
```

Интервал сбора — 15 секунд, timeout — 10 секунд. Страница Prometheus Targets должна показывать состояние `UP` для всех трех job.

## 7. Переменная Grafana `$service`

Один provisioned dashboard обслуживает все API. Template-переменная `$service` получает значения `job` из Prometheus и поддерживает выбор одного, нескольких или всех сервисов.

Datasource закреплен по UID `prometheus`. Это делает JSON дашборда воспроизводимым: после пересоздания контейнеров панели не теряют источник данных и не требуют ручного выбора datasource.

## 8. Панели и запросы

Дашборд содержит девять панелей:

| Панель | PromQL / назначение |
|---|---|
| Latency p50 | `histogram_quantile(0.50, sum by (le) (rate(http_server_request_duration_seconds_bucket{job=~"$service"}[$__rate_interval])))` |
| Latency p95 | тот же histogram query с квантилем `0.95` |
| Latency p99 | тот же histogram query с квантилем `0.99` |
| Throughput | `sum(rate(http_server_request_duration_seconds_count{job=~"$service"}[$__rate_interval]))` |
| Active Requests | `sum(http_server_active_requests{job=~"$service"})` |
| 5xx Error Rate | доля ответов со статусом `5..` |
| 4xx Error Rate | доля ответов со статусом `4..` |
| GC Collections Rate | `sum by (gc_heap_generation) (rate(dotnet_gc_collections_total{job=~"$service"}[$__rate_interval]))` |
| .NET Thread Pool | число потоков и длина очереди Thread Pool |

Error rate вычисляется как отношение частоты ошибочных ответов к частоте всех ответов. Знаменатель защищен:

```promql
clamp_min(
  sum(rate(http_server_request_duration_seconds_count{job=~"$service"}[$__rate_interval])),
  0.000001
)
```

К результату добавлено `or vector(0)`. Поэтому при отсутствии трафика панель показывает корректный ноль вместо деления на ноль или пустого графика.

Панель Thread Pool содержит два PromQL target:

```promql
sum(dotnet_thread_pool_thread_count_total{job=~"$service"})
sum(dotnet_thread_pool_queue_length_total{job=~"$service"})
```

## 9. Интерпретация сигналов

Сигналы полезно читать вместе:

| Наблюдение | Возможная причина | Следующая проверка |
|---|---|---|
| растет p95/p99, throughput стабилен | медленная зависимость или SQL | найти медленный trace и SQL-спан |
| растут 5xx | ошибка приложения или зависимости | отфильтровать JSON-логи по `TraceId` |
| растет очередь Thread Pool | блокирующая работа или перегрузка | сравнить с active requests и latency |
| растет частота GC | повышенное выделение памяти | проверить нагрузку и runtime-профиль |
| target Prometheus в состоянии DOWN | API или сеть недоступны | проверить health endpoint и compose logs |

Метрики показывают наличие проблемы, трейсы локализуют задержку, а логи объясняют конкретную ошибку. Ценность observability достигается именно совместным использованием трех сигналов.

Далее: [реализация](04-implementation.md).
