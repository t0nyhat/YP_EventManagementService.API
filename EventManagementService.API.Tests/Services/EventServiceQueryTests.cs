using EventManagementService.API.Dtos;
using EventManagementService.API.Services;

namespace EventManagementService.API.Tests.Services;

public class EventServiceQueryTests
{
    [Fact]
    public void GetEvents_WhenFilteredByTitle_ReturnsMatchingEventsCaseInsensitive()
    {
        // Arrange
        var service = CreateServiceWithSampleEvents();

        // Act
        var result = service.GetEvents(new GetEventsQuery
        {
            Title = "dotnet"
        });

        // Assert
        var items = result.Items.ToArray();
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Count);
        Assert.Equal(2, items.Length);
        Assert.All(items, item => Assert.Contains("dotnet", item.Title, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetEvents_WhenFilteredByDates_ReturnsOnlyEventsInRequestedRange()
    {
        // Arrange
        var service = CreateServiceWithSampleEvents();

        // Act
        var result = service.GetEvents(new GetEventsQuery
        {
            From = new DateTime(2026, 5, 2, 0, 0, 0),
            To = new DateTime(2026, 5, 4, 23, 59, 59)
        });

        // Assert
        var items = result.Items.ToArray();
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Count);
        Assert.Equal(3, items.Length);
        Assert.Equal("DotNet Advanced", items[0].Title);
        Assert.Equal("Архитектурный воркшоп", items[1].Title);
        Assert.Equal("DotNet Meetup", items[2].Title);
        Assert.All(items, item =>
        {
            Assert.True(item.StartAt >= new DateTime(2026, 5, 2, 0, 0, 0));
            Assert.True(item.EndAt <= new DateTime(2026, 5, 4, 23, 59, 59));
        });
    }

    [Fact]
    public void GetEvents_WhenUsingCombinedFilters_AppliesLogicalAnd()
    {
        // Arrange
        var service = CreateServiceWithSampleEvents();

        // Act
        var result = service.GetEvents(new GetEventsQuery
        {
            Title = "dotnet",
            From = new DateTime(2026, 5, 4, 0, 0, 0),
            To = new DateTime(2026, 5, 4, 23, 59, 59)
        });

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("DotNet Meetup", item.Title);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.Count);
    }

    [Fact]
    public void GetEvents_WhenPaginationIsRequested_ReturnsRequestedPageInStartAtOrder()
    {
        // Arrange
        var service = CreateServiceWithSampleEvents();

        // Act
        var result = service.GetEvents(new GetEventsQuery
        {
            Page = 2,
            PageSize = 2
        });

        // Assert
        var items = result.Items.ToArray();
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.Count);
        Assert.Equal(2, items.Length);
        Assert.Equal("Архитектурный воркшоп", items[0].Title);
        Assert.Equal("DotNet Meetup", items[1].Title);
    }

    [Fact]
    public void GetEvents_WhenNoEventsMatch_ReturnsEmptyPageWithZeroCount()
    {
        // Arrange
        var service = CreateServiceWithSampleEvents();

        // Act
        var result = service.GetEvents(new GetEventsQuery
        {
            Title = "python"
        });

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.Count);
    }

    private static EventService CreateServiceWithSampleEvents()
    {
        var service = new EventService();

        service.CreateEvent(EventTestData.CreateEvent(
            title: "Введение в C#",
            description: "Базовая лекция",
            startAt: new DateTime(2026, 5, 1, 9, 0, 0),
            endAt: new DateTime(2026, 5, 1, 11, 0, 0)));
        service.CreateEvent(EventTestData.CreateEvent(
            title: "DotNet Advanced",
            description: "Продвинутый курс",
            startAt: new DateTime(2026, 5, 2, 10, 0, 0),
            endAt: new DateTime(2026, 5, 2, 13, 0, 0)));
        service.CreateEvent(EventTestData.CreateEvent(
            title: "Архитектурный воркшоп",
            description: "Практика по архитектуре",
            startAt: new DateTime(2026, 5, 3, 14, 0, 0),
            endAt: new DateTime(2026, 5, 3, 17, 0, 0)));
        service.CreateEvent(EventTestData.CreateEvent(
            title: "DotNet Meetup",
            description: "Встреча сообщества",
            startAt: new DateTime(2026, 5, 4, 18, 0, 0),
            endAt: new DateTime(2026, 5, 4, 20, 0, 0)));
        service.CreateEvent(EventTestData.CreateEvent(
            title: "DevOps Basics",
            description: "Основы CI/CD",
            startAt: new DateTime(2026, 5, 5, 12, 0, 0),
            endAt: new DateTime(2026, 5, 5, 15, 0, 0)));

        return service;
    }
}
