# Тестирование и запуск sprint 7

## 1. Стратегия тестирования

После разделения на слои тесты ссылаются на конкретные проекты:

- Domain tests используют Domain;
- Application service tests используют Application, Domain и test repositories;
- Infrastructure integration tests используют Infrastructure и PostgreSQL Testcontainers;
- API pipeline tests используют Presentation только там, где проверяется HTTP pipeline.

Это уменьшает случайную зависимость всех тестов от Presentation.

## 2. Test projects

```text
tests/
  EventManagementService.API.Tests/
  EventManagementService.API.IntegrationTests/
```

Названия тестовых проектов исторически остались `API.Tests`, но production web-проект называется `EventManagementService.Presentation`.

## 3. Что покрыто тестами

### Domain/Application

- создание и обновление событий;
- валидация доменных правил;
- фильтрация и пагинация;
- создание бронирований;
- защита от овербукинга;
- обработка статусов бронирования.

### Presentation/TestServer

- ProblemDetails responses;
- HTTP status codes;
- создание события через API;
- создание брони через API;
- фоновая обработка брони до `Confirmed`.

### Infrastructure/PostgreSQL

- repository behavior;
- schema checks;
- migrations applied;
- FK и ограничения схемы.

## 4. Команды проверки

### Restore

```bash
dotnet restore EventManagementService.API.sln
```

### Build

```bash
dotnet build EventManagementService.API.sln
```

### Full test suite

```bash
dotnet test EventManagementService.API.sln
```

### Только unit/API tests

```bash
dotnet test tests/EventManagementService.API.Tests/EventManagementService.API.Tests.csproj
```

### Только integration tests

```bash
dotnet test tests/EventManagementService.API.IntegrationTests/EventManagementService.API.IntegrationTests.csproj
```

## 5. Запуск приложения

Поднять PostgreSQL:

```bash
docker compose up -d
```

Запустить Presentation:

```bash
dotnet run --project src/EventManagementService.Presentation/EventManagementService.Presentation.csproj
```

Swagger UI:

```text
http://localhost:5248/swagger
```

OpenAPI JSON:

```text
http://localhost:5248/swagger/v1/swagger.json
```

Остановить PostgreSQL:

```bash
docker compose down
```

## 6. Ручная проверка API

Создать событие:

```bash
curl -i -H "Content-Type: application/json" \
  -d '{"title":"Sprint 7 event","description":"Manual check","startAt":"2026-07-01T10:00:00Z","endAt":"2026-07-01T12:00:00Z","totalSeats":2}' \
  http://localhost:5248/api/events
```

Создать бронь:

```bash
curl -i -X POST http://localhost:5248/api/events/{eventId}/book
```

Получить бронь:

```bash
curl -i http://localhost:5248/api/bookings/{bookingId}
```

После фоновой обработки статус должен стать `Confirmed`.

## 7. Частые проблемы

1. `dotnet run` падает на подключении к PostgreSQL:
- проверить `docker compose up -d`;
- проверить строку подключения в `src/EventManagementService.Presentation/appsettings.json`.

2. Testcontainers не стартует:
- проверить, что Docker запущен и доступен текущему пользователю.

3. Application случайно ссылается на Infrastructure:
- выполнить `rg "Infrastructure" src/EventManagementService.Application`;
- удалить инфраструктурный `using` или перенести контракт в Application.

4. EF migration создается не там:
- использовать `--project Infrastructure` и `--startup-project Presentation`.

---

[Далее: Диаграммы →](06-diagrams.md)
