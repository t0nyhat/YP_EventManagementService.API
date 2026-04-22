# Реализация компонентов sprint 4

Этот раздел разбирает реализацию каждого компонента, который изменился или добавился в sprint 4.

## Модель Event с поддержкой вместимости

Файл: `Models/Event.cs`

### Новые поля

```csharp
/// <summary>
/// Total number of seats available for the event.
/// </summary>
public int TotalSeats { get; set; }

/// <summary>
/// Current number of free seats available for booking.
/// </summary>
public int AvailableSeats { get; set; }
```

### Методы управления местами

```csharp
public bool TryReserveSeats(int count = 1)
{
    if (count <= 0)
    {
        throw new ArgumentOutOfRangeException(nameof(count), 
            "Количество мест должно быть больше нуля.");
    }

    if (AvailableSeats < count)
    {
        return false;  // Нет достаточно мест
    }

    AvailableSeats -= count;
    return true;  // Места зарезервированы успешно
}

public void ReleaseSeats(int count = 1)
{
    if (count <= 0)
    {
        throw new ArgumentOutOfRangeException(nameof(count), 
            "Количество мест должно быть больше нуля.");
    }

    // Не может быть больше, чем TotalSeats
    AvailableSeats = Math.Min(TotalSeats, AvailableSeats + count);
}
```

**Обоснование**:

- `TryReserveSeats` возвращает `bool`, а не выбрасывает исключение, потому что это нормальное условие (не хватает мест) — это решение принимается на уровне сервиса
- `ReleaseSeats` использует `Math.Min` для защиты от случайного превышения TotalSeats
- Оба метода валидируют `count > 0` чтобы предотвратить логические ошибки

## Исключение NoAvailableSeatsException

Файл: `Exceptions/NoAvailableSeatsException.cs`

```csharp
namespace EventManagementService.API.Exceptions;

/// <summary>
/// Thrown when a booking cannot be created because no seats are available for the event.
/// </summary>
public class NoAvailableSeatsException : Exception
{
    public NoAvailableSeatsException(string message) : base(message) { }
}
```

**Простая реализация**, потому что:
- Исключение не несёт дополнительного состояния
- Сообщение об ошибке передаётся через параметр конструктора
- Стек вызовов достаточен для диагностики

Выбрасывается только в одном месте: `BookingService.CreateBookingAsync`.

## EventService: методы синхронизации

Файл: `Services/EventService.cs`

### Метод TryReserveSeats

```csharp
/// <inheritdoc />
public bool TryReserveSeats(Guid eventId)
{
    lock (_lock)
    {
        var eventItem = _events.FirstOrDefault(item => item.Id == eventId)
            ?? throw new NotFoundException($"Событие с id {eventId} не найдено.");

        return eventItem.TryReserveSeats();
    }
}
```

**Что это делает**:

1. Захватывает lock (синхронизация с другими операциями в EventService)
2. Ищет событие по Id
3. Выбрасывает NotFoundException, если события нет
4. Вызывает метод модели Event для резервирования

**Почему NotFoundException перед резервированием**:

- Если события нет, нельзя резервировать места
- Сервис должен явно предоставить эту информацию

### Метод ReleaseSeats

```csharp
/// <inheritdoc />
public void ReleaseSeats(Guid eventId)
{
    lock (_lock)
    {
        var eventItem = _events.FirstOrDefault(item => item.Id == eventId);
        eventItem?.ReleaseSeats();  // Безопасно, если события нет
    }
}
```

**Что это делает**:

1. Захватывает lock
2. Ищет событие по Id
3. Если событие найдено — вызывает ReleaseSeats на модели
4. Если события нет — просто ничего не делает

**Почему не выбрасывает NotFoundException**:

- Это метод для отката (откат при ошибке обработки)
- Если события нет, это означает, что оно было удалено между резервированием и откатом
- Нет смысла "откатывать" место для несуществующего события
- Обработчик ошибок в worker'е не хочет получать дополнительное исключение

### Обновление структуры ValidateEvent

```csharp
private static void ValidateEvent(Event eventItem)
{
    if (string.IsNullOrWhiteSpace(eventItem.Title))
    {
        throw new BusinessValidationException("Название события не должно быть пустым.");
    }

    if (eventItem.EndAt <= eventItem.StartAt)
    {
        throw new BusinessValidationException(
            "Дата окончания должна быть позже даты начала события.");
    }

    // НОВОЕ в sprint 4
    if (eventItem.TotalSeats <= 0)
    {
        throw new BusinessValidationException(
            "Количество мест должно быть больше нуля.");
    }

    if (eventItem.AvailableSeats < 0 || eventItem.AvailableSeats > eventItem.TotalSeats)
    {
        throw new BusinessValidationException(
            "Количество свободных мест должно быть в диапазоне от 0 до общего количества мест.");
    }
}
```

**Новые проверки**:

