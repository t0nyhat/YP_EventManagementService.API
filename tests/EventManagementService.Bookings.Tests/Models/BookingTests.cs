using EventManagementService.Bookings.Domain.Exceptions;
using EventManagementService.Bookings.Domain.Models;
using FluentAssertions;

namespace EventManagementService.Bookings.Tests.Models;

public class BookingTests
{
    [Fact]
    public void CreatePending_WhenIdsAreValid_CreatesPendingBooking()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 7, 7, 1, 0, 0, DateTimeKind.Utc);

        var booking = Booking.CreatePending(eventId, userId, createdAt);

        booking.Id.Should().NotBe(Guid.Empty);
        booking.EventId.Should().Be(eventId);
        booking.UserId.Should().Be(userId);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.CreatedAt.Should().Be(createdAt);
        booking.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void CreatePending_WhenEventIdIsEmpty_ThrowsBusinessValidationException()
    {
        var action = () => Booking.CreatePending(Guid.Empty, Guid.NewGuid());

        action.Should().Throw<BusinessValidationException>()
            .WithMessage("Идентификатор события должен быть указан.");
    }

    [Fact]
    public void Confirm_WhenBookingIsPending_ConfirmsBooking()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        var processedAt = new DateTime(2026, 7, 7, 2, 0, 0, DateTimeKind.Utc);

        booking.Confirm(processedAt);

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ProcessedAt.Should().Be(processedAt);
    }

    [Fact]
    public void Cancel_WhenBookingIsRejected_ThrowsBookingAlreadyProcessedException()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        booking.Reject();

        var action = () => booking.Cancel();

        action.Should().Throw<BookingAlreadyProcessedException>();
    }
}
