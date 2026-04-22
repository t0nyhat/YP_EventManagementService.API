using EventManagementService.API.BackgroundServices;
using EventManagementService.API.Dtos;
using EventManagementService.API.Exceptions;
using EventManagementService.API.Models;
using EventManagementService.API.Services;
using EventManagementService.API.Stores;
using EventManagementService.API.Tests.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventManagementService.API.Tests.BackgroundServices;

public class BookingProcessingBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenPendingBookingExists_ConfirmsBookingAndSetsProcessedAt()
    {
        // Arrange
        var eventService = new EventService();
        var store = new InMemoryBookingStore();
        var createdAt = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);
        var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(
            title: "Событие",
            description: null,
            startAt: new DateTime(2026, 5, 1, 10, 0, 0),
            endAt: new DateTime(2026, 5, 1, 12, 0, 0)));
        var booking = store.Add(Booking.CreatePending(createdEvent.Id, createdAt));
        var worker = new BookingProcessingBackgroundService(
            store,
            eventService,
            NullLogger<BookingProcessingBackgroundService>.Instance);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            // Act
            await worker.StartAsync(cancellation.Token);
            var processedBooking = await WaitForBookingStatusAsync(store, booking.Id, BookingStatus.Confirmed, TimeSpan.FromSeconds(5));

            // Assert
            processedBooking.Status.Should().Be(BookingStatus.Confirmed);
            processedBooking.ProcessedAt.Should().NotBeNull();
            processedBooking.ProcessedAt!.Value.Should().BeAfter(createdAt);
        }
        finally
        {
            cancellation.Cancel();
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenEventIsDeletedBeforeProcessing_RejectsBooking()
    {
        // Arrange
        var eventService = new EventService();
        var store = new InMemoryBookingStore();
        var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(
            title: "Удаляемое событие",
            description: null,
            startAt: new DateTime(2026, 5, 2, 10, 0, 0),
            endAt: new DateTime(2026, 5, 2, 12, 0, 0)));
        var booking = store.Add(Booking.CreatePending(createdEvent.Id));

        // Delete the event before the background service processes the booking.
        eventService.DeleteEvent(createdEvent.Id);

        var worker = new BookingProcessingBackgroundService(
            store,
            eventService,
            NullLogger<BookingProcessingBackgroundService>.Instance);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            // Act
            await worker.StartAsync(cancellation.Token);
            var processedBooking = await WaitForBookingStatusAsync(store, booking.Id, BookingStatus.Rejected, TimeSpan.FromSeconds(5));

            // Assert
            processedBooking.Status.Should().Be(BookingStatus.Rejected);
            processedBooking.ProcessedAt.Should().NotBeNull();
        }
        finally
        {
            cancellation.Cancel();
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenEventServiceThrows_RejectsBookingAndReleasesSeats()
    {
        // Arrange
        var eventService = new EventService();
        var store = new InMemoryBookingStore();
        var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(
            title: "Событие с ошибкой",
            description: null,
            startAt: new DateTime(2026, 5, 3, 10, 0, 0),
            endAt: new DateTime(2026, 5, 3, 12, 0, 0),
            totalSeats: 5));

        // Reserve one seat manually to simulate what BookingService does.
        eventService.TryReserveSeats(createdEvent.Id);
        var booking = store.Add(Booking.CreatePending(createdEvent.Id));

        // Use a stubbed event service that throws after the seat was reserved.
        var throwingService = new ThrowingEventService(eventService);

        var worker = new BookingProcessingBackgroundService(
            store,
            throwingService,
            NullLogger<BookingProcessingBackgroundService>.Instance);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            // Act
            await worker.StartAsync(cancellation.Token);
            var processedBooking = await WaitForBookingStatusAsync(store, booking.Id, BookingStatus.Rejected, TimeSpan.FromSeconds(5));

            // Assert
            processedBooking.Status.Should().Be(BookingStatus.Rejected);
            var eventAfter = eventService.GetEventById(createdEvent.Id);
            // Seat should be restored because ReleaseSeats was called during exception handling.
            eventAfter.AvailableSeats.Should().Be(5);
        }
        finally
        {
            cancellation.Cancel();
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenMultiplePendingBookingsExist_ProcessesThemAllInParallel()
    {
        // Arrange
        const int bookingCount = 3;
        var eventService = new EventService();
        var store = new InMemoryBookingStore();
        var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(
            title: "Параллельное событие",
            description: null,
            startAt: new DateTime(2026, 5, 4, 10, 0, 0),
            endAt: new DateTime(2026, 5, 4, 12, 0, 0),
            totalSeats: bookingCount));
        var bookings = Enumerable.Range(0, bookingCount)
            .Select(_ => store.Add(Booking.CreatePending(createdEvent.Id)))
            .ToArray();

        var worker = new BookingProcessingBackgroundService(
            store,
            eventService,
            NullLogger<BookingProcessingBackgroundService>.Instance);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            var startedAt = DateTime.UtcNow;

            // Act
            await worker.StartAsync(cancellation.Token);

            // Wait for all bookings to be confirmed.
            await Task.WhenAll(bookings.Select(b =>
                WaitForBookingStatusAsync(store, b.Id, BookingStatus.Confirmed, TimeSpan.FromSeconds(8))));

            var elapsed = DateTime.UtcNow - startedAt;

            // Assert: all three bookings confirmed
            foreach (var b in bookings)
            {
                store.GetById(b.Id)!.Status.Should().Be(BookingStatus.Confirmed);
            }

            // With Task.WhenAll the total elapsed time should be closer to one
            // processing cycle (delay ~2s) than to bookingCount cycles (~6s).
            elapsed.Should().BeLessThan(TimeSpan.FromSeconds(6));
        }
        finally
        {
            cancellation.Cancel();
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_StopsGracefullyWithoutException()
    {
        // Arrange
        var eventService = new EventService();
        var store = new InMemoryBookingStore();
        var worker = new BookingProcessingBackgroundService(
            store,
            eventService,
            NullLogger<BookingProcessingBackgroundService>.Instance);

        using var cancellation = new CancellationTokenSource();

        // Act
        await worker.StartAsync(cancellation.Token);
        cancellation.Cancel();

        var act = async () => await worker.StopAsync(CancellationToken.None);

        // Assert: stopping must complete without throwing
        await act.Should().NotThrowAsync();
    }

    private static async Task<Booking> WaitForBookingStatusAsync(
        InMemoryBookingStore store,
        Guid bookingId,
        BookingStatus expectedStatus,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow <= deadline)
        {
            var booking = store.GetById(bookingId);

            if (booking is not null && booking.Status == expectedStatus)
            {
                return booking;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        throw new TimeoutException(
            $"Бронирование с id {bookingId} не достигло статуса {expectedStatus} за {timeout.TotalSeconds} секунд.");
    }
}

/// <summary>
/// Test double that delegates all calls to the real EventService
/// but throws an InvalidOperationException on GetEventById to simulate an unexpected error.
/// </summary>
file sealed class ThrowingEventService : IEventService
{
    private readonly IEventService _inner;

    public ThrowingEventService(IEventService inner) => _inner = inner;

    public Event GetEventById(Guid id) =>
        throw new InvalidOperationException("Симулированная ошибка при получении события.");

    public void ReleaseSeats(Guid eventId) => _inner.ReleaseSeats(eventId);

    public bool TryReserveSeats(Guid eventId) => _inner.TryReserveSeats(eventId);

    public Event CreateEvent(Event newEvent) => _inner.CreateEvent(newEvent);
    public PaginatedResult<Event> GetEvents(GetEventsQuery query) => _inner.GetEvents(query);
    public Event UpdateEvent(Guid id, Event updatedEvent) => _inner.UpdateEvent(id, updatedEvent);
    public void DeleteEvent(Guid id) => _inner.DeleteEvent(id);
}
