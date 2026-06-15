using EventManagementService.Domain.Exceptions;
using EventManagementService.Domain.Models;
using FluentAssertions;

namespace EventManagementService.API.Tests.Models;

public class BookingDomainRulesTests
{
    [Fact]
    public void CreatePending_WhenUserIdIsProvided_CreatesBookingForUser()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.CreatePending(eventId, userId, createdAt);

        booking.EventId.Should().Be(eventId);
        booking.UserId.Should().Be(userId);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.CreatedAt.Should().Be(createdAt);
        booking.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void CreatePending_WhenUserIdIsEmpty_ThrowsArgumentException()
    {
        var action = () => Booking.CreatePending(Guid.NewGuid(), Guid.Empty);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Идентификатор пользователя должен быть указан.*");
    }

    [Fact]
    public void Cancel_WhenBookingIsPending_SetsCancelledStatusAndProcessedAt()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());

        booking.Cancel(new DateTime(2026, 4, 3, 12, 5, 0, DateTimeKind.Utc));

        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_WhenBookingIsAlreadyProcessed_ThrowsInvalidOperationException()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        booking.Confirm(new DateTime(2026, 4, 3, 12, 5, 0, DateTimeKind.Utc));

        var action = () => booking.Cancel();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Обрабатывать можно только бронирования в статусе ожидания.");
    }
}
