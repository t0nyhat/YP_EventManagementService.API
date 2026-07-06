using EventManagementService.Events.Domain.Exceptions;
using EventManagementService.Events.Domain.Models;
using FluentAssertions;

namespace EventManagementService.Events.Tests.Models;

public class EventTests
{
    [Fact]
    public void Create_WhenValidParameters_CreatesEvent()
    {
        var startAt = DateTime.UtcNow.AddDays(1);
        var endAt = DateTime.UtcNow.AddDays(2);

        var ev = Event.Create("Test Event", startAt, endAt, 100, "Description");

        ev.Id.Should().NotBe(Guid.Empty);
        ev.Title.Should().Be("Test Event");
        ev.Description.Should().Be("Description");
        ev.StartAt.Should().Be(startAt);
        ev.EndAt.Should().Be(endAt);
        ev.TotalSeats.Should().Be(100);
        ev.AvailableSeats.Should().Be(100);
    }

    [Fact]
    public void Create_WhenTitleIsEmpty_ThrowsBusinessValidationException()
    {
        var startAt = DateTime.UtcNow.AddDays(1);
        var endAt = DateTime.UtcNow.AddDays(2);

        var action = () => Event.Create("", startAt, endAt, 100);

        action.Should().Throw<BusinessValidationException>()
            .WithMessage("Название события не должно быть пустым.");
    }

    [Fact]
    public void Create_WhenEndAtIsBeforeStartAt_ThrowsBusinessValidationException()
    {
        var startAt = DateTime.UtcNow.AddDays(2);
        var endAt = DateTime.UtcNow.AddDays(1);

        var action = () => Event.Create("Test", startAt, endAt, 100);

        action.Should().Throw<BusinessValidationException>()
            .WithMessage("Дата окончания должна быть позже даты начала события.");
    }

    [Fact]
    public void Create_WhenTotalSeatsIsZero_ThrowsBusinessValidationException()
    {
        var startAt = DateTime.UtcNow.AddDays(1);
        var endAt = DateTime.UtcNow.AddDays(2);

        var action = () => Event.Create("Test", startAt, endAt, 0);

        action.Should().Throw<BusinessValidationException>()
            .WithMessage("Количество мест должно быть больше нуля.");
    }

    [Fact]
    public void Update_WhenValidParameters_UpdatesEvent()
    {
        var ev = Event.Create("Original", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        var newStartAt = DateTime.UtcNow.AddDays(3);
        var newEndAt = DateTime.UtcNow.AddDays(4);

        ev.Update("Updated", newStartAt, newEndAt, "New description");

        ev.Title.Should().Be("Updated");
        ev.StartAt.Should().Be(newStartAt);
        ev.EndAt.Should().Be(newEndAt);
        ev.Description.Should().Be("New description");
    }

    [Fact]
    public void TryDecreaseAvailableSeats_WhenEnoughSeats_DecreasesAndReturnsTrue()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);

        var result = ev.TryDecreaseAvailableSeats(3);

        result.Should().BeTrue();
        ev.AvailableSeats.Should().Be(97);
    }

    [Fact]
    public void TryDecreaseAvailableSeats_WhenNotEnoughSeats_ReturnsFalse()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);

        var result = ev.TryDecreaseAvailableSeats(200);

        result.Should().BeFalse();
        ev.AvailableSeats.Should().Be(100);
    }

}
