using EventManagementService.API.BackgroundServices;
using EventManagementService.API.Exceptions;
using EventManagementService.API.Models;
using EventManagementService.API.Services;
using EventManagementService.API.Stores;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace EventManagementService.API.Tests.BackgroundServices;

public class BookingProcessingBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenPendingBookingExists_ConfirmsBookingAndSetsProcessedAt()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var eventService = new Mock<IEventService>();
        eventService
            .Setup(service => service.GetEventById(eventId))
            .Returns(Event.Create("Событие", new DateTime(2026, 5, 1, 10, 0, 0), new DateTime(2026, 5, 1, 12, 0, 0), 10));

        var store = new InMemoryBookingStore();
        var createdAt = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);
        var booking = store.Add(Booking.CreatePending(eventId, createdAt));
        var worker = new BookingProcessingBackgroundService(
            store,
            eventService.Object,
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
        var eventId = Guid.NewGuid();
        var eventService = new Mock<IEventService>();
        eventService
            .Setup(service => service.GetEventById(eventId))
            .Throws(new NotFoundException("Событие не найдено."));

        var store = new InMemoryBookingStore();
        var booking = store.Add(Booking.CreatePending(eventId));

        var worker = new BookingProcessingBackgroundService(
            store,
            eventService.Object,
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
        var eventId = Guid.NewGuid();
        var eventService = new Mock<IEventService>();
        eventService
            .Setup(service => service.GetEventById(eventId))
            .Throws(new InvalidOperationException("Симулированная ошибка при получении события."));

        var store = new InMemoryBookingStore();
        var booking = store.Add(Booking.CreatePending(eventId));

        var worker = new BookingProcessingBackgroundService(
            store,
            eventService.Object,
            NullLogger<BookingProcessingBackgroundService>.Instance);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            // Act
            await worker.StartAsync(cancellation.Token);
            var processedBooking = await WaitForBookingStatusAsync(store, booking.Id, BookingStatus.Rejected, TimeSpan.FromSeconds(5));

            // Assert
            processedBooking.Status.Should().Be(BookingStatus.Rejected);
            eventService.Verify(service => service.ReleaseSeats(eventId), Times.Once);
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
        var eventId = Guid.NewGuid();
        var eventService = new Mock<IEventService>();
        eventService
            .Setup(service => service.GetEventById(eventId))
            .Returns(Event.Create("Параллельное событие", new DateTime(2026, 5, 4, 10, 0, 0), new DateTime(2026, 5, 4, 12, 0, 0), bookingCount));

        var store = new InMemoryBookingStore();
        var bookings = Enumerable.Range(0, bookingCount)
            .Select(_ => store.Add(Booking.CreatePending(eventId)))
            .ToArray();

        var worker = new BookingProcessingBackgroundService(
            store,
            eventService.Object,
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
        var eventService = new Mock<IEventService>();
        var store = new InMemoryBookingStore();
        var worker = new BookingProcessingBackgroundService(
            store,
            eventService.Object,
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
