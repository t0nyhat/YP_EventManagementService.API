# Модели синхронизации и примитивы в sprint 4

Этот раздел разбирает мельчайшие детали двух систем синхронизации и объясняет выбор примитивов.

## Проблема race condition (гонка данных)

### Сценарий овербукинга без синхронизации

Представьте событие на **5 мест** и **20 параллельных запросов на бронирование**.

Без синхронизации:

```
Поток 1:  Проверка availableSeats = 5 ✓ Есть места
Поток 2:  Проверка availableSeats = 5 ✓ Есть места  (читает ещё не изменённое значение!)
Поток 3:  Проверка availableSeats = 5 ✓ Есть места  (читает ещё не изменённое значение!)
...
Поток 20: Проверка availableSeats = 5 ✓ Есть места

Поток 1:  Резервирование availableSeats -= 1 → 4
Поток 2:  Резервирование availableSeats -= 1 → 4  (перезаписал значение потока 1!)
Поток 3:  Резервирование availableSeats -= 1 → 4
...
Поток 20: Резервирование availableSeats -= 1 → 4  (результат: 4 вместо -15!)
```

Итог: все 20 запросов пройдут, хотя должны пройти только 5! **Овербукинг**.

### Решение: lock в BookingService

```csharp
private readonly object _bookingLock = new();

public Task<Booking> CreateBookingAsync(Guid eventId)
{
    lock (_bookingLock)  // ← Только один поток за раз может войти сюда
    {
        var reserved = _eventService.TryReserveSeats(eventId);
        if (!reserved)
            throw new NoAvailableSeatsException(...);
        
        var booking = Booking.CreatePending(eventId);
        var storedBooking = _bookingStore.Add(booking);
        return Task.FromResult(storedBooking);
    }
}
```

Теперь при одновременных запросах:

```
Поток 1:  lock(_bookingLock) → ВЫ ПЕРВЫЙ, ВХОДИТЕ
Поток 2:  lock(_bookingLock) → ЖДЁТЕ (очередь)
Поток 3:  lock(_bookingLock) → ЖДЁТЕ (очередь)
...
Поток 20: lock(_bookingLock) → ЖДЁТЕ (очередь)

Поток 1:  Проверка availableSeats = 5, резервирование → 4, сохранение
Поток 1:  Выход из lock (exit lock)

Поток 2:  lock(_bookingLock) → ВЫ СЛЕДУЮЩИЙ, ВХОДИТЕ
Поток 2:  Проверка availableSeats = 4, резервирование → 3, сохранение
...
```

Итог: ровно 5 потоков пройдут, остальные 15 получат `NoAvailableSeatsException`. **Не овербукинг**.

## Почему lock работает

### Гарантии lock'а

`lock (object)` в C#:

1. **Взаимное исключение** — только один поток может находиться в критической секции за раз
2. **Упорядочение** — когда поток выходит, очередной ждущий поток входит (очередь FIFO)
3. **Видимость памяти** — когда поток выходит из lock'а, все изменения в памяти видны следующему потоку

### Как это работает под капотом

```csharp
lock (obj)  // ← На самом деле это синтаксический сахар
{
    // критическая секция
}

// Реальный код (примерно):
Monitor.Enter(obj);          // Захват
try
{
    // критическая секция
}
finally
{
    Monitor.Exit(obj);       // Освобождение
}
```

`Monitor.Enter` блокирует поток (он просто ждёт в памяти), пока он не сможет захватить lock.

## SemaphoreSlim в BackgroundService

### Почему lock не подходит для async кода

Это ошибка:

```csharp
// ❌ ОШИБКА!
private async Task ProcessAsync()
{
    lock (_lockObj)
    {
        await Task.Delay(1000);  // ❌ Нельзя! lock не поддерживает async
    }
}
```

Компилятор выбросит ошибку: _"await cannot be used with lock statement"_.

**Почему**:

- `lock` — синхронный примитив, требует полного контроля над потоком
- `await` может передать управление другому потоку (thread context switch)
- Если после `await` вернётся другой поток, то он может заблокировать lock, но это не тот поток, который его захватил — deadlock!

### Решение: SemaphoreSlim

