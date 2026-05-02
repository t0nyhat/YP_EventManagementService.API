using EventManagementService.API.Models;
using FluentAssertions;

namespace EventManagementService.API.Tests.Models;

public class EventTests
{
    [Fact]
    public void TryReserveSeats_WhenEnoughSeatsExist_DecreasesAvailableSeats()
    {
        // Arrange
        var eventItem = new Event
        {
            Title = "Конференция",
            StartAt = new DateTime(2026, 4, 10, 10, 0, 0),
            EndAt = new DateTime(2026, 4, 10, 12, 0, 0),
            TotalSeats = 5,
            AvailableSeats = 5
        };

        // Act
        var result = eventItem.TryReserveSeats(2);

        // Assert
        result.Should().BeTrue();
        eventItem.AvailableSeats.Should().Be(3);
    }

    [Fact]
    public void TryReserveSeats_WhenNotEnoughSeatsExist_ReturnsFalse()
    {
        // Arrange
        var eventItem = new Event
        {
            Title = "Конференция",
            StartAt = new DateTime(2026, 4, 10, 10, 0, 0),
            EndAt = new DateTime(2026, 4, 10, 12, 0, 0),
            TotalSeats = 2,
            AvailableSeats = 1
        };

        // Act
        var result = eventItem.TryReserveSeats(2);

        // Assert
        result.Should().BeFalse();
        eventItem.AvailableSeats.Should().Be(1);
    }

    [Fact]
    public void ReleaseSeats_WhenReleasedSeatsExceedCapacity_CapsAvailableSeatsAtTotalSeats()
    {
        // Arrange
        var eventItem = new Event
        {
            Title = "Конференция",
            StartAt = new DateTime(2026, 4, 10, 10, 0, 0),
            EndAt = new DateTime(2026, 4, 10, 12, 0, 0),
            TotalSeats = 5,
            AvailableSeats = 4
        };

        // Act
        eventItem.ReleaseSeats(3);

        // Assert
        eventItem.AvailableSeats.Should().Be(5);
    }

    [Fact]
    public void TryReserveSeats_WhenCountIsLessThanOne_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var eventItem = new Event
        {
            Title = "Конференция",
            StartAt = new DateTime(2026, 4, 10, 10, 0, 0),
            EndAt = new DateTime(2026, 4, 10, 12, 0, 0),
            TotalSeats = 5,
            AvailableSeats = 5
        };

        // Act
        var action = () => eventItem.TryReserveSeats(0);

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}