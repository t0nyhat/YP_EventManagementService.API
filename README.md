# EventManagementService

REST API для управления событиями и бронированиями, разделённая на три независимых микросервиса с асинхронным обменом через Apache Kafka.

## Состав системы

Система состоит из трёх микросервисов, каждый со своей базой данных PostgreSQL и зоной ответственности:

| Сервис | Назначение | База данных | Порт (host) |
|--------|-----------|-------------|-------------|
| **Users** | Регистрация, вход, хеширование паролей, выдача JWT | `users_db` | `5101` |
| **Events** | CRUD событий, учёт доступных мест | `events_db` | `5102` |
| **Bookings** | Создание и отмена броней, подтверждение через outbox | `bookings_db` | `5103` |

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
└──────────┘          └────┬─────┘          └─────┬────┘
                           │                      │
                           │  ┌──────────────┐    │
                           │  │    Kafka     │    │
                           │  │ booking-     │ ◄──┘
                           └─►│ confirmed    │
                              │ (topic)      │
                              └──────────────┘
```

### Поток BookingConfirmed

1. Пользователь создаёт бронь через `POST /events/{id}/book` в сервисе **Bookings**.
2. Бронь сохраняется со статусом `Pending`.
3. Фоновый сервис [`BookingProcessingBackgroundService`](src/EventManagementService.Bookings.Presentation/BackgroundServices/BookingProcessingBackgroundService.cs) периодически выбирает `Pending`-брони и вызывает [`BookingProcessingService.ProcessPendingBookingAsync`](src/EventManagementService.Bookings.Application/Services/BookingProcessingService.cs).
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
- JWT Bearer Authentication
- PostgreSQL 17
- xUnit v3 + FluentAssertions
- Testcontainers для интеграционных тестов

## Требования

1. .NET SDK 10+
2. Docker (для запуска PostgreSQL, Kafka и сервисов)

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

### 3. Запустить тесты

```bash
dotnet test EventManagementService.API.sln
```

Интеграционные тесты поднимают собственные PostgreSQL-контейнеры через Testcontainers.

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

## Идемпотентность и отказоустойчивость

- **Outbox** в Bookings: сообщение сначала сохраняется в БД, потом публикуется. При сбое Kafka публикация повторяется.
- **Inbox** в Events: дубликаты `BookingConfirmed` не уменьшают места дважды (unique `booking_id`).
- **Ретраи**: outbox publisher увеличивает счётчик попыток и сохраняет `last_error` при неудаче.
- **Обработка ошибок Kafka**: bad JSON логируется и пропускается, сервис не падает.
- **Топик создаётся автоматически** при старте Events через [`KafkaTopicInitializer`](src/EventManagementService.Events.Infrastructure/Messaging/KafkaTopicInitializer.cs).

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
