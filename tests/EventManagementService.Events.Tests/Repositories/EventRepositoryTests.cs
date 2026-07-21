using EventManagementService.Events.Domain.Models;
using EventManagementService.Events.Infrastructure.Repositories;
using EventManagementService.Events.Tests.Infrastructure;
using FluentAssertions;

namespace EventManagementService.Events.Tests.Repositories;

/// <summary>
/// Requires a running Docker daemon (Testcontainers spins up a real PostgreSQL instance).
/// Excluded from Docker-less runs via `dotnet test --filter "Category!=RequiresDocker"`.
/// Real PostgreSQL proves that the top-events ranking is translated to SQL
/// with fractional (non-integer) division.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "RequiresDocker")]
public class EventRepositoryTests
{
    private static readonly DateTime BaseStartAt = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly PostgreSqlTestcontainerFixture _fixture;

    public EventRepositoryTests(PostgreSqlTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetTopEventsAsync_WhenDenominatorsDiffer_OrdersByFractionalSoldRatio()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        // Продано 50%, но в абсолютных числах всего 5 мест.
        var halfSold = CreateEventWithSales("Half sold", totalSeats: 10, soldSeats: 5);
        // Продано 40%, но целых 40 мест. При целочисленном делении оба коэффициента
        // схлопнулись бы в 0, и tie-breaker по проданным местам ошибочно поставил бы это событие первым.
        var fortyPercentSold = CreateEventWithSales("Forty percent sold", totalSeats: 100, soldSeats: 40);
        await SeedAsync(cancellationToken, fortyPercentSold, halfSold);

        var result = await GetTopEventsAsync(10, cancellationToken);

        result.Select(eventItem => eventItem.Id).Should().Equal(halfSold.Id, fortyPercentSold.Id);
        result.First().Title.Should().Be("Half sold");
        result.First().AvailableSeats.Should().Be(5);
        result.First().TotalSeats.Should().Be(10);
    }

    [Fact]
    public async Task GetTopEventsAsync_WhenRatiosAreEqual_OrdersBySoldSeatsDescendingAndIsDeterministic()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var manySold = CreateEventWithSales("Many sold", totalSeats: 100, soldSeats: 50);
        var fewSold = CreateEventWithSales("Few sold", totalSeats: 10, soldSeats: 5);
        await SeedAsync(cancellationToken, fewSold, manySold);

        var firstRun = await GetTopEventsAsync(10, cancellationToken);
        var secondRun = await GetTopEventsAsync(10, cancellationToken);

