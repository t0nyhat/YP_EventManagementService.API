using EventManagementService.Bookings.Infrastructure.DataAccess;
using EventManagementService.Bookings.Infrastructure.Messaging;
using EventManagementService.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace EventManagementService.Bookings.Tests.Services;

public sealed class BookingOutboxPublisherTests : IDisposable
{
    private readonly BookingsDbContext _context;
    private readonly Mock<IBookingConfirmedPublisher> _publisher = new();

    public BookingOutboxPublisherTests()
    {
        var options = new DbContextOptionsBuilder<BookingsDbContext>()
            .UseInMemoryDatabase($"BookingsOutboxPublisherTestDb_{Guid.NewGuid()}")
            .Options;

        _context = new BookingsDbContext(options);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PublishPendingBatchAsync_WhenPublishSucceeds_MarksOutboxRowAsPublished()
    {
        var outboxMessage = await AddOutboxMessageAsync();
        _publisher
            .Setup(publisher => publisher.PublishAsync(outboxMessage.EventId, outboxMessage.Payload, TestCancellationToken))
            .Returns(Task.CompletedTask);

        var publisherService = CreatePublisherService();

        var processed = await publisherService.PublishPendingBatchAsync(10, TestCancellationToken);

        processed.Should().Be(1);

        var storedMessage = await _context.BookingOutbox.SingleAsync(TestCancellationToken);
        storedMessage.PublishedAtUtc.Should().NotBeNull();
        storedMessage.PublishAttempts.Should().Be(0);
        storedMessage.LastError.Should().BeNull();

        _publisher.Verify(
            publisher => publisher.PublishAsync(outboxMessage.EventId, outboxMessage.Payload, TestCancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task PublishPendingBatchAsync_WhenPublishFails_LeavesRowUnpublishedForRetry()
    {
        var outboxMessage = await AddOutboxMessageAsync();
        _publisher
            .Setup(publisher => publisher.PublishAsync(outboxMessage.EventId, outboxMessage.Payload, TestCancellationToken))
            .ThrowsAsync(new InvalidOperationException("Kafka is unavailable"));

        var publisherService = CreatePublisherService();

        var processed = await publisherService.PublishPendingBatchAsync(10, TestCancellationToken);

        processed.Should().Be(1);

        var storedMessage = await _context.BookingOutbox.SingleAsync(TestCancellationToken);
        storedMessage.PublishedAtUtc.Should().BeNull();
        storedMessage.PublishAttempts.Should().Be(1);
        storedMessage.LastError.Should().Be("Kafka is unavailable");
    }

    [Fact]
    public async Task PublishPendingBatchAsync_WhenRowsAreAlreadyPublished_SkipsThem()
    {
        var outboxMessage = await AddOutboxMessageAsync();
        outboxMessage.MarkPublished(DateTimeOffset.UtcNow);
        await _context.SaveChangesAsync(TestCancellationToken);

        var publisherService = CreatePublisherService();

        var processed = await publisherService.PublishPendingBatchAsync(10, TestCancellationToken);

        processed.Should().Be(0);
        _publisher.Verify(
            publisher => publisher.PublishAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private async Task<BookingOutboxMessage> AddOutboxMessageAsync()
    {
        var message = new BookingConfirmed(
            BookingId: Guid.NewGuid(),
            EventId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Seats: 1,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);
        var outboxMessage = BookingOutboxMessage.Create(message, "{\"bookingId\":\"test\"}", DateTimeOffset.UtcNow);

        await _context.BookingOutbox.AddAsync(outboxMessage, TestCancellationToken);
        await _context.SaveChangesAsync(TestCancellationToken);

        return outboxMessage;
    }

    private BookingOutboxPublisher CreatePublisherService()
    {
        return new BookingOutboxPublisher(
            _context,
            _publisher.Object,
            NullLogger<BookingOutboxPublisher>.Instance);
    }
}
