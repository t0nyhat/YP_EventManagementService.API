using EventManagementService.API.Dtos;
using EventManagementService.API.Exceptions;
using EventManagementService.API.Services;

namespace EventManagementService.API.Tests.Services;

public class EventServiceCrudTests
{
    [Fact]
    public void CreateEvent_WhenEventIsValid_CreatesEventWithGeneratedId()
    {
        // Arrange
        var service = new EventService();
        var newEvent = EventTestData.CreateEvent(
            title: "Конференция .NET",
            description: "Технологическое мероприятие",
            startAt: new DateTime(2026, 4, 10, 10, 0, 0),
            endAt: new DateTime(2026, 4, 10, 18, 0, 0));

        // Act
        var createdEvent = service.CreateEvent(newEvent);

        // Assert
        Assert.NotEqual(Guid.Empty, createdEvent.Id);
        Assert.Equal("Конференция .NET", createdEvent.Title);
        Assert.Equal("Технологическое мероприятие", createdEvent.Description);
        Assert.Equal(new DateTime(2026, 4, 10, 10, 0, 0), createdEvent.StartAt);
        Assert.Equal(new DateTime(2026, 4, 10, 18, 0, 0), createdEvent.EndAt);
    }

    [Fact]
    public void GetEvents_WhenEventsExist_ReturnsAllEventsOnSinglePage()
    {
        // Arrange
        var service = new EventService();
        var firstEvent = service.CreateEvent(EventTestData.CreateEvent(
            title: "Лекция",
            description: "Первая лекция",
            startAt: new DateTime(2026, 5, 1, 9, 0, 0),
            endAt: new DateTime(2026, 5, 1, 11, 0, 0)));
        var secondEvent = service.CreateEvent(EventTestData.CreateEvent(
            title: "Воркшоп",
            description: "Практическое занятие",
            startAt: new DateTime(2026, 5, 2, 12, 0, 0),
            endAt: new DateTime(2026, 5, 2, 15, 0, 0)));

        // Act
        var result = service.GetEvents(new GetEventsQuery
        {
            Page = 1,
            PageSize = 10
        });
        var events = result.Items.ToArray();

        // Assert
        Assert.Equal(2, events.Length);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(firstEvent.Id, events[0].Id);
        Assert.Equal(secondEvent.Id, events[1].Id);
    }

    [Fact]
    public void GetEventById_WhenEventExists_ReturnsRequestedEvent()
    {
        // Arrange
        var service = new EventService();
        var createdEvent = service.CreateEvent(EventTestData.CreateEvent(
            title: "Митап",
            description: "Встреча сообщества",
            startAt: new DateTime(2026, 6, 3, 18, 0, 0),
            endAt: new DateTime(2026, 6, 3, 20, 0, 0)));

        // Act
        var eventItem = service.GetEventById(createdEvent.Id);

        // Assert
        Assert.Equal(createdEvent.Id, eventItem.Id);
        Assert.Equal("Митап", eventItem.Title);
        Assert.Equal("Встреча сообщества", eventItem.Description);
    }

    [Fact]
    public void UpdateEvent_WhenEventExists_UpdatesEventFields()
    {
        // Arrange
        var service = new EventService();
        var createdEvent = service.CreateEvent(EventTestData.CreateEvent(
            title: "Старое название",
            description: "Старое описание",
            startAt: new DateTime(2026, 7, 1, 10, 0, 0),
            endAt: new DateTime(2026, 7, 1, 12, 0, 0)));
        var updatedEvent = EventTestData.CreateEvent(
            title: "Новое название",
            description: "Новое описание",
            startAt: new DateTime(2026, 7, 2, 13, 0, 0),
            endAt: new DateTime(2026, 7, 2, 15, 0, 0));

        // Act
        var result = service.UpdateEvent(createdEvent.Id, updatedEvent);

        // Assert
        Assert.Equal(createdEvent.Id, result.Id);
        Assert.Equal("Новое название", result.Title);
        Assert.Equal("Новое описание", result.Description);
        Assert.Equal(new DateTime(2026, 7, 2, 13, 0, 0), result.StartAt);
        Assert.Equal(new DateTime(2026, 7, 2, 15, 0, 0), result.EndAt);
    }

    [Fact]
    public void UpdateEvent_WhenDescriptionIsNull_ClearsDescription()
    {
        // Arrange
        var service = new EventService();
        var createdEvent = service.CreateEvent(EventTestData.CreateEvent(
            title: "Событие с описанием",
            description: "Описание будет очищено",
            startAt: new DateTime(2026, 7, 5, 10, 0, 0),
            endAt: new DateTime(2026, 7, 5, 12, 0, 0)));
        var updatedEvent = EventTestData.CreateEvent(
            title: "Событие без описания",
            description: null,
            startAt: new DateTime(2026, 7, 6, 13, 0, 0),
            endAt: new DateTime(2026, 7, 6, 15, 0, 0));

        // Act
        var result = service.UpdateEvent(createdEvent.Id, updatedEvent);

        // Assert
        Assert.Equal(createdEvent.Id, result.Id);
        Assert.Equal("Событие без описания", result.Title);
        Assert.Null(result.Description);
        Assert.Equal(new DateTime(2026, 7, 6, 13, 0, 0), result.StartAt);
        Assert.Equal(new DateTime(2026, 7, 6, 15, 0, 0), result.EndAt);
    }

    [Fact]
    public void DeleteEvent_WhenEventExists_RemovesEventFromStorage()
    {
        // Arrange
        var service = new EventService();
        var createdEvent = service.CreateEvent(EventTestData.CreateEvent(
            title: "Удаляемое событие",
            description: "Будет удалено",
            startAt: new DateTime(2026, 8, 12, 14, 0, 0),
            endAt: new DateTime(2026, 8, 12, 16, 0, 0)));

        // Act
        service.DeleteEvent(createdEvent.Id);
        var result = service.GetEvents(new GetEventsQuery());

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Throws<NotFoundException>(() => service.GetEventById(createdEvent.Id));
    }

}
