# EventManagementService.API — Полная учебная документация

Документация показывает эволюцию проекта от базового CRUD до Clean Architecture с PostgreSQL и EF Core.

## Спринты

### Sprint 1: Основы CRUD и архитектура

Папка: [docs/sprint1](sprint1/)

Что покрывает:
- базовый REST API для событий;
- DTO, валидация, маппинг;
- сервисный слой и контроллеры.

### Sprint 2: Фильтрация, пагинация, ошибки

Папка: [docs/sprint2](sprint2/)

Что покрывает:
- фильтрация и пагинация в `GET /api/events`;
- централизованная обработка ошибок;
- формат `ProblemDetails`.

### Sprint 3: Бронирования и background processing

Папка: [docs/sprint3](sprint3/)

Что покрывает:
- жизненный цикл брони (`Pending`/`Confirmed`/`Rejected`);
- фоновые задачи и асинхронная обработка;
- API-контракт с `202 Accepted`.

### Sprint 4: Синхронизация и конкурентность

Папка: [docs/sprint4](sprint4/)

Что покрывает:
- ограничение мест (`TotalSeats`/`AvailableSeats`);
- защита критических секций;
- конкурентные тесты и race conditions.

### Sprint 5: PostgreSQL и EF Core

Папка: [docs/sprint5](sprint5/)

Что покрывает:
- `AppDbContext` и Fluent API конфигурации;
- scoped lifecycle для `DbContext` и сервисов;
- `IServiceScopeFactory` в `BackgroundService`;
- запуск PostgreSQL через Docker;
- тестирование с `UseInMemoryDatabase`.

### Sprint 6: Миграции, репозитории и интеграционные тесты PostgreSQL

Папка: [docs/sprint6](sprint6/)

Что покрывает:
- переход с `EnsureCreated()` на EF Core migrations;
- выделение репозиториев для событий и бронирований;
- интеграционные тесты репозиториев на PostgreSQL через Testcontainers;
- проверка схемы, FK и ограничений на уровне PostgreSQL.

Ключевые материалы:
- [README sprint6](sprint6/README.md)
- [Введение](sprint6/01-introduction.md)
- [Архитектура](sprint6/02-architecture.md)
- [Репозитории и миграции](sprint6/03-repositories-and-migrations.md)
- [Реализация](sprint6/04-implementation.md)
- [Тестирование и запуск](sprint6/05-testing-and-run.md)
- [Диаграммы](sprint6/06-diagrams.md)

### Sprint 7: Clean Architecture и разделение на проекты

Папка: [docs/sprint7](sprint7/)

Что покрывает:
- разделение production-кода на Domain, Application, Infrastructure и Presentation;
- перенос доменной модели, use cases, портов, EF Core adapters и HTTP-слоя по отдельным сборкам;
- строгие направления зависимостей через `ProjectReference`;
- DI composition root в Presentation;
- обновление тестовых references и команд миграций.

Ключевые материалы:
- [README sprint7](sprint7/README.md)
- [Введение](sprint7/01-introduction.md)
- [Архитектура](sprint7/02-architecture.md)
- [Слои, порты и адаптеры](sprint7/03-layers-and-ports.md)
- [Реализация](sprint7/04-implementation.md)
- [Тестирование и запуск](sprint7/05-testing-and-run.md)
- [Диаграммы](sprint7/06-diagrams.md)

### Sprint 8: Пользователи, роли и JWT-аутентификация

Папка: [docs/sprint8](sprint8/)

Что покрывает:
- доменные правила бронирования: запрет брони прошедшего события, лимит активных броней, отмена с защитой от повторной отмены;
- сущность `User`, роли `User`/`Admin`, связь `Booking -> User`;
- SHA-256 хеширование паролей и генерация JWT-токена с параметрами из конфигурации;
- JWT-аутентификация и авторизация по ролям и владению бронью;
- маппинг новых исключений в коды `400/401/403/404/409` и Swagger с кнопкой `Authorize`.

Ключевые материалы:
- [README sprint8](sprint8/README.md)
- [Введение](sprint8/01-introduction.md)
- [Архитектура](sprint8/02-architecture.md)
- [Доменные правила и безопасность](sprint8/03-domain-rules-and-security.md)
- [Реализация](sprint8/04-implementation.md)
- [Тестирование и запуск](sprint8/05-testing-and-run.md)
- [Диаграммы](sprint8/06-diagrams.md)

## Рекомендуемый порядок изучения

1. Sprint 1
2. Sprint 2
3. Sprint 3
4. Sprint 4
5. Sprint 5
6. Sprint 6
7. Sprint 7
8. Sprint 8

## Как запускать проект

Из корня репозитория:

```bash
docker compose up -d
dotnet restore
dotnet build
dotnet run --project src/EventManagementService.Presentation/EventManagementService.Presentation.csproj
```

Запуск тестов:

```bash
dotnet test
```

Интеграционные тесты Sprint 6+ используют Testcontainers и требуют установленный Docker.

## Где искать детали по sprint 5

- [README sprint5](sprint5/README.md)
- [EF Core и DataAccess](sprint5/03-ef-core-data-access.md)
- [Реализация](sprint5/04-implementation.md)
- [Тестирование и запуск](sprint5/05-testing-and-run.md)
- [Диаграммы](sprint5/06-diagrams.md)

## Где искать детали по sprint 6

- [README sprint6](sprint6/README.md)
- [Тестовое задание sprint 6](sprint6/sprint6-task.md)

## Где искать детали по sprint 7

- [README sprint7](sprint7/README.md)
- [Тестовое задание sprint 7](sprint7/sprint7-task.md)
- [Clean Architecture](sprint7/02-architecture.md)
