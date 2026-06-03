using EventManagementService.API.Dtos;
using EventManagementService.API.Exceptions;
using EventManagementService.API.Repositories;
using EventManagementService.API.Services;
using EventManagementService.API.Tests.Infrastructure;
using FluentAssertions;

namespace EventManagementService.API.Tests.Services;

public class EventServiceCrudTests
{
    [Fact]
    public async Task CreateEvent_WhenEventIsValid_CreatesEventWithGeneratedId()
    {
        // Arrange
        var context = TestDbContextFactory.CreateContext();
        var service = new EventService(new EventRepository(context));
        var newEvent = EventTestData.CreateEvent(
            title: "Конференция .NET",
            description: "Технологическое мероприятие",
            startAt: new DateTime(2026, 4, 10, 10, 0, 0),
            endAt: new DateTime(2026, 4, 10, 18, 0, 0));

        // Act
        var createdEvent = await service.CreateEventAsync(newEvent);

        // Assert
        createdEvent.Id.Should().NotBe(Guid.Empty);
        createdEvent.Title.Should().Be("Конференция .NET");
        createdEvent.Description.Should().Be("Технологическое мероприятие");
        createdEvent.StartAt.Should().Be(new DateTime(2026, 4, 10, 10, 0, 0));
        createdEvent.EndAt.Should().Be(new DateTime(2026, 4, 10, 18, 0, 0));
        createdEvent.TotalSeats.Should().Be(10);
        createdEvent.AvailableSeats.Should().Be(10);
    }

    [Fact]
    public async Task GetEvents_WhenEventsExist_ReturnsAllEventsOnSinglePage()
    {
        // Arrange
        var context = TestDbContextFactory.CreateContext();
        var service = new EventService(new EventRepository(context));
        var firstEvent = await service.CreateEventAsync(EventTestData.CreateEvent(
            title: "Лекция",
            description: "Первая лекция",
            startAt: new DateTime(2026, 5, 1, 9, 0, 0),
            endAt: new DateTime(2026, 5, 1, 11, 0, 0)));
        var secondEvent = await service.CreateEventAsync(EventTestData.CreateEvent(
            title: "Воркшоп",
            description: "Практическое занятие",
            startAt: new DateTime(2026, 5, 2, 12, 0, 0),
            endAt: new DateTime(2026, 5, 2, 15, 0, 0)));

        // Act
        var result = await service.GetEventsAsync(new GetEventsQuery
        {
            Page = 1,
            PageSize = 10
        });
        var events = result.Items.ToArray();

        // Assert
        events.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        events.Select(item => item.Id).Should().ContainInOrder(firstEvent.Id, secondEvent.Id);
    }

    [Fact]
    public async Task GetEventById_WhenEventExists_ReturnsRequestedEvent()
    {
        // Arrange
        var context = TestDbContextFactory.CreateContext();
        var service = new EventService(new EventRepository(context));
        var createdEvent = await service.CreateEventAsync(EventTestData.CreateEvent(
            title: "Митап",
            description: "Встреча сообщества",
            startAt: new DateTime(2026, 6, 3, 18, 0, 0),
            endAt: new DateTime(2026, 6, 3, 20, 0, 0)));

        // Act
        var eventItem = await service.GetEventByIdAsync(createdEvent.Id);

        // Assert
        eventItem.Id.Should().Be(createdEvent.Id);
        eventItem.Title.Should().Be("Митап");
        eventItem.Description.Should().Be("Встреча сообщества");
        eventItem.TotalSeats.Should().Be(10);
        eventItem.AvailableSeats.Should().Be(10);
    }

    [Fact]
    public async Task UpdateEvent_WhenEventExists_UpdatesEventFields()
    {
        // Arrange
        var context = TestDbContextFactory.CreateContext();
        var service = new EventService(new EventRepository(context));
        var createdEvent = await service.CreateEventAsync(EventTestData.CreateEvent(
            title: "Старое название",
            description: "Старое описание",
            startAt: new DateTime(2026, 7, 1, 10, 0, 0),
            endAt: new DateTime(2026, 7, 1, 12, 0, 0)));
        var request = new UpdateEventRequest
        {
            Title = "Новое название",
            Description = "Новое описание",
            StartAt = new DateTime(2026, 7, 2, 13, 0, 0),
            EndAt = new DateTime(2026, 7, 2, 15, 0, 0)
        };

        // Act
        var result = await service.UpdateEventAsync(createdEvent.Id, request);

        // Assert
        result.Id.Should().Be(createdEvent.Id);
        result.Title.Should().Be("Новое название");
        result.Description.Should().Be("Новое описание");
        result.StartAt.Should().Be(new DateTime(2026, 7, 2, 13, 0, 0));
        result.EndAt.Should().Be(new DateTime(2026, 7, 2, 15, 0, 0));
    }

    [Fact]
    public async Task UpdateEvent_WhenDescriptionIsNull_ClearsDescription()
    {
        // Arrange
        var context = TestDbContextFactory.CreateContext();
        var service = new EventService(new EventRepository(context));
        var createdEvent = await service.CreateEventAsync(EventTestData.CreateEvent(
            title: "Событие с описанием",
            description: "Описание будет очищено",
            startAt: new DateTime(2026, 7, 5, 10, 0, 0),
            endAt: new DateTime(2026, 7, 5, 12, 0, 0)));
        var request = new UpdateEventRequest
        {
            Title = "Событие без описания",
            Description = null,
            StartAt = new DateTime(2026, 7, 6, 13, 0, 0),
            EndAt = new DateTime(2026, 7, 6, 15, 0, 0)
        };

        // Act
        var result = await service.UpdateEventAsync(createdEvent.Id, request);

        // Assert
        result.Id.Should().Be(createdEvent.Id);
        result.Title.Should().Be("Событие без описания");
        result.Description.Should().BeNull();
        result.StartAt.Should().Be(new DateTime(2026, 7, 6, 13, 0, 0));
        result.EndAt.Should().Be(new DateTime(2026, 7, 6, 15, 0, 0));
    }

    [Fact]
    public async Task DeleteEvent_WhenEventExists_RemovesEventFromStorage()
    {
        // Arrange
        var context = TestDbContextFactory.CreateContext();
        var service = new EventService(new EventRepository(context));
        var createdEvent = await service.CreateEventAsync(EventTestData.CreateEvent(
            title: "Удаляемое событие",
            description: "Будет удалено",
            startAt: new DateTime(2026, 8, 12, 14, 0, 0),
            endAt: new DateTime(2026, 8, 12, 16, 0, 0)));

        // Act
        await service.DeleteEventAsync(createdEvent.Id);
        var result = await service.GetEventsAsync(new GetEventsQuery());

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        var action = async () => await service.GetEventByIdAsync(createdEvent.Id);
        await action.Should().ThrowAsync<NotFoundException>();
    }
}
