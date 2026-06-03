using EventManagementService.API.Dtos;
using EventManagementService.API.IntegrationTests.Infrastructure;
using EventManagementService.API.Models;
using EventManagementService.API.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EventManagementService.API.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public class EventRepositoryTests
{
    private readonly PostgreSqlTestcontainerFixture _fixture;

    public EventRepositoryTests(PostgreSqlTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddAsync_WhenEventIsValid_PersistsEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var newEvent = Event.Create(
            title: "DotNet Conf",
            startAt: Utc(2026, 6, 1, 10, 0, 0),
            endAt: Utc(2026, 6, 1, 12, 0, 0),
            totalSeats: 100,
            description: "Большая конференция");

        await using (var actContext = _fixture.CreateDbContext())
        {
            var repository = new EventRepository(actContext);
            await repository.AddAsync(newEvent, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }

        await using var verifyContext = _fixture.CreateDbContext();
        var persistedEvent = await verifyContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == newEvent.Id, cancellationToken);

        persistedEvent.Should().NotBeNull();
        persistedEvent!.Title.Should().Be("DotNet Conf");
        persistedEvent.Description.Should().Be("Большая конференция");
        persistedEvent.TotalSeats.Should().Be(100);
        persistedEvent.AvailableSeats.Should().Be(100);
    }

    [Fact]
    public async Task GetByIdAsync_WhenEventExists_ReturnsEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var seededEvent = Event.Create(
            title: "Архитектурный воркшоп",
            startAt: Utc(2026, 6, 2, 11, 0, 0),
            endAt: Utc(2026, 6, 2, 14, 0, 0),
            totalSeats: 30,
            description: "DDD и слои");

        await using (var seedContext = _fixture.CreateDbContext())
        {
            seedContext.Events.Add(seededEvent);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        await using var actContext = _fixture.CreateDbContext();
        var repository = new EventRepository(actContext);

        var found = await repository.GetByIdAsync(seededEvent.Id, cancellationToken);

        found.Should().NotBeNull();
        found!.Title.Should().Be("Архитектурный воркшоп");
        found.Description.Should().Be("DDD и слои");
    }

    [Fact]
    public async Task GetByIdAsync_WhenEventDoesNotExist_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        await using var context = _fixture.CreateDbContext();
        var repository = new EventRepository(context);

        var found = await repository.GetByIdAsync(Guid.NewGuid(), cancellationToken);

        found.Should().BeNull();
    }

    [Fact]
    public async Task Remove_WhenEventExists_DeletesEventAfterSaveChanges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var seededEvent = Event.Create(
            title: "Удаляемое событие",
            startAt: Utc(2026, 6, 3, 10, 0, 0),
            endAt: Utc(2026, 6, 3, 11, 0, 0),
            totalSeats: 10);

        await using (var seedContext = _fixture.CreateDbContext())
        {
            seedContext.Events.Add(seededEvent);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        await using (var actContext = _fixture.CreateDbContext())
        {
            var repository = new EventRepository(actContext);
            var forDelete = await repository.GetByIdAsync(seededEvent.Id, cancellationToken);
            forDelete.Should().NotBeNull();

            repository.Remove(forDelete!);
            await repository.SaveChangesAsync(cancellationToken);
        }

        await using var verifyContext = _fixture.CreateDbContext();
        var exists = await verifyContext.Events
            .AsNoTracking()
            .AnyAsync(item => item.Id == seededEvent.Id, cancellationToken);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenTrackedEventUpdated_PersistsUpdatedValues()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var seededEvent = Event.Create(
            title: "Исходное событие",
            startAt: Utc(2026, 6, 4, 10, 0, 0),
            endAt: Utc(2026, 6, 4, 12, 0, 0),
            totalSeats: 25,
            description: "Первичное описание");

        await using (var seedContext = _fixture.CreateDbContext())
        {
            seedContext.Events.Add(seededEvent);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        await using (var actContext = _fixture.CreateDbContext())
        {
            var repository = new EventRepository(actContext);
            var tracked = await repository.GetByIdAsync(seededEvent.Id, cancellationToken);
            tracked.Should().NotBeNull();

            tracked!.Update(
                title: "Обновлённое событие",
                startAt: Utc(2026, 6, 4, 13, 0, 0),
                endAt: Utc(2026, 6, 4, 15, 0, 0),
                description: "Актуальное описание");

            await repository.SaveChangesAsync(cancellationToken);
        }

        await using var verifyContext = _fixture.CreateDbContext();
        var persistedEvent = await verifyContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == seededEvent.Id, cancellationToken);

        persistedEvent.Should().NotBeNull();
        persistedEvent!.Title.Should().Be("Обновлённое событие");
        persistedEvent.Description.Should().Be("Актуальное описание");
        persistedEvent.StartAt.Should().Be(Utc(2026, 6, 4, 13, 0, 0));
        persistedEvent.EndAt.Should().Be(Utc(2026, 6, 4, 15, 0, 0));
        persistedEvent.TotalSeats.Should().Be(25);
        persistedEvent.AvailableSeats.Should().Be(25);
    }

    [Fact]
    public async Task GetEventsAsync_WhenFilteredByTitleWithWhitespace_ReturnsCaseInsensitiveMatches()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);
        await SeedSampleEventsAsync(cancellationToken);

        await using var context = _fixture.CreateDbContext();
        var repository = new EventRepository(context);

        var result = await repository.GetEventsAsync(new GetEventsQuery
        {
            Title = "  dotnet  "
        }, cancellationToken);

        var items = result.Items.ToArray();
        result.TotalCount.Should().Be(2);
        result.Count.Should().Be(2);
        items.Should().HaveCount(2);
        items.Should().OnlyContain(item => item.Title.Contains("dotnet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetEventsAsync_WhenDateRangeMatchesBoundaries_IncludesBoundaryEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);
        await SeedSampleEventsAsync(cancellationToken);

        await using var context = _fixture.CreateDbContext();
        var repository = new EventRepository(context);

        var result = await repository.GetEventsAsync(new GetEventsQuery
        {
            From = Utc(2026, 5, 2, 10, 0, 0),
            To = Utc(2026, 5, 2, 13, 0, 0)
        }, cancellationToken);

        var item = result.Items.Should().ContainSingle().Subject;
        item.Title.Should().Be("DotNet Advanced");
        item.StartAt.Should().Be(Utc(2026, 5, 2, 10, 0, 0));
        item.EndAt.Should().Be(Utc(2026, 5, 2, 13, 0, 0));
        result.TotalCount.Should().Be(1);
        result.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetEventsAsync_WhenCombinedFiltersApplied_ReturnsOnlyMatchingEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);
        await SeedSampleEventsAsync(cancellationToken);

        await using var context = _fixture.CreateDbContext();
        var repository = new EventRepository(context);

        var result = await repository.GetEventsAsync(new GetEventsQuery
        {
            Title = "dotnet",
            From = Utc(2026, 5, 4, 0, 0, 0),
            To = Utc(2026, 5, 4, 23, 59, 59)
        }, cancellationToken);

        var item = result.Items.Should().ContainSingle().Subject;
        item.Title.Should().Be("DotNet Meetup");
        result.TotalCount.Should().Be(1);
        result.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetEventsAsync_WhenPaginationRequested_ReturnsSecondPageInStartAtOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);
        await SeedSampleEventsAsync(cancellationToken);

        await using var context = _fixture.CreateDbContext();
        var repository = new EventRepository(context);

        var result = await repository.GetEventsAsync(new GetEventsQuery
        {
            Page = 2,
            PageSize = 2
        }, cancellationToken);

        var titles = result.Items.Select(item => item.Title).ToArray();
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(2);
        result.Count.Should().Be(2);
        titles.Should().ContainInOrder("Архитектурный воркшоп", "DotNet Meetup");
    }

    [Fact]
    public async Task GetEventsAsync_WhenNoEventsMatch_ReturnsEmptyPage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);
        await SeedSampleEventsAsync(cancellationToken);

        await using var context = _fixture.CreateDbContext();
        var repository = new EventRepository(context);

        var result = await repository.GetEventsAsync(new GetEventsQuery
        {
            Title = "python"
        }, cancellationToken);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Count.Should().Be(0);
        result.Page.Should().Be(1);
    }

    private async Task SeedSampleEventsAsync(CancellationToken cancellationToken)
    {
        var events = new[]
        {
            Event.Create(
                title: "Введение в C#",
                description: "Базовая лекция",
                startAt: Utc(2026, 5, 1, 9, 0, 0),
                endAt: Utc(2026, 5, 1, 11, 0, 0),
                totalSeats: 100),
            Event.Create(
                title: "DotNet Advanced",
                description: "Продвинутый курс",
                startAt: Utc(2026, 5, 2, 10, 0, 0),
                endAt: Utc(2026, 5, 2, 13, 0, 0),
                totalSeats: 50),
            Event.Create(
                title: "Архитектурный воркшоп",
                description: "Практика по архитектуре",
                startAt: Utc(2026, 5, 3, 14, 0, 0),
                endAt: Utc(2026, 5, 3, 17, 0, 0),
                totalSeats: 30),
            Event.Create(
                title: "DotNet Meetup",
                description: "Встреча сообщества",
                startAt: Utc(2026, 5, 4, 18, 0, 0),
                endAt: Utc(2026, 5, 4, 20, 0, 0),
                totalSeats: 80),
            Event.Create(
                title: "DevOps Basics",
                description: "Основы CI/CD",
                startAt: Utc(2026, 5, 5, 12, 0, 0),
                endAt: Utc(2026, 5, 5, 15, 0, 0),
                totalSeats: 40)
        };

        await using var seedContext = _fixture.CreateDbContext();
        seedContext.Events.AddRange(events);
        await seedContext.SaveChangesAsync(cancellationToken);
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute, int second)
    {
        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
    }
}
