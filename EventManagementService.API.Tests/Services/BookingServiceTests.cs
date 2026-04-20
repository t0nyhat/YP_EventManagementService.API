using EventManagementService.API.Exceptions;
using EventManagementService.API.Models;
using EventManagementService.API.Services;
using EventManagementService.API.Stores;
using FluentAssertions;

namespace EventManagementService.API.Tests.Services;

public class BookingServiceTests
{
    [Fact]
    public async Task CreateBookingAsync_WhenEventExists_ReturnsPendingBooking()
    {
        // Arrange
        var eventService = new EventService();
        var bookingStore = new InMemoryBookingStore();
        var bookingService = new BookingService(bookingStore, eventService);
        var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(
            title: "Конференция",
            description: "Проверка бронирования",
            startAt: new DateTime(2026, 5, 10, 10, 0, 0),
            endAt: new DateTime(2026, 5, 10, 12, 0, 0)));

        // Act
        var booking = await bookingService.CreateBookingAsync(createdEvent.Id);

        // Assert
        booking.Id.Should().NotBe(Guid.Empty);
        booking.EventId.Should().Be(createdEvent.Id);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.ProcessedAt.Should().BeNull();
        bookingStore.GetById(booking.Id).Should().NotBeNull();
    }

    [Fact]
    public async Task CreateBookingAsync_WhenCreatingMultipleBookingsForSameEvent_ReturnsUniqueIds()
    {
        // Arrange
        var eventService = new EventService();
        var bookingStore = new InMemoryBookingStore();
        var bookingService = new BookingService(bookingStore, eventService);
        var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(
            title: "Митап",
            description: "Несколько броней",
            startAt: new DateTime(2026, 5, 11, 18, 0, 0),
            endAt: new DateTime(2026, 5, 11, 20, 0, 0)));

        // Act
        var firstBooking = await bookingService.CreateBookingAsync(createdEvent.Id);
        var secondBooking = await bookingService.CreateBookingAsync(createdEvent.Id);

        // Assert
        firstBooking.Id.Should().NotBe(secondBooking.Id);
        firstBooking.EventId.Should().Be(createdEvent.Id);
        secondBooking.EventId.Should().Be(createdEvent.Id);
    }

    [Fact]
    public async Task GetBookingByIdAsync_WhenBookingExists_ReturnsStoredBooking()
    {
        // Arrange
        var eventService = new EventService();
        var bookingStore = new InMemoryBookingStore();
        var bookingService = new BookingService(bookingStore, eventService);
        var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(
            title: "Воркшоп",
            description: "Поиск по id",
            startAt: new DateTime(2026, 5, 12, 14, 0, 0),
            endAt: new DateTime(2026, 5, 12, 16, 0, 0)));
        var createdBooking = await bookingService.CreateBookingAsync(createdEvent.Id);

        // Act
        var booking = await bookingService.GetBookingByIdAsync(createdBooking.Id);

        // Assert
        booking.Id.Should().Be(createdBooking.Id);
        booking.EventId.Should().Be(createdEvent.Id);
        booking.Status.Should().Be(BookingStatus.Pending);
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Rejected)]
    public async Task GetBookingByIdAsync_WhenBookingStatusChanges_ReturnsUpdatedBooking(BookingStatus status)
    {
        // Arrange
        var eventService = new EventService();
        var bookingStore = new InMemoryBookingStore();
        var bookingService = new BookingService(bookingStore, eventService);
        var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(
            title: "Статусная проверка",
            description: "Подтверждение или отказ",
            startAt: new DateTime(2026, 5, 13, 12, 0, 0),
            endAt: new DateTime(2026, 5, 13, 14, 0, 0)));
        var createdBooking = await bookingService.CreateBookingAsync(createdEvent.Id);
        var processedAt = new DateTime(2026, 5, 13, 12, 10, 0, DateTimeKind.Utc);
        bookingStore.TrySetStatus(createdBooking.Id, status, processedAt);

        // Act
        var booking = await bookingService.GetBookingByIdAsync(createdBooking.Id);

        // Assert
        booking.Status.Should().Be(status);
        booking.ProcessedAt.Should().Be(processedAt);
    }

    [Fact]
    public async Task CreateBookingAsync_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var bookingService = new BookingService(new InMemoryBookingStore(), new EventService());

        // Act
        var action = async () => await bookingService.CreateBookingAsync(Guid.NewGuid());

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateBookingAsync_WhenEventWasDeleted_ThrowsNotFoundException()
    {
        // Arrange
        var eventService = new EventService();
        var bookingService = new BookingService(new InMemoryBookingStore(), eventService);
        var createdEvent = eventService.CreateEvent(EventTestData.CreateEvent(
            title: "Удаляемое событие",
            description: "Проверка удаленного события",
            startAt: new DateTime(2026, 5, 14, 10, 0, 0),
            endAt: new DateTime(2026, 5, 14, 12, 0, 0)));
        eventService.DeleteEvent(createdEvent.Id);

        // Act
        var action = async () => await bookingService.CreateBookingAsync(createdEvent.Id);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetBookingByIdAsync_WhenBookingDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var bookingService = new BookingService(new InMemoryBookingStore(), new EventService());

        // Act
        var action = async () => await bookingService.GetBookingByIdAsync(Guid.NewGuid());

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }
}
