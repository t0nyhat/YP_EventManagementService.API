using EventManagementService.Bookings.Application.Abstractions.Repositories;
using EventManagementService.Bookings.Domain.Exceptions;
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

    public async Task<IReadOnlyCollection<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status == BookingStatus.Pending)
            .OrderBy(booking => booking.CreatedAt)
            .Select(booking => booking.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task AddWithActiveLimitAsync(
        Booking booking,
        int maxActiveBookingsPerUser,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(booking);

        if (_context.Database.IsNpgsql())
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // Advisory-lock сериализует создание броней одного пользователя между
            // запросами и инстансами сервиса, не блокируя остальных пользователей.
            // Лок держится до конца транзакции и снимается автоматически.
            await _context.Database.ExecuteSqlAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({booking.UserId.ToString()}, 0))",
                cancellationToken);

            await EnforceLimitAndAddAsync(booking, maxActiveBookingsPerUser, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        // Нереляционный провайдер (InMemory в тестах) не поддерживает транзакции и raw SQL.
        await EnforceLimitAndAddAsync(booking, maxActiveBookingsPerUser, cancellationToken);
    }

    public Task ReloadAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(booking);
        return _context.Entry(booking).ReloadAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(
                "Бронирование было изменено параллельной операцией.",
                exception);
        }
    }

    private async Task EnforceLimitAndAddAsync(
        Booking booking,
        int maxActiveBookingsPerUser,
        CancellationToken cancellationToken)
    {
        var activeBookings = await _context.Bookings.CountAsync(
            existing => existing.UserId == booking.UserId
                && (existing.Status == BookingStatus.Pending || existing.Status == BookingStatus.Confirmed),
            cancellationToken);

        if (activeBookings >= maxActiveBookingsPerUser)
        {
            throw new TooManyActiveBookingsException(maxActiveBookingsPerUser);
        }

        await _context.Bookings.AddAsync(booking, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
