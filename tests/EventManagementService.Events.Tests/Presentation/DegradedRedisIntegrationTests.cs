using System.Net;
using System.Net.Http.Json;
using EventManagementService.Events.Application.Dtos;
using EventManagementService.Events.Domain.Models;
using EventManagementService.Events.Infrastructure.Repositories;
using EventManagementService.Events.Tests.Infrastructure;
using FluentAssertions;

namespace EventManagementService.Events.Tests.Presentation;

/// <summary>
/// Proves the degraded-Redis guarantee on the real production wiring:
/// the full DI graph (EventService with Cache-Aside, RedisCacheService, EventRepository,
/// singleton multiplexer with AbortOnConnectFail=false) serves reads from PostgreSQL
/// when Redis is unreachable — every cache call fails fast and degrades to a miss/no-op.
/// Requires a running Docker daemon (Testcontainers spins up a real PostgreSQL instance).
/// Excluded from Docker-less runs via `dotnet test --filter "Category!=RequiresDocker"`.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "RequiresDocker")]
public sealed class DegradedRedisIntegrationTests : IAsyncDisposable
{
    private static readonly DateTime BaseStartAt = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly PostgreSqlTestcontainerFixture _fixture;
    private readonly DegradedRedisWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DegradedRedisIntegrationTests(PostgreSqlTestcontainerFixture fixture)
    {
        _fixture = fixture;
        _factory = new DegradedRedisWebApplicationFactory(fixture.ConnectionString);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetTopEvents_WhenRedisIsDown_Returns200WithDataFromPostgres()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var hot = CreateEventWithSales("Hot", totalSeats: 10, soldSeats: 9);
        var warm = CreateEventWithSales("Warm", totalSeats: 10, soldSeats: 5);
        var cold = CreateEventWithSales("Cold", totalSeats: 10, soldSeats: 1);
        await SeedAsync(cancellationToken, cold, hot, warm);

        var response = await _client.GetAsync("/events/top", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var topEvents = await response.Content.ReadFromJsonAsync<List<EventResponse>>(cancellationToken);
        topEvents.Should().NotBeNull();
        // Чтение кэша упало (промах), поэтому ранжирование приходит напрямую из PostgreSQL:
        // по убыванию доли проданных мест.
        topEvents!.Select(eventItem => eventItem.Id).Should().Equal(hot.Id, warm.Id, cold.Id);
        topEvents.Select(eventItem => eventItem.Title).Should().Equal("Hot", "Warm", "Cold");
    }

    [Fact]
    public async Task GetTopEvents_WhenRedisIsDown_SecondCallStillReturns200()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var soldOut = CreateEventWithSales("Sold out", totalSeats: 10, soldSeats: 10);
        var halfSold = CreateEventWithSales("Half sold", totalSeats: 10, soldSeats: 5);
        await SeedAsync(cancellationToken, halfSold, soldOut);

        // Неудачная запись в кэш после первого промаха не должна ломать следующий запрос:
        // каждый вызов — промах, и каждый вызов всё равно должен обслуживаться из PostgreSQL.
        var firstResponse = await _client.GetAsync("/events/top", cancellationToken);
        var secondResponse = await _client.GetAsync("/events/top", cancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var topEvents = await secondResponse.Content.ReadFromJsonAsync<List<EventResponse>>(cancellationToken);
        topEvents.Should().NotBeNull();
        topEvents!.Select(eventItem => eventItem.Id).Should().Equal(soldOut.Id, halfSold.Id);
    }

    [Fact]
    public async Task GetEventById_WhenRedisIsDown_ReturnsEventFromPostgres()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var eventItem = CreateEventWithSales("Concert", totalSeats: 100, soldSeats: 30);
        await SeedAsync(cancellationToken, eventItem);

        var response = await _client.GetAsync($"/events/{eventItem.Id:D}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<EventResponse>(cancellationToken);
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(eventItem.Id);
        payload.Title.Should().Be("Concert");
        payload.StartAt.Should().Be(BaseStartAt);
        payload.EndAt.Should().Be(BaseStartAt.AddHours(2));
        payload.TotalSeats.Should().Be(100);
        payload.AvailableSeats.Should().Be(70);
    }

    [Fact]
    public async Task GetEventById_WhenRedisIsDown_MissingEventReturns404()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var response = await _client.GetAsync($"/events/{Guid.NewGuid():D}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Creates an event through the public domain API and models sales by decreasing
    /// available seats, exactly like production code does.
    /// </summary>
    private static Event CreateEventWithSales(string title, int totalSeats, int soldSeats)
    {
        var eventItem = Event.Create(title, BaseStartAt, BaseStartAt.AddHours(2), totalSeats);

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
}
