# Реализация sprint 5

## 1. Program.cs

В startup выполнены ключевые шаги:

1. Регистрация `AppDbContext` через `UseNpgsql`.
2. Регистрация scoped-сервисов через DI.
3. Вызов `EnsureCreated` после `builder.Build()`.

Это обеспечивает автоматическое создание схемы БД при первом запуске.

## 2. EventService

`EventService` теперь:

- работает через `_context.Events`;
- использует async EF Core методы (`FirstOrDefaultAsync`, `CountAsync`, `ToArrayAsync`);
- вызывает `SaveChangesAsync()` после операций записи.

Бизнес-валидация сохранена и не вынесена в контроллеры.

## 3. BookingService

`BookingService` теперь:

- читает событие из `_context.Events`;
- создаёт бронь в `_context.Bookings`;
- фиксирует изменения одним `SaveChangesAsync()`.

Для защиты критической секции используется `SemaphoreSlim`, чтобы корректно работать в async-коде.

## 4. BackgroundService

Фоновый обработчик:

- использует `IServiceScopeFactory`;
- получает pending-брони в отдельном scope;
- обрабатывает каждую бронь в своём scope;
- подтверждает/отклоняет бронирование и сохраняет изменения через EF Core.

## 5. Рефакторинг и cleanup

В рамках миграции удалены устаревшие элементы in-memory архитектуры и соответствующие тесты.

Результат: кодовая база стала проще, а ответственность слоёв — более явной.

---

[Далее: Тестирование и запуск →](05-testing-and-run.md)
