using EventManagementService.Bookings.Application.Abstractions.Repositories;
using EventManagementService.Bookings.Domain.Models;
using EventManagementService.Bookings.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace EventManagementService.Bookings.Infrastructure.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly BookingsDbContext _context;

    public BookingRepository(BookingsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<Booking?> GetByIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        return _context.Bookings.FirstOrDefaultAsync(booking => booking.Id == bookingId, cancellationToken);
    }

    public Task<int> CountActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.Bookings.CountAsync(
            booking => booking.UserId == userId
                && (booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Confirmed),
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status == BookingStatus.Pending)
            .OrderBy(booking => booking.CreatedAt)
            .Select(booking => booking.Id)
            .ToArrayAsync(cancellationToken);
    }

    public Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        return _context.Bookings.AddAsync(booking, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