- `TotalSeats > 0` — событие должно иметь хотя бы одно место
- `AvailableSeats ∈ [0, TotalSeats]` — инвариант консистентности

### Изменение CreateEvent

```csharp
public Event CreateEvent(Event newEvent)
{
    newEvent.AvailableSeats = newEvent.TotalSeats;  // НОВОЕ: инициализация
    ValidateEvent(newEvent);

    lock (_lock)
    {
        newEvent.Id = Guid.NewGuid();
        _events.Add(newEvent);
        return newEvent;
    }
}
```

**НОВОЕ**: перед валидацией `AvailableSeats = TotalSeats`. Это гарантирует, что при создании события все места свободны.

### Изменение UpdateEvent

```csharp
public Event UpdateEvent(Guid id, Event updatedEvent)
{
    lock (_lock)
    {
        var existingEvent = _events.FirstOrDefault(item => item.Id == id);
        if (existingEvent is null)
        {
            throw new NotFoundException($"Событие с id {id} не найдено.");
        }

        // НОВОЕ: количество мест из существующего события (не меняется)
        updatedEvent.TotalSeats = existingEvent.TotalSeats;
        updatedEvent.AvailableSeats = existingEvent.AvailableSeats;

        ValidateEvent(updatedEvent);

        // Обновляем только текстовые поля
        existingEvent.Title = updatedEvent.Title;
        existingEvent.Description = updatedEvent.Description;
        existingEvent.StartAt = updatedEvent.StartAt;
        existingEvent.EndAt = updatedEvent.EndAt;

        return existingEvent;
    }
}
```

**НОВОЕ**: перед валидацией восстанавливаем `TotalSeats` и `AvailableSeats` из существующего события. Это предотвращает случайное изменение вместимости при обновлении.

## BookingService: защита критической секции

Файл: `Services/BookingService.cs`

### Поле блокировки

```csharp
// Protects the atomic check-reserve-save sequence against concurrent booking requests.
private readonly object _bookingLock = new();
```

Простой объект для использования с `lock`. Никакой особой логики здесь не требуется.

### Метод CreateBookingAsync

```csharp
/// <inheritdoc />
public Task<Booking> CreateBookingAsync(Guid eventId)
{
    lock (_bookingLock)
    {
        // Throws NotFoundException if event does not exist.
        var reserved = _eventService.TryReserveSeats(eventId);

        if (!reserved)
        {
            throw new NoAvailableSeatsException("Нет свободных мест на данное событие.");
        }

        var booking = Booking.CreatePending(eventId);
        var storedBooking = _bookingStore.Add(booking);

        return Task.FromResult(storedBooking);
    }
}
```

**Поток выполнения**:

1. Захват `lock (_bookingLock)` — только один поток за раз
2. Вызов `_eventService.TryReserveSeats(eventId)` — может выбросить `NotFoundException`
3. Если returned `false` — выброс `NoAvailableSeatsException`
4. Создание брони в статусе `Pending`
5. Сохранение в store
6. Возврат через `Task.FromResult` (имитация async результата)
7. Освобождение `lock`

**Почему this is atomic**:

Всё между `lock {` и `}` выполняется атомарно. Никакой другой поток не может пройти через эту последовательность одновременно.

## ExceptionHandlingMiddleware: маппирование 409

Файл: `Middleware/ExceptionHandlingMiddleware.cs`

```csharp
private static (int StatusCode, string Title, Uri Type) MapException(Exception exception)
{
    return exception switch
    {
        BusinessValidationException => 
            (StatusCodes.Status400BadRequest, "Validation error", ValidationType),
        
        // НОВОЕ в sprint 4
        NoAvailableSeatsException => 
            (StatusCodes.Status409Conflict, "Conflict", ConflictType),
        
        NotFoundException => 
            (StatusCodes.Status404NotFound, "Resource not found", NotFoundType),
        
        _ => 
            (StatusCodes.Status500InternalServerError, "Internal server error", ServerErrorType)
    };
}
```

**Маппирование**:

- `NoAvailableSeatsException` → `409 Conflict`
- Конфликт говорит: "Ваш запрос невозможно выполнить из-за текущего состояния сервера"

## EventBookingsController: документирование 409

Файл: `Controllers/EventBookingsController.cs`

```csharp
[HttpPost("{id:guid}/book")]
[ProducesResponseType(StatusCodes.Status202Accepted)]
[ProducesResponseType(StatusCodes.Status409Conflict)]  // НОВОЕ
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<BookingResponse>> CreateBooking([FromRoute] CreateBookingRequest request)
{
    var booking = await bookingService.CreateBookingAsync(request.EventId);
    return AcceptedAtRoute(BookingsController.GetBookingByIdRouteName, 
        new { id = booking.Id }, 
        booking.ToResponse());
}
```

**ProducesResponseType**: сообщает Swagger/OpenAPI, какие коды ответов может вернуть метод.

## BookingProcessingBackgroundService: параллельная обработка