```csharp
private readonly SemaphoreSlim _semaphore = new(1, 1);

private async Task ProcessAsync()
{
    await _semaphore.WaitAsync();  // ← Асинхронное ожидание
    try
    {
        // Критическая секция
        await Task.Delay(1000);    // ← Теперь это OK
    }
    finally
    {
        _semaphore.Release();      // ← Освобождение
    }
}
```

### Как работает SemaphoreSlim

`SemaphoreSlim(1, 1)` означает:

- Первый параметр (1) — начальное количество "токенов" (разрешений)
- Второй параметр (1) — максимальное количество токенов

Поведение:

1. Первый поток вызывает `WaitAsync()` → берёт токен, продолжает
2. Второй поток вызывает `WaitAsync()` → нет токена, добавляется в очередь, ждёт (**без блокировки потока!**)
3. Первый поток вызывает `Release()` → возвращает токен
4. Второй поток автоматически продолжает (ему был возвращен токен)

### Ключевое отличие: блокировка потока vs. блокировка выполнения

**lock**: если не может захватить, **весь поток блокируется** (не может выполнять другой код)

```csharp
lock (obj)
{
    // Если не может захватить — поток просто стоит и ждёт, ничего не делает
}
// Пока здесь не может быть!
```

**SemaphoreSlim**: если не может захватить, **только задача приостанавливается** (поток может выполнять другой код)

```csharp
await _semaphore.WaitAsync();
{
    // Если не может захватить — задача приостанавливается
    // Но поток может перейти на другую задачу!
}
// Поток свободен до тех пор, пока задача не продолжится
```

## Почему в BackgroundService задержка вне семафора

Файл: `BackgroundServices/BookingProcessingBackgroundService.cs`

```csharp
private async Task ProcessBookingAsync(Guid bookingId, CancellationToken cancellationToken)
{
    // ✓ Задержка ВНЕ семафора
    try
    {
        await Task.Delay(ProcessingDelay, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }

    var semaphoreAcquired = false;
    try
    {
        // Семафор захватывается ПОСЛЕ задержки
        await _processingSemaphore.WaitAsync(cancellationToken);
        semaphoreAcquired = true;

        // Критическая секция: только чтение и обновление в store
        var booking = _bookingStore.GetById(bookingId);
        if (booking is null || booking.Status != BookingStatus.Pending)
            return;
        
        // ... логика обработки ...
        
        _bookingStore.TrySetStatus(bookingId, BookingStatus.Confirmed, DateTime.UtcNow);
    }
    finally
    {
        if (semaphoreAcquired)
            _processingSemaphore.Release();
    }
}
```

### Анализ времени выполнения

**Вариант 1: задержка внутри семафора (❌ неправильно)**

```csharp
await _semaphore.WaitAsync();
{
    await Task.Delay(2000);        // Занимает семафор на 2 секунды
    _bookingStore.TrySetStatus(...); // Потом пишет
}
_semaphore.Release();
```

Для 10 broней: 10 * 2 = **20 секунд** (последовательно)

**Вариант 2: задержка вне семафора (✓ правильно)**

```csharp
await Task.Delay(2000);           // Все 10 дёргают параллельно

await _semaphore.WaitAsync();
{
    _bookingStore.TrySetStatus(...); // Только это сериализовано
}
_semaphore.Release();
```

Для 10 broней: ~2 + (100 мс * 10 на write) = **~3 секунды** (параллельно)

**Математика**:

- Все 10 задач ждут Task.Delay(2 сек) одновременно
- По истечении 2 сек все 10 попадают на семафор
- Семафор позволяет одной задаче войти, остальные 9 ждут
- Первая пишет (~1 мс), выходит
- Вторая пишет (~1 мс), выходит
- И так далее
- Всего на writes: 10 * 1 мс = 10 мс

Итого: 2 сек на задержки + 10 мс на writes = ~2.01 сек (вместо 20!)

## Atomicity: почему TryReserveSeats возвращает bool

Файл: `Models/Event.cs`

```csharp
public bool TryReserveSeats(int count = 1)
{
    if (count <= 0)
        throw new ArgumentOutOfRangeException(nameof(count), "...");

    if (AvailableSeats < count)
        return false;

    AvailableSeats -= count;
    return true;
}
```

