# Диаграммы

## 1. Полная топология

```mermaid
flowchart LR
    Client[Client / Swagger]

    subgraph APIs[Application services]
        Users[Users API]
        Events[Events API]
        Bookings[Bookings API]
    end

    subgraph Data[Data and messaging]
        UsersDb[(Users PostgreSQL)]
        EventsDb[(Events PostgreSQL)]
        BookingsDb[(Bookings PostgreSQL)]
        Redis[(Redis)]
        Kafka[Kafka]
        Zookeeper[Zookeeper]
    end

    subgraph Observability[Observability stack]
        Prometheus[Prometheus]
        Jaeger[Jaeger]
        Grafana[Grafana]
        DockerLogs[Docker JSON logs]
    end

    Client --> Users
    Client --> Events
    Client --> Bookings

    Users --> UsersDb
    Events --> EventsDb
    Bookings --> BookingsDb
    Events --> Redis
    Bookings --> Kafka
    Kafka --> Events
    Zookeeper --> Kafka

    Prometheus -. GET /metrics .-> Users
    Prometheus -. GET /metrics .-> Events
    Prometheus -. GET /metrics .-> Bookings
    Grafana -->|PromQL| Prometheus

    Users -->|OTLP gRPC| Jaeger
    Events -->|OTLP gRPC| Jaeger
    Bookings -->|OTLP gRPC| Jaeger

    Users -->|stdout JSON| DockerLogs
    Events -->|stdout JSON| DockerLogs
    Bookings -->|stdout JSON| DockerLogs
```

## 2. Pull-модель метрик

```mermaid
sequenceDiagram
    participant P as Prometheus
    participant U as Users API
    participant E as Events API
    participant B as Bookings API
    participant G as Grafana

    loop every 15 seconds
        P->>U: GET /metrics
        U-->>P: OpenMetrics payload
        P->>E: GET /metrics
        E-->>P: OpenMetrics payload
        P->>B: GET /metrics
        B-->>P: OpenMetrics payload
    end

    G->>P: PromQL(job=~"$service")
    P-->>G: time series
```

Prometheus инициирует сбор. API не знают адрес Prometheus и не отправляют ему данные самостоятельно.

## 3. HTTP- и SQL-спаны

```mermaid
sequenceDiagram
    participant C as Client
    participant API as ASP.NET Core API
    participant EF as EF Core
    participant DB as PostgreSQL
    participant J as Jaeger OTLP

    C->>API: HTTP request
    activate API
    Note over API: create server HTTP span
    API->>EF: repository operation
    activate EF
    Note over EF: create child SQL span
    EF->>DB: SQL command
    DB-->>EF: result
    EF-->>API: entities / result
    deactivate EF
    API-->>C: HTTP response
    deactivate API
    API-->>J: batched HTTP + SQL spans
```

Одинаковый trace context связывает HTTP parent со спанами доступа к данным.

## 4. Корреляция логов и трейсов

```mermaid
flowchart LR
    Request[HTTP request]
    Activity[ASP.NET Activity]
    Trace[Jaeger trace]
    Log[Serilog JSON event]
    Search[Incident investigation]

    Request --> Activity
    Activity -->|TraceId + SpanId| Trace
    Activity -->|LogContext| Log
    Trace --> Search
    Log --> Search
```

Оператор находит ошибочный запрос по метрике, открывает trace и затем фильтрует JSON-логи по тому же `TraceId`.

## 5. Ветка `/metrics`

```mermaid
flowchart TD
    Incoming[Incoming HTTP request]
    IsMetrics{Path is /metrics?}

    RequestLog[Serilog request logging]
    ExceptionsNormal[Exception middleware]
    Https[HTTPS redirection]
    Auth[Authentication and authorization]
    Controller[Controller endpoint]

    Metrics[Prometheus scraping endpoint]
    NoTrace[ASP.NET tracing filter: skip]
    ExceptionsMetrics[Exception middleware]
    NoHttpMetric[DisableHttpMetrics]

    Incoming --> IsMetrics
    IsMetrics -->|no| RequestLog
    RequestLog --> ExceptionsNormal
    ExceptionsNormal --> Https
    Https --> Auth
    Auth --> Controller

    IsMetrics -->|yes| NoTrace
    NoTrace --> ExceptionsMetrics
    ExceptionsMetrics --> Metrics
    Metrics --> NoHttpMetric
```

Ветка сохраняет общую обработку ошибок, но исключает HTTPS redirect, request log, trace и самонаблюдение HTTP-метрик.

## 6. Provisioning Grafana

```mermaid
flowchart TD
    Compose[docker compose up]
    DatasourceFile[grafana/provisioning/datasources/prometheus.yml]
    ProviderFile[grafana/provisioning/dashboards/dashboards.yml]
    DashboardFile[grafana/dashboards/event-management-observability.json]
    Datasource[Prometheus datasource<br/>UID: prometheus]
    Provider[Event Management provider]
    Dashboard[Event Management Observability<br/>9 panels]
    Prometheus[Prometheus API]

    Compose --> DatasourceFile
    Compose --> ProviderFile
    Compose --> DashboardFile
    DatasourceFile --> Datasource
    ProviderFile --> Provider
    Provider --> DashboardFile
    DashboardFile --> Dashboard
    Dashboard --> Datasource
    Datasource --> Prometheus
```

Все настройки воспроизводятся из Git; ручные действия после старта контейнера не нужны.

## 7. Деградация monitoring backend

```mermaid
flowchart TD
    Request[Business request]
    API[API]
    Response[Business response]
    Telemetry[Telemetry SDK buffer]
    Backend{Jaeger available?}
    Export[Export spans]
    Drop[Retry / eventual drop]

    Request --> API
    API --> Response
    API -. asynchronous .-> Telemetry
    Telemetry --> Backend
    Backend -->|yes| Export
    Backend -->|no| Drop
```

Экспорт выполняется вне критического пути. Недоступность Jaeger не должна задерживать или отменять бизнес-ответ.

## 8. Цикл диагностики

```mermaid
flowchart LR
    Alert[Unexpected metric]
    Dashboard[Grafana dashboard]
    Trace[Jaeger trace]
    Span[Slow or failed span]
    Logs[JSON logs by TraceId]
    Cause[Root cause]

    Alert --> Dashboard
    Dashboard --> Trace
    Trace --> Span
    Span --> Logs
    Logs --> Cause
```

Назад к [обзору Sprint 11](README.md) или к [общей документации](../README.md).
