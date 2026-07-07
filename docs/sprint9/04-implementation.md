# Реализация sprint 9 в коде

## 1. Users: выпуск токена

Сервис почти без изменений унаследовал контур спринта 8:

- `AuthController`: `POST /auth/register` (204, дубликат логина → 400) и `POST /auth/login` (200 + `{ "token": ... }`, неверные данные → единый 404 против перебора логинов);
- пароли хешируются [`Pbkdf2PasswordHasher`](../../src/EventManagementService.Users.Infrastructure/Security/Pbkdf2PasswordHasher.cs): PBKDF2-HMACSHA256, 600 000 итераций, соль на пароль, версионированный формат хеша;
- [`JwtTokenGenerator`](../../src/EventManagementService.Users.Infrastructure/Security/JwtTokenGenerator.cs) кладёт claims `sub`, `unique_name`, `NameIdentifier`, `Name`, `Role`; обработчик токенов — статический (`JwtSecurityTokenHandler` потокобезопасен для записи);
- `JwtOptions` валидируются на старте: `SigningKey` не короче 32 байт — иначе приложение не поднимется.

## 2. Проверка JWT в Events и Bookings

Оба сервиса подключают ту же схему Bearer с одинаковыми параметрами из секции `Jwt` (см. `Program.cs` каждого сервиса):

- `ValidIssuer` / `ValidAudience` / `IssuerSigningKey` — общие с Users, иначе токен одного сервиса не пройдёт в другом;
- Events: `GET /events*` — анонимно, `POST/PUT/DELETE /events*` — `[Authorize(Roles = "Admin")]` (403 для не-админа, 401 без токена);
- Bookings: все эндпоинты требуют аутентификации; `userId` читается из claims через `ClaimsPrincipalExtensions`, владение бронью проверяется в `BookingService` (владелец или `Admin`);
- Swagger обоих сервисов настроен с security definition — кнопка `Authorize` принимает `Bearer <token>`.

## 3. Bookings: жизненный цикл брони

Маршруты (canonical, без префикса `/api`):

- `POST /events/{id:guid}/book` → создаёт `Pending`-бронь, отвечает `202 Accepted` + `Location: /bookings/{bookingId}` (`AcceptedAtRoute`);
- `GET /bookings/{id:guid}` / `DELETE /bookings/{id:guid}` — владелец или админ.

Конвейер подтверждения:

1. [`BookingProcessingBackgroundService`](../../src/EventManagementService.Bookings.Presentation/BackgroundServices/BookingProcessingBackgroundService.cs) раз в секунду опрашивает `Pending`-брони (частичный индекс `status = 'Pending'`), выдерживает задержку обработки 2 с и для каждой брони создаёт **отдельный DI-scope**;
2. [`BookingProcessingService`](../../src/EventManagementService.Bookings.Application/Services/BookingProcessingService.cs) перепроверяет статус, вызывает `booking.Confirm()`, сериализует `BookingConfirmed` через `KafkaJson.Options` и сохраняет бронь + outbox-строку одним `SaveChanges`;
3. конфликт конкурентности (бронь успели отменить) → лог и выход без изменений.

Создание брони: доменная фабрика `Booking.CreatePending` валидирует идентификаторы, лимит активных броней (10, унаследован от монолита) проверяется атомарно в репозитории под advisory-lock'ом — см. [03-messaging-and-consistency.md](03-messaging-and-consistency.md#6-конкурентность-внутри-bookings).

## 4. Events: топик, консюмер, обработчик

Порядок hosted-сервисов в [`DependencyInjection`](../../src/EventManagementService.Events.Infrastructure/DependencyInjection.cs) важен: сначала инициализатор топика, затем консюмер.

**[`KafkaTopicInitializer`](../../src/EventManagementService.Events.Infrastructure/Messaging/KafkaTopicInitializer.cs)** — hosted-сервис, который через админ-клиент создаёт `booking-confirmed`, если его нет. Уже существующий топик — не ошибка; недоступный брокер — warning в лог, но **старт сервиса не валится**: топик догонит первый успешный продюсер/ретрай.

