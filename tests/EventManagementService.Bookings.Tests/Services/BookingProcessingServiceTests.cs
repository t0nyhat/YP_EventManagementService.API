using EventManagementService.Bookings.Application.Services;
using EventManagementService.Bookings.Domain.Models;
using EventManagementService.Bookings.Infrastructure.DataAccess;
using EventManagementService.Bookings.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventManagementService.Bookings.Tests.Services;

public sealed class BookingProcessingServiceTests : IDisposable
{
    private readonly BookingsDbContext _context;

    public BookingProcessingServiceTests()
    {
        var options = new DbContextOptionsBuilder<BookingsDbContext>()
            .UseInMemoryDatabase($"BookingsProcessingTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new BookingsDbContext(options);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProcessPendingBookingAsync_WhenBookingIsPending_ConfirmsAndCreatesOutboxRow()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync(TestCancellationToken);

        var service = CreateService();

        await service.ProcessPendingBookingAsync(booking.Id, TestCancellationToken);

        var storedBooking = await _context.Bookings.SingleAsync(TestCancellationToken);
        storedBooking.Status.Should().Be(BookingStatus.Confirmed);
        storedBooking.ProcessedAt.Should().NotBeNull();

        var outbox = await _context.BookingOutbox.SingleAsync(TestCancellationToken);
        outbox.BookingId.Should().Be(booking.Id);
        outbox.EventId.Should().Be(booking.EventId);
        outbox.UserId.Should().Be(booking.UserId);
        outbox.Seats.Should().Be(1);
        outbox.PublishedAtUtc.Should().BeNull();
        outbox.PublishAttempts.Should().Be(0);
        outbox.Payload.Should().Contain("\"bookingId\"");
        outbox.Payload.Should().Contain("\"eventId\"");
    }

    [Fact]
    public async Task ProcessPendingBookingAsync_WhenBookingIsAlreadyConfirmed_SkipsOutboxCreation()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        booking.Confirm();
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync(TestCancellationToken);

        var service = CreateService();

        await service.ProcessPendingBookingAsync(booking.Id, TestCancellationToken);

        var outboxCount = await _context.BookingOutbox.CountAsync(TestCancellationToken);
        outboxCount.Should().Be(0);
    }

    private BookingProcessingService CreateService()
    {
        return new BookingProcessingService(
            new BookingRepository(_context),
            new BookingOutboxRepository(_context),
            NullLogger<BookingProcessingService>.Instance);
    }
}
