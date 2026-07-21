# EventManagementService

REST API для управления событиями и бронированиями, разделённая на три независимых микросервиса с асинхронным обменом через Apache Kafka.

## Состав системы

Система состоит из трёх микросервисов, каждый со своей базой данных PostgreSQL и зоной ответственности:

| Сервис | Назначение | База данных | Порт (host) |
|--------|-----------|-------------|-------------|
| **Users** | Регистрация, вход, хеширование паролей, выдача JWT | `users_db` | `5101` |
| **Events** | CRUD событий, топ-10 событий, учёт доступных мест | `events_db` | `5102` |
| **Bookings** | Создание и отмена броней, подтверждение через outbox | `bookings_db` | `5103` |

Сервис **Events** дополнительно использует **Redis** как best-effort кеш чтения для `GET /events/{id}` и `GET /events/top` — детали в разделе [«Кеширование (Events)»](#кеширование-events).

Все сервисы построены по принципам **Clean Architecture** с четырьмя слоями:

```text
Domain  ←  Application  ←  Infrastructure  ←  Presentation
```

- **Domain** — сущности, перечисления, доменные исключения. Не зависит от фреймворков.
- **Application** — use cases, DTO, валидация, порты репозиториев и сервисов. Зависит только от Domain.
- **Infrastructure** — EF Core DbContext, миграции, репозитории, Kafka producer/consumer. Зависит от Application и Domain.
- **Presentation** — контроллеры, middleware, Swagger, composition root. Зависит от Application и Infrastructure.

### Общий проект контрактов

`src/EventManagementService.Contracts/` — разделяемая библиотека без внешних зависимостей, содержащая:

- [`KafkaTopics`](src/EventManagementService.Contracts/BookingConfirmed.cs:3) — константы имён топиков (`booking-confirmed`).
- [`BookingConfirmed`](src/EventManagementService.Contracts/BookingConfirmed.cs:8) — record-контракт события: `BookingId`, `EventId`, `UserId`, `Seats`, `ConfirmedAtUtc`.

Сериализация — `System.Text.Json` с `JsonSerializerDefaults.Web`.

## Архитектура взаимодействия

Сервисы **не вызывают друг друга напрямую по HTTP**. Единственный канал межсервисного обмена — **Apache Kafka**.

```text
┌──────────┐    JWT   ┌──────────┐    JWT   ┌──────────┐
│  Users   │ ◄─────── │  Events  │ ◄─────── │ Bookings │
│  (auth)  │          │  (CRUD)  │          │ (брони)  │
└──────────┘          └─┬──┬─────┘          └─────┬────┘
                        │  │                      │
        ┌─────────┐     │  │  ┌──────────────┐    │
        │  Redis  │ ◄───┘  │  │    Kafka     │    │
        │  (кеш)  │        │  │ booking-     │ ◄──┘
        └─────────┘        └─►│ confirmed    │
                              │ (topic)      │
                              └──────────────┘
```

Redis — приватная инфраструктура сервиса Events (кеш чтения), а не канал межсервисного обмена.

### Поток BookingConfirmed

1. Пользователь создаёт бронь через `POST /events/{id}/book` в сервисе **Bookings**.
2. Бронь сохраняется со статусом `Pending`.
3. Фоновый сервис [`BookingProcessingBackgroundService`](src/EventManagementService.Bookings.Infrastructure/BackgroundServices/BookingProcessingBackgroundService.cs) периодически выбирает `Pending`-брони и вызывает [`BookingProcessingService.ProcessPendingBookingAsync`](src/EventManagementService.Bookings.Application/Services/BookingProcessingService.cs).
4. При подтверждении брони:
   - Статус меняется на `Confirmed`.
   - В таблицу `booking_outbox` сохраняется сообщение `BookingConfirmed`.
   - Обе операции — в одной транзакции.
5. Фоновый сервис [`BookingOutboxPublisherBackgroundService`](src/EventManagementService.Bookings.Infrastructure/Messaging/BookingOutboxPublisherBackgroundService.cs) публикует непрочитанные сообщения из outbox в топик `booking-confirmed`.
   - Ключ сообщения — `EventId.ToString("D")` (гарантирует порядок обработки для одного события).
   - После успешной публикации outbox-строка помечается как опубликованная.
6. Сервис **Events** через [`BookingConfirmedConsumerService`](src/EventManagementService.Events.Infrastructure/Messaging/BookingConfirmedConsumerService.cs) читает сообщения из топика.
7. [`BookingConfirmedHandler`](src/EventManagementService.Events.Infrastructure/Messaging/BookingConfirmedHandler.cs) обрабатывает событие:
   - Если `booking_id` уже есть в inbox — **no-op** (идемпотентность).
   - Если событие не найдено — логирует warning и записывает inbox с результатом `EventNotFound`.
   - Если событие уже началось — логирует warning и записывает inbox с результатом `EventAlreadyStarted`.
   - Если недостаточно мест — логирует warning и записывает inbox с результатом `NotEnoughSeats`.
   - Если всё корректно — уменьшает `available_seats` и сохраняет inbox-строку в одной транзакции.
   - При неожиданной ошибке (например, недоступна БД) консюмер делает `Seek` на упавший оффсет и повторяет сообщение до `Kafka:MaxHandlerAttempts` раз (по умолчанию 5) — подтверждённые брони не теряются.
   - Сообщение, которое невозможно обработать в принципе (битый JSON, `Seats <= 0`), а также сообщение, исчерпавшее лимит попыток, публикуется в **Dead Letter Topic** `booking-confirmed.DLT` с исходным payload в `Value` и диагностикой в заголовках (`error-reason`, `error-source-topic/partition/offset`, `error-timestamp`) — партиция не блокируется навсегда одним «отравленным» сообщением.

Опубликованные outbox-строки старше 7 дней периодически удаляются фоновым сервисом; inbox-строки не удаляются — они хранят историю идемпотентности.

### Осознанные ограничения (eventual consistency)

- Bookings **не проверяет** существование события, дату начала и наличие мест при создании брони — это ответственность Events при обработке `BookingConfirmed`. Если событие не найдено, уже началось или мест нет, бронь в Bookings **остаётся `Confirmed`**: компенсирующего события в рамках спринта 9 нет (задание требует только `BookingConfirmed`).
- Отмена брони — локальная операция Bookings: место в Events **не возвращается** (событие `BookingCancelled` не входит в рамки спринта).

### Поток JWT

1. **Users** — единственный сервис, выпускающий токены (`POST /auth/login`).
2. Токен содержит claims: `NameIdentifier`, `Name`, `Role`, `sub`, `unique_name`.
3. **Events** и **Bookings** проверяют тот же токен через общие параметры (`Issuer`, `Audience`, `SigningKey`).
4. Ролевая модель:
   - `Admin` — управление событиями (создание, обновление, удаление), просмотр и отмена любых броней.
   - `User` — создание броней, просмотр и отмена только своих броней.

## Технологии

- .NET SDK 10+
- ASP.NET Core Web API
- Entity Framework Core + Npgsql
- Apache Kafka (Confluent.Kafka)
- Redis 7.2 (StackExchange.Redis) — кеш чтения в сервисе Events
- JWT Bearer Authentication
- PostgreSQL 17
- xUnit v3 + FluentAssertions
- Testcontainers для интеграционных тестов

## Наблюдаемость (Observability)

Все три микросервиса инструментированы единым стеком OpenTelemetry и Serilog для сбора метрик, трейсов и структурированных логов.

### Три сигнала

| Сигнал | Назначение | Инструмент |
|--------|-----------|------------|
| **Метрики** | HTTP latency, throughput, error rate, активные запросы, .NET Runtime (GC, memory) | OpenTelemetry → Prometheus |
| **Трейсы** | HTTP-запросы, вызовы EF Core/PostgreSQL, исходящие HTTP-вызовы | OpenTelemetry → OTLP → Jaeger |
| **Логи** | Структурированные JSON-логи с SourceContext, service.name и trace/span ID | Serilog → Compact JSON → stdout |

### Путь данных

```text
┌─────────────┐     /metrics      ┌────────────┐     PromQL      ┌──────────┐
│  Users API  │ ────────────────► │ Prometheus │ ◄────────────── │  Grafana │
│  Events API │ ────────────────► │ :9090      │                 │  :3000   │
│ Bookings API│ ────────────────► └────────────┘                 │ dashboard│
└──────┬──────┘                                                  └──────────┘
       │
       │ OTLP gRPC :4317
       ▼
┌──────────────┐
│    Jaeger    │
│   :16686     │
│  trace UI    │
└──────────────┘

Все API → stdout → Compact JSON (docker compose logs | jq)
```

### Состав стека

| Компонент | Образ | Назначение |
|-----------|-------|------------|
| Prometheus | `prom/prometheus:v2.51.0` | Хранилище и query-движок метрик |
| Jaeger | `jaegertracing/all-in-one:1.56` | Приём и визуализация трейсов (OTLP gRPC) |
| Grafana | `grafana/grafana:10.4.2` | Визуализация метрик через provisioned dashboard |

### Service names

| Сервис | `service.name` в OpenTelemetry |
|--------|-------------------------------|
| Users | `users-service` |
| Events | `events-service` |
| Bookings | `bookings-service` |

### Известные ограничения

- Kafka-поток между Bookings и Events **не продолжает HTTP trace**: OpenTelemetry Kafka instrumentation не входит в Sprint 11. Трейс обрывается на границе async-публикации outbox.
- Исходящие HTTP-вызовы между сервисами отсутствуют — `AddHttpClientInstrumentation()` добавлена на перспективу.
- `/metrics` endpoint публичный (dev-only). В production требуется ограничение сетевыми политиками или reverse proxy.

## Требования

1. .NET SDK 10+
2. Docker (для запуска PostgreSQL, Kafka, Redis и сервисов)

## Быстрый старт

### 1. Запустить полный стек

```bash
docker compose up --build -d
```

Проверка статуса:

```bash
docker compose ps
```

Все сервисы применяют миграции EF Core автоматически при запуске.

### 2. Swagger UI

| Сервис | URL |
|--------|-----|
| Users | `http://localhost:5101/swagger` |
| Events | `http://localhost:5102/swagger` |
| Bookings | `http://localhost:5103/swagger` |

Для защищённых эндпоинтов нажмите `Authorize` и передайте токен в формате `Bearer <jwt>`.

### 3. Observability — адреса

| Компонент | Адрес с host | Назначение |
|-----------|-------------|------------|
| Users metrics | `http://localhost:5101/metrics` | Prometheus text format |
| Events metrics | `http://localhost:5102/metrics` | Prometheus text format |
| Bookings metrics | `http://localhost:5103/metrics` | Prometheus text format |
| Prometheus | `http://localhost:9090` | Query UI и targets |
| Jaeger | `http://localhost:16686` | Trace search и визуализация |
| OTLP gRPC | `localhost:4317` | Технический ingest port (внутренний) |
| Grafana | `http://localhost:3000` | Dashboard (admin/admin, dev-only) |

Grafana dashboard provisioned автоматически: папка **Event Management**, datasource UID `prometheus`.

### 4. Запустить тесты

```bash
dotnet test EventManagementService.API.sln
```

Полный прогон включает интеграционные тесты Events и Users: они поднимают PostgreSQL через Testcontainers и требуют запущенный Docker. Чтобы без Docker запустить unit-тесты и остальные тесты, исключите сценарии с `[Trait("Category", "RequiresDocker")]`:

```bash
dotnet test EventManagementService.API.sln --filter "Category!=RequiresDocker"
```

## Эндпоинты

### Users (`http://localhost:5101`)

| Метод | Путь | Аутентификация | Описание |
|-------|------|---------------|----------|
| `POST` | `/auth/register` | Нет | Регистрация пользователя |
| `POST` | `/auth/login` | Нет | Вход, получение JWT |

Тело `POST /auth/register`:

```json
{
  "login": "user",
  "password": "securePass123",
  "role": "User"
}
```

- `role` опциональна, по умолчанию `User`.
- Успех: `204 No Content`.
- Дубликат логина: `400 Bad Request`.

Тело `POST /auth/login`:

```json
{
  "login": "user",
  "password": "securePass123"
}
```

- Успех: `200 OK` с `{ "token": "..." }`.
- Неверные данные: `404 Not Found` (одинаковое сообщение для любого неверного ввода).

### Events (`http://localhost:5102`)

| Метод | Путь | Аутентификация | Описание |
|-------|------|---------------|----------|
| `GET` | `/events` | Нет | Список событий с фильтрацией и пагинацией |
| `GET` | `/events/top` | Нет | Топ до 10 событий по доле проданных мест (кеш, до 1 минуты) |
| `GET` | `/events/{id}` | Нет | Событие по ID |
| `POST` | `/events` | Admin | Создать событие |
| `PUT` | `/events/{id}` | Admin | Обновить событие |
| `DELETE` | `/events/{id}` | Admin | Удалить событие |

Параметры фильтрации `GET /events`:

- `title` — поиск по названию, регистронезависимый.
- `from` — не раньше указанной даты.
- `to` — не позже указанной даты.
- `page` — номер страницы (по умолчанию `1`).
- `pageSize` — размер страницы (по умолчанию `10`, макс. `100`).

`GET /events/top` возвращает до 10 событий, отсортированных по доле проданных мест — `(totalSeats - availableSeats) / totalSeats`. Рейтинг считается на стороне PostgreSQL с дробным делением; при равных долях порядок детерминирован: больше проданных мест, затем раньше `startAt`, затем меньше `id`. Ответ кешируется на 1 минуту и может отставать от актуальных данных (см. [«Кеширование (Events)»](#кеширование-events)).

Тело `POST /events`:

```json
{
  "title": "Конференция .NET",
  "description": "Технологическое мероприятие",
  "startAt": "2026-07-10T10:00:00",
  "endAt": "2026-07-10T18:00:00",
  "totalSeats": 50
}
```

`PUT /events/{id}` — без поля `totalSeats` (вместимость не меняется при обновлении).

### Bookings (`http://localhost:5103`)

| Метод | Путь | Аутентификация | Описание |
|-------|------|---------------|----------|
| `POST` | `/events/{eventId}/book` | Требуется | Создать бронь |
| `GET` | `/bookings/{id}` | Требуется | Статус брони (владелец или Admin) |
| `DELETE` | `/bookings/{id}` | Требуется | Отмена брони (владелец или Admin) |

`POST /events/{eventId}/book`:
- Тело не требуется, `eventId` в URL.
- Возвращает `202 Accepted` с телом брони и заголовком `Location: /bookings/{bookingId}`.

Статусы брони (`BookingStatus`):

| Статус | Описание |
|--------|----------|
| `Pending` | Создана, ожидает обработки |
| `Confirmed` | Подтверждена |
| `Rejected` | Отклонена |
| `Cancelled` | Отменена пользователем или администратором |

Пример ответа:

```json
{
  "id": "5b178c2f-247d-4e6f-bf64-c40aeb9f95ef",
  "eventId": "0c6bbd2b-4f64-4fb9-8d73-dcd7f6f36611",
  "status": "Pending",
  "createdAt": "2026-07-07T12:00:00Z",
  "processedAt": null
}
```

## Конфигурация

### Переменные окружения (Docker Compose)

| Переменная | Описание | Пример |
|-----------|----------|--------|
| `ConnectionStrings__DefaultConnection` | Строка подключения к PostgreSQL | `Host=users-db;Port=5432;Database=users_db;...` |
| `Jwt__Issuer` | Издатель токена | `EventManagementService.API` |
| `Jwt__Audience` | Аудитория токена | `EventManagementService.API` |
| `Jwt__SigningKey` | Секретный ключ (мин. 32 байта) | — |
| `Jwt__LifetimeMinutes` | Время жизни токена (только Users — он выпускает токены) | `60` |
| `Kafka__BootstrapServers` | Адрес Kafka-брокера | `kafka:9092` |
| `Kafka__ConsumerGroup` | Группа потребителей (только Events) | `events-service` |
| `Kafka__MaxHandlerAttempts` | Попыток обработки перед отправкой в Dead Letter Topic (только Events) | `5` |
| `Redis__ConnectionString` | Адрес Redis (только Events); пустое значение — ошибка конфигурации, сервис не стартует | `redis:6379` |
| `Cache__EventTtl` | TTL кеша события `event:{id}` (только Events; в compose не задаётся — действует дефолт из `appsettings.json`) | `00:10:00` |
| `Cache__TopEventsTtl` | TTL кеша топ-10 `events:top10` (только Events; в compose не задаётся) | `00:01:00` |

### appsettings.json

Локальный запуск без Docker использует `appsettings.json` и `appsettings.Development.json` в каждом Presentation-проекте.

## Обработка ошибок

Все сервисы возвращают ошибки в формате `ProblemDetails` (`application/problem+json`).

| Код | Описание |
|-----|----------|
| `400 Bad Request` | Ошибки валидации |
| `401 Unauthorized` | Отсутствует или невалидный токен |
| `403 Forbidden` | Недостаточно прав |
| `404 Not Found` | Ресурс не найден или неверные учётные данные |
| `409 Conflict` | Бизнес-конфликты (нет мест, лимит броней, повторная обработка) |
| `500 Internal Server Error` | Непредвиденная ошибка |

## Валидация

- `title` — обязателен, не пустой.
- `endAt` — должен быть позже `startAt`.
- `totalSeats` — больше нуля.
- `from` — не позже `to`.
- `page` — не меньше `1`.
- `pageSize` — от `1` до `100`.
- Лимит активных бронирований на пользователя — `10`.

## Структура проекта

```text
EventManagementService.API.sln
src/
  EventManagementService.Contracts/        # Общий контракт (KafkaTopics, BookingConfirmed)
  EventManagementService.Users.Domain/
  EventManagementService.Users.Application/
  EventManagementService.Users.Infrastructure/
  EventManagementService.Users.Presentation/
  EventManagementService.Events.Domain/
  EventManagementService.Events.Application/
  EventManagementService.Events.Infrastructure/
  EventManagementService.Events.Presentation/
  EventManagementService.Bookings.Domain/
  EventManagementService.Bookings.Application/
  EventManagementService.Bookings.Infrastructure/
  EventManagementService.Bookings.Presentation/
tests/
  EventManagementService.Users.Tests/
  EventManagementService.Events.Tests/
  EventManagementService.Bookings.Tests/
docs/
```

## E2E-сценарий проверки

1. Запустить полный стек:
   ```bash
   docker compose up --build -d
   ```

2. Зарегистрировать администратора через Users:
   ```bash
   curl -X POST http://localhost:5101/auth/register \
     -H "Content-Type: application/json" \
     -d '{"login":"admin","password":"admin123","role":"Admin"}'
   ```

3. Получить токен администратора:
   ```bash
   curl -X POST http://localhost:5101/auth/login \
     -H "Content-Type: application/json" \
     -d '{"login":"admin","password":"admin123"}'
   ```

4. Создать событие через Events (с токеном Admin):
   ```bash
   curl -X POST http://localhost:5102/events \
     -H "Content-Type: application/json" \
     -H "Authorization: Bearer <token>" \
     -d '{"title":"Конференция","description":"Описание","startAt":"2026-07-10T10:00:00","endAt":"2026-07-10T18:00:00","totalSeats":3}'
   ```

5. Зарегистрировать обычного пользователя и получить токен.

6. Создать бронь через Bookings:
   ```bash
   curl -X POST http://localhost:5103/events/{eventId}/book \
     -H "Authorization: Bearer <user-token>"
   ```

7. Дождаться подтверждения (несколько секунд) и проверить статус:
   ```bash
   curl http://localhost:5103/bookings/{bookingId} \
     -H "Authorization: Bearer <user-token>"
   ```

8. Проверить, что `availableSeats` уменьшилось в Events:
   ```bash
   curl http://localhost:5102/events/{eventId}
   ```

9. Проверить кеш топ-10 в Redis (порт Redis на host не публикуется — доступ только через `docker exec`):
   ```bash
   curl http://localhost:5102/events/top

   docker exec eventapi_redis redis-cli KEYS '*'
   docker exec eventapi_redis redis-cli TTL events:top10
   docker exec eventapi_redis redis-cli GET events:top10
   ```

10. Проверить degraded mode — API отвечает и без Redis:
    ```bash
    docker compose stop redis
    curl http://localhost:5102/events/top   # по-прежнему 200, данные из PostgreSQL
    docker compose start redis
    ```

11. Проверить инвалидацию `event:{id}` (токен Admin — из шага 3):
    ```bash
    # прогреть кеш события
    curl http://localhost:5102/events/{eventId}
    docker exec eventapi_redis redis-cli KEYS 'event:*'   # ключ event:{eventId} появился

    # обновить событие — ключ удаляется после успешного сохранения
    curl -X PUT http://localhost:5102/events/{eventId} \
      -H "Content-Type: application/json" \
      -H "Authorization: Bearer <token>" \
      -d '{"title":"Конференция (обновлено)","description":"Описание","startAt":"2026-07-10T10:00:00","endAt":"2026-07-10T18:00:00"}'

    docker exec eventapi_redis redis-cli KEYS 'event:*'   # ключа больше нет
    ```
    `DELETE /events/{id}` инвалидирует ключ так же — после успешного сохранения.

### 12. Smoke-сценарий observability

После запуска полного стека (`docker compose up --build -d`) и генерации бизнес-трафика (шаги 1–8) можно проверить наблюдаемость:

**Метрики каждого API:**

```bash
curl -fsS http://localhost:5101/metrics | head -20
curl -fsS http://localhost:5102/metrics | head -20
curl -fsS http://localhost:5103/metrics | head -20
```

**Prometheus targets (все три должны быть `UP`):**

```bash
curl -fsS "http://localhost:9090/api/v1/targets?state=active" | jq '.data.activeTargets[] | {job: .labels.job, health: .health}'
```

**Jaeger service names (после bounded ожидания batch export):**

```bash
curl -fsS http://localhost:16686/api/services | jq .
```

**Проверка HTTP и SQL трейсов Events:**

```bash
curl -fsS "http://localhost:16686/api/traces?service=events-service&limit=5" | jq '.data[].spans[] | {operationName, spanKind, tags: [.tags[] | select(.key == "http.response.status_code" or .key == "db.system")]}'
```

**JSON-логи (каждая строка должна парситься `jq`):**

```bash
docker compose logs --no-color --no-log-prefix events-api | tail -5 | jq .
```

**Grafana health и provisioned datasource:**

```bash
curl -fsS http://localhost:3000/api/health | jq -e '.database == "ok"'
curl -fsS -u admin:admin http://localhost:3000/api/datasources/uid/prometheus | jq '.name'
```

**Проверка отсутствия секретов в логах (пароль из smoke-запроса не должен появляться):**

```bash
docker compose logs --no-color --no-log-prefix users-api | grep -c "missing-password" || echo "PASS: no secrets in logs"
```

## Идемпотентность и отказоустойчивость

- **Outbox** в Bookings: сообщение сначала сохраняется в БД, потом публикуется. При сбое Kafka публикация повторяется.
- **Inbox** в Events: дубликаты `BookingConfirmed` не уменьшают места дважды (unique `booking_id`).
- **Ретраи**: outbox publisher увеличивает счётчик попыток и сохраняет `last_error` при неудаче.
- **Обработка ошибок Kafka**: bad JSON логируется и пропускается, сервис не падает.
- **Топик создаётся автоматически** при старте Events через [`KafkaTopicInitializer`](src/EventManagementService.Events.Infrastructure/Messaging/KafkaTopicInitializer.cs).
- **Деградация кеша** в Events: Redis — best-effort. При недоступном Redis чтение трактуется как промах, запись/удаление — no-op с логированием, API продолжает обслуживать запросы из PostgreSQL (см. [«Кеширование (Events)»](#кеширование-events)).

## Кеширование (Events)

Сервис Events кеширует в Redis два read-пути: `GET /events/{id}` и `GET /events/top`. Кешируется DTO ответа (`EventResponse`), а не доменная сущность; payload — JSON с централизованными настройками `CacheJson.Options` на основе `JsonSerializerDefaults.Web` (camelCase). Если контракт ответа изменится между версиями API, формат сериализации кеша настраивается в одном месте. Кеш **best-effort**: его недоступность никогда не ломает бизнес-логику (см. «Деградация» ниже).

| Ключ | Payload | TTL | Инвалидация |
|------|---------|-----|-------------|
| `event:{id}` (GUID в формате `D`, lowercase) | `EventResponse` (JSON) | 10 мин (`Cache:EventTtl`) | Активная: удаление после каждой успешной записи (CRUD и Kafka); TTL — страховка на случай пропущенной инвалидации |
| `events:top10` | Массив `EventResponse` (JSON) | 1 мин (`Cache:TopEventsTtl`) | Только по TTL — bounded staleness до 1 минуты |

Форматы ключей задаются в одном месте — [`EventCacheKeys`](src/EventManagementService.Events.Application/Caching/EventCacheKeys.cs).

### Чтение — Cache-Aside

[`EventService`](src/EventManagementService.Events.Application/Services/EventService.cs) сначала спрашивает кеш; при промахе читает PostgreSQL и best-effort записывает результат обратно.

- `404` не кешируется — в кеш попадает только найденное событие.
- Пустой топ — валидный результат: кешируется как `[]`, а не трактуется как промах.
- Рейтинг топ-10 считается на стороне PostgreSQL: доля проданных мест `(TotalSeats - AvailableSeats) / TotalSeats` с дробным делением; при равенстве — детерминированные tie-breakers (проданных мест по убыванию, затем `StartAt`, затем `Id`); максимум 10 записей.

### Запись — инвалидация вместо write-through

Все write-пути (`POST`/`PUT`/`DELETE`) работают по одной схеме: **сначала успешный `SaveChanges`, потом удаление `event:{id}`**. Удаление вместо перезаписи выбрано осознанно: оно идемпотентно и не может «опередить» БД — если транзакция откатилась, до удаления дело не доходит и кеш не расходится с базой; write-through при откате оставил бы в кеше данные, которых в БД нет. Следующий читатель просто наполнит кеш заново. Для `POST` инвалидация нового id защитная (под свежим id ничего лежать не должно, т.к. `404` не кешируется), но единое правило для всех write-путей проще проверять. Ключ `events:top10` write-пути не трогают — он истекает только по TTL.

### Инвалидация из Kafka

[`BookingConfirmedHandler`](src/EventManagementService.Events.Infrastructure/Messaging/BookingConfirmedHandler.cs) после успешного commit транзакции Event+Inbox удаляет `event:{eventId}`. Удаление выполняется с `CancellationToken.None`: post-commit инвалидация не должна отменяться при shutdown — иначе переотправленное сообщение было бы пропущено как дубликат (этот путь кеш не трогает), и stale-запись жила бы до конца TTL. Пути «дубликат», `EventNotFound`, `EventAlreadyStarted`, `NotEnoughSeats` кеш не трогают — данные события не менялись.

### Выбор TTL

- **10 минут для `event:{id}`** — ключ активно инвалидируется всеми write-путями, поэтому TTL лишь ограничивает жизнь stale-записи после пропущенной инвалидации.
- **1 минута для `events:top10`** — топ никем явно не инвалидируется, TTL напрямую задаёт максимальное отставание списка от БД.

### Деградация (Redis недоступен)

- Один `IConnectionMultiplexer` на процесс (singleton) с `AbortOnConnectFail=false` — API стартует и отвечает даже при недоступном Redis, мультиплексор переподключается в фоне.
- Ошибки кеша в [`RedisCacheService`](src/EventManagementService.Events.Infrastructure/Caching/RedisCacheService.cs) логируются и деградируют: чтение → промах, запись/удаление → no-op; все запросы обслуживаются из PostgreSQL.
- Повреждённый JSON в кеше — промах + best-effort удаление битой записи.
- `OperationCanceledException` не маскируется и доходит до вызывающего кода.
- Пустая `Redis:ConnectionString` — ошибка конфигурации: сервис не стартует (`ValidateOnStart`). Недоступный сервер — штатный degraded mode.
- Поведение закреплено интеграционными тестами [`DegradedRedisIntegrationTests`](tests/EventManagementService.Events.Tests/Presentation/DegradedRedisIntegrationTests.cs) на production DI-графе сервиса.

## Миграции EF Core

Каждый сервис применяет миграции автоматически при запуске. Для ручного управления:

```bash
# Users
dotnet ef database update \
  --project src/EventManagementService.Users.Infrastructure \
  --startup-project src/EventManagementService.Users.Presentation

# Events
dotnet ef database update \
  --project src/EventManagementService.Events.Infrastructure \
  --startup-project src/EventManagementService.Events.Presentation

# Bookings
dotnet ef database update \
  --project src/EventManagementService.Bookings.Infrastructure \
  --startup-project src/EventManagementService.Bookings.Presentation
```

Создание новой миграции:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/EventManagementService.<Service>.Infrastructure \
  --startup-project src/EventManagementService.<Service>.Presentation
