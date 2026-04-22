# Архитектурные решения sprint 4

После sprint 4 проект остаётся многослойным, но становится **потокобезопасным** и **параллельным**.

## Структура систем синхронизации

В sprint 4 появляются две **ортогональные** системы синхронизации:

### 1. Синхронизация в BookingService (синхронный lock)

**Где**: `Services/BookingService.cs`, метод `CreateBookingAsync`

**Что защищает**: атомарная последовательность "проверка мест → резервирование → сохранение брони"

```csharp
private readonly object _bookingLock = new();

public Task<Booking> CreateBookingAsync(Guid eventId)
{
    lock (_bookingLock)
    {
        // Критическая секция: только один поток за раз
        var reserved = _eventService.TryReserveSeats(eventId);
        if (!reserved)
            throw new NoAvailableSeatsException("Нет свободных мест на данное событие.");
        
        var booking = Booking.CreatePending(eventId);
        var storedBooking = _bookingStore.Add(booking);
        return Task.FromResult(storedBooking);
    }
}
```

**Почему lock, а не другие примитивы**:

- Синхронный код внутри — нет `await`
- Простота — lock гарантирует, что из критической секции выйдет ровно один поток за раз
- Предсказуемость — нет сложной очереди, очередность определяется ОС

**Почему lock внутри async-метода допустимо**:

- Lock захватывается до `await` (в нашем случае вообще нет `await` внутри)
- Если бы был `await` внутри, это было бы ошибкой, потому что `await` может передать управление другому потоку

### 2. Синхронизация в BackgroundService (асинхронный SemaphoreSlim)

**Где**: `BackgroundServices/BookingProcessingBackgroundService.cs`

**Что защищает**: сериализацию **записи в store**, при этом позволяя задержкам выполняться параллельно

```csharp
private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

private async Task ProcessBookingAsync(Guid bookingId, CancellationToken cancellationToken)
{
    // Задержка вне семафора — все bookings дёргают параллельно
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
        // Семафор — ожидание без блокировки потока
        await _processingSemaphore.WaitAsync(cancellationToken);
        semaphoreAcquired = true;

        // Критическая секция: чтение и обновление в store
        var booking = _bookingStore.GetById(bookingId);
        // ... логика обработки ...
        _bookingStore.TrySetStatus(bookingId, BookingStatus.Confirmed, DateTime.UtcNow);
    }
    finally
    {
        if (semaphoreAcquired)
        {
            _processingSemaphore.Release();
        }
    }
}
```

**Почему SemaphoreSlim, а не lock**:

- `lock` — синхронный примитив, не может использоваться с `await`
- `SemaphoreSlim` — асинхронный примитив, `WaitAsync` не блокирует поток, а переключает на другую работу
- Позволяет оставлять поток свободным для других задач во время `Task.Delay`

**Почему задержка вне семафора**:

```csharp
// ❌ Если бы задержка была внутри:
await Task.Delay(...);  // Один bookings держит семафор, остальные ждут
await _processingSemaphore.WaitAsync(...);  // Последовательная обработка!

// ✓ Правильно: задержка вне семафора
await Task.Delay(...);  // Все bookings дёргают параллельно
await _processingSemaphore.WaitAsync(...);  // Только запись сериализована
```

Это гарантирует, что если у нас 10 pending-broней, все 10 дёргают `Task.Delay(2 сек)` одновременно, и только потом попадают на семафор. Итого ~2 секунды, а не ~20.

## Почему мы защищаем разные части

### BookingService защищает создание бронирования

- Проблема: несколько HTTP-запросов одновременно
- Решение: `lock` на уровне сервиса
- Защищает: атомарность "проверка + резервирование"
- Результат: не может быть овербукинга

### BackgroundService защищает обновление в store

- Проблема: worker обрабатывает несколько broней параллельно, обновляет их состояние
- Решение: `SemaphoreSlim` для сериализации записи
- Защищает: не может быть race condition при `TrySetStatus`
- Результат: каждая бронь либо обновляется, либо нет, состояние консистентно

## Почему обработка параллельная

В sprint 3 обработка была последовательной:

```csharp
// Sprint 3: последовательно
foreach (var id in pendingIds)
{
    await ProcessBookingAsync(id);  // Ждём 1-й, потом 2-й, потом 3-й
}
```

Если 10 broней → 10 * 2 сек = 20 секунд обработки.

В sprint 4 обработка параллельная:

```csharp
// Sprint 4: параллельно
var tasks = pendingIds.Select(id => ProcessBookingAsync(id, stoppingToken));
await Task.WhenAll(tasks);  // Все ждут 2 сек одновременно
```

Если 10 broней → ~2 сек обработки (плюс overhead на сериализованные write-операции).

**Это безопасно**, потому что:
- Каждая обработка независима (работает с разными booking-объектами)
- Запись в store сериализована через `SemaphoreSlim`
- Нет гонок между обработчиками

## Что изменилось в модели Event

В sprint 3 `Event` был "только читаем" после создания (кроме удаления).

В sprint 4 `Event` имеет изменяемое состояние:

```csharp
public int TotalSeats { get; set; }
public int AvailableSeats { get; set; }

public bool TryReserveSeats(int count = 1)
{
    if (AvailableSeats < count)
        return false;
    AvailableSeats -= count;
    return true;
}

public void ReleaseSeats(int count = 1)
{
    AvailableSeats = Math.Min(TotalSeats, AvailableSeats + count);
}
```

**Почему TryReserveSeats возвращает bool**:

- Позволяет проверить наличие мест **и** зарезервировать их в одном вызове
- Если returned `false`, резервирование не произошло, и мы можем выбросить исключение
- Это работает внутри `lock`, поэтому безопасно

## Что изменилось в работе с событиями

### EventService теперь имеет методы синхронизации

```csharp
public bool TryReserveSeats(Guid eventId)
{
    lock (_lock)
    {
        var eventItem = _events.FirstOrDefault(item => item.Id == eventId)
            ?? throw new NotFoundException(...);
        return eventItem.TryReserveSeats();
    }
}

public void ReleaseSeats(Guid eventId)
{
    lock (_lock)
    {
        var eventItem = _events.FirstOrDefault(item => item.Id == eventId);
        eventItem?.ReleaseSeats();  // Безопасно, если события нет
    }
}
```

**Почему ReleaseSeats не выбрасывает NotFoundException**:

- При отклонении брони (из-за удаления события) мы уже знаем, что события нет
- Просто игнорируем: `eventItem?.ReleaseSeats()` работает, даже если события нет
- Это упрощает обработку ошибок в worker'е

### EventService при UPDATE не позволяет менять TotalSeats и AvailableSeats

```csharp
public Event UpdateEvent(Guid id, Event updatedEvent)
{
    lock (_lock)
    {
        var existingEvent = _events.FirstOrDefault(item => item.Id == id)
            ?? throw new NotFoundException(...);

        // Количество мест не меняется при обновлении!
        updatedEvent.TotalSeats = existingEvent.TotalSeats;
        updatedEvent.AvailableSeats = existingEvent.AvailableSeats;
        
        // Обновляем только текстовые поля и даты
        existingEvent.Title = updatedEvent.Title;
        existingEvent.Description = updatedEvent.Description;
        existingEvent.StartAt = updatedEvent.StartAt;
        existingEvent.EndAt = updatedEvent.EndAt;

        return existingEvent;
    }
}
```

Это предотвращает случайное изменение состояния мест при обновлении события.

## Новое исключение: NoAvailableSeatsException

```csharp
public class NoAvailableSeatsException : Exception
{
    public NoAvailableSeatsException(string message) : base(message) { }
}
```

Выбрасывается в `BookingService.CreateBookingAsync`, когда `TryReserveSeats()` вернул `false`.

Middleware маппирует его на `409 Conflict`.

## Почему валидация TotalSeats находится в EventService.ValidateEvent

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

Это гарантирует, что:
- `TotalSeats` всегда > 0
- `AvailableSeats` всегда в диапазоне `[0, TotalSeats]`
- Инвариант соблюдается везде, где проходит Event через валидацию

## Почему фоновый worker остаётся синглтоном

```csharp
builder.Services.AddHostedService<BookingProcessingBackgroundService>();
```

**HostedService** автоматически становится синглтоном:
- Один экземпляр на всё приложение
- Стартует при запуске приложения
- Останавливается при остановке приложения

Это правильно, потому что:
- Нам нужна одна единственная loop, обрабатывающая все pending-брони
- Если бы было несколько экземпляров, одна и та же бронь могла бы обрабатываться дважды

## Итоговая архитектура после sprint 4

```
┌─────────────────────────────────────────────────────────────┐
│                    HTTP Layer (Controllers)                 │
│         EventBookingsController, BookingsController         │
└────────┬────────────────────────────────────┬───────────────┘
         │                                    │
         ▼                                    ▼
┌──────────────────────────┐   ┌──────────────────────────┐
│   BookingService         │   │   EventService           │
│  lock (_bookingLock) ───▶│   │                          │
│  Atomic: check + save    │   │  lock (_lock)            │
│  May throw:              │   │  ├─ GetEventById         │
│  - NotFoundException     │   │  ├─ TryReserveSeats      │
│  - NoAvailableSeatsEx    │   │  └─ ReleaseSeats         │
└────────┬─────────────────┘   └──────────┬───────────────┘
         │                               │
         └───────────────┬────────────────┘
                         ▼
                   ┌─────────────────┐
                   │  InMemoryStore  │
                   │  Dictionary     │
                   │  lock (_lock)   │
                   └────────┬────────┘
                            │
       ┌────────────────────┴────────────────────┐
       │                                         │
       ▼                                         ▼
┌──────────────────────┐         ┌──────────────────────┐
│ BackgroundService    │         │ Event Loop (polling) │
│ SemaphoreSlim (1,1)  │         │ Task.WhenAll (par)   │
│ Parallelizes delays  │         │ Serializes writes    │
│ Serializes writes    │         │                      │
└──────────────────────┘         └──────────────────────┘
```

---

[Далее: Модели синхронизации →](03-synchronization.md)
