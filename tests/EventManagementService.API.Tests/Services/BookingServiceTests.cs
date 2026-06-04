using EventManagementService.Domain.Exceptions;
using EventManagementService.API.DataAccess;
using EventManagementService.Domain.Models;
using EventManagementService.API.Repositories;
using EventManagementService.API.Services;
using EventManagementService.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace EventManagementService.API.Tests.Services;

public class BookingServiceTests
{
    [Fact]
    public async Task CreateBookingAsync_WhenEventExists_ReturnsPendingBooking()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        using var context = TestDbContextFactory.CreateContext();
        var eventService = new EventService(new EventRepository(context));
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));
        var createdEvent = await eventService.CreateEventAsync(EventTestData.CreateEvent(
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
        (await context.Bookings.FirstOrDefaultAsync(item => item.Id == booking.Id, cancellationToken)).Should().NotBeNull();
    }

    [Fact]
    public async Task CreateBookingAsync_WhenCreatingMultipleBookingsForSameEvent_ReturnsUniqueIds()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateContext();
        var eventService = new EventService(new EventRepository(context));
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));
        var createdEvent = await eventService.CreateEventAsync(EventTestData.CreateEvent(
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
        using var context = TestDbContextFactory.CreateContext();
        var eventService = new EventService(new EventRepository(context));
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));
        var createdEvent = await eventService.CreateEventAsync(EventTestData.CreateEvent(
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
        var cancellationToken = TestContext.Current.CancellationToken;
        using var context = TestDbContextFactory.CreateContext();
        var eventService = new EventService(new EventRepository(context));
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));
        var createdEvent = await eventService.CreateEventAsync(EventTestData.CreateEvent(
            title: "Статусная проверка",
            description: "Подтверждение или отказ",
            startAt: new DateTime(2026, 5, 13, 12, 0, 0),
            endAt: new DateTime(2026, 5, 13, 14, 0, 0)));
        var createdBooking = await bookingService.CreateBookingAsync(createdEvent.Id);
        var processedAt = new DateTime(2026, 5, 13, 12, 10, 0, DateTimeKind.Utc);
        var storedBooking = await context.Bookings.FirstAsync(item => item.Id == createdBooking.Id, cancellationToken);
        switch (status)
        {
            case BookingStatus.Confirmed:
                storedBooking.Confirm(processedAt);
                break;
            case BookingStatus.Rejected:
                storedBooking.Reject(processedAt);
                break;
        }

        await context.SaveChangesAsync(cancellationToken);

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
        using var context = TestDbContextFactory.CreateContext();
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));

        // Act
        var action = async () => await bookingService.CreateBookingAsync(Guid.NewGuid());

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateBookingAsync_WhenEventWasDeleted_ThrowsNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateContext();
        var eventService = new EventService(new EventRepository(context));
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));
        var createdEvent = await eventService.CreateEventAsync(EventTestData.CreateEvent(
            title: "Удаляемое событие",
            description: "Проверка удаленного события",
            startAt: new DateTime(2026, 5, 14, 10, 0, 0),
            endAt: new DateTime(2026, 5, 14, 12, 0, 0)));
        await eventService.DeleteEventAsync(createdEvent.Id);

        // Act
        var action = async () => await bookingService.CreateBookingAsync(createdEvent.Id);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetBookingByIdAsync_WhenBookingDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateContext();
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));

        // Act
        var action = async () => await bookingService.GetBookingByIdAsync(Guid.NewGuid());

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateBookingAsync_WhenSeatsAreAvailable_DecreasesAvailableSeats()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateContext();
        var eventService = new EventService(new EventRepository(context));
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));
        var createdEvent = await eventService.CreateEventAsync(EventTestData.CreateEvent(
            title: "Событие с местами",
            description: null,
            startAt: new DateTime(2026, 5, 10, 10, 0, 0),
            endAt: new DateTime(2026, 5, 10, 12, 0, 0),
            totalSeats: 3));

        // Act
        await bookingService.CreateBookingAsync(createdEvent.Id);

        // Assert
        var updatedEvent = await eventService.GetEventByIdAsync(createdEvent.Id);
        updatedEvent.AvailableSeats.Should().Be(2);
    }

    [Fact]
    public async Task CreateBookingAsync_WhenAllSeatsAreTaken_ThrowsNoAvailableSeatsException()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateContext();
        var eventService = new EventService(new EventRepository(context));
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));
        var createdEvent = await eventService.CreateEventAsync(EventTestData.CreateEvent(
            title: "Однoместное событие",
            description: null,
            startAt: new DateTime(2026, 5, 11, 10, 0, 0),
            endAt: new DateTime(2026, 5, 11, 12, 0, 0),
            totalSeats: 1));

        await bookingService.CreateBookingAsync(createdEvent.Id);

        // Act
        var action = async () => await bookingService.CreateBookingAsync(createdEvent.Id);

        // Assert
        await action.Should().ThrowAsync<NoAvailableSeatsException>();
    }

    [Fact]
    public async Task CreateBookingAsync_WhenRequestedConcurrently_DoesNotExceedTotalSeats()
    {
        // Arrange
        const int totalSeats = 5;
        const int concurrentRequests = 20;

        using var serviceProvider = TestDbContextFactory.CreateServiceProvider();
        Guid eventId;
        using (var seedScope = serviceProvider.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var eventService = new EventService(new EventRepository(seedContext));
            var createdEvent = await eventService.CreateEventAsync(EventTestData.CreateEvent(
                title: "Конкурентное событие",
                description: null,
                startAt: new DateTime(2026, 5, 12, 10, 0, 0),
                endAt: new DateTime(2026, 5, 12, 12, 0, 0),
                totalSeats: totalSeats));
            eventId = createdEvent.Id;
        }

        var exceptions = new ConcurrentBag<Exception>();

        // Act
        var tasks = Enumerable.Range(0, concurrentRequests).Select(async _ =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));
                await bookingService.CreateBookingAsync(eventId);
            }
            catch (NoAvailableSeatsException ex)
            {
                exceptions.Add(ex);
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        var successCount = concurrentRequests - exceptions.Count;
        successCount.Should().Be(totalSeats);
        exceptions.Should().HaveCount(concurrentRequests - totalSeats);

        using var assertScope = serviceProvider.CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assertEventService = new EventService(new EventRepository(assertContext));
        var finalEvent = await assertEventService.GetEventByIdAsync(eventId);
        finalEvent.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task CreateBookingAsync_WhenRequestedConcurrently_ReturnsUniqueBookingIds()
    {
        // Arrange
        const int totalSeats = 10;

        using var serviceProvider = TestDbContextFactory.CreateServiceProvider();
        Guid eventId;
        using (var seedScope = serviceProvider.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var eventService = new EventService(new EventRepository(seedContext));
            var createdEvent = await eventService.CreateEventAsync(EventTestData.CreateEvent(
                title: "Событие для Id-проверки",
                description: null,
                startAt: new DateTime(2026, 5, 13, 10, 0, 0),
                endAt: new DateTime(2026, 5, 13, 12, 0, 0),
                totalSeats: totalSeats));
            eventId = createdEvent.Id;
        }

        // Act — ровно totalSeats параллельных запросов, все должны пройти
        var tasks = Enumerable.Range(0, totalSeats)
            .Select(async _ =>
            {
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));
                return await bookingService.CreateBookingAsync(eventId);
            });

        var bookings = await Task.WhenAll(tasks);

        // Assert
        bookings.Select(b => b.Id).Should().OnlyHaveUniqueItems();
        bookings.Should().HaveCount(totalSeats);
    }
}
