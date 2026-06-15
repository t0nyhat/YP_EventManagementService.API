# EventManagementService

REST API для управления событиями и бронированиями на ASP.NET Core Web API.

## Технологии

- .NET SDK 10+
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL (`Npgsql`)
- JWT Bearer Authentication (`System.IdentityModel.Tokens.Jwt`)
- xUnit v3 + FluentAssertions
- PostgreSQL Testcontainers для интеграционных тестов

## Архитектура

Решение разделено на четыре production-проекта по принципам Clean Architecture:

- `src/EventManagementService.Domain/` — доменные сущности, перечисления и доменные исключения. Не зависит от фреймворков и других проектов.
- `src/EventManagementService.Application/` — use cases, application-сервисы, DTO, validation и порты репозиториев. Зависит только от Domain.
- `src/EventManagementService.Infrastructure/` — EF Core `AppDbContext`, конфигурации моделей, migrations и реализации репозиториев. Зависит от Application и Domain.
- `src/EventManagementService.Presentation/` — Presentation-слой: controllers, HTTP mapping, middleware, Swagger, hosted service adapter и composition root в `Program.cs`. Зависит от Application и Infrastructure.

Направление зависимостей:

```text
Domain <- Application <- Infrastructure <- Presentation
```

`Application` не содержит ссылок на `Infrastructure`; доступ к данным идет через интерфейсы портов из Application, а реализации подключаются в Infrastructure через DI.

## Требования

1. .NET SDK 10+
2. Docker (для запуска PostgreSQL)

## Быстрый старт

### 1. Поднять PostgreSQL

```bash
docker compose up -d
```

Проверка статуса:

```bash
docker compose ps
```

### 2. Запустить API

```bash
dotnet restore
dotnet build
dotnet run --project src/EventManagementService.Presentation/EventManagementService.Presentation.csproj
```

При первом запуске таблицы будут применены через EF Core migrations (`Database.Migrate()`).

### 2a. Применить миграции вручную

Если нужно подготовить схему без запуска API, используйте команды EF Core:

```bash
dotnet ef database update \
  --project src/EventManagementService.Infrastructure/EventManagementService.Infrastructure.csproj \
  --startup-project src/EventManagementService.Presentation/EventManagementService.Presentation.csproj
```

Для текущего состояния репозитория создавать новую миграцию не требуется: достаточно выполнить команду `dotnet ef database update` выше с указанными проектами.

### 3. Запустить тесты

```bash
dotnet test EventManagementService.API.sln
```

Интеграционные тесты поднимают собственный PostgreSQL-контейнер через Testcontainers, поэтому отдельный локальный контейнер для них не нужен.

## Конфигурация подключения

По умолчанию используется строка подключения из `src/EventManagementService.Presentation/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=eventapi;Username=postgres;Password=postgres"
  }
}
```

JWT-параметры также настраиваются в `src/EventManagementService.Presentation/appsettings.json`:

```json
{
  "Jwt": {
    "Issuer": "EventManagementService.API",
    "Audience": "EventManagementService.API",
    "SigningKey": "замени-на-сложный-секрет-минимум-32-байта",
    "LifetimeMinutes": 60
  }
}
```

Важно: для HS256 длина `SigningKey` должна быть не меньше 32 байт.

## Swagger / OpenAPI

В режиме `Development` доступны:

- Swagger UI: `http://localhost:5248/swagger`
- OpenAPI JSON: `http://localhost:5248/openapi/v1.json`

Для защищенных эндпоинтов в Swagger нажмите `Authorize` и передайте токен в формате `Bearer <jwt>`.

## Эндпоинты

События:

- `GET /api/events` — список с фильтрацией и пагинацией
- `GET /api/events/{id}` — событие по `id`
- `POST /api/events` — создать событие (только `Admin`)
- `PUT /api/events/{id}` — обновить событие (только `Admin`)
- `DELETE /api/events/{id}` — удалить событие (только `Admin`)
- `POST /api/events/{id}/book` — создать бронирование (требуется аутентификация)

Аутентификация:

- `POST /api/auth/register` — регистрация пользователя
- `POST /api/auth/login` — вход и получение JWT-токена

