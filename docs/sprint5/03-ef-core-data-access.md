# EF Core и DataAccess в sprint 5

## AppDbContext

`AppDbContext`:

- наследуется от `DbContext`;
- содержит `DbSet<Event>` и `DbSet<Booking>`;
- подключает конфигурации через `ApplyConfigurationsFromAssembly`.

Это гарантирует, что все `IEntityTypeConfiguration<T>` будут применены автоматически.

## Конфигурация Event

В `EventConfiguration` настроены:

- таблица `events`;
- PK `id` с `ValueGeneratedNever()`;
- `title` как required + ограничение длины;
- `description` с ограничением длины;
- поля времени и мест как required.

## Конфигурация Booking

В `BookingConfiguration` настроены:

- таблица `bookings`;
- PK `id` с `ValueGeneratedNever()`;
- внешний ключ `event_id`;
- `status` как строка через `HasConversion<string>()`;
- `created_at`/`processed_at`.

## Связь сущностей

Связь между `Event` и `Booking` реализована через:

- навигацию `Event.Bookings`;
- навигацию `Booking.Event`;
- внешний ключ `Booking.EventId`.

При удалении события связанные бронирования удаляются каскадно.

## Почему это лучше in-memory хранения

1. Данные не теряются после перезапуска.
2. Можно анализировать состояние БД внешними инструментами.
3. Архитектура становится ближе к production-практике.
4. Маппинг схемы контролируется в коде через Fluent API.

---

[Далее: Реализация в коде →](04-implementation.md)
