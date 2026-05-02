using EventManagementService.API.Dtos;
using EventManagementService.API.Exceptions;
using EventManagementService.API.Services;
using FluentAssertions;

namespace EventManagementService.API.Tests.Services;

public class EventServiceValidationTests
{
    [Fact]
    public void GetEventById_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var service = new EventService();

        // Act
        var action = () => service.GetEventById(Guid.NewGuid());

        // Assert
        action.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void UpdateEvent_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var service = new EventService();
        var updatedEvent = EventTestData.CreateEvent(
            title: "Обновление",
            description: "Не существует",
            startAt: new DateTime(2026, 9, 1, 10, 0, 0),
            endAt: new DateTime(2026, 9, 1, 12, 0, 0));

        // Act
        var action = () => service.UpdateEvent(Guid.NewGuid(), updatedEvent);

        // Assert
        action.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void DeleteEvent_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var service = new EventService();

        // Act
        var action = () => service.DeleteEvent(Guid.NewGuid());

        // Assert
        action.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void CreateEvent_WhenTitleIsWhitespace_ThrowsBusinessValidationException()
    {
        // Arrange
        var service = new EventService();
        var invalidEvent = EventTestData.CreateEvent(
            title: "   ",
            description: "Некорректное событие",
            startAt: new DateTime(2026, 10, 1, 10, 0, 0),
            endAt: new DateTime(2026, 10, 1, 12, 0, 0));

        // Act
        var action = () => service.CreateEvent(invalidEvent);

        // Assert
        action.Should().Throw<BusinessValidationException>()
            .WithMessage("Название события не должно быть пустым.");
    }

    [Fact]
    public void CreateEvent_WhenEndAtIsEarlierThanStartAt_ThrowsBusinessValidationException()
    {
        // Arrange
        var service = new EventService();
        var invalidEvent = EventTestData.CreateEvent(
            title: "Некорректные даты",
            description: "Ошибка дат",
            startAt: new DateTime(2026, 10, 2, 12, 0, 0),
            endAt: new DateTime(2026, 10, 2, 11, 0, 0));

        // Act
        var action = () => service.CreateEvent(invalidEvent);

        // Assert
        action.Should().Throw<BusinessValidationException>()
            .WithMessage("Дата окончания должна быть позже даты начала события.");
    }

    [Fact]
    public void CreateEvent_WhenTotalSeatsIsLessThanOne_ThrowsBusinessValidationException()
    {
        // Arrange
        var service = new EventService();
        var invalidEvent = EventTestData.CreateEvent(
            title: "Некорректная вместимость",
            description: "Ошибка мест",
            startAt: new DateTime(2026, 10, 2, 12, 0, 0),
            endAt: new DateTime(2026, 10, 2, 14, 0, 0),
            totalSeats: 0);

        // Act
        var action = () => service.CreateEvent(invalidEvent);

        // Assert
        action.Should().Throw<BusinessValidationException>()
            .WithMessage("Количество мест должно быть больше нуля.");
    }

    [Fact]
    public void UpdateEvent_WhenEndAtIsEarlierThanStartAt_ThrowsBusinessValidationException()
    {
        // Arrange
        var service = new EventService();
        var createdEvent = service.CreateEvent(EventTestData.CreateEvent(
            title: "Корректное событие",
            description: "Будет обновлено",
            startAt: new DateTime(2026, 10, 3, 9, 0, 0),
            endAt: new DateTime(2026, 10, 3, 11, 0, 0)));
        var invalidEvent = EventTestData.CreateEvent(
            title: "Обновлённое событие",
            description: "Некорректные даты",
            startAt: new DateTime(2026, 10, 4, 16, 0, 0),
            endAt: new DateTime(2026, 10, 4, 15, 0, 0));

        // Act
        var action = () => service.UpdateEvent(createdEvent.Id, invalidEvent);

        // Assert
        action.Should().Throw<BusinessValidationException>()
            .WithMessage("Дата окончания должна быть позже даты начала события.");
    }

    [Fact]
    public void GetEvents_WhenPageIsLessThanOne_ThrowsBusinessValidationException()
    {
        // Arrange
        var service = new EventService();

        // Act
        var action = () => service.GetEvents(new GetEventsQuery
        {
            Page = 0,
            PageSize = 10
        });

        // Assert
        action.Should().Throw<BusinessValidationException>();
    }

    [Fact]
    public void GetEvents_WhenFromIsLaterThanTo_ThrowsBusinessValidationException()
    {
        // Arrange
        var service = new EventService();

        // Act
        var action = () => service.GetEvents(new GetEventsQuery
        {
            From = new DateTime(2026, 11, 5, 0, 0, 0),
            To = new DateTime(2026, 11, 4, 23, 59, 59),
            Page = 1,
            PageSize = 10
        });

        // Assert
        action.Should().Throw<BusinessValidationException>()
            .WithMessage("Дата начала диапазона не должна быть позже даты окончания.");
    }

    [Fact]
    public void GetEvents_WhenPageSizeIsGreaterThanHundred_ThrowsBusinessValidationException()
    {
        // Arrange
        var service = new EventService();

        // Act
        var action = () => service.GetEvents(new GetEventsQuery
        {
            Page = 1,
            PageSize = 101
        });

        // Assert
        action.Should().Throw<BusinessValidationException>();
    }
}
