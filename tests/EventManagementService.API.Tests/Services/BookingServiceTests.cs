using EventManagementService.Application.Configuration;
using EventManagementService.Domain.Exceptions;
using EventManagementService.Domain.Models;
using EventManagementService.Infrastructure.DataAccess;
using EventManagementService.Infrastructure.Repositories;
using EventManagementService.Application.Services;
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
            startAt: DateTime.UtcNow.AddDays(2),
            endAt: DateTime.UtcNow.AddDays(2).AddHours(2)));

        // Act
        var booking = await bookingService.CreateBookingAsync(createdEvent.Id, User.SystemUserId);

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
            startAt: DateTime.UtcNow.AddDays(3),
            endAt: DateTime.UtcNow.AddDays(3).AddHours(2)));

        // Act
        var firstBooking = await bookingService.CreateBookingAsync(createdEvent.Id, User.SystemUserId);
        var secondBooking = await bookingService.CreateBookingAsync(createdEvent.Id, User.SystemUserId);

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
            startAt: DateTime.UtcNow.AddDays(4),
            endAt: DateTime.UtcNow.AddDays(4).AddHours(2)));
        var createdBooking = await bookingService.CreateBookingAsync(createdEvent.Id, User.SystemUserId);

        // Act
        var booking = await bookingService.GetBookingByIdAsync(createdBooking.Id, User.SystemUserId, UserRole.User);

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
            startAt: DateTime.UtcNow.AddDays(5),
            endAt: DateTime.UtcNow.AddDays(5).AddHours(2)));
        var createdBooking = await bookingService.CreateBookingAsync(createdEvent.Id, User.SystemUserId);
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
        var booking = await bookingService.GetBookingByIdAsync(createdBooking.Id, User.SystemUserId, UserRole.User);

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
        var action = async () => await bookingService.CreateBookingAsync(Guid.NewGuid(), User.SystemUserId);

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
            startAt: DateTime.UtcNow.AddDays(6),
            endAt: DateTime.UtcNow.AddDays(6).AddHours(2)));
        await eventService.DeleteEventAsync(createdEvent.Id);

        // Act
        var action = async () => await bookingService.CreateBookingAsync(createdEvent.Id, User.SystemUserId);

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
        var action = async () => await bookingService.GetBookingByIdAsync(Guid.NewGuid(), User.SystemUserId, UserRole.User);

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
            startAt: DateTime.UtcNow.AddDays(7),
            endAt: DateTime.UtcNow.AddDays(7).AddHours(2),
            totalSeats: 3));

        // Act
        await bookingService.CreateBookingAsync(createdEvent.Id, User.SystemUserId);

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
            startAt: DateTime.UtcNow.AddDays(8),
            endAt: DateTime.UtcNow.AddDays(8).AddHours(2),
            totalSeats: 1));

        await bookingService.CreateBookingAsync(createdEvent.Id, User.SystemUserId);

        // Act
        var action = async () => await bookingService.CreateBookingAsync(createdEvent.Id, User.SystemUserId);

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
                startAt: DateTime.UtcNow.AddDays(9),
                endAt: DateTime.UtcNow.AddDays(9).AddHours(2),
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
                await bookingService.CreateBookingAsync(eventId, Guid.NewGuid());
            }
            catch (Exception ex) when (ex is NoAvailableSeatsException or TooManyActiveBookingsException)
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
                startAt: DateTime.UtcNow.AddDays(10),
                endAt: DateTime.UtcNow.AddDays(10).AddHours(2),
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
                return await bookingService.CreateBookingAsync(eventId, Guid.NewGuid());
            });

        var bookings = await Task.WhenAll(tasks);

        // Assert
        bookings.Select(b => b.Id).Should().OnlyHaveUniqueItems();
        bookings.Should().HaveCount(totalSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_WhenEventAlreadyStarted_ThrowsBookingInPastException()
    {
        using var context = TestDbContextFactory.CreateContext();
        var eventService = new EventService(new EventRepository(context));
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));

        var createdEvent = await eventService.CreateEventAsync(EventTestData.CreateEvent(
            title: "Прошедшее событие",
            description: null,
            startAt: DateTime.UtcNow.AddHours(-1),
            endAt: DateTime.UtcNow.AddMinutes(30),
            totalSeats: 10));

        var action = async () => await bookingService.CreateBookingAsync(createdEvent.Id, User.SystemUserId);

        await action.Should().ThrowAsync<BookingInPastException>();
    }

    [Fact]
    public async Task CreateBookingAsync_WhenUserExceedsActiveBookingsLimit_ThrowsTooManyActiveBookingsException()
    {
        using var context = TestDbContextFactory.CreateContext();
        var eventService = new EventService(new EventRepository(context));
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));
        var userId = Guid.NewGuid();

        for (var i = 0; i < BookingRules.MaxActiveBookingsPerUser; i++)
        {
            var createdEvent = await eventService.CreateEventAsync(EventTestData.CreateEvent(
                title: $"Лимит {i}",
                description: null,
                startAt: DateTime.UtcNow.AddDays(2).AddHours(i),
                endAt: DateTime.UtcNow.AddDays(2).AddHours(i + 1),
                totalSeats: 5));

            await bookingService.CreateBookingAsync(createdEvent.Id, userId);
        }

        var overLimitEvent = await eventService.CreateEventAsync(EventTestData.CreateEvent(
            title: "Лимит 11",
            description: null,
            startAt: DateTime.UtcNow.AddDays(3),
            endAt: DateTime.UtcNow.AddDays(3).AddHours(1),
            totalSeats: 5));

        var action = async () => await bookingService.CreateBookingAsync(overLimitEvent.Id, userId);

        await action.Should().ThrowAsync<TooManyActiveBookingsException>();
    }

    [Fact]
    public async Task CreateBookingAsync_WhenFirstUserReachedLimit_SecondUserCanStillBook()
    {
        using var context = TestDbContextFactory.CreateContext();
        var eventService = new EventService(new EventRepository(context));
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();

        for (var i = 0; i < BookingRules.MaxActiveBookingsPerUser; i++)
        {
            var eventForFirstUser = await eventService.CreateEventAsync(EventTestData.CreateEvent(
                title: $"Лимит первого пользователя {i}",
                description: null,
                startAt: DateTime.UtcNow.AddDays(4).AddHours(i),
                endAt: DateTime.UtcNow.AddDays(4).AddHours(i + 1),
                totalSeats: 2));

            await bookingService.CreateBookingAsync(eventForFirstUser.Id, firstUserId);
        }

        var eventForSecondUser = await eventService.CreateEventAsync(EventTestData.CreateEvent(
            title: "Отдельный лимит второго пользователя",
            description: null,
            startAt: DateTime.UtcNow.AddDays(5),
            endAt: DateTime.UtcNow.AddDays(5).AddHours(1),
            totalSeats: 2));

        var booking = await bookingService.CreateBookingAsync(eventForSecondUser.Id, secondUserId);

        booking.UserId.Should().Be(secondUserId);
        booking.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task CancelBookingAsync_WhenCalledByNonOwnerAndNotAdmin_ThrowsForbiddenOperationException()
    {
        using var context = TestDbContextFactory.CreateContext();
        var eventService = new EventService(new EventRepository(context));
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));
        var ownerId = Guid.NewGuid();

        var createdEvent = await eventService.CreateEventAsync(EventTestData.CreateEvent(
            title: "Отмена",
            description: null,
            startAt: DateTime.UtcNow.AddDays(1),
            endAt: DateTime.UtcNow.AddDays(1).AddHours(1),
            totalSeats: 2));
        var booking = await bookingService.CreateBookingAsync(createdEvent.Id, ownerId);

        var action = async () => await bookingService.CancelBookingAsync(booking.Id, Guid.NewGuid(), UserRole.User);

        await action.Should().ThrowAsync<ForbiddenOperationException>();
    }

    [Fact]
    public async Task CancelBookingAsync_WhenCalledByOwnerForConfirmedBooking_CancelsAndReleasesSeat()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var context = TestDbContextFactory.CreateContext();
        var eventService = new EventService(new EventRepository(context));
        var bookingService = new BookingService(new EventRepository(context), new BookingRepository(context));
        var ownerId = Guid.NewGuid();

        var createdEvent = await eventService.CreateEventAsync(EventTestData.CreateEvent(
            title: "Отмена подтвержденной брони",
            description: null,
            startAt: DateTime.UtcNow.AddDays(2),
            endAt: DateTime.UtcNow.AddDays(2).AddHours(1),
            totalSeats: 2));

        var booking = await bookingService.CreateBookingAsync(createdEvent.Id, ownerId);
        booking.Confirm(DateTime.UtcNow);
        await context.SaveChangesAsync(cancellationToken);

        await bookingService.CancelBookingAsync(booking.Id, ownerId, UserRole.User);

        var updatedBooking = await context.Bookings.FirstAsync(item => item.Id == booking.Id, cancellationToken);
        updatedBooking.Status.Should().Be(BookingStatus.Cancelled);

        var updatedEvent = await eventService.GetEventByIdAsync(createdEvent.Id);
        updatedEvent.AvailableSeats.Should().Be(2);
    }
}