        // Оба события проданы на 50%; ничью выигрывает то, у которого продано больше мест.
        var expectedOrder = new[] { manySold.Id, fewSold.Id };
        firstRun.Select(eventItem => eventItem.Id).Should().Equal(expectedOrder);
        secondRun.Select(eventItem => eventItem.Id).Should().Equal(expectedOrder);
    }

    [Fact]
    public async Task GetTopEventsAsync_WhenRatioAndSoldSeatsAreEqual_OrdersByStartAtAscending()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var later = CreateEventWithSales("Later", totalSeats: 40, soldSeats: 20, startAt: BaseStartAt.AddDays(5));
        var earlier = CreateEventWithSales("Earlier", totalSeats: 40, soldSeats: 20, startAt: BaseStartAt);
        await SeedAsync(cancellationToken, later, earlier);

        var result = await GetTopEventsAsync(10, cancellationToken);

        result.Select(eventItem => eventItem.Id).Should().Equal(earlier.Id, later.Id);
    }

    [Fact]
    public async Task GetTopEventsAsync_WhenEventsAreFullyTied_OrdersByIdAscendingAndIsDeterministic()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var first = CreateEventWithSales("Twin A", totalSeats: 40, soldSeats: 20);
        var second = CreateEventWithSales("Twin B", totalSeats: 40, soldSeats: 20);
        await SeedAsync(cancellationToken, first, second);

        // Одинаковые коэффициент, число проданных мест и StartAt: ничью разбивает только Id.
        // Порядок сортировки .NET Guid совпадает с порядком PostgreSQL uuid (каноническая
        // последовательность байтов); для SQL Server это НЕ так — у него свой порядок байтов.
        var expectedOrder = new[] { first.Id, second.Id }.OrderBy(id => id).ToArray();

        var firstRun = await GetTopEventsAsync(10, cancellationToken);
        var secondRun = await GetTopEventsAsync(10, cancellationToken);

        firstRun.Select(eventItem => eventItem.Id).Should().Equal(expectedOrder);
        secondRun.Select(eventItem => eventItem.Id).Should().Equal(expectedOrder);
    }

    [Fact]
    public async Task GetTopEventsAsync_WhenMoreEventsThanCount_ReturnsExactlyTopCount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        // Процент проданных мест растёт с индексом: 5%, 10%, ..., 60%.
        var events = new List<Event>();
        for (var index = 1; index <= 12; index++)
        {
            events.Add(CreateEventWithSales(
                $"Event {index}",
                totalSeats: 100,
                soldSeats: index * 5,
                startAt: BaseStartAt.AddDays(index)));
        }

        await SeedAsync(cancellationToken, events.ToArray());

        var result = await GetTopEventsAsync(10, cancellationToken);

        result.Should().HaveCount(10);
        // Убывание коэффициента — это обратный порядок сидинга; два наименее проданных события отсекаются.
        var expectedIds = events.AsEnumerable().Reverse().Take(10).Select(eventItem => eventItem.Id);
        result.Select(eventItem => eventItem.Id).Should().Equal(expectedIds);
    }

    [Fact]
    public async Task GetTopEventsAsync_WhenEventHasNoSales_IncludesItWithZeroRatio()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var withSale = CreateEventWithSales("One ticket sold", totalSeats: 10, soldSeats: 1);
        var noSales = CreateEventWithSales("No sales", totalSeats: 10, soldSeats: 0);
        await SeedAsync(cancellationToken, noSales, withSale);

        var result = await GetTopEventsAsync(10, cancellationToken);

        result.Select(eventItem => eventItem.Id).Should().Equal(withSale.Id, noSales.Id);
        result.Last().AvailableSeats.Should().Be(result.Last().TotalSeats);
    }

    [Fact]
    public async Task GetTopEventsAsync_WhenDatabaseIsEmpty_ReturnsEmptyCollection()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var result = await GetTopEventsAsync(10, cancellationToken);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetTopEventsAsync_WhenCountIsNotPositive_ThrowsArgumentOutOfRangeException(int count)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var context = _fixture.CreateDbContext();
        var repository = new EventRepository(context);

        var act = async () => await repository.GetTopEventsAsync(count, cancellationToken);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("count");
    }

    [Fact]
    public async Task GetTopEventsAsync_WhenCountDiffersFromEventNumber_ReturnsCorrectSizes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var top = CreateEventWithSales("Top", totalSeats: 10, soldSeats: 9);
        var middle = CreateEventWithSales("Middle", totalSeats: 10, soldSeats: 5);
        var bottom = CreateEventWithSales("Bottom", totalSeats: 10, soldSeats: 1);
        await SeedAsync(cancellationToken, middle, bottom, top);

        var limited = await GetTopEventsAsync(2, cancellationToken);
        var unlimited = await GetTopEventsAsync(10, cancellationToken);

        limited.Select(eventItem => eventItem.Id).Should().Equal(top.Id, middle.Id);
        unlimited.Select(eventItem => eventItem.Id).Should().Equal(top.Id, middle.Id, bottom.Id);
    }

    /// <summary>
    /// Creates an event through the public domain API and models sales by decreasing
    /// available seats, exactly like production code does.
    /// </summary>
    private static Event CreateEventWithSales(string title, int totalSeats, int soldSeats, DateTime? startAt = null)
    {
        var start = startAt ?? BaseStartAt;
        var eventItem = Event.Create(title, start, start.AddHours(2), totalSeats);

        if (soldSeats > 0 && !eventItem.TryDecreaseAvailableSeats(soldSeats))
        {
            throw new InvalidOperationException(
                $"Cannot sell {soldSeats} of {totalSeats} seats for test event '{title}'.");
        }

        return eventItem;
    }

    private async Task SeedAsync(CancellationToken cancellationToken, params Event[] events)
    {
        await using var seedContext = _fixture.CreateDbContext();
        var seedRepository = new EventRepository(seedContext);

        foreach (var eventItem in events)
        {
            await seedRepository.AddAsync(eventItem, cancellationToken);
        }

        await seedRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<Event>> GetTopEventsAsync(int count, CancellationToken cancellationToken)
    {
        await using var readContext = _fixture.CreateDbContext();
        var repository = new EventRepository(readContext);

        return await repository.GetTopEventsAsync(count, cancellationToken);
    }
}