**[`BookingConfirmedConsumerService`](../../src/EventManagementService.Events.Infrastructure/Messaging/BookingConfirmedConsumerService.cs)** — `BackgroundService` (singleton), внутри цикла:

```csharp
await Task.Yield();                       // не блокировать StartAsync хоста
_consumer.Subscribe(KafkaTopics.BookingConfirmed);
while (!stoppingToken.IsCancellationRequested)
{
    var result = _consumer.Consume(stoppingToken);   // блокирующий вызов
    using var scope = _scopeFactory.CreateScope();   // scope на сообщение
    var handler = scope.ServiceProvider.GetRequiredService<IBookingConfirmedHandler>();
    // десериализация, валидация, HandleAsync, Commit / Seek — см. файл
}
```

Два нюанса из задания решены явно:

- `Consume` — блокирующий: `await Task.Yield()` в начале `ExecuteAsync` отпускает `StartAsync`, иначе хост не поднял бы HTTP до первого сообщения;
- `BackgroundService` — singleton, а `EventsDbContext` — scoped: на каждое сообщение создаётся собственный scope, из него берётся обработчик.

**`BookingConfirmedHandler`** живёт в Infrastructure (порт `IBookingConfirmedHandler` — в Application) и реализует идемпотентную обработку с inbox — таблица решений в [03-messaging-and-consistency.md](03-messaging-and-consistency.md#4-паттерн-inbox-и-идемпотентность-events).

## 5. Надёжность фоновых циклов

Все три фоновых цикла (поллер броней, outbox-паблишер, консюмер) написаны по одному правилу: **итерация может упасть — цикл не может**. Тело итерации обёрнуто в `catch (Exception ex) when (ex is not OperationCanceledException)` с логированием: транзиентная ошибка БД или брокера приводит к ретраю на следующем тике, а не к остановке хоста (по умолчанию необработанное исключение `BackgroundService` останавливает всё приложение — `BackgroundServiceExceptionBehavior.StopHost`).

## 6. Даты и UTC

PostgreSQL-колонки `timestamptz` требуют `DateTime` с `Kind = Utc` — Npgsql отклоняет `Unspecified`/`Local`. Клиенты же присылают даты без таймзоны (`?from=2026-07-01T10:00:00`). [`UtcDateTimeConverter`](../../src/EventManagementService.Events.Infrastructure/DataAccess/UtcDateTimeConverter.cs) на свойствах `StartAt`/`EndAt` нормализует значения: `Local → ToUniversalTime()`, `Unspecified → трактуется как UTC`. Конвертер применяется EF Core и к параметрам запросов, сравниваемым с этими колонками, поэтому фильтры `from`/`to` тоже работают с любым `Kind`.

## 7. Конфигурация и Docker

Каждый Presentation-проект имеет multi-stage `Dockerfile` (restore по csproj-графу → publish → runtime-образ `aspnet:10.0`, порт 8080). [`docker-compose.yml`](../../docker-compose.yml) поднимает 8 контейнеров: Zookeeper, Kafka, три PostgreSQL с healthcheck'ами и три API, зависящие от готовности своих БД и Kafka.

Ключевые переменные окружения (переопределяют `appsettings.json` через `__`):

| Переменная | Кто использует |
|---|---|
| `ConnectionStrings__DefaultConnection` | все сервисы (каждый — свою БД) |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey` | все сервисы, значения одинаковые |
| `Jwt__LifetimeMinutes` | только Users (он выпускает токены) |
| `Kafka__BootstrapServers` | Events и Bookings (`kafka:9092` в compose, `localhost:29092` локально) |
| `Kafka__ConsumerGroup` | только Events (`events-service`) |

Миграции EF Core применяются на старте каждого сервиса (`Database.Migrate()`); в тестах это отключается настройкой `SkipDatabaseMigration`.

---

[Далее: Тестирование и запуск →](05-testing-and-run.md)
