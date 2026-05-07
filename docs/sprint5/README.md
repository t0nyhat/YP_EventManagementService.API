# Sprint 5: Документация

Полная учебная документация по спринту 5: переход с in-memory хранения на PostgreSQL через Entity Framework Core.

## Структура документации

1. **[01-introduction.md](01-introduction.md)** — Обзор спринта, цели и мотивация
   - Почему in-memory перестаёт быть достаточным
   - Что меняется при переходе на БД
   - Какие инженерные риски закрывает sprint 5

2. **[02-architecture.md](02-architecture.md)** — Архитектурные решения
   - `AppDbContext` и слой `DataAccess`
   - Scoped lifetime для сервисов и контекста
   - Почему `BackgroundService` работает через `IServiceScopeFactory`

3. **[03-ef-core-data-access.md](03-ef-core-data-access.md)** — EF Core и маппинг
   - Конфигурации `Event` и `Booking`
   - Fluent API и схема таблиц
   - Навигационные свойства и связи

4. **[04-implementation.md](04-implementation.md)** — Реализация в коде
   - Рефакторинг `EventService` и `BookingService`
   - Обновление `Program.cs` и `EnsureCreated`
   - Удаление устаревшего store-слоя

5. **[05-testing-and-run.md](05-testing-and-run.md)** — Тестирование и запуск
   - Unit/Integration тесты с `UseInMemoryDatabase`
   - Конкурентные тесты через отдельные scope
   - Пошаговый запуск через Docker + `dotnet`

6. **[06-diagrams.md](06-diagrams.md)** — Диаграммы
   - Архитектура после миграции
   - Поток создания бронирования
   - Работа фонового обработчика

## Как читать эту документацию

### Для новичков

1. [01-introduction.md](01-introduction.md)
2. [06-diagrams.md](06-diagrams.md)
3. [02-architecture.md](02-architecture.md)

### Для понимания кода

1. [03-ef-core-data-access.md](03-ef-core-data-access.md)
2. [04-implementation.md](04-implementation.md)
3. [05-testing-and-run.md](05-testing-and-run.md)

### Для проверки соответствия заданию

1. [sprint5-task.md](sprint5-task.md)
2. [sprint5-implementation-plan.md](sprint5-implementation-plan.md)
3. [05-testing-and-run.md](05-testing-and-run.md)

## Что принципиально изменилось относительно sprint 4

- Данные событий и бронирований теперь хранятся в PostgreSQL, а не в памяти процесса.
- Сервисный слой работает с `AppDbContext` и асинхронными EF Core операциями.
- Фоновый сервис использует scope на каждую операцию, чтобы безопасно работать со scoped-зависимостями.
- Удалён in-memory store-слой, который больше не нужен при EF Core архитектуре.

---

[Назад к документации по спринтам](../README.md)
