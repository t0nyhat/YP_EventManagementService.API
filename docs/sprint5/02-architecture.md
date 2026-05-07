# Архитектурные решения sprint 5

## 1. Слой данных

В проект добавлен отдельный слой `DataAccess`:

- `AppDbContext`
- `EventConfiguration`
- `BookingConfiguration`

Такое разделение оставляет бизнес-логику в сервисах, а конфигурацию хранения — в одном месте.

## 2. Модель жизненных циклов

### До sprint 5

- in-memory коллекции
- singleton-сервисы
- без внешней БД

### После sprint 5

- `AppDbContext` — scoped
- `IEventService` / `IBookingService` — scoped
- `BackgroundService` (singleton) создаёт scope через `IServiceScopeFactory`

Это устраняет конфликт жизненных циклов и делает поведение предсказуемым.

## 3. Почему `IServiceScopeFactory` в worker

`BackgroundService` живёт весь срок приложения, а `DbContext` должен жить в пределах операции.

Поэтому worker:

1. создаёт scope,
2. получает `AppDbContext`,
3. выполняет чтение/обновление,
4. освобождает scope.

Так исключаются утечки контекста и ошибки вида "Cannot consume scoped service from singleton".

## 4. Контракты сервисов

Сервис событий переведён на async API:

- `GetEventsAsync`
- `GetEventByIdAsync`
- `CreateEventAsync`
- `UpdateEventAsync`
- `DeleteEventAsync`

Это важно, потому что операции с БД не должны блокировать поток синхронными вызовами.

## 5. Что удалено из архитектуры

После перехода на EF Core удалены:

- `IBookingStore`
- `InMemoryBookingStore`
- тесты store-слоя

Хранилище теперь единообразно реализовано через PostgreSQL + EF Core.

---

[Далее: EF Core и DataAccess →](03-ef-core-data-access.md)
