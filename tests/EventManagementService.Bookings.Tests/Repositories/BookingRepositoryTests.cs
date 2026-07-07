using EventManagementService.Bookings.Domain.Exceptions;
using EventManagementService.Bookings.Domain.Models;
using EventManagementService.Bookings.Infrastructure.DataAccess;
using EventManagementService.Bookings.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EventManagementService.Bookings.Tests.Repositories;

public sealed class BookingRepositoryTests : IDisposable
{
    private readonly BookingsDbContext _context;
    private readonly BookingRepository _repository;

    public BookingRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BookingsDbContext>()
            .UseInMemoryDatabase($"BookingsRepositoryTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new BookingsDbContext(options);
        _repository = new BookingRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddWithActiveLimitAsync_WhenBelowLimit_SavesBooking()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());

        await _repository.AddWithActiveLimitAsync(booking, maxActiveBookingsPerUser: 2, TestCancellationToken);

        var stored = await _context.Bookings.SingleAsync(TestCancellationToken);
        stored.Id.Should().Be(booking.Id);
    }

    [Fact]
    public async Task AddWithActiveLimitAsync_WhenLimitReached_ThrowsAndDoesNotSave()
    {
        var userId = Guid.NewGuid();
        _context.Bookings.Add(Booking.CreatePending(Guid.NewGuid(), userId));
        var confirmed = Booking.CreatePending(Guid.NewGuid(), userId);
        confirmed.Confirm();
        _context.Bookings.Add(confirmed);
        await _context.SaveChangesAsync(TestCancellationToken);

        var action = async () => await _repository.AddWithActiveLimitAsync(
            Booking.CreatePending(Guid.NewGuid(), userId),
            maxActiveBookingsPerUser: 2,
            TestCancellationToken);

        await action.Should().ThrowAsync<TooManyActiveBookingsException>();
        var count = await _context.Bookings.CountAsync(TestCancellationToken);
        count.Should().Be(2);
    }

    [Fact]
    public async Task AddWithActiveLimitAsync_CancelledBookingsDoNotCountTowardsLimit()
    {
        var userId = Guid.NewGuid();
        var cancelled = Booking.CreatePending(Guid.NewGuid(), userId);
        cancelled.Cancel();
        _context.Bookings.Add(cancelled);
        await _context.SaveChangesAsync(TestCancellationToken);

        var action = async () => await _repository.AddWithActiveLimitAsync(
            Booking.CreatePending(Guid.NewGuid(), userId),
            maxActiveBookingsPerUser: 1,
            TestCancellationToken);

        await action.Should().NotThrowAsync();
    }
}