**Почему bool, а не два вызова**:

```csharp
// ❌ Неправильно (два вызова):
if (myEvent.AvailableSeats > 0)      // Проверка
{
    myEvent.AvailableSeats--;        // Резервирование
}
// Между проверкой и резервированием другой поток может изменить AvailableSeats!

// ✓ Правильно (одна операция):
if (myEvent.TryReserveSeats())       // Атомарная проверка + резервирование
{
    // Гарантия: если вернула true, место точно зарезервировано
}
```

Это работает только внутри `lock`, но сам метод `TryReserveSeats` гарантирует, что если он вернул `true`, то место точно зарезервировано.

## ReleaseSeats: безопасная операция

```csharp
public void ReleaseSeats(int count = 1)
{
    if (count <= 0)
        throw new ArgumentOutOfRangeException(nameof(count), "...");

    AvailableSeats = Math.Min(TotalSeats, AvailableSeats + count);
}
```

**Почему Math.Min**:

- Не может быть больше, чем TotalSeats
- Не может быть отрицательным (Math.Min гарантирует)

Пример:

```
TotalSeats = 10
AvailableSeats = 8 (брони 2 места)

ReleaseSeats(5) вернёт Math.Min(10, 8 + 5) = Math.Min(10, 13) = 10
// Не может отдать 13 мест, максимум 10
```

## Валидация в EventService.ValidateEvent

```csharp
private static void ValidateEvent(Event eventItem)
{
    // ...
    if (eventItem.TotalSeats <= 0)
    {
        throw new BusinessValidationException("Количество мест должно быть больше нуля.");
    }

    if (eventItem.AvailableSeats < 0 || eventItem.AvailableSeats > eventItem.TotalSeats)
    {
        throw new BusinessValidationException(
            "Количество свободных мест должно быть в диапазоне от 0 до общего количества мест.");
    }
}
```

Это гарантирует инварианты:

- TotalSeats ≥ 1 всегда
- 0 ≤ AvailableSeats ≤ TotalSeats всегда

## Сравнение примитивов синхронизации в C#

| Примитив | Тип | Async? | Для чего | Когда использовать |
|----------|-----|--------|---------|-------------------|
| `lock` | Взаимное исключение | ❌ | Синхронная критическая секция | Когда внутри нет `await` |
| `SemaphoreSlim` | Семафор (Mutex, если count=1) | ✓ | Асинхронная критическая секция | Когда внутри есть `await` |
| `Mutex` | Взаимное исключение (named) | ❌ | Inter-process sync | Когда нужен доступ из разных процессов |
| `ReaderWriterLockSlim` | Читающие vs. пишущие | ❌ | Когда много читающих, мало пишущих | Когда большой асимметрия в доступе |
| `Monitor` | Low-level примитив | ❌ | Уведомление между потоками | Когда нужны сложные сценарии Wait/Pulse |

## Исключение: NoAvailableSeatsException

```csharp
public class NoAvailableSeatsException : Exception
{
    public NoAvailableSeatsException(string message) : base(message) { }
}
```

Выбрасывается в `BookingService.CreateBookingAsync`:

```csharp
var reserved = _eventService.TryReserveSeats(eventId);
if (!reserved)
    throw new NoAvailableSeatsException("Нет свободных мест на данное событие.");
```

Middleware маппирует на `409 Conflict`:

```csharp
private static (int StatusCode, string Title, Uri Type) MapException(Exception exception)
{
    return exception switch
    {
        // ...
        NoAvailableSeatsException => 
            (StatusCodes.Status409Conflict, "Conflict", ConflictType),
        // ...
    };
}
```

**Почему 409, а не 400**:

- `400 Bad Request` — клиент отправил невалидные данные (неправильный формат, отсутствующее поле)
- `409 Conflict` — запрос конфликтует с текущим состоянием сервера (нет мест)

409 говорит: "Ваш запрос хорошо сформирован, но нельзя его выполнить из-за текущего состояния".

---

[Далее: Реализация компонентов →](04-implementation.md)
