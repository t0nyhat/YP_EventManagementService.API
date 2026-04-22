# Стратегия тестирования sprint 4

В sprint 4 тестирование фокусируется на **конкурентности**: обеспечение того, что система безопасна при одновременных запросах.

## Типы тестов в sprint 4

### 1. Тесты моделей: BookingTests.cs

Файл: `EventManagementService.API.Tests/Models/BookingTests.cs`

#### CreatePending

```csharp
[Fact]
public void CreatePending_WhenEventIdIsProvided_CreatesPendingBooking()
{
    var eventId = Guid.NewGuid();
    var createdAt = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);

    var booking = Booking.CreatePending(eventId, createdAt);

    booking.Status.Should().Be(BookingStatus.Pending);
    booking.ProcessedAt.Should().BeNull();
}
```

Проверяет, что фабричный метод правильно инициализирует бронь.

#### Confirm и Reject

```csharp
[Fact]
public void Confirm_WhenBookingIsPending_SetsConfirmedStatusAndProcessedAt()
{
    var booking = Booking.CreatePending(Guid.NewGuid(), new DateTime(...));
    var processedAt = new DateTime(...);

    booking.Confirm(processedAt);

    booking.Status.Should().Be(BookingStatus.Confirmed);
    booking.ProcessedAt.Should().Be(processedAt);
}
```

Проверяет переходы состояния.

#### Откат с возвратом мест

```csharp
[Fact]
public async Task BookingService_AfterRejectAndReleaseSeats_AllowsNewBookingOnSameEvent()
{
    // Arrange: event with 1 seat, one booking reserved and then rejected + seat released
    var eventService = new EventService();
    var bookingStore = new InMemoryBookingStore();
    var bookingService = new BookingService(bookingStore, eventService);

    var createdEvent = eventService.CreateEvent(new Event
    {
        Title = "Событие с возвратом",
        TotalSeats = 1,
        AvailableSeats = 1
    });

    var firstBooking = await bookingService.CreateBookingAsync(createdEvent.Id);

    // Simulate rejection + seat release
    bookingStore.TrySetStatus(firstBooking.Id, BookingStatus.Rejected, DateTime.UtcNow);
    eventService.ReleaseSeats(createdEvent.Id);

    // Act: should be able to create second booking
    var secondBooking = await bookingService.CreateBookingAsync(createdEvent.Id);

    // Assert
    secondBooking.Id.Should().NotBe(firstBooking.Id);
    eventService.GetEventById(createdEvent.Id).AvailableSeats.Should().Be(0);
}
```

**Ключевой тест**: проверяет, что откат мест действительно позволяет создать новую бронь.

### 2. Тесты сервиса: BookingServiceTests.cs

Файл: `EventManagementService.API.Tests/Services/BookingServiceTests.cs`

#### Успешное создание брони

```csharp
[Fact]
public async Task CreateBookingAsync_WhenEventExists_ReturnsPendingBooking()
{
    var eventService = new EventService();
    var bookingStore = new InMemoryBookingStore();
    var bookingService = new BookingService(bookingStore, eventService);
    var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(...));

    var booking = await bookingService.CreateBookingAsync(createdEvent.Id);

    booking.Status.Should().Be(BookingStatus.Pending);
    bookingStore.GetById(booking.Id).Should().NotBeNull();
}
```

Базовый тест успешного потока.

#### Уменьшение AvailableSeats

```csharp
[Fact]
public async Task CreateBookingAsync_WhenSeatsAreAvailable_DecreasesAvailableSeats()
{
    var eventService = new EventService();
    var bookingService = new BookingService(new InMemoryBookingStore(), eventService);
    var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(totalSeats: 3));

    await bookingService.CreateBookingAsync(createdEvent.Id);

    var updatedEvent = eventService.GetEventById(createdEvent.Id);
    updatedEvent.AvailableSeats.Should().Be(2);
}
```

**КРИТИЧЕСКИЙ ТЕСТ**: проверяет, что место действительно зарезервировано.

#### Недостаток мест

```csharp
[Fact]
public async Task CreateBookingAsync_WhenAllSeatsAreTaken_ThrowsNoAvailableSeatsException()
{
    var eventService = new EventService();
    var bookingService = new BookingService(new InMemoryBookingStore(), eventService);
    var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(totalSeats: 1));

    await bookingService.CreateBookingAsync(createdEvent.Id);

    var action = async () => await bookingService.CreateBookingAsync(createdEvent.Id);

    await action.Should().ThrowAsync<NoAvailableSeatsException>();
}
```

Проверяет, что вторая бронь при 1 месте выбрасывает исключение.

#### ⭐ Конкурентный доступ: защита от овербукинга

