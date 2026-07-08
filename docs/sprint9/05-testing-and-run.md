# Тестирование и запуск sprint 9

## 1. Структура тестов

Тесты пересобраны по границам сервисов — по проекту на сервис (80 тестов суммарно):

```text
tests/
  EventManagementService.Users.Tests/     # 21 тест
  EventManagementService.Events.Tests/    # 34 теста
  EventManagementService.Bookings.Tests/  # 25 тестов
```

Стек прежний: xUnit v3 + FluentAssertions + Moq; уровни изоляции разные и подобраны под предмет теста.

### Users

- `UserServiceTests` — unit (Moq): регистрация обеих ролей, дубликат логина, успешный вход, одинаковый `404` для неверного логина и пароля, claims токена.
- `AuthControllerTests` — unit (Moq): контроллер парсит `Role` из тела запроса (`Admin`/пусто → `User`/неизвестная роль → `400`) и передаёт её в `IUserService`, а не теряет на уровне DTO.
- `SecurityPrimitivesTests` — PBKDF2-хеширование и генерация/разбор JWT.
- `UserRepositoryTests` — **Testcontainers**: реальный PostgreSQL, уникальность нормализованного логина на уровне БД (фикстура [`PostgreSqlTestcontainerFixture`](../../tests/EventManagementService.Users.Tests/Infrastructure/PostgreSqlTestcontainerFixture.cs)).

### Events

- `EventTests` / `EventServiceTests` — доменные правила и CRUD (unit).
- `BookingConfirmedHandlerTests` — **InMemory EF**: уменьшение мест, идемпотентность дубля, `EventNotFound`, `EventAlreadyStarted`, `NotEnoughSeats`, «места не уходят в минус», roundtrip сообщения через общие `KafkaJson.Options`.
- `KafkaDeadLetterPublisherTests` — unit (Moq `IProducer`): сообщение уходит в `booking-confirmed.DLT` с оригинальным payload в `Value` и диагностическими заголовками (`error-reason`, `error-source-topic/partition/offset`, `error-timestamp`).
- `EventsControllerAuthIntegrationTests` — `WebApplicationFactory`: 401 без токена, 403 для роли `User` на `POST/PUT/DELETE /events`, 2xx для `Admin`, анонимное чтение. Hosted-сервисы (консюмер, инициализатор топика) в фабрике удаляются — Kafka для HTTP-тестов не нужна.

### Bookings

- `BookingTests` / `BookingServiceTests` — домен и use cases (unit): создание `Pending`, валидация идентификаторов, лимит, доступ владельца/админа.
- `BookingRepositoryTests` — InMemory: атомарная проверка лимита (`AddWithActiveLimitAsync`), отменённые брони не считаются активными.
- `BookingProcessingServiceTests` — InMemory: подтверждение создаёт outbox-строку; уже подтверждённая бронь пропускается.
- `BookingOutboxPublisherTests` — InMemory + Moq: успех помечает строку опубликованной; падение Kafka оставляет строку на ретрай с `publish_attempts`/`last_error`.
- `KafkaBookingConfirmedPublisherTests` — топик и ключ сообщения (`EventId`).
- `BookingConfirmedSerializationTests` — контракт: camelCase-JSON и roundtrip.
- `BookingsControllerAuthIntegrationTests` — 401 без токена, `202 Accepted` + `Location` при создании брони.

Замечание про уровни: то, что в монолите проверялось одним большим integration-тестом, теперь распадается на unit/InMemory-тесты по сервисам плюс сквозной ручной сценарий — автоматический E2E через реальный Kafka сознательно не строился (для учебного проекта его цена выше пользы). По той же причине сам цикл `BookingConfirmedConsumerService` (лимит попыток, `Seek`, переход в Dead Letter Topic) не покрыт unit-тестом — класс напрямую строит `Confluent.Kafka`-клиент в конструкторе, тестируется вручную через живой Kafka (раздел 4); юнит-тестами покрыт только продюсер DLT (`KafkaDeadLetterPublisherTests`) и обработчик (`BookingConfirmedHandlerTests`).

## 2. Команды

```bash
# сборка всего решения
dotnet build EventManagementService.API.sln

# все тесты (Testcontainers требуют запущенный Docker)
dotnet test EventManagementService.API.sln

# полный стек: Zookeeper, Kafka, 3 БД, 3 API
docker compose up --build -d
docker compose ps

# остановка с удалением томов (пересоздать БД с нуля)
docker compose down -v
```

Локальный запуск сервиса без Docker (инфраструктуру поднять из compose):

```bash
dotnet run --project src/EventManagementService.Users.Presentation
dotnet run --project src/EventManagementService.Events.Presentation
dotnet run --project src/EventManagementService.Bookings.Presentation
```

## 3. Swagger

| Сервис | URL |
|--------|-----|
| Users | http://localhost:5101/swagger |
| Events | http://localhost:5102/swagger |
| Bookings | http://localhost:5103/swagger |

У каждого сервиса — кнопка `Authorize`, токен передаётся как `Bearer <jwt>`.

## 4. Ручной end-to-end сценарий (этап 8 задания)

1. `docker compose up --build -d`.
2. Users: `POST /auth/register` с ролью `Admin`, затем `POST /auth/login` — получить admin-токен.
3. Users: зарегистрировать обычного пользователя, получить user-токен.
4. Events (admin-токен): `POST /events`, запомнить `availableSeats`.
5. Bookings (user-токен): `POST /events/{eventId}/book` → `202 Accepted`.
6. Через несколько секунд `GET /bookings/{id}` → статус `Confirmed`.
7. Events: `GET /events/{id}` → `availableSeats` уменьшился на 1 — событие прошло через Kafka.
8. Убедиться, что уменьшение произошло без прямых вызовов: в логах `events-api` виден consume из топика `booking-confirmed`; в коде нет `HttpClient` между сервисами.

Идемпотентность дублей проверяется тестами обработчика (`BookingConfirmedHandlerTests`) — вручную реплеить Kafka не требуется.

## 5. Частые проблемы

- **Testcontainers-тесты падают** — не запущен Docker Desktop.
- **API в compose перезапускается** — БД ещё не прошла healthcheck; `depends_on: condition: service_healthy` обычно решает, смотрите `docker compose logs <svc>`.
- **Старая схема БД в томах** — после изменения миграций пересоздайте тома: `docker compose down -v && docker compose up --build`.
- **401 при валидном токене** — проверьте, что `Jwt__Issuer/Audience/SigningKey` совпадают во всех трёх сервисах.

---

[Далее: Диаграммы →](06-diagrams.md)
