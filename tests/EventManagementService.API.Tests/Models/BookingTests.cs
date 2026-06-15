using EventManagementService.Domain.Models;
using EventManagementService.Infrastructure.Repositories;
using EventManagementService.API.Tests.Infrastructure;
using EventManagementService.Application.Abstractions.Repositories;
using EventManagementService.Application.Services;
using FluentAssertions;

namespace EventManagementService.API.Tests.Models;

public class BookingTests
{
    [Fact]
    public void CreatePending_WhenEventIdIsProvided_CreatesPendingBooking()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var booking = Booking.CreatePending(eventId, User.SystemUserId, createdAt);

        // Assert
        booking.Id.Should().NotBe(Guid.Empty);
        booking.EventId.Should().Be(eventId);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.CreatedAt.Should().Be(createdAt);
        booking.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void CreatePending_WhenEventIdIsEmpty_ThrowsArgumentException()
    {
        // Act
        var action = () => Booking.CreatePending(Guid.Empty, User.SystemUserId);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("Идентификатор события должен быть указан.*");
    }

    [Fact]
    public void Confirm_WhenBookingIsPending_SetsConfirmedStatusAndProcessedAt()
    {
        // Arrange
        var booking = Booking.CreatePending(Guid.NewGuid(), User.SystemUserId, new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc));
        var processedAt = new DateTime(2026, 4, 3, 12, 5, 0, DateTimeKind.Utc);

        // Act
        booking.Confirm(processedAt);

        // Assert
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ProcessedAt.Should().Be(processedAt);
    }

    [Fact]
    public void Reject_WhenBookingIsPending_SetsRejectedStatusAndProcessedAt()
    {
        // Arrange
        var booking = Booking.CreatePending(Guid.NewGuid(), User.SystemUserId, new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc));
        var processedAt = new DateTime(2026, 4, 3, 12, 5, 0, DateTimeKind.Utc);

        // Act
        booking.Reject(processedAt);

        // Assert
        booking.Status.Should().Be(BookingStatus.Rejected);
        booking.ProcessedAt.Should().Be(processedAt);
    }

    [Fact]
    public void Confirm_WhenBookingIsAlreadyProcessed_ThrowsInvalidOperationException()
    {
        // Arrange
        var booking = Booking.CreatePending(Guid.NewGuid(), User.SystemUserId);
        booking.Confirm(new DateTime(2026, 4, 3, 12, 5, 0, DateTimeKind.Utc));

        // Act
        var action = () => booking.Confirm();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Обрабатывать можно только бронирования в статусе ожидания.");
    }

    [Fact]
    public async Task BookingService_AfterRejectAndReleaseSeats_AllowsNewBookingOnSameEvent()
    {
        // Arrange: event with 1 seat, one booking reserved and then rejected + seat released
        var cancellationToken = TestContext.Current.CancellationToken;
        using var context = TestDbContextFactory.CreateContext();
        var eventService = new EventService(new EventRepository(context));
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));

        var createdEvent = await eventService.CreateEventAsync(Event.Create(
            "Событие с возвратом",
            DateTime.UtcNow.AddDays(2),
            DateTime.UtcNow.AddDays(2).AddHours(2),
            totalSeats: 1));

        var firstBooking = await bookingService.CreateBookingAsync(createdEvent.Id, User.SystemUserId);

        // Simulate rejection + seat release (what background service does on error/delete path)
        var storedBooking = await context.Bookings.FindAsync([firstBooking.Id], cancellationToken);
        storedBooking!.Reject(DateTime.UtcNow);
        var storedEvent = await context.Events.FindAsync([createdEvent.Id], cancellationToken);
        storedEvent!.ReleaseSeats();
        await context.SaveChangesAsync(cancellationToken);

        // Act: now there should be a free seat again
        var secondBooking = await bookingService.CreateBookingAsync(createdEvent.Id, User.SystemUserId);

        // Assert
        secondBooking.Id.Should().NotBe(firstBooking.Id);
        secondBooking.Status.Should().Be(BookingStatus.Pending);
        (await eventService.GetEventByIdAsync(createdEvent.Id)).AvailableSeats.Should().Be(0);
    }
}