Файл: `BackgroundServices/BookingProcessingBackgroundService.cs`

### Константы

```csharp
private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);
```

Вынесены в поля вместо магических чисел.

**PollingInterval**: как часто worker проверяет наличие pending-broней (1 сек)

**ProcessingDelay**: имитация внешнего вызова при обработке (2 сек)

### Семафор

```csharp
// Serializes write operations (status updates) while allowing delays to run in parallel.
private readonly SemaphoreSlim _processingSemaphore = new(1, 1);
```

`SemaphoreSlim(1, 1)` = mutex (0 или 1 токен).

### ExecuteAsync: основной loop

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _logger.LogInformation("Фоновая обработка бронирований запущена.");

    try
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var pendingIds = _bookingStore.GetPendingIds();

            if (pendingIds.Count > 0)
            {
                // Delays for all bookings run in parallel; writes are serialized inside ProcessBookingAsync.
                var tasks = pendingIds.Select(id => ProcessBookingAsync(id, stoppingToken));
                await Task.WhenAll(tasks);  // НОВОЕ: параллельно
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
        _logger.LogInformation("Фоновая обработка бронирований остановлена.");
    }
}
```

**Поток выполнения**:

1. Получить список pending-Id's
2. Для каждого Id создать Task обработки
3. Ждать все задачи одновременно (`Task.WhenAll`)
4. Спать полсекунды перед следующей итерацией
5. При CancellationToken корректно завершить

### ProcessBookingAsync: обработка одной брони

```csharp
private async Task ProcessBookingAsync(Guid bookingId, CancellationToken cancellationToken)
{
    _logger.LogInformation("Начата обработка бронирования с id {BookingId}.", bookingId);

    // Processing delay runs outside the semaphore so all bookings delay in parallel.
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
        await _processingSemaphore.WaitAsync(cancellationToken);
        semaphoreAcquired = true;

        // Критическая секция: чтение и обновление состояния
        var booking = _bookingStore.GetById(bookingId);
        if (booking is null || booking.Status != BookingStatus.Pending)
        {
            _logger.LogInformation(
                "Бронирование с id {BookingId} пропущено: оно уже не находится в статусе ожидания.",
                bookingId);
            return;
        }

        // Проверка существования события
        Event? eventItem = null;
        try
        {
            eventItem = _eventService.GetEventById(booking.EventId);
        }
        catch (NotFoundException) { }

        if (eventItem is null)
        {
            // Событие удалено — отклоняем бронь
            _bookingStore.TrySetStatus(bookingId, BookingStatus.Rejected, DateTime.UtcNow);
            _logger.LogWarning(
                "Событие для бронирования с id {BookingId} удалено. Бронирование отклонено.",
                bookingId);
            return;
        }

        // Событие существует — подтверждаем бронь
        _bookingStore.TrySetStatus(bookingId, BookingStatus.Confirmed, DateTime.UtcNow);
        _logger.LogInformation("Бронирование с id {BookingId} переведено в статус Confirmed.", bookingId);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception exception)
    {
        _logger.LogError(
            exception,
            "Ошибка при фоновой обработке бронирования с id {BookingId}.",
            bookingId);

        // При непредвиденной ошибке: отклоняем бронь и возвращаем место
        var booking = _bookingStore.GetById(bookingId);
        if (booking is not null && booking.Status == BookingStatus.Pending)
        {
            _bookingStore.TrySetStatus(bookingId, BookingStatus.Rejected, DateTime.UtcNow);
            _eventService.ReleaseSeats(booking.EventId);
        }
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

**Поток выполнения**:

1. **Логирование** начала обработки
2. **Задержка вне семафора** — `Task.Delay` выполняется для всех broней параллельно
3. **Попытка захватить семафор** — `WaitAsync` с флагом `semaphoreAcquired`
4. **Проверка статуса брони** — если уже обработана, выход
5. **Проверка существования события** — может быть удалено
6. **Если события нет** — отклоняем бронь, логируем warning
7. **Если событие есть** — подтверждаем бронь, логируем info
8. **При ошибке** — отклоняем бронь и возвращаем место через `ReleaseSeats`
9. **Finally блок** — всегда освобождаем семафор

**Обработка исключений**:

- `OperationCanceledException` при остановке — пробрасываем дальше
- `NotFoundException` при поиске события — игнорируем (событие удалено)
- Другие исключения — логируем и откатываем (отклоняем бронь, возвращаем место)

## DTO: CreateBookingRequest и BookingResponse

Файл: `Dtos/CreateBookingRequest.cs`

```csharp
public class CreateBookingRequest
{
    [FromRoute(Name = "id")]
    public Guid EventId { get; set; }
}
```

Файл: `Dtos/BookingResponse.cs`

```csharp
public class BookingResponse
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
```

Эти DTO не изменились от sprint 3, но теперь они используются в контексте синхронизации и параллельной обработки.

---

[Далее: Стратегия тестирования →](05-testing.md)
