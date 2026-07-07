using EventManagementService.Bookings.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventManagementService.Bookings.Infrastructure.Messaging;

/// <summary>
/// Publishes unpublished outbox rows and stores retry state.
/// </summary>
public sealed class BookingOutboxPublisher
{
    private readonly BookingsDbContext _context;
    private readonly IBookingConfirmedPublisher _publisher;
    private readonly ILogger<BookingOutboxPublisher> _logger;

    public BookingOutboxPublisher(
        BookingsDbContext context,
        IBookingConfirmedPublisher publisher,
        ILogger<BookingOutboxPublisher> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> PublishPendingBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
        }

        var messages = await _context.BookingOutbox
            .Where(message => message.PublishedAtUtc == null)
            .OrderBy(message => message.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            await PublishSingleAsync(message, cancellationToken);
        }

        if (messages.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return messages.Count;
    }

    /// <summary>
    /// Deletes published outbox rows older than the retention period
    /// so the table does not grow without bound.
    /// </summary>
    public async Task<int> PurgePublishedAsync(TimeSpan retention, CancellationToken cancellationToken = default)
    {
        var cutoffUtc = DateTimeOffset.UtcNow.Subtract(retention);

        if (_context.Database.IsRelational())
        {
            return await _context.BookingOutbox
                .Where(message => message.PublishedAtUtc != null && message.PublishedAtUtc < cutoffUtc)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var expired = await _context.BookingOutbox
            .Where(message => message.PublishedAtUtc != null && message.PublishedAtUtc < cutoffUtc)
            .ToListAsync(cancellationToken);

        _context.BookingOutbox.RemoveRange(expired);
        await _context.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    private async Task PublishSingleAsync(BookingOutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishAsync(message.EventId, message.Payload, cancellationToken);
            message.MarkPublished(DateTimeOffset.UtcNow);

            _logger.LogInformation(
                "Published BookingConfirmed outbox message {OutboxMessageId} for booking {BookingId}.",
                message.Id,
                message.BookingId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            message.RecordFailure(exception.Message);

            _logger.LogWarning(
                exception,
                "Failed to publish BookingConfirmed outbox message {OutboxMessageId} for booking {BookingId}. Attempt {Attempt}.",
                message.Id,
                message.BookingId,
                message.PublishAttempts);
        }
    }
}
