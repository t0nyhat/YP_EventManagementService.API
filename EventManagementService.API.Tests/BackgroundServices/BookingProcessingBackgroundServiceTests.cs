using EventManagementService.API.BackgroundServices;
using EventManagementService.API.Models;
using EventManagementService.API.Stores;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventManagementService.API.Tests.BackgroundServices;

public class BookingProcessingBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenPendingBookingExists_ConfirmsBookingAndSetsProcessedAt()
    {
        // Arrange
        var store = new InMemoryBookingStore();
        var createdAt = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);
        var booking = store.Add(Booking.CreatePending(Guid.NewGuid(), createdAt));
        var worker = new BookingProcessingBackgroundService(
            store,
            NullLogger<BookingProcessingBackgroundService>.Instance);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        try
        {
            // Act
            await worker.StartAsync(cancellation.Token);

            var processedBooking = await WaitForProcessedBookingAsync(store, booking.Id, TimeSpan.FromSeconds(5));

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

    private static async Task<Booking> WaitForProcessedBookingAsync(
        InMemoryBookingStore store,
        Guid bookingId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow <= deadline)
        {
            var booking = store.GetById(bookingId);

            if (booking is not null && booking.Status == BookingStatus.Confirmed)
            {
                return booking;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        throw new TimeoutException(
            $"Фоновый обработчик не подтвердил бронирование с id {bookingId} за {timeout.TotalSeconds} секунд.");
    }
}
