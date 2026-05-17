using EventManagementService.API.Dtos;
using EventManagementService.API.Exceptions;
using EventManagementService.API.Models;
using EventManagementService.API.Repositories;
using EventManagementService.API.Services;
using EventManagementService.API.Tests.Infrastructure;
using FluentAssertions;

namespace EventManagementService.API.Tests.Services;

public class EventServiceValidationTests
{
    [Fact]
    public async Task GetEventById_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var context = TestDbContextFactory.CreateContext();
        var service = new EventService(new EventRepository(context));

        // Act
        var action = async () => await service.GetEventByIdAsync(Guid.NewGuid());

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateEvent_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var context = TestDbContextFactory.CreateContext();
        var service = new EventService(new EventRepository(context));
        var request = new UpdateEventRequest
        {
            Title = "Обновление",
            Description = "Не существует",
            StartAt = new DateTime(2026, 9, 1, 10, 0, 0),
            EndAt = new DateTime(2026, 9, 1, 12, 0, 0)
        };

        // Act
        var action = async () => await service.UpdateEventAsync(Guid.NewGuid(), request);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteEvent_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var context = TestDbContextFactory.CreateContext();
        var service = new EventService(new EventRepository(context));

        // Act
        var action = async () => await service.DeleteEventAsync(Guid.NewGuid());

        // Assert
        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void CreateEvent_WhenTitleIsWhitespace_ThrowsBusinessValidationException()
    {
        // Act
        var action = () => Event.Create(
            title: "   ",
            startAt: new DateTime(2026, 10, 1, 10, 0, 0),
            endAt: new DateTime(2026, 10, 1, 12, 0, 0),
            totalSeats: 10);

        // Assert
        action.Should().Throw<BusinessValidationException>()
            .WithMessage("Название события не должно быть пустым.");
    }

    [Fact]
    public void CreateEvent_WhenEndAtIsEarlierThanStartAt_ThrowsBusinessValidationException()
    {
        // Act
        var action = () => Event.Create(
            title: "Некорректные даты",
            startAt: new DateTime(2026, 10, 2, 12, 0, 0),
            endAt: new DateTime(2026, 10, 2, 11, 0, 0),
            totalSeats: 10);

        // Assert
        action.Should().Throw<BusinessValidationException>()
            .WithMessage("Дата окончания должна быть позже даты начала события.");
    }

    [Fact]
    public void CreateEvent_WhenTotalSeatsIsLessThanOne_ThrowsBusinessValidationException()
    {
        // Act
        var action = () => Event.Create(
            title: "Некорректная вместимость",
            startAt: new DateTime(2026, 10, 2, 12, 0, 0),
            endAt: new DateTime(2026, 10, 2, 14, 0, 0),
            totalSeats: 0);

        // Assert
        action.Should().Throw<BusinessValidationException>()
            .WithMessage("Количество мест должно быть больше нуля.");
    }

    [Fact]
    public async Task UpdateEvent_WhenEndAtIsEarlierThanStartAt_ThrowsBusinessValidationException()
    {
        // Arrange
        var context = TestDbContextFactory.CreateContext();
        var service = new EventService(new EventRepository(context));
        var createdEvent = await service.CreateEventAsync(EventTestData.CreateEvent(
            title: "Корректное событие",
            description: "Будет обновлено",
            startAt: new DateTime(2026, 10, 3, 9, 0, 0),
            endAt: new DateTime(2026, 10, 3, 11, 0, 0)));
        var request = new UpdateEventRequest
        {
            Title = "Обновлённое событие",
            Description = "Некорректные даты",
            StartAt = new DateTime(2026, 10, 4, 16, 0, 0),
            EndAt = new DateTime(2026, 10, 4, 15, 0, 0)
        };

        // Act
        var action = async () => await service.UpdateEventAsync(createdEvent.Id, request);

        // Assert
        await action.Should().ThrowAsync<BusinessValidationException>()
            .WithMessage("Дата окончания должна быть позже даты начала события.");
    }

    [Fact]
    public async Task GetEvents_WhenPageIsLessThanOne_ThrowsBusinessValidationException()
    {
        // Arrange
        var context = TestDbContextFactory.CreateContext();
        var service = new EventService(new EventRepository(context));

        // Act
        var action = async () => await service.GetEventsAsync(new GetEventsQuery
        {
            Page = 0,
            PageSize = 10
        });

        // Assert
        await action.Should().ThrowAsync<BusinessValidationException>();
    }

    [Fact]
    public async Task GetEvents_WhenFromIsLaterThanTo_ThrowsBusinessValidationException()
    {
        // Arrange
        var context = TestDbContextFactory.CreateContext();
        var service = new EventService(new EventRepository(context));

        // Act
        var action = async () => await service.GetEventsAsync(new GetEventsQuery
        {
            From = new DateTime(2026, 11, 5, 0, 0, 0),
            To = new DateTime(2026, 11, 4, 23, 59, 59),
            Page = 1,
            PageSize = 10
        });

        // Assert
        await action.Should().ThrowAsync<BusinessValidationException>()
            .WithMessage("Дата начала диапазона не должна быть позже даты окончания.");
    }

    [Fact]
    public async Task GetEvents_WhenPageSizeIsGreaterThanHundred_ThrowsBusinessValidationException()
    {
        // Arrange
        var context = TestDbContextFactory.CreateContext();
        var service = new EventService(new EventRepository(context));

        // Act
        var action = async () => await service.GetEventsAsync(new GetEventsQuery
        {
            Page = 1,
            PageSize = 101
        });

        // Assert
        await action.Should().ThrowAsync<BusinessValidationException>();
    }
}
