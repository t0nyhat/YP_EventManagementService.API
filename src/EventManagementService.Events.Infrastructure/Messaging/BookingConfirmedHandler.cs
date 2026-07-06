using EventManagementService.Contracts;
using EventManagementService.Events.Application.Abstractions.Messaging;
using EventManagementService.Events.Domain.Models;
using EventManagementService.Events.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventManagementService.Events.Infrastructure.Messaging;

/// <summary>
/// Handles BookingConfirmed messages by decreasing available seats.
/// Uses an inbox table for idempotency.
/// </summary>
public sealed class BookingConfirmedHandler : IBookingConfirmedHandler
{
    private readonly EventsDbContext _context;
    private readonly ILogger<BookingConfirmedHandler> _logger;

    public BookingConfirmedHandler(
        EventsDbContext context,
        ILogger<BookingConfirmedHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> HandleAsync(BookingConfirmed message, CancellationToken cancellationToken = default)
    {
        // Check for duplicate
        var existing = await _context.BookingConfirmedInbox
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BookingId == message.BookingId, cancellationToken);

        if (existing is not null)
        {
            _logger.LogInformation(
                "BookingConfirmed {BookingId} already processed with result {Result}. Skipping.",
                message.BookingId, existing.Result);
            return false;
        }

        var eventEntity = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == message.EventId, cancellationToken);

        if (eventEntity is null)
        {
            _logger.LogWarning(
                "Event {EventId} not found for BookingConfirmed {BookingId}. Recording inbox with skipped result.",
                message.EventId, message.BookingId);

            _context.BookingConfirmedInbox.Add(new BookingConfirmedInbox
            {
                BookingId = message.BookingId,
                EventId = message.EventId,
                UserId = message.UserId,
                Seats = message.Seats,
                ConfirmedAtUtc = message.ConfirmedAtUtc,
                ProcessedAtUtc = DateTimeOffset.UtcNow,
                Result = "EventNotFound"
            });

            await _context.SaveChangesAsync(cancellationToken);
            return false;
        }

        if (!eventEntity.TryDecreaseAvailableSeats(message.Seats))
        {
            _logger.LogWarning(
                "Not enough available seats for event {EventId}. Available: {Available}, Requested: {Seats}. Recording inbox with skipped result.",
                message.EventId, eventEntity.AvailableSeats, message.Seats);

            _context.BookingConfirmedInbox.Add(new BookingConfirmedInbox
            {
                BookingId = message.BookingId,
                EventId = message.EventId,
                UserId = message.UserId,
                Seats = message.Seats,
                ConfirmedAtUtc = message.ConfirmedAtUtc,
                ProcessedAtUtc = DateTimeOffset.UtcNow,
                Result = "NotEnoughSeats"
            });

            await _context.SaveChangesAsync(cancellationToken);
            return false;
        }

        _context.BookingConfirmedInbox.Add(new BookingConfirmedInbox
        {
            BookingId = message.BookingId,
            EventId = message.EventId,
            UserId = message.UserId,
            Seats = message.Seats,
            ConfirmedAtUtc = message.ConfirmedAtUtc,
            ProcessedAtUtc = DateTimeOffset.UtcNow,
            Result = "Processed"
        });

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Successfully processed BookingConfirmed {BookingId}. Decreased available seats for event {EventId} by {Seats}.",
            message.BookingId, message.EventId, message.Seats);

        return true;
    }
}