```csharp
[Fact]
public async Task CreateBookingAsync_WhenRequestedConcurrently_DoesNotExceedTotalSeats()
{
    const int totalSeats = 5;
    const int concurrentRequests = 20;

    var eventService = new EventService();
    var bookingService = new BookingService(new InMemoryBookingStore(), eventService);
    var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(totalSeats: totalSeats));

    var exceptions = new ConcurrentBag<Exception>();

    // Act: запустить 20 параллельных запросов
    var tasks = Enumerable.Range(0, concurrentRequests).Select(async _ =>
    {
        try
        {
            await bookingService.CreateBookingAsync(createdEvent.Id);
        }
        catch (NoAvailableSeatsException ex)
        {
            exceptions.Add(ex);
        }
    });

    await Task.WhenAll(tasks);

    // Assert: ровно 5 успешных, 15 ошибок
    var successCount = concurrentRequests - exceptions.Count;
    successCount.Should().Be(totalSeats);
    exceptions.Should().HaveCount(concurrentRequests - totalSeats);

    var finalEvent = eventService.GetEventById(createdEvent.Id);
    finalEvent.AvailableSeats.Should().Be(0);
}
```

**ЭТО САМЫЙ ВАЖНЫЙ ТЕСТ СПРИНТА**.

Проверяет:
1. 20 параллельных потоков одновременно вызывают CreateBookingAsync
2. Благодаря `lock`, ровно 5 пройдут, 15 получат исключение
3. `AvailableSeats` будет ровно 0 (не отрицательным!)

Если бы lock'а не было, все 20 могли бы пройти (овербукинг).

#### ⭐ Конкурентный доступ: уникальность Id

```csharp
[Fact]
public async Task CreateBookingAsync_WhenRequestedConcurrently_ReturnsUniqueBookingIds()
{
    const int totalSeats = 10;

    var eventService = new EventService();
    var bookingStore = new InMemoryBookingStore();
    var bookingService = new BookingService(bookingStore, eventService);
    var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(totalSeats: totalSeats));

    // Act: ровно 10 параллельных успешных запросов
    var tasks = Enumerable.Range(0, totalSeats)
        .Select(_ => bookingService.CreateBookingAsync(createdEvent.Id));

    var bookings = await Task.WhenAll(tasks);

    // Assert: все Id уникальны
    bookings.Select(b => b.Id).Should().OnlyHaveUniqueItems();
    bookings.Should().HaveCount(totalSeats);
}
```

Проверяет, что при параллельном создании нет дублирования Id.

### 3. Тесты фонового сервиса

Файл: `EventManagementService.API.Tests/BackgroundServices/BookingProcessingBackgroundServiceTests.cs`

#### Базовая обработка

```csharp
[Fact]
public async Task ExecuteAsync_WhenPendingBookingExists_ConfirmsBookingAndSetsProcessedAt()
{
    var eventService = new EventService();
    var store = new InMemoryBookingStore();
    var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(...));
    var booking = store.Add(Booking.CreatePending(createdEvent.Id, new DateTime(...)));
    var worker = new BookingProcessingBackgroundService(store, eventService, ...);

    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
    try
    {
        await worker.StartAsync(cancellation.Token);
        var processedBooking = await WaitForBookingStatusAsync(
            store, booking.Id, BookingStatus.Confirmed, TimeSpan.FromSeconds(5));

        processedBooking.Status.Should().Be(BookingStatus.Confirmed);
        processedBooking.ProcessedAt.Should().NotBeNull();
    }
    finally
    {
        cancellation.Cancel();
        await worker.StopAsync(CancellationToken.None);
    }
}
```

Проверяет, что worker правильно обрабатывает pending-бронь.

#### Обработка удалённого события

```csharp
[Fact]
public async Task ExecuteAsync_WhenEventIsDeletedBeforeProcessing_RejectsBooking()
{
    var eventService = new EventService();
    var store = new InMemoryBookingStore();
    var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(...));
    var booking = store.Add(Booking.CreatePending(createdEvent.Id));

    // Delete the event BEFORE background service processes the booking
    eventService.DeleteEvent(createdEvent.Id);

    var worker = new BookingProcessingBackgroundService(store, eventService, ...);

    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
    try
    {
        await worker.StartAsync(cancellation.Token);
        var processedBooking = await WaitForBookingStatusAsync(
            store, booking.Id, BookingStatus.Rejected, TimeSpan.FromSeconds(5));

        processedBooking.Status.Should().Be(BookingStatus.Rejected);
    }
    finally
    {
        cancellation.Cancel();
        await worker.StopAsync(CancellationToken.None);
    }
}
```

Проверяет корректность обработки: если событие удалено, бронь должна быть отклонена.

#### Обработка исключений

