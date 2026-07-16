using EventManagementService.Contracts;
using EventManagementService.Events.Application.Abstractions.Caching;
using EventManagementService.Events.Application.Abstractions.Messaging;
using EventManagementService.Events.Application.Caching;
using EventManagementService.Events.Domain.Models;
using EventManagementService.Events.Infrastructure.DataAccess;
using EventManagementService.Events.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace EventManagementService.Events.Tests.Services;

public class BookingConfirmedHandlerTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = KafkaJson.Options;

    private readonly DbContextOptions<EventsDbContext> _options;
    private readonly EventsDbContext _context;

    // Loose mock: RemoveAsync returns a completed task by default,
    // so the handler's await works without explicit setups.
    private readonly Mock<ICacheService> _cache = new();

    private readonly IBookingConfirmedHandler _handler;

    public BookingConfirmedHandlerTests()
    {
        _options = new DbContextOptionsBuilder<EventsDbContext>()
            .UseInMemoryDatabase($"EventsTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new EventsDbContext(_options);
        _handler = new BookingConfirmedHandler(_context, NullLogger<BookingConfirmedHandler>.Instance, _cache.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private ValueTask<Event?> FindEventAsync(Guid eventId)
    {
        return _context.Events.FindAsync([eventId], TestCancellationToken);
    }

    [Fact]
    public async Task HandleAsync_WhenEventExistsAndHasSeats_DecreasesAvailableSeats()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _context.Events.Add(ev);
        await _context.SaveChangesAsync(TestCancellationToken);

        var message = new BookingConfirmed(
            BookingId: Guid.NewGuid(),
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 3,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        var result = await _handler.HandleAsync(message, TestCancellationToken);

        result.Should().BeTrue();

        var updatedEvent = await FindEventAsync(ev.Id);
        updatedEvent!.AvailableSeats.Should().Be(97);

        var inbox = await _context.BookingConfirmedInbox
            .FirstOrDefaultAsync(x => x.BookingId == message.BookingId, TestCancellationToken);
        inbox.Should().NotBeNull();
        inbox!.Result.Should().Be("Processed");
    }

    [Fact]
    public async Task HandleAsync_WhenMessageIsDeserializedFromWebJson_DecreasesAvailableSeats()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 10);
        _context.Events.Add(ev);
        await _context.SaveChangesAsync(TestCancellationToken);

        var originalMessage = new BookingConfirmed(
            BookingId: Guid.NewGuid(),
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 2,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);
        var payload = JsonSerializer.Serialize(originalMessage, JsonOptions);
        var message = JsonSerializer.Deserialize<BookingConfirmed>(payload, JsonOptions);

        message.Should().NotBeNull();

        var result = await _handler.HandleAsync(message!, TestCancellationToken);

        result.Should().BeTrue();

        var updatedEvent = await FindEventAsync(ev.Id);
        updatedEvent!.AvailableSeats.Should().Be(8);
    }

    [Fact]
    public async Task HandleAsync_WhenDuplicateBookingId_ReturnsFalseAndDoesNotDecreaseSeats()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _context.Events.Add(ev);
        await _context.SaveChangesAsync(TestCancellationToken);

        var bookingId = Guid.NewGuid();
        var firstMessage = new BookingConfirmed(
            BookingId: bookingId,
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 3,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        await _handler.HandleAsync(firstMessage, TestCancellationToken);

        var secondMessage = new BookingConfirmed(
            BookingId: bookingId,
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 3,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        var result = await _handler.HandleAsync(secondMessage, TestCancellationToken);

        result.Should().BeFalse();

        var updatedEvent = await FindEventAsync(ev.Id);
        updatedEvent!.AvailableSeats.Should().Be(97);
    }

    [Fact]
    public async Task HandleAsync_WhenEventAlreadyStarted_RecordsInboxAndDoesNotDecreaseSeats()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), 100);
        _context.Events.Add(ev);
        await _context.SaveChangesAsync(TestCancellationToken);

        var message = new BookingConfirmed(
            BookingId: Guid.NewGuid(),
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 1,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        var result = await _handler.HandleAsync(message, TestCancellationToken);

        result.Should().BeFalse();

        var updatedEvent = await FindEventAsync(ev.Id);
        updatedEvent!.AvailableSeats.Should().Be(100);

        var inbox = await _context.BookingConfirmedInbox
            .FirstOrDefaultAsync(x => x.BookingId == message.BookingId, TestCancellationToken);
        inbox.Should().NotBeNull();
        inbox!.Result.Should().Be("EventAlreadyStarted");
    }

    [Fact]
    public async Task HandleAsync_WhenEventDoesNotExist_RecordsInboxWithSkippedResult()
    {
        var message = new BookingConfirmed(
            BookingId: Guid.NewGuid(),
            EventId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Seats: 3,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        var result = await _handler.HandleAsync(message, TestCancellationToken);

        result.Should().BeFalse();

        var inbox = await _context.BookingConfirmedInbox
            .FirstOrDefaultAsync(x => x.BookingId == message.BookingId, TestCancellationToken);
        inbox.Should().NotBeNull();
        inbox!.Result.Should().Be("EventNotFound");
    }

    [Fact]
    public async Task HandleAsync_WhenNotEnoughSeats_RecordsInboxWithSkippedResult()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 5);
        _context.Events.Add(ev);
        await _context.SaveChangesAsync(TestCancellationToken);

        var message = new BookingConfirmed(
            BookingId: Guid.NewGuid(),
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 10,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        var result = await _handler.HandleAsync(message, TestCancellationToken);

        result.Should().BeFalse();

        var updatedEvent = await FindEventAsync(ev.Id);
        updatedEvent!.AvailableSeats.Should().Be(5);

        var inbox = await _context.BookingConfirmedInbox
            .FirstOrDefaultAsync(x => x.BookingId == message.BookingId, TestCancellationToken);
        inbox.Should().NotBeNull();
        inbox!.Result.Should().Be("NotEnoughSeats");
    }

    [Fact]
    public async Task HandleAsync_WhenDuplicateMessage_DoesNotMakeSeatsNegative()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 1);
        _context.Events.Add(ev);
        await _context.SaveChangesAsync(TestCancellationToken);

        var bookingId = Guid.NewGuid();
        var firstMessage = new BookingConfirmed(
            BookingId: bookingId,
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 1,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        await _handler.HandleAsync(firstMessage, TestCancellationToken);

        var secondMessage = new BookingConfirmed(
            BookingId: bookingId,
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 1,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        var result = await _handler.HandleAsync(secondMessage, TestCancellationToken);

        result.Should().BeFalse();

        var updatedEvent = await FindEventAsync(ev.Id);
        updatedEvent!.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_WhenProcessedSuccessfully_InvalidatesExactlyEventCacheKeyOnce()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _context.Events.Add(ev);
        await _context.SaveChangesAsync(TestCancellationToken);

        var message = new BookingConfirmed(
            BookingId: Guid.NewGuid(),
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 3,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        var result = await _handler.HandleAsync(message, TestCancellationToken);

        result.Should().BeTrue();

        // Deliberately a literal (not EventCacheKeys.ForEvent) to pin the exact key format.
        _cache.Verify(cache => cache.RemoveAsync($"event:{ev.Id:D}", It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(cache => cache.RemoveAsync(EventCacheKeys.Top10, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenProcessedSuccessfully_InvalidatesCacheOnlyAfterSeatsAreSaved()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _context.Events.Add(ev);
        await _context.SaveChangesAsync(TestCancellationToken);

        var message = new BookingConfirmed(
            BookingId: Guid.NewGuid(),
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 3,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        // At the moment RemoveAsync is called, read the database through a FRESH context
        // over the same in-memory store: it sees only what SaveChanges has already
        // persisted, not the handler's tracked (unsaved) state. If the handler
        // invalidated before committing, the snapshot would still show 100 seats.
        var observations = new List<(int AvailableSeats, bool InboxPersisted)>();
        _cache.Setup(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((_, _) =>
            {
                using var freshContext = new EventsDbContext(_options);
                var availableSeats = freshContext.Events
                    .AsNoTracking()
                    .Single(e => e.Id == ev.Id)
                    .AvailableSeats;
                var inboxPersisted = freshContext.BookingConfirmedInbox
                    .AsNoTracking()
                    .Any(x => x.BookingId == message.BookingId);
                observations.Add((availableSeats, inboxPersisted));
            })
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(message, TestCancellationToken);

        result.Should().BeTrue();
        observations.Should().ContainSingle();
        observations[0].AvailableSeats.Should().Be(97);
        observations[0].InboxPersisted.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_PassesUncancellableTokenToCacheInvalidation()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _context.Events.Add(ev);
        await _context.SaveChangesAsync(TestCancellationToken);

        var message = new BookingConfirmed(
            BookingId: Guid.NewGuid(),
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 3,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        // A live (cancellable) token, so passing it through would be observable.
        using var cts = new CancellationTokenSource();

        var result = await _handler.HandleAsync(message, cts.Token);

        result.Should().BeTrue();

        // Post-commit invalidation must run with CancellationToken.None, not the stopping
        // token: cancelling it after the commit would leave the offset uncommitted and the
        // redelivery would be skipped as a duplicate without ever removing the stale key.
        _cache.Verify(cache => cache.RemoveAsync($"event:{ev.Id:D}", CancellationToken.None), Times.Once);
        _cache.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenCancellationRequestedDuringInvalidation_StillInvalidatesAndReturnsTrue()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _context.Events.Add(ev);
        await _context.SaveChangesAsync(TestCancellationToken);

        var message = new BookingConfirmed(
            BookingId: Guid.NewGuid(),
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 3,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        // Simulate a shutdown arriving exactly while the invalidation is in flight:
        // the stopping token gets cancelled inside the RemoveAsync call.
        using var cts = new CancellationTokenSource();
        _cache.Setup(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((_, _) => cts.Cancel())
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(message, cts.Token);

        result.Should().BeTrue();

        var updatedEvent = await FindEventAsync(ev.Id);
        updatedEvent!.AvailableSeats.Should().Be(97);

        _cache.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenDuplicateBookingId_DoesNotInvalidateCacheAgain()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _context.Events.Add(ev);
        await _context.SaveChangesAsync(TestCancellationToken);

        var bookingId = Guid.NewGuid();
        var firstMessage = new BookingConfirmed(
            BookingId: bookingId,
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 3,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        await _handler.HandleAsync(firstMessage, TestCancellationToken);

        var secondMessage = new BookingConfirmed(
            BookingId: bookingId,
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 3,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        await _handler.HandleAsync(secondMessage, TestCancellationToken);

        // Only the first (processed) call invalidates; the duplicate changes nothing.
        _cache.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenEventDoesNotExist_DoesNotInvalidateCache()
    {
        var message = new BookingConfirmed(
            BookingId: Guid.NewGuid(),
            EventId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Seats: 3,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        await _handler.HandleAsync(message, TestCancellationToken);

        _cache.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenEventAlreadyStarted_DoesNotInvalidateCache()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), 100);
        _context.Events.Add(ev);
        await _context.SaveChangesAsync(TestCancellationToken);

        var message = new BookingConfirmed(
            BookingId: Guid.NewGuid(),
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 1,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        await _handler.HandleAsync(message, TestCancellationToken);

        _cache.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenNotEnoughSeats_DoesNotInvalidateCache()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 5);
        _context.Events.Add(ev);
        await _context.SaveChangesAsync(TestCancellationToken);

        var message = new BookingConfirmed(
            BookingId: Guid.NewGuid(),
            EventId: ev.Id,
            UserId: Guid.NewGuid(),
            Seats: 10,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        await _handler.HandleAsync(message, TestCancellationToken);

        _cache.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
