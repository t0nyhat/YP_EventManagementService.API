# EventManagementService.API — Полная учебная документация

Документация показывает эволюцию проекта от базового CRUD до микросервисной Clean Architecture с PostgreSQL, Kafka и Redis.

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

### Sprint 9: Микросервисы и Apache Kafka

Папка: [docs/sprint9](sprint9/)

Что покрывает:
- декомпозиция монолита на три сервиса (Users, Events, Bookings), у каждого своя база PostgreSQL и свои миграции;
- разделяемый проект контрактов: имя топика, record `BookingConfirmed`, общие настройки сериализации;
- асинхронный обмен через Kafka: паттерн Outbox в Bookings, паттерн Inbox (идемпотентность) в Events;
- гарантии доставки at-least-once, ключ сообщения `EventId`, ручное управление оффсетами и `Seek` при ошибке;
- Dead Letter Topic для сообщений, которые нельзя обработать (лимит попыток или заведомо невалидный payload);
- конкурентность: concurrency token на статусе брони, advisory lock для лимита броней;
- проверка общего JWT в трёх сервисах и запуск всей системы через `docker compose up`.

Ключевые материалы:
- [README sprint9](sprint9/README.md)
- [Введение](sprint9/01-introduction.md)
- [Архитектура](sprint9/02-architecture.md)
- [Обмен сообщениями и согласованность](sprint9/03-messaging-and-consistency.md)
- [Реализация](sprint9/04-implementation.md)
- [Тестирование и запуск](sprint9/05-testing-and-run.md)
- [Диаграммы](sprint9/06-diagrams.md)

### Sprint 10: Redis и Cache-Aside

Папка: [docs/sprint10](sprint10/)

Что покрывает:
- Redis как best-effort кеш сервиса Events и singleton-подключение через StackExchange.Redis;
- Cache-Aside для `GET /events/{id}` и нового публичного `GET /events/top`;
- расчёт топ-10 по доле проданных мест в PostgreSQL с детерминированным порядком;
- разные TTL для карточки события и агрегата топа;
- инвалидация `event:{id}` после CRUD и успешного `BookingConfirmed`, строго после commit БД;
- безопасная деградация при недоступном Redis и централизованные ключи/JSON-настройки;
- unit-, Testcontainers- и HTTP integration-тесты кеширования.

Ключевые материалы:
- [README sprint10](sprint10/README.md)
- [Введение](sprint10/01-introduction.md)
- [Архитектура](sprint10/02-architecture.md)
- [Стратегия кеширования и согласованность](sprint10/03-cache-strategy.md)
- [Реализация](sprint10/04-implementation.md)
- [Тестирование и запуск](sprint10/05-testing-and-run.md)
- [Диаграммы](sprint10/06-diagrams.md)

## Рекомендуемый порядок изучения

1. Sprint 1
2. Sprint 2
3. Sprint 3
4. Sprint 4
5. Sprint 5
6. Sprint 6
7. Sprint 7
8. Sprint 8
9. Sprint 9
10. Sprint 10

## Как запускать проект

Начиная со спринта 10 весь стек включает три микросервиса, Zookeeper, Kafka, Redis и три PostgreSQL-базы; он поднимается из корня репозитория одной командой:

```bash
docker compose up --build -d
```

Swagger: Users — `http://localhost:5101/swagger`, Events — `http://localhost:5102/swagger`, Bookings — `http://localhost:5103/swagger`.

Локальная сборка:

```bash
dotnet restore
dotnet build
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