```csharp
[Fact]
public async Task ExecuteAsync_WhenEventServiceThrows_RejectsBookingAndReleasesSeats()
{
    var eventService = new EventService();
    var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(totalSeats: 5));

    // Reserve one seat
    eventService.TryReserveSeats(createdEvent.Id);
    var booking = store.Add(Booking.CreatePending(createdEvent.Id));

    // Use a stubbed service that throws
    var throwingService = new ThrowingEventService(eventService);
    var worker = new BookingProcessingBackgroundService(store, throwingService, ...);

    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
    try
    {
        await worker.StartAsync(cancellation.Token);
        var processedBooking = await WaitForBookingStatusAsync(
            store, booking.Id, BookingStatus.Rejected, TimeSpan.FromSeconds(5));

        processedBooking.Status.Should().Be(BookingStatus.Rejected);
        var eventAfter = eventService.GetEventById(createdEvent.Id);
        // Seat should be restored
        eventAfter.AvailableSeats.Should().Be(5);
    }
    finally
    {
        cancellation.Cancel();
        await worker.StopAsync(CancellationToken.None);
    }
}
```

**КЛЮЧЕВОЙ ТЕСТ**: при ошибке место должно быть возвращено.

#### ⭐ Параллельная обработка

```csharp
[Fact]
public async Task ExecuteAsync_WhenMultiplePendingBookingsExist_ProcessesThemAllInParallel()
{
    const int bookingCount = 3;
    var eventService = new EventService();
    var store = new InMemoryBookingStore();
    var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(totalSeats: bookingCount));
    var bookings = Enumerable.Range(0, bookingCount)
        .Select(_ => store.Add(Booking.CreatePending(createdEvent.Id)))
        .ToArray();

    var worker = new BookingProcessingBackgroundService(store, eventService, ...);

    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12));
    try
    {
        var startedAt = DateTime.UtcNow;

        await worker.StartAsync(cancellation.Token);

        // Wait for all to be confirmed
        await Task.WhenAll(bookings.Select(b =>
            WaitForBookingStatusAsync(store, b.Id, BookingStatus.Confirmed, 
                TimeSpan.FromSeconds(8))));

        var elapsed = DateTime.UtcNow - startedAt;

        // Assert
        foreach (var b in bookings)
        {
            store.GetById(b.Id)!.Status.Should().Be(BookingStatus.Confirmed);
        }

        // With Task.WhenAll total elapsed should be ~2s, not ~6s
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(6));
    }
    finally
    {
        cancellation.Cancel();
        await worker.StopAsync(CancellationToken.None);
    }
}
```

**КЛЮЧЕВОЙ ТЕСТ ПАРАЛЛЕЛИЗМА**.

Проверяет:
1. 3 pending-брони обрабатываются
2. Каждая имеет `Task.Delay(2 сек)`
3. Если были последовательны → 6 сек
4. Если параллельны → ~2 сек
5. Тест проверяет, что elapsed < 6 сек (параллелизм работает!)

Если SemaphoreSlim был бы внутри Task.Delay (неправильная реализация), тест бы не прошёл.

## Помощные методы в тестах

### EventTestData.CreateEvent

```csharp
public static Event CreateEvent(
    string title = "Test Event",
    string? description = null,
    DateTime? startAt = null,
    DateTime? endAt = null,
    int totalSeats = 10)
{
    return new Event
    {
        Title = title,
        Description = description,
        StartAt = startAt ?? new DateTime(2026, 5, 1, 10, 0, 0),
        EndAt = endAt ?? new DateTime(2026, 5, 1, 12, 0, 0),
        TotalSeats = totalSeats
    };
}
```

**Важно**: новый параметр `totalSeats` для управления вместимостью в тестах.

### WaitForBookingStatusAsync

```csharp
private static async Task<Booking> WaitForBookingStatusAsync(
    IBookingStore store,
    Guid bookingId,
    BookingStatus expectedStatus,
    TimeSpan timeout)
{
    var deadline = DateTime.UtcNow.Add(timeout);
    while (DateTime.UtcNow < deadline)
    {
        var booking = store.GetById(bookingId);
        if (booking?.Status == expectedStatus)
        {
            return booking;
        }
        await Task.Delay(100);
    }
    throw new TimeoutException($"Booking {bookingId} did not reach status {expectedStatus} within {timeout}");
}
```

Ждёт, пока бронь не достигнет нужного статуса (с timeout).

### ThrowingEventService

```csharp
internal sealed class ThrowingEventService : IEventService
{
    private readonly IEventService _inner;

    public ThrowingEventService(IEventService inner) => _inner = inner;

    public bool TryReserveSeats(Guid eventId)
    {
        throw new InvalidOperationException("Stubbed to throw");
    }

    // ... другие методы делегируют к _inner
}
```

Используется для имитации ошибок в service.

## Как запустить тесты

```bash
dotnet test
```

Все тесты должны пройти.

## Ключевые метрики покрытия

В sprint 4 важны тесты для:

- ✓ Создание брони с проверкой AvailableSeats
- ✓ Исчерпание мест
- ✓ **Конкурентные запросы (5 мест, 20 запросов)**
- ✓ **Уникальность Id при параллелизме**
- ✓ Параллельная обработка в worker (3 брони, <6 сек)
- ✓ Обработка удалённого события (reject)
- ✓ Откат мест при ошибке

Без конкурентных тестов невозможно убедиться, что синхронизация работает.

---

[Далее: Диаграммы и визуализация →](06-diagrams.md)
