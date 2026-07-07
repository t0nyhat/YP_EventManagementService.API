using EventManagementService.Contracts;
using EventManagementService.Events.Application.Abstractions.Messaging;
using EventManagementService.Events.Domain.Models;
using EventManagementService.Events.Infrastructure.DataAccess;
using EventManagementService.Events.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace EventManagementService.Events.Tests.Services;

public class BookingConfirmedHandlerTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = KafkaJson.Options;

    private readonly EventsDbContext _context;
    private readonly IBookingConfirmedHandler _handler;

    public BookingConfirmedHandlerTests()
    {
        var options = new DbContextOptionsBuilder<EventsDbContext>()
            .UseInMemoryDatabase($"EventsTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new EventsDbContext(options);
        _handler = new BookingConfirmedHandler(_context, NullLogger<BookingConfirmedHandler>.Instance);
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
}
