using EventManagementService.Contracts;
using EventManagementService.Events.Application.Abstractions.Caching;
using EventManagementService.Events.Application.Abstractions.Messaging;
using EventManagementService.Events.Application.Caching;
using EventManagementService.Events.Domain.Models;
using EventManagementService.Events.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventManagementService.Events.Infrastructure.Messaging;

/// <summary>
/// Handles BookingConfirmed messages by decreasing available seats.
/// Uses an inbox table for idempotency.
/// After a successful commit, invalidates the cached entry of the affected event.
/// </summary>
public sealed class BookingConfirmedHandler : IBookingConfirmedHandler
{
    private readonly EventsDbContext _context;
    private readonly ILogger<BookingConfirmedHandler> _logger;
    private readonly ICacheService _cache;

    public BookingConfirmedHandler(
        EventsDbContext context,
        ILogger<BookingConfirmedHandler> logger,
        ICacheService cache)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
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

            await RecordInboxAsync(message, "EventNotFound", cancellationToken);
            return false;
        }

        if (eventEntity.StartAt <= DateTime.UtcNow)
        {
            _logger.LogWarning(
                "Event {EventId} already started at {StartAt} for BookingConfirmed {BookingId}. Recording inbox with skipped result.",
                message.EventId, eventEntity.StartAt, message.BookingId);

            await RecordInboxAsync(message, "EventAlreadyStarted", cancellationToken);
            return false;
        }

        if (!eventEntity.TryDecreaseAvailableSeats(message.Seats))
        {
            _logger.LogWarning(
                "Not enough available seats for event {EventId}. Available: {Available}, Requested: {Seats}. Recording inbox with skipped result.",
                message.EventId, eventEntity.AvailableSeats, message.Seats);

            await RecordInboxAsync(message, "NotEnoughSeats", cancellationToken);
            return false;
        }

        await RecordInboxAsync(message, "Processed", cancellationToken);

        // Invalidate only after RecordInboxAsync has committed the Event+Inbox transaction:
        // a removed cache entry cannot be restored if the save rolled back, so the cache
        // must never get ahead of the database. The skipped paths above (duplicate,
        // EventNotFound, EventAlreadyStarted, NotEnoughSeats) do not invalidate because
        // the event data was not changed.
        //
        // Deliberately NOT the stopping token: once the commit has happened, the
        // invalidation must not be abandoned halfway because of a shutdown. If it were
        // cancelled here, the consumer would exit without committing the offset, the
        // redelivered message would be skipped as a duplicate (no invalidation on that
        // path), and the stale cache entry would never be removed. The call is short and
        // best-effort (the adapter never throws and the Redis timeout is bounded), so
        // CancellationToken.None is safe.
        await _cache.RemoveAsync(EventCacheKeys.ForEvent(message.EventId), CancellationToken.None);

        _logger.LogInformation(
            "Successfully processed BookingConfirmed {BookingId}. Decreased available seats for event {EventId} by {Seats}.",
            message.BookingId, message.EventId, message.Seats);

        return true;
    }

    /// <summary>
    /// Records the processing result in the inbox and saves all pending context changes
    /// (including the seat decrement) with a single SaveChanges — one transaction.
    /// </summary>
    private Task RecordInboxAsync(BookingConfirmed message, string result, CancellationToken cancellationToken)
    {
        _context.BookingConfirmedInbox.Add(new BookingConfirmedInbox
        {
            BookingId = message.BookingId,
            EventId = message.EventId,
            UserId = message.UserId,
            Seats = message.Seats,
            ConfirmedAtUtc = message.ConfirmedAtUtc,
            ProcessedAtUtc = DateTimeOffset.UtcNow,
            Result = result
        });

        return _context.SaveChangesAsync(cancellationToken);
    }
}