# Sprint 6: Документация

Полная учебная документация по спринту 6: переход на миграции EF Core, репозиторный слой и интеграционные тесты на PostgreSQL через Testcontainers.

## Структура документации

1. **[01-introduction.md](01-introduction.md)** — Обзор спринта, цели и мотивация
   - Почему `EnsureCreated` недостаточен
   - Зачем репозитории между сервисами и EF Core
   - Почему проверка на реальном PostgreSQL важнее InMemory

2. **[02-architecture.md](02-architecture.md)** — Архитектурные решения
   - `IEventRepository` / `IBookingRepository` и их роли
   - Scope-модель и общая транзакционная граница через один `DbContext`
   - Обновление background processing после перехода на репозитории

3. **[03-repositories-and-migrations.md](03-repositories-and-migrations.md)** — Репозитории и миграции
   - Контракты и реализации репозиториев
   - `InitialCreate` migration и `Database.Migrate()`
   - Почему это production-ближе, чем `EnsureCreated`

4. **[04-implementation.md](04-implementation.md)** — Реализация в коде
   - Рефакторинг сервисов и DI
   - Изменения в worker
   - Эволюция контуров чтения/записи

5. **[05-testing-and-run.md](05-testing-and-run.md)** — Тестирование и запуск
   - Unit + integration стратегия
   - PostgreSQL Testcontainers fixture
   - Запуск и диагностика типовых проблем

6. **[06-diagrams.md](06-diagrams.md)** — Диаграммы
   - Архитектура после Sprint 6
   - Поток бронирования через сервис и репозитории
   - Поток интеграционных тестов с Testcontainers

## Как читать эту документацию

### Для обзора и защиты решения

1. [01-introduction.md](01-introduction.md)
2. [02-architecture.md](02-architecture.md)
3. [06-diagrams.md](06-diagrams.md)

### Для работы с кодом

1. [03-repositories-and-migrations.md](03-repositories-and-migrations.md)
2. [04-implementation.md](04-implementation.md)
3. [05-testing-and-run.md](05-testing-and-run.md)

### Для сверки с требованиями

1. [sprint6-task.md](sprint6-task.md)
2. [05-testing-and-run.md](05-testing-and-run.md)

## Что принципиально изменилось относительно sprint 5

- Runtime-путь создания схемы переведен с `EnsureCreated` на миграции (`Database.Migrate()`).
- Доступ к данным вынесен из сервисов в репозитории.
- Интеграционные тесты работают на реальном PostgreSQL через Testcontainers.
- Добавлены проверки схемы, FK и ограничений на уровне СУБД.

---

[Назад к документации по спринтам](../README.md)