Решение по контракту логина:

- при неверных учетных данных `POST /api/auth/login` возвращает `404 Not Found`;
- сообщение всегда одинаковое (`Неверный логин или пароль.`), чтобы не раскрывать, существует ли конкретный логин;
- логины нормализуются в нижний регистр (`ToLowerInvariant`) при регистрации и входе, поэтому `Admin` и `admin` считаются одним пользователем.

Бронирования:

- `GET /api/bookings/{id}` — текущее состояние бронирования (владелец или `Admin`)
- `DELETE /api/bookings/{id}` — отмена бронирования (владелец или `Admin`)

## Фильтрация и пагинация

`GET /api/events` поддерживает query-параметры:

- `title` — поиск по названию, регистронезависимый, частичное совпадение
- `from` — события не раньше указанной даты
- `to` — события не позже указанной даты
- `page` — номер страницы, по умолчанию `1`
- `pageSize` — размер страницы, по умолчанию `10`

Пример запроса:

```http
GET /api/events?title=dotnet&from=2026-05-01T00:00:00&page=1&pageSize=2
```

Пример ответа:

```json
{
  "items": [
    {
      "id": "0c6bbd2b-4f64-4fb9-8d73-dcd7f6f36611",
      "title": "DotNet Advanced",
      "description": "Продвинутый курс",
      "startAt": "2026-05-02T10:00:00",
      "endAt": "2026-05-02T13:00:00",
      "totalSeats": 50,
      "availableSeats": 47
    }
  ],
  "page": 1,
  "count": 1,
  "totalCount": 1
}
```

## Примеры тел запросов

`POST /api/events`:

```json
{
  "title": "Конференция .NET",
  "description": "Технологическое мероприятие",
  "startAt": "2026-04-10T10:00:00",
  "endAt": "2026-04-10T18:00:00",
  "totalSeats": 50
}
```

`PUT /api/events/{id}` — без поля `totalSeats`, вместимость не меняется при обновлении:

```json
{
  "title": "Конференция .NET (обновлено)",
  "description": "Технологическое мероприятие",
  "startAt": "2026-04-10T10:00:00",
  "endAt": "2026-04-10T18:00:00"
}
```

`POST /api/events/{id}/book` — тело не нужно, `id` события передаётся в URL.

## Бронирования

Статусы `BookingStatus`:

- `Pending` — создано, ожидает обработки
- `Confirmed` — подтверждено
- `Rejected` — отклонено
- `Cancelled` — отменено пользователем/администратором

Пример ответа `POST /api/events/{id}/book` и `GET /api/bookings/{id}`:

```json
{
  "id": "5b178c2f-247d-4e6f-bf64-c40aeb9f95ef",
  "eventId": "0c6bbd2b-4f64-4fb9-8d73-dcd7f6f36611",
  "status": "Pending",
  "createdAt": "2026-04-03T12:00:00Z",
  "processedAt": null
}
```

`POST /api/events/{id}/book` возвращает `202 Accepted` и заголовок `Location: /api/bookings/{bookingId}`.

## Вместимость события

- `totalSeats` — задаётся при создании, не меняется при обновлении
- `availableSeats` — уменьшается с каждым успешным бронированием

При исчерпании мест API возвращает `409 Conflict`.

## Валидация

- `title` — обязателен, не пустой
- `endAt` — должен быть позже `startAt`
- `totalSeats` — больше нуля
- `from` — не позже `to`
- `page` — не меньше `1`
- `pageSize` — от `1` до `100`

## Обработка ошибок

Ошибки возвращаются в формате `ProblemDetails` (`application/problem+json`).

Коды ответа:

- `400 Bad Request` — ошибки валидации
- `401 Unauthorized` — отсутствует или невалидный токен
- `403 Forbidden` — недостаточно прав для выполнения операции
- `404 Not Found` — ресурс не найден или неверные логин/пароль при входе
- `409 Conflict` — бизнес-конфликты (нет свободных мест, лимит активных броней, недопустимая повторная обработка)
- `500 Internal Server Error` — непредвиденная ошибка

