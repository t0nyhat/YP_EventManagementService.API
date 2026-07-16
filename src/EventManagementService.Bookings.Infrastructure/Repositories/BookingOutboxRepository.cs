using EventManagementService.Bookings.Application.Abstractions.Repositories;
using EventManagementService.Bookings.Infrastructure.DataAccess;
using EventManagementService.Contracts;

namespace EventManagementService.Bookings.Infrastructure.Repositories;

public sealed class BookingOutboxRepository : IBookingOutboxRepository
{
    private readonly BookingsDbContext _context;

    public BookingOutboxRepository(BookingsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task AddAsync(
        BookingConfirmed message,
        string payload,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        var outboxMessage = BookingOutboxMessage.Create(message, payload, createdAtUtc);
        return _context.BookingOutbox.AddAsync(outboxMessage, cancellationToken).AsTask();
    }
}
