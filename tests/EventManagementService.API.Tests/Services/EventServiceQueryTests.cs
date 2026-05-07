using EventManagementService.API.Dtos;
using EventManagementService.API.Services;
using EventManagementService.API.Tests.Infrastructure;
using FluentAssertions;

namespace EventManagementService.API.Tests.Services;

public class EventServiceQueryTests
{
    [Fact]
    public async Task GetEvents_WhenFilteredByTitle_ReturnsMatchingEventsCaseInsensitive()
    {
        // Arrange
        var service = await CreateServiceWithSampleEvents();

        // Act
        var result = await service.GetEventsAsync(new GetEventsQuery
        {
            Title = "dotnet"
        });

        // Assert
        var items = result.Items.ToArray();
        result.TotalCount.Should().Be(2);
        result.Count.Should().Be(2);
        items.Should().HaveCount(2);
        items.Should().OnlyContain(item => item.Title.Contains("dotnet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetEvents_WhenTitleContainsLeadingAndTrailingWhitespace_TrimsFilterBeforeSearch()
    {
        // Arrange
        var service = await CreateServiceWithSampleEvents();

        // Act
        var result = await service.GetEventsAsync(new GetEventsQuery
        {
            Title = "  dotnet  "
        });

        // Assert
        var items = result.Items.ToArray();
        result.TotalCount.Should().Be(2);
        result.Count.Should().Be(2);
        items.Should().HaveCount(2);
        items.Should().OnlyContain(item => item.Title.Contains("dotnet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetEvents_WhenTitleContainsOnlyWhitespace_IgnoresTitleFilter()
    {
        // Arrange
        var service = await CreateServiceWithSampleEvents();

        // Act
        var result = await service.GetEventsAsync(new GetEventsQuery
        {
            Title = "   "
        });

        // Assert
        var items = result.Items.ToArray();
        result.TotalCount.Should().Be(5);
        result.Count.Should().Be(5);
        items.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetEvents_WhenFilteredByDates_ReturnsOnlyEventsInRequestedRange()
    {
        // Arrange
        var service = await CreateServiceWithSampleEvents();

        // Act
        var result = await service.GetEventsAsync(new GetEventsQuery
        {
            From = new DateTime(2026, 5, 2, 0, 0, 0),
            To = new DateTime(2026, 5, 4, 23, 59, 59)
        });

        // Assert
        var items = result.Items.ToArray();
        result.TotalCount.Should().Be(3);
        result.Count.Should().Be(3);
        items.Should().HaveCount(3);
        items.Select(item => item.Title)
            .Should()
            .ContainInOrder("DotNet Advanced", "Архитектурный воркшоп", "DotNet Meetup");
        items.Should().OnlyContain(item =>
            item.StartAt >= new DateTime(2026, 5, 2, 0, 0, 0)
            && item.EndAt <= new DateTime(2026, 5, 4, 23, 59, 59));
    }

    [Fact]
    public async Task GetEvents_WhenDateRangeMatchesEventBoundaries_IncludesEventOnBoundary()
    {
        // Arrange
        var service = await CreateServiceWithSampleEvents();

        // Act
        var result = await service.GetEventsAsync(new GetEventsQuery
        {
            From = new DateTime(2026, 5, 2, 10, 0, 0),
            To = new DateTime(2026, 5, 2, 13, 0, 0)
        });

        // Assert
        var item = result.Items.Should().ContainSingle().Subject;
        item.Title.Should().Be("DotNet Advanced");
        item.StartAt.Should().Be(new DateTime(2026, 5, 2, 10, 0, 0));
        item.EndAt.Should().Be(new DateTime(2026, 5, 2, 13, 0, 0));
        result.TotalCount.Should().Be(1);
        result.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetEvents_WhenUsingCombinedFilters_AppliesLogicalAnd()
    {
        // Arrange
        var service = await CreateServiceWithSampleEvents();

        // Act
        var result = await service.GetEventsAsync(new GetEventsQuery
        {
            Title = "dotnet",
            From = new DateTime(2026, 5, 4, 0, 0, 0),
            To = new DateTime(2026, 5, 4, 23, 59, 59)
        });

        // Assert
        var item = result.Items.Should().ContainSingle().Subject;
        item.Title.Should().Be("DotNet Meetup");
        result.TotalCount.Should().Be(1);
        result.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetEvents_WhenPaginationIsRequested_ReturnsRequestedPageInStartAtOrder()
    {
        // Arrange
        var service = await CreateServiceWithSampleEvents();

        // Act
        var result = await service.GetEventsAsync(new GetEventsQuery
        {
            Page = 2,
            PageSize = 2
        });

        // Assert
        var items = result.Items.ToArray();
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(2);
        result.Count.Should().Be(2);
        items.Should().HaveCount(2);
        items.Select(item => item.Title).Should().ContainInOrder("Архитектурный воркшоп", "DotNet Meetup");
    }

    [Fact]
    public async Task GetEvents_WhenNoEventsMatch_ReturnsEmptyPageWithZeroCount()
    {
        // Arrange
        var service = await CreateServiceWithSampleEvents();

        // Act
        var result = await service.GetEventsAsync(new GetEventsQuery
        {
            Title = "python"
        });

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Count.Should().Be(0);
    }

    private static async Task<EventService> CreateServiceWithSampleEvents()
    {
        var service = new EventService(TestDbContextFactory.CreateContext());

        await service.CreateEventAsync(EventTestData.CreateEvent(
            title: "Введение в C#",
            description: "Базовая лекция",
            startAt: new DateTime(2026, 5, 1, 9, 0, 0),
            endAt: new DateTime(2026, 5, 1, 11, 0, 0)));
        await service.CreateEventAsync(EventTestData.CreateEvent(
            title: "DotNet Advanced",
            description: "Продвинутый курс",
            startAt: new DateTime(2026, 5, 2, 10, 0, 0),
            endAt: new DateTime(2026, 5, 2, 13, 0, 0)));
        await service.CreateEventAsync(EventTestData.CreateEvent(
            title: "Архитектурный воркшоп",
            description: "Практика по архитектуре",
            startAt: new DateTime(2026, 5, 3, 14, 0, 0),
            endAt: new DateTime(2026, 5, 3, 17, 0, 0)));
        await service.CreateEventAsync(EventTestData.CreateEvent(
            title: "DotNet Meetup",
            description: "Встреча сообщества",
            startAt: new DateTime(2026, 5, 4, 18, 0, 0),
            endAt: new DateTime(2026, 5, 4, 20, 0, 0)));
        await service.CreateEventAsync(EventTestData.CreateEvent(
            title: "DevOps Basics",
            description: "Основы CI/CD",
            startAt: new DateTime(2026, 5, 5, 12, 0, 0),
            endAt: new DateTime(2026, 5, 5, 15, 0, 0)));

        return service;
    }
}
