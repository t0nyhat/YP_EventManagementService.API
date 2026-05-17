using EventManagementService.API.BackgroundServices;
using EventManagementService.API.DataAccess;
using EventManagementService.API.Models;
using EventManagementService.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace EventManagementService.API.Tests.BackgroundServices;

public class BookingProcessingBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenPendingBookingExists_ConfirmsBookingAndSetsProcessedAt()
    {
        // Arrange
        using var serviceProvider = TestDbContextFactory.CreateServiceProvider();
        var createdAt = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);
        var eventId = await SeedEventAsync(serviceProvider);
        var bookingId = await SeedBookingAsync(serviceProvider, Booking.CreatePending(eventId, createdAt));
        var worker = new BookingProcessingBackgroundService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BookingProcessingBackgroundService>.Instance);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            // Act
            await worker.StartAsync(cancellation.Token);
            var processedBooking = await WaitForBookingStatusAsync(serviceProvider, bookingId, BookingStatus.Confirmed, TimeSpan.FromSeconds(5));

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
        using var serviceProvider = TestDbContextFactory.CreateServiceProvider();
        var eventId = Guid.NewGuid();
        var bookingId = await SeedBookingAsync(serviceProvider, Booking.CreatePending(eventId));

        var worker = new BookingProcessingBackgroundService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BookingProcessingBackgroundService>.Instance);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            // Act
            await worker.StartAsync(cancellation.Token);
            var processedBooking = await WaitForBookingStatusAsync(serviceProvider, bookingId, BookingStatus.Rejected, TimeSpan.FromSeconds(5));

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
    public async Task ExecuteAsync_WhenBookingAlreadyProcessed_SkipsItWithoutChangingStatus()
    {
        // Arrange
        using var serviceProvider = TestDbContextFactory.CreateServiceProvider();
        var processedAt = DateTime.UtcNow;
        var eventId = await SeedEventAsync(serviceProvider);
        var booking = Booking.CreatePending(eventId);
        booking.Confirm(processedAt);
        await SeedBookingAsync(serviceProvider, booking);

        var worker = new BookingProcessingBackgroundService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BookingProcessingBackgroundService>.Instance);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            // Act
            await worker.StartAsync(cancellation.Token);
            await Task.Delay(TimeSpan.FromSeconds(3), cancellation.Token);
            var storedBooking = await GetBookingAsync(serviceProvider, booking.Id);

            // Assert
            storedBooking.Should().NotBeNull();
            storedBooking!.Status.Should().Be(BookingStatus.Confirmed);
            storedBooking.ProcessedAt.Should().Be(processedAt);
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
        using var serviceProvider = TestDbContextFactory.CreateServiceProvider();
        var eventId = await SeedEventAsync(serviceProvider, bookingCount);
        var bookingIds = new List<Guid>();
        foreach (var booking in Enumerable.Range(0, bookingCount).Select(_ => Booking.CreatePending(eventId)))
        {
            bookingIds.Add(await SeedBookingAsync(serviceProvider, booking));
        }

        var worker = new BookingProcessingBackgroundService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BookingProcessingBackgroundService>.Instance);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            var startedAt = DateTime.UtcNow;

            // Act
            await worker.StartAsync(cancellation.Token);

            // Wait for all bookings to be confirmed.
            await Task.WhenAll(bookingIds.Select(id =>
                WaitForBookingStatusAsync(serviceProvider, id, BookingStatus.Confirmed, TimeSpan.FromSeconds(8))));

            var elapsed = DateTime.UtcNow - startedAt;

            // Assert: all three bookings confirmed
            foreach (var bookingId in bookingIds)
            {
                (await GetBookingAsync(serviceProvider, bookingId))!.Status.Should().Be(BookingStatus.Confirmed);
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
        using var serviceProvider = TestDbContextFactory.CreateServiceProvider();
        var worker = new BookingProcessingBackgroundService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BookingProcessingBackgroundService>.Instance);

        using var cancellation = new CancellationTokenSource();

        // Act
        await worker.StartAsync(cancellation.Token);
        cancellation.Cancel();

        var act = async () => await worker.StopAsync(CancellationToken.None);

        // Assert: stopping must complete without throwing
        await act.Should().NotThrowAsync();
    }

    private static async Task<Guid> SeedEventAsync(IServiceProvider serviceProvider, int totalSeats = 10)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var eventItem = Event.Create(
            "Событие",
            new DateTime(2026, 5, 1, 10, 0, 0),
            new DateTime(2026, 5, 1, 12, 0, 0),
            totalSeats);

        context.Events.Add(eventItem);
        await context.SaveChangesAsync();
        return eventItem.Id;
    }

    private static async Task<Guid> SeedBookingAsync(IServiceProvider serviceProvider, Booking booking)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        return booking.Id;
    }

    private static async Task<Booking?> GetBookingAsync(IServiceProvider serviceProvider, Guid bookingId)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Bookings.FirstOrDefaultAsync(item => item.Id == bookingId);
    }

    private static async Task<Booking> WaitForBookingStatusAsync(
        IServiceProvider serviceProvider,
        Guid bookingId,
        BookingStatus expectedStatus,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow <= deadline)
        {
            var booking = await GetBookingAsync(serviceProvider, bookingId);

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
