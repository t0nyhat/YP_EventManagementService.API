# Sprint 11: Документация

Полная учебная документация по спринту 11: единая наблюдаемость трёх микросервисов через OpenTelemetry, Prometheus, Jaeger, Serilog и Grafana.

## Структура документации

1. **[01-introduction.md](01-introduction.md)** — Обзор спринта, цели и мотивация
   - Зачем распределённой системе нужны метрики, трейсы и структурированные логи
   - Почему сигналы собираются одинаково во всех трёх API
   - Что изменилось относительно sprint 10

2. **[02-architecture.md](02-architecture.md)** — Архитектура observability
   - Потоки pull-метрик, push-трейсов и stdout-логов
   - Границы Clean Architecture и роль Presentation
   - Service identity, конфигурация и runtime-топология

3. **[03-signals-and-dashboard.md](03-signals-and-dashboard.md)** — Сигналы и дашборд
   - HTTP- и .NET Runtime-метрики
   - HTTP + EF Core/PostgreSQL spans и корреляция логов
   - PromQL девяти Grafana-панелей и template-переменная `$service`
   - Исключение `/metrics` из собственной телеметрии

4. **[04-implementation.md](04-implementation.md)** — Реализация в коде
   - NuGet-зависимости и симметричный composition root
   - Валидация `service.name` и OTLP endpoint
   - Middleware order, Docker Compose и provisioning Grafana

5. **[05-testing-and-run.md](05-testing-and-run.md)** — Тестирование и запуск
   - Release build, быстрый и полный Testcontainers-прогон
   - Проверка `/metrics`, Prometheus targets, Jaeger traces и JSON logs
   - Проверка datasource, dashboard и всех PromQL targets

6. **[06-diagrams.md](06-diagrams.md)** — Диаграммы
   - Полная observability-топология
   - Сбор метрик, экспорт трейсов и обработка логов
   - Ветвление pipeline для `/metrics` и provisioning Grafana

## Как читать эту документацию

### Для обзора и защиты решения

1. [01-introduction.md](01-introduction.md)
2. [02-architecture.md](02-architecture.md)
3. [06-diagrams.md](06-diagrams.md)

### Для работы с кодом

1. [03-signals-and-dashboard.md](03-signals-and-dashboard.md)
2. [04-implementation.md](04-implementation.md)
3. [05-testing-and-run.md](05-testing-and-run.md)

### Для сверки с требованиями

1. [sprint11-task.md](sprint11-task.md)
2. [03-signals-and-dashboard.md](03-signals-and-dashboard.md)
3. [05-testing-and-run.md](05-testing-and-run.md)

## Что принципиально изменилось относительно sprint 10

- Users, Events и Bookings получили одинаковый OpenTelemetry pipeline с уникальными именами ресурсов.
- Каждый API публикует анонимный `/metrics`; Prometheus опрашивает три target каждые 15 секунд.
- Входящие HTTP-запросы, исходящие HTTP-вызовы и EF Core-команды создают spans; трейсы отправляются в Jaeger по OTLP gRPC.
- Стандартный console logging заменён Serilog Compact JSON; события содержат `service.name`, а при активном `Activity` — trace/span correlation.
- В Compose добавлены Prometheus, Jaeger и Grafana; observability backend не является жёсткой зависимостью запуска API.
- Grafana datasource и dashboard создаются из файлов. Девять панелей работают для одного или нескольких сервисов через `$service`.
- `/metrics` исключён из HTTP-метрик, trace export, request logging и HTTPS-redirection, поэтому monitoring traffic не зашумляет собственные сигналы.
- Конфигурация `OpenTelemetry:ServiceName` и `Otlp:Endpoint` проверяется до запуска приложения.
- Docker runtime содержит GSSAPI library, поэтому весь поток логов API остаётся newline-delimited JSON.

---

[Назад к документации по спринтам](../README.md)
