# Проверка и запуск

## 1. Уровни проверки

Sprint 11 проверяется на двух уровнях:

1. build и автоматические тесты подтверждают отсутствие регрессий в коде;
2. black-box проверка Docker Compose подтверждает реальный экспорт метрик, трейсов и JSON-логов.

Экспортеры нельзя полноценно проверить только через `WebApplicationFactory`: тестовые фабрики Events и Bookings удаляют `IHostedService`, чтобы изолировать HTTP-тесты от фоновых consumers и внешней инфраструктуры. Поэтому приемка observability обязательно включает запущенный compose-стек.

## 2. Build и тесты

Из корня репозитория:

```bash
dotnet restore EventManagementService.API.sln
dotnet build EventManagementService.API.sln -c Release --no-restore
```

Быстрый прогон без тестов, требующих Docker:

```bash
dotnet test EventManagementService.API.sln \
  -c Release \
  --no-build \
  --filter "Category!=Docker"
```

Полный прогон с диагностикой зависания:

```bash
dotnet test EventManagementService.API.sln \
  -c Release \
  --no-build \
  --blame-hang \
  --blame-hang-timeout 2m
```

Ожидаемый результат текущего набора:

| Набор | Тестов |
|---|---:|
| Fast, `Category!=Docker` | 114 |
| Полный | 131 |
| Bookings | 25 |
| Users | 21 |
| Events | 85 |

`--blame-hang` является защитой от исходного дефекта test-host: HTTP-тесты Events/Bookings не должны оставлять общий прогон зависшим. Любой timeout означает ошибку, а не допустимое поведение тестов.

## 3. Проверка конфигурации Compose

Перед запуском:

```bash
docker compose config --quiet
```

Проверка Prometheus-конфигурации штатным `promtool`:

```bash
docker run --rm \
  -v "$PWD/prometheus.yml:/etc/prometheus/prometheus.yml:ro" \
  prom/prometheus:v2.51.0 \
  promtool check config /etc/prometheus/prometheus.yml
```

Сборка и запуск всего стека:

```bash
docker compose build
docker compose up -d
docker compose ps
```

В состоянии `running` должны находиться 12 контейнеров.

## 4. Доступные интерфейсы

| Компонент | URL | Учетные данные |
|---|---|---|
| Users Swagger | `http://localhost:5101/swagger` | — |
| Events Swagger | `http://localhost:5102/swagger` | — |
| Bookings Swagger | `http://localhost:5103/swagger` | — |
| Prometheus | `http://localhost:9090` | — |
| Jaeger | `http://localhost:16686` | — |
| Grafana | `http://localhost:3000` | `admin` / `admin` |

Порты API следует сверять с актуальным [docker-compose.yml](../../docker-compose.yml), если локальная конфигурация была переопределена.

## 5. Метрики и Prometheus targets

Endpoint каждого сервиса должен отвечать без JWT:

```bash
curl --fail http://localhost:5101/metrics
curl --fail http://localhost:5102/metrics
curl --fail http://localhost:5103/metrics
```

Проверка targets через Prometheus API:

```bash
curl --fail --silent http://localhost:9090/api/v1/targets
```

Для `users-service`, `events-service` и `bookings-service` ожидается `health: "up"`.

Runtime-метрики можно проверить напрямую:

```bash
curl --silent http://localhost:5101/metrics \
  | grep -E 'dotnet_gc_collections_total|dotnet_thread_pool_(thread_count|queue_length)_total'
```

После вызова `/metrics` повторная выборка не должна содержать отдельные временные ряды с `http_route="/metrics"`.

## 6. Генерация пользовательского трафика

Для появления HTTP-метрик и трейсов достаточно вызвать общедоступные endpoints, а затем выполнить один из бизнес-сценариев через Swagger.

```bash
curl --fail http://localhost:5101/swagger/index.html
curl --fail http://localhost:5102/events/top
curl --fail http://localhost:5103/swagger/index.html
```

Для подтверждения SQL-spans нужен запрос, который действительно обращается к PostgreSQL. После выполнения такого запроса подождите несколько секунд на batch-export и откройте Jaeger.

## 7. Проверка трейсов

В Jaeger UI:

1. убедиться, что в списке Service есть `users-service`, `events-service`, `bookings-service`;
2. выбрать сервис и выполнить Find Traces;
3. открыть trace бизнес-запроса;
4. проверить корневой HTTP-спан;
5. проверить дочерний спан EF Core/PostgreSQL;
6. убедиться, что scrape `/metrics` не создает trace.

Через Jaeger API список сервисов проверяется так:

```bash
curl --fail --silent http://localhost:16686/api/services
```

## 8. Проверка JSON-логов

Для каждого API первая непустая строка application log должна разбираться как JSON:

```bash
docker compose logs --no-color users-api
docker compose logs --no-color events-api
docker compose logs --no-color bookings-api
```

У request log проверяются как минимум:

- `service.name`;
- `RequestPath`;
- `StatusCode`;
- `Elapsed`;
- `TraceId` и `SpanId` для инструментированного запроса.

Отдельно вызовите несуществующий route и endpoint, возвращающий ошибку. Статус в JSON-записи должен совпадать с фактическим HTTP-ответом.

Запросы Prometheus к `/metrics` не должны появляться в request logs.

## 9. Проверка Grafana

После входа откройте папку `Event Management` и dashboard `Event Management Observability`. Проверьте:

1. datasource Prometheus имеет состояние working;
2. переменная `$service` содержит три job и значение All;
3. dashboard содержит девять панелей;
4. latency, throughput и active requests получают данные после трафика;
5. 4xx/5xx панели показывают `0`, а не ошибку деления при отсутствии ошибок;
6. GC и Thread Pool панели получают runtime-серии;
7. переключение `$service` меняет выбранный API.

Datasource можно проверить через API Grafana:

```bash
curl --fail --user admin:admin \
  http://localhost:3000/api/datasources/uid/prometheus/health
```

Учетные данные подходят только для локального окружения и не должны переноситься в production.

## 10. Аудит зависимостей

```bash
dotnet list EventManagementService.API.sln package \
  --vulnerable \
  --include-transitive
```

Ожидается отсутствие известных уязвимых пакетов для настроенных NuGet-источников.

## 11. Матрица приемки

| Требование | Проверка | Ожидаемый результат |
|---|---|---|
| OpenTelemetry во всех API | Jaeger services + Prometheus targets | три корректных `service.name` |
| Prometheus собирает `/metrics` | `/api/v1/targets` | три target `UP` |
| HTTP- и SQL-спаны | Jaeger trace | HTTP parent и EF Core child |
| JSON-логи | `docker compose logs` | каждая application-запись — JSON |
| Grafana provisioning | UI/API | datasource и dashboard созданы автоматически |
| `/metrics` не создает шум | метрики, Jaeger, logs | нет self-observation для scrape |
| тесты не зависают | `dotnet test --blame-hang` | 131 тест завершен без timeout |

## 12. Завершение работы

```bash
docker compose down
```

Для удаления также локальных volumes используйте `docker compose down -v` только если сохраненные данные PostgreSQL, Redis и Grafana больше не нужны.

Далее: [диаграммы](06-diagrams.md).
