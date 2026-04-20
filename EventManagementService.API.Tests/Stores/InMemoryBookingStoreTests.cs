using EventManagementService.API.Models;
using EventManagementService.API.Stores;
using FluentAssertions;

namespace EventManagementService.API.Tests.Stores;

public class InMemoryBookingStoreTests
{
    [Fact]
    public void Add_WhenBookingIsStored_ReturnsDetachedCopyAndAllowsReadingById()
    {
        // Arrange
        var store = new InMemoryBookingStore();
        var booking = Booking.CreatePending(Guid.NewGuid(), new DateTime(2026, 4, 3, 12, 0, 0, DateTimeKind.Utc));

        // Act
        var storedBooking = store.Add(booking);
        var loadedBooking = store.GetById(booking.Id);

        // Assert
        storedBooking.Should().NotBeSameAs(booking);
        storedBooking.Id.Should().Be(booking.Id);
        loadedBooking.Should().NotBeNull();
        loadedBooking.Should().NotBeSameAs(storedBooking);
        loadedBooking!.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public void Add_WhenBookingWithSameIdAlreadyExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var store = new InMemoryBookingStore();
        var booking = Booking.CreatePending(Guid.NewGuid());
        store.Add(booking);

        // Act
        var action = () => store.Add(booking);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"Бронирование с id {booking.Id} уже существует.");
    }

    [Fact]
    public void GetPendingIds_WhenStoreContainsProcessedBookings_ReturnsOnlyPendingIds()
    {
        // Arrange
        var store = new InMemoryBookingStore();
        var pendingBooking = store.Add(Booking.CreatePending(Guid.NewGuid()));
        var confirmedBooking = store.Add(Booking.CreatePending(Guid.NewGuid()));
        store.TrySetStatus(confirmedBooking.Id, BookingStatus.Confirmed, new DateTime(2026, 4, 3, 12, 5, 0, DateTimeKind.Utc));

        // Act
        var pendingIds = store.GetPendingIds();

        // Assert
        pendingIds.Should().ContainSingle().Which.Should().Be(pendingBooking.Id);
    }

    [Fact]
    public void TrySetStatus_WhenBookingIsPending_UpdatesStatusAndProcessedAt()
    {
        // Arrange
        var store = new InMemoryBookingStore();
        var booking = store.Add(Booking.CreatePending(Guid.NewGuid()));
        var processedAt = new DateTime(2026, 4, 3, 12, 5, 0, DateTimeKind.Utc);

        // Act
        var result = store.TrySetStatus(booking.Id, BookingStatus.Rejected, processedAt);
        var updatedBooking = store.GetById(booking.Id);

        // Assert
        result.Should().BeTrue();
        updatedBooking.Should().NotBeNull();
        updatedBooking!.Status.Should().Be(BookingStatus.Rejected);
        updatedBooking.ProcessedAt.Should().Be(processedAt);
    }

    [Fact]
    public void TrySetStatus_WhenBookingDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var store = new InMemoryBookingStore();

        // Act
        var result = store.TrySetStatus(Guid.NewGuid(), BookingStatus.Confirmed, DateTime.UtcNow);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetById_WhenBookingWasUpdated_ReturnedSnapshotDoesNotChangeRetrospectively()
    {
        // Arrange
        var store = new InMemoryBookingStore();
        var booking = store.Add(Booking.CreatePending(Guid.NewGuid()));
        var initialSnapshot = store.GetById(booking.Id);

        // Act
        store.TrySetStatus(booking.Id, BookingStatus.Confirmed, new DateTime(2026, 4, 3, 12, 5, 0, DateTimeKind.Utc));
        var updatedSnapshot = store.GetById(booking.Id);

        // Assert
        initialSnapshot.Should().NotBeNull();
        initialSnapshot!.Status.Should().Be(BookingStatus.Pending);
        updatedSnapshot.Should().NotBeNull();
        updatedSnapshot!.Status.Should().Be(BookingStatus.Confirmed);
    }
}
