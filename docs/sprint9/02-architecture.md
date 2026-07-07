# Архитектура решения sprint 9

## 1. Состав решения

Решение — 13 production-проектов: три сервиса по четыре слоя плюс общий проект контрактов.

```text
src/
  EventManagementService.Contracts/            # общий контракт сообщений

  EventManagementService.Users.Domain/
  EventManagementService.Users.Application/
  EventManagementService.Users.Infrastructure/
  EventManagementService.Users.Presentation/   # host: порт 5101

  EventManagementService.Events.Domain/
  EventManagementService.Events.Application/
  EventManagementService.Events.Infrastructure/
  EventManagementService.Events.Presentation/  # host: порт 5102

  EventManagementService.Bookings.Domain/
  EventManagementService.Bookings.Application/
  EventManagementService.Bookings.Infrastructure/
  EventManagementService.Bookings.Presentation/ # host: порт 5103
```

Внутри каждого сервиса действует то же правило, что и в спринтах 7–8:

```text
Domain <- Application <- Infrastructure <- Presentation
```

## 2. Правила зависимостей

- `*.Domain` — не ссылается ни на что.
- `*.Application` — только на свой `Domain`; у Bookings и Events дополнительно на `Contracts` (им нужен тип сообщения).
- `*.Infrastructure` — на свои `Application` и `Domain`; там, где используется Kafka, — на `Contracts` и `Confluent.Kafka`.
- `*.Presentation` — на свои `Application` и `Infrastructure`; composition root в `Program.cs`.
- **Ни один сервис не ссылается на проекты другого сервиса.** Единственная общая сборка — `Contracts`.
- В рантайме нет межсервисных `HttpClient`/gRPC: проверяется поиском по коду (`rg "HttpClient|Refit|GrpcChannel" src`).

Следствие: некоторые типы существуют в трёх копиях (`UserRole`, middleware обработки ошибок). Это осознанный компромисс — независимость сервисов ценнее DRY между ними; общим сделано только то, что обязано совпадать байт-в-байт: контракт сообщения и настройки его сериализации.

## 3. Общий проект контрактов

[`EventManagementService.Contracts`](../../src/EventManagementService.Contracts/) содержит ровно три вещи:

```csharp
public static class KafkaTopics
{
    public const string BookingConfirmed = "booking-confirmed";
}

public sealed record BookingConfirmed(
    Guid BookingId,
    Guid EventId,
    Guid UserId,
    int Seats,
    DateTimeOffset ConfirmedAtUtc);

public static class KafkaJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
```

Принципы:

- контракт — публичный «договор» между сервисами: только данные, нужные подписчику, никаких внутренних деталей;
- record неизменяем — событие описывает свершившийся факт;
- `KafkaJson.Options` — один экземпляр настроек для продюсера, консюмера и тестов, чтобы формат не разъехался;
- проект не ссылается на EF Core, ASP.NET Core или Confluent.Kafka.

## 4. Разделение данных

| Сервис | БД (compose) | Host-порт БД | Таблицы |
|--------|--------------|--------------|---------|
| Users | `users_db` | 5433 | `users` |
| Events | `events_db` | 5434 | `events`, `booking_confirmed_inbox` |
| Bookings | `bookings_db` | 5435 | `bookings`, `booking_outbox` |

Ключевые отличия от монолита:

- в `bookings` **нет FK** на `events` и `users` — только колонки `event_id`, `user_id` типа `uuid`;
- у `Event` больше нет навигации `Bookings`; у `Booking` — навигаций на `Event`/`User`;
- ссылочную целостность между сервисами никто не гарантирует — это фундаментальное свойство микросервисной модели, а не упущение;
- у каждого сервиса свой `DbContext` (`UsersDbContext`, `EventsDbContext`, `BookingsDbContext`) и своя миграция `InitialCreate`, применяемая на старте.

Служебные таблицы обмена:

- `booking_outbox` (Bookings): `id`, `booking_id` (unique), `event_id`, `user_id`, `seats`, `confirmed_at_utc`, `payload` (jsonb), `created_at_utc`, `published_at_utc`, `publish_attempts`, `last_error`;
- `booking_confirmed_inbox` (Events): PK `booking_id`, поля сообщения, `processed_at_utc`, `result` (`Processed` / `EventNotFound` / `EventAlreadyStarted` / `NotEnoughSeats`).

## 5. Распределение кода монолита по сервисам

| Было в монолите | Стало |
|---|---|
| `User`, `UserRole`, `UserService`, хеширование, JWT-генерация | Users |
| `Event`, event DTO, валидация запросов, `EventService`, `EventRepository` | Events |
| `Booking`, `BookingStatus`, `BookingService`, фоновая обработка броней | Bookings |
| `BookingService` напрямую менял `Event.AvailableSeats` | ушло: Bookings публикует событие, места меняет только Events |
| Правило «нельзя бронировать прошедшее событие» (проверка при создании) | переехало в обработчик Events (`EventAlreadyStarted` при обработке сообщения) |
| Возврат места при отмене брони | не переносится: требует события `BookingCancelled`, вне scope спринта |

## 6. Взаимодействие в рантайме

```text
клиент ──JWT──> Users (5101)      выдаёт токен
клиент ──JWT──> Events (5102)     CRUD событий (Admin), чтение — анонимно
клиент ──JWT──> Bookings (5103)   брони

Bookings ──BookingConfirmed──> Kafka (topic booking-confirmed) ──> Events
```

- один и тот же JWT принимают все три сервиса: общие `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey` заданы в конфигурации каждого;
- Kafka-брокер один (`kafka:9092` внутри compose-сети, `localhost:29092` с хоста), Zookeeper — для координации;
- группа потребителей Events — `events-service`: при масштабировании инстансов каждое сообщение получит один инстанс группы.

---

[Далее: Обмен сообщениями и согласованность →](03-messaging-and-consistency.md)
