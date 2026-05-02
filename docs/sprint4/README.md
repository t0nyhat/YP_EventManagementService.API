# Sprint 4: Документация

Полная учебная документация по спринту 4: Параллельная обработка и синхронизация при конкурентных запросах.

## Структура документации

1. **[01-introduction.md](01-introduction.md)** — Обзор спринта, проблемы и цели
   - Назначение спринта
   - Ключевые изменения
   - Почему это важно

2. **[02-architecture.md](02-architecture.md)** — Архитектурные решения
   - Две системы синхронизации
   - Почему `lock` в BookingService
   - Почему `SemaphoreSlim` в BackgroundService
   - Общая архитектура после sprint 4

3. **[03-synchronization.md](03-synchronization.md)** — Модели синхронизации и примитивы
   - Проблема race condition
   - Как работает `lock`
   - Как работает `SemaphoreSlim`
   - Почему задержка должна быть вне семафора
   - Атомарность и валидация

4. **[04-implementation.md](04-implementation.md)** — Реализация каждого компонента
   - Model Event с TotalSeats и AvailableSeats
   - NoAvailableSeatsException
   - EventService методы синхронизации
   - BookingService критическая секция
   - ExceptionHandlingMiddleware маппирование 409
   - BackgroundService параллельная обработка

5. **[05-testing.md](05-testing.md)** — Стратегия тестирования
   - Типы тестов (модели, сервис, worker)
   - Конкурентные тесты (самые важные)
   - Как запустить и ключевые метрики

6. **[06-diagrams.md](06-diagrams.md)** — Диаграммы и визуализация
   - Диаграмма потоков при конкурентных запросах
   - Параллельная обработка vs. последовательная
   - Timeline обработки
   - Диаграмма обработки ошибок

## Как читать эту документацию

### Для новичков в многопоточности

**Начните здесь**:
1. [01-introduction.md](01-introduction.md) — понять проблему
2. [06-diagrams.md](06-diagrams.md) — посмотреть диаграммы race condition
3. [03-synchronization.md](03-synchronization.md) — разобраться с примитивами

### Для понимания архитектуры

1. [02-architecture.md](02-architecture.md) — общий дизайн
2. [04-implementation.md](04-implementation.md) — деталь каждого компонента
3. [05-testing.md](05-testing.md) — как это тестируется

### Для разработки

1. [04-implementation.md](04-implementation.md) — где что находится
2. [05-testing.md](05-testing.md) — как писать тесты
3. Исходный код с комментариями

## Ключевые концепции

### Race Condition (Гонка данных)

Когда несколько потоков одновременно читают и пишут в общую переменную, и порядок выполнения недетерминирован.

**Пример**: 2 потока, 5 мест
```
Thread 1: читает availableSeats = 5
Thread 2: читает availableSeats = 5  ← Оба читают старое значение!
Thread 1: пишет availableSeats -= 1 → 4
Thread 2: пишет availableSeats -= 1 → 4  ← Перезаписала!
Результат: 4 (должно быть 3)
```

**Решение**: `lock` — только один поток за раз

### Critical Section (Критическая секция)

Блок кода, который должен выполняться только одним потоком за раз.

В sprint 4:
- **BookingService**: `lock (_bookingLock) { проверка + резервирование + сохранение }`
- **BackgroundService**: `await _semaphore.WaitAsync() { чтение и обновление в store }`

### Atomicity (Атомарность)

Операция либо полностью выполняется, либо вообще не выполняется. Никаких промежуточных состояний видно внешнему наблюдателю.

`TryReserveSeats()` атомарна: если вернула `true`, место гарантировано зарезервировано.

### Synchronization Primitive (Примитив синхронизации)

Инструмент ОС для защиты доступа к общим ресурсам:

- **`lock`** — синхронный (блокирует поток если не может захватить)
- **`SemaphoreSlim`** — асинхронный (задача ждёт, но поток может выполнять другое)

### Overoverbooking (Овербукинг)

Когда количество успешных броней превышает количество доступных мест.

**Пример**: событие на 5 мест, но создано 20 броней.

Решение в sprint 4: `lock` в BookingService.

## Примеры из кода

### Пример 1: Lock в BookingService

```csharp
private readonly object _bookingLock = new();

public Task<Booking> CreateBookingAsync(Guid eventId)
{
    lock (_bookingLock)
    {
        var reserved = _eventService.TryReserveSeats(eventId);
        if (!reserved)
            throw new NoAvailableSeatsException(...);
        
        var booking = Booking.CreatePending(eventId);
        return Task.FromResult(_bookingStore.Add(booking));
    }
}
```