Пример `409 Conflict`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "Нет свободных мест на данное событие.",
  "instance": "/api/events/0c6bbd2b-4f64-4fb9-8d73-dcd7f6f36611/book"
}
```

Пример `404 Not Found`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Resource not found",
  "status": 404,
  "detail": "Событие с id 8a1d2c54-0c43-4db6-bd7f-0e6d6f9191f8 не найдено.",
  "instance": "/api/events/8a1d2c54-0c43-4db6-bd7f-0e6d6f9191f8"
}
```

## Фоновая обработка бронирований

`BookingProcessingBackgroundService`:

- периодически выбирает `Pending`-бронирования;
- обрабатывает их параллельно через `Task.WhenAll`;
- каждая бронь обрабатывается в отдельном scope через `IServiceScopeFactory`;
- делегирует бизнес-решения в `IBookingProcessingService` из Application;
- если событие удалено до обработки — бронь переводится в `Rejected`;
- результат сохраняется через scoped-репозитории из Infrastructure.

При создании бронирования `BookingService` защищает критическую секцию через `SemaphoreSlim` — исключает овербукинг при конкурентных запросах.

## База данных и миграции

- Схема данных управляется EF Core migrations.
- `AppDbContext`, configurations и migrations находятся в `EventManagementService.Infrastructure`.
- Старт приложения применяет миграции автоматически.
- Схему можно применить вручную через `dotnet ef database update` с Infrastructure как `--project` и API как `--startup-project`.
- Основные ограничения схемы проверяются интеграционными тестами на реальном PostgreSQL.

Создание новой миграции:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/EventManagementService.Infrastructure/EventManagementService.Infrastructure.csproj \
  --startup-project src/EventManagementService.Presentation/EventManagementService.Presentation.csproj
```

При необходимости можно сразу указать конкретный `DbContext`:

```bash
dotnet ef migrations add <MigrationName> \
  --context AppDbContext \
  --project src/EventManagementService.Infrastructure/EventManagementService.Infrastructure.csproj \
  --startup-project src/EventManagementService.Presentation/EventManagementService.Presentation.csproj
```

Применение миграций:

```bash
dotnet ef database update \
  --project src/EventManagementService.Infrastructure/EventManagementService.Infrastructure.csproj \
  --startup-project src/EventManagementService.Presentation/EventManagementService.Presentation.csproj
```

## Тестирование

- `tests/EventManagementService.API.Tests/` — unit-тесты Domain/Application, тесты hosted service adapter и API pipeline на TestServer.
- `tests/EventManagementService.API.IntegrationTests/` — интеграционные тесты Infrastructure-репозиториев и схемы PostgreSQL через Testcontainers.
- Для интеграционных тестов нужен только Docker, отдельный PostgreSQL вручную поднимать не требуется.

## Пример сценария: регистрация и бронирование

1. `POST /api/auth/register` — зарегистрировать пользователя.
2. `POST /api/auth/login` — получить JWT-токен.
3. В Swagger нажать `Authorize` и вставить `Bearer <jwt>`.
4. `POST /api/events/{id}/book` — создать бронирование.
5. `GET /api/bookings/{bookingId}` — проверить статус.

## Пример сценария: успешное бронирование

1. `POST /api/events` с `totalSeats: 3` — создать событие.
2. `POST /api/events/{id}/book` — получить `202 Accepted` и `Location`.
3. `GET /api/bookings/{bookingId}` — увидеть статус `Pending`.
4. Подождать несколько секунд и повторить — статус изменится на `Confirmed`.

## Пример сценария: овербукинг

1. Создать событие с `totalSeats: 3`.
2. Создать три бронирования — все вернут `202 Accepted`.
3. Четвёртое бронирование вернёт `409 Conflict`.
4. `availableSeats` у события будет `0`.

## Структура проекта

```text
EventManagementService.API.sln
src/
  EventManagementService.Domain/
  EventManagementService.Application/
  EventManagementService.Infrastructure/
  EventManagementService.Presentation/
tests/
  EventManagementService.API.Tests/
  EventManagementService.API.IntegrationTests/
docs/
```
