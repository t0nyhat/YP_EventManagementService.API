# Введение в sprint 11

## 1. Цель спринта

После декомпозиции на Users, Events и Bookings система стала распределённой. Ошибка одного HTTP-запроса теперь может быть связана с конкретным API, запросом к отдельной PostgreSQL-базе, фоновым обработчиком или внешней инфраструктурой. Одних функциональных тестов недостаточно: во время работы нужно видеть состояние сервисов и уметь связать симптом с его причиной.

Sprint 11 добавляет общий observability-контур с тремя основными сигналами:

| Сигнал | На какой вопрос отвечает | Реализация |
|---|---|---|
| Метрики | Что происходит с системой в целом? | OpenTelemetry Metrics → `/metrics` → Prometheus → Grafana |
| Трейсы | Где запрос потратил время или завершился ошибкой? | OpenTelemetry Traces → OTLP gRPC → Jaeger |
| Логи | Какой диагностический контекст сопровождал операцию? | Serilog Compact JSON → stdout |

## 2. Единый контракт для трёх API

Наблюдаемость полезна только при согласованной семантике. Во всех Presentation-проектах зарегистрирован один и тот же набор instrumentation и exporters, а различается только имя ресурса:

| API | `service.name` | Host endpoint метрик |
|---|---|---|
| Users | `users-service` | `http://localhost:5101/metrics` |
| Events | `events-service` | `http://localhost:5102/metrics` |
| Bookings | `bookings-service` | `http://localhost:5103/metrics` |

Имена используются Jaeger, Serilog и Prometheus job labels. Благодаря этому один dashboard переключается между сервисами через переменную `$service`, а trace и log event можно сопоставить без догадок по имени контейнера.

## 3. Почему метрики собираются по pull-модели

Prometheus сам опрашивает `/metrics`. API не хранит состояние monitoring backend и не обязан знать, доступен ли Prometheus. Если Prometheus временно остановлен, бизнес-запросы продолжают выполняться; после восстановления сбор возобновляется автоматически.

Сам scrape endpoint не должен искажать измерения. Запросы Prometheus приходят каждые 15 секунд, поэтому `/metrics` исключён из:

- ASP.NET Core HTTP metrics;
- ASP.NET Core tracing;
- Serilog request logging;
- HTTPS-redirection pipeline.

При этом endpoint остаётся анонимным и доступным внутри Compose-сети и с host в dev-окружении.

## 4. Почему трейсы отправляются по push-модели

API формирует spans во время выполнения запроса и пакетно отправляет их в Jaeger через OTLP gRPC. Автоматическая инструментация покрывает:

- server span входящего HTTP-запроса;
- client span исходящего `HttpClient`-вызова;
- EF Core/PostgreSQL span обращения к базе данных.

OTLP endpoint задаётся конфигурацией. В Docker это `http://jaeger:4317`, локально — `http://localhost:4317`. Недоступный Jaeger не участвует в обработке бизнес-запроса и не должен превращаться в отказ API.

## 5. Роль структурированных логов

Текстовые строки плохо подходят для машинного поиска: timestamp, severity, path и status приходится извлекать регулярными выражениями. Serilog Compact JSON делает каждую application log line отдельным JSON-объектом.

Обязательный контекст:

- `service.name` — источник события;
- `@t`, `@l`, `@mt` — время, уровень и message template;
- `RequestMethod`, `RequestPath`, `StatusCode`, `Elapsed` — request summary;
- `@tr` и `@sp` — trace/span identifiers при активном `Activity`.

Request logging оборачивает exception middleware. Поэтому доменное исключение, преобразованное в `404`, `409` или другой ожидаемый ответ, записывается с итоговым HTTP status, а не как ложный `500`.

## 6. Что осталось прежним

- Domain и Application не зависят от OpenTelemetry, Prometheus, Jaeger, Grafana или Serilog.
- У каждого микросервиса остаётся собственная PostgreSQL-база.
- Kafka, Outbox, Inbox, DLT, Redis Cache-Aside и JWT продолжают работать по правилам предыдущих спринтов.
- Functional и integration tests не используют observability backend как условие корректности бизнес-логики.
- PostgreSQL остаётся источником истины, Redis — best-effort кешем, Kafka — транспортом асинхронных событий.

## 7. Осознанный scope

В спринт не входят централизованное хранилище логов, alerting rules, distributed trace propagation через Kafka, authentication для monitoring UI и production network policy. Prometheus, Jaeger и Grafana публикуются как dev-инструменты; credentials `admin/admin` не предназначены для production.

---

[Далее: Архитектура observability →](02-architecture.md)
