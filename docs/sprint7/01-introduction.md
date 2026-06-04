# Введение в sprint 7

## 1. Цель спринта

Главная цель sprint 7 — разрезать текущий сервис бронирований на четыре отдельных проекта по принципам Clean Architecture:

- `EventManagementService.Domain`;
- `EventManagementService.Application`;
- `EventManagementService.Infrastructure`;
- `EventManagementService.Presentation`.

До этого приложение развивалось как один Web API-проект: в одной сборке находились controllers, DTO, сервисы, доменные сущности, EF Core, repositories, migrations и background service. Такой подход работал на ранних спринтах, но начал смешивать разные причины изменения.

## 2. Почему потребовалось разделение

Монолитная сборка не защищала архитектурные границы компилятором:

- application-сервисы могли случайно начать зависеть от EF Core;
- доменные сущности могли получить framework-specific атрибуты;
- tests были вынуждены ссылаться на весь Web API-проект;
- composition root смешивался с инфраструктурными регистрациями;
- фоновая обработка содержала и hosted-service orchestration, и бизнес-решения.

Sprint 7 устраняет эти риски через отдельные проекты и направленные `ProjectReference`.

## 3. Что означает Clean Architecture в этом проекте

Clean Architecture здесь применяется прагматично:

- Domain хранит бизнес-правила предметной области.
- Application описывает use cases и порты, которые нужны бизнес-логике.
- Infrastructure реализует эти порты через EF Core/PostgreSQL.
- Presentation принимает HTTP-запросы и вызывает Application.

Важное правило:

```text
Domain <- Application <- Infrastructure <- Presentation
```

Внутренние слои не знают о внешних. `Application` не знает, что данные хранятся в PostgreSQL, а `Domain` не знает ни об HTTP, ни о DI, ни об EF Core.

## 4. Что осталось прежним

Публичное поведение API сохранено:

- `GET /api/events`;
- `GET /api/events/{id}`;
- `POST /api/events`;
- `PUT /api/events/{id}`;
- `DELETE /api/events/{id}`;
- `POST /api/events/{id}/book`;
- `GET /api/bookings/{id}`.

Сохранились:

- Swagger/OpenAPI;
- EF Core migrations;
- PostgreSQL как основная БД;
- Testcontainers для integration tests;
- background processing pending-бронирований.

## 5. Подход к паттернам и принципам

В спринте использован минимальный набор паттернов, который реально нужен текущему коду:

- Factory: доменные фабричные методы `Event.Create` и `Booking.CreatePending`.
- Adapter: EF Core repositories адаптируют Application-порты к PostgreSQL.
- Facade: Application-сервисы дают Presentation простой use case API.
- Dependency Injection: composition root находится в Presentation.

Не добавлялись лишние generic repository, Unit of Work, MediatR, Strategy registry или Builder-классы. Это соответствует KISS и YAGNI: архитектура стала строже, но без искусственного усложнения.

---

[Далее: Архитектура решения →](02-architecture.md)