**Что это гарантирует**: даже если 100 потоков вызывают это одновременно, они будут проходить по очереди. Овербукинга не будет.

### Пример 2: SemaphoreSlim и параллельные задержки

```csharp
private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

private async Task ProcessBookingAsync(Guid bookingId, CancellationToken cancellationToken)
{
    // Задержка ВНЕ семафора — все bookings дёргают параллельно
    await Task.Delay(2000);

    // Семафор захватывается ПОСЛЕ задержки
    await _processingSemaphore.WaitAsync(cancellationToken);
    try
    {
        // Только эта часть сериализована
        _bookingStore.TrySetStatus(bookingId, BookingStatus.Confirmed, DateTime.UtcNow);
    }
    finally
    {
        _processingSemaphore.Release();
    }
}
```

**Что это обеспечивает**: 10 bookings обрабатываются за ~2 сек (не за 20 сек), потому что все дёргают Task.Delay одновременно.

### Пример 3: Обработка удаления события

```csharp
Event? eventItem = null;
try
{
    eventItem = _eventService.GetEventById(booking.EventId);
}
catch (NotFoundException) { }

if (eventItem is null)
{
    // Событие было удалено до обработки
    _bookingStore.TrySetStatus(bookingId, BookingStatus.Rejected, DateTime.UtcNow);
    _logger.LogWarning("Событие удалено. Бронь отклонена.");
    return;
}

// Событие существует — подтверждаем
_bookingStore.TrySetStatus(bookingId, BookingStatus.Confirmed, DateTime.UtcNow);
```

**Что это гарантирует**: если событие было удалено между созданием брони и фоновой обработкой, бронь будет корректно отклонена.

## Тестирование

### Команда для запуска тестов

```bash
dotnet test
```

### Ключевые тесты на конкурентность

1. **Защита от овербукинга** (5 мест, 20 конкурентных запросов)
   ```csharp
   CreateBookingAsync_WhenRequestedConcurrently_DoesNotExceedTotalSeats
   ```
   Проверяет: ровно 5 успешных, 15 ошибок, availableSeats = 0

2. **Параллельная обработка** (3 broni, должны обработаться за ~2 сек)
   ```csharp
   ExecuteAsync_WhenMultiplePendingBookingsExist_ProcessesThemAllInParallel
   ```
   Проверяет: elapsed < 6 сек (параллелизм работает!)

## Запуск и проверка

### Подготовка

```bash
dotnet build
dotnet run
```

### Swagger для ручного тестирования

1. Открыть `http://localhost:5248/swagger`
2. Создать событие `POST /api/events` с `totalSeats: 3`
3. Создать 4 брони `POST /api/events/{id}/book`
4. Первые 3 вернут `202 Accepted`
5. 4-я вернёт `409 Conflict` (нет мест)
6. Проверить `GET /api/bookings/{id}` — статус изменится на `Confirmed` через 2-3 сек

### Проверка логов

```
[info] Фоновая обработка бронирований запущена.
[info] Начата обработка бронирования с id ...
[info] Начата обработка бронирования с id ...
[info] Начата обработка бронирования с id ...
[info] Бронирование с id ... переведено в статус Confirmed.
[info] Бронирование с id ... переведено в статус Confirmed.
[info] Бронирование с id ... переведено в статус Confirmed.
```

Логи должны показывать параллельную обработку (все 3 стартуют почти одновременно).

## Дополнительные ресурсы

- **README главного проекта**: `../README.md`
- **Задание спринта**: `sprint4-task.md`
- **Исходный код**: `../Program.cs`, `../Services/`, `../Models/`, `../BackgroundServices/`
- **Тесты**: `../EventManagementService.API.Tests/`

## Контрольные вопросы

После прочтения документации вы должны уметь ответить:

1. **Что такое race condition и почему это проблема?**
2. **Почему нельзя использовать `lock` с `await`?**
3. **Как `SemaphoreSlim` отличается от `lock`?**
4. **Почему в BackgroundService задержка должна быть вне семафора?**
5. **Что гарантирует `TryReserveSeats`?**
6. **Как откатить место при ошибке обработки?**
7. **Почему background worker остаётся синглтоном?**
8. **Как тестировать конкурентность?**

---

[Назад к документации по спринтам](../README.md)
