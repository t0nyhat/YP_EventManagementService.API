using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventManagementService.Events.Application.Dtos;
using EventManagementService.Events.Tests.Infrastructure;
using FluentAssertions;

namespace EventManagementService.Events.Tests.Presentation;

public class EventsControllerAuthIntegrationTests : IClassFixture<EventsWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly EventsWebApplicationFactory _factory;

    public EventsControllerAuthIntegrationTests(EventsWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostEvents_WithoutToken_Returns401()
    {
        var payload = new CreateEventRequest
        {
            Title = "Test Event",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 100
        };

        var response = await _client.PostAsync("/events", JsonContent(payload), TestContext.Current.CancellationToken);

        await AssertStatusCodeAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostEvents_WithUserToken_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.UserToken);

        var payload = new CreateEventRequest
        {
            Title = "Test Event",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 100
        };

        var response = await _client.PostAsync("/events", JsonContent(payload), TestContext.Current.CancellationToken);

        await AssertStatusCodeAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostEvents_WithAdminToken_Returns201()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.AdminToken);

        var payload = new CreateEventRequest
        {
            Title = "Test Event",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 100
        };

        var response = await _client.PostAsync("/events", JsonContent(payload), TestContext.Current.CancellationToken);

        await AssertStatusCodeAsync(response, HttpStatusCode.Created);
    }

    [Fact]
    public async Task PutEvents_WithoutToken_Returns401()
    {
        var payload = new UpdateEventRequest
        {
            Title = "Updated",
            StartAt = DateTime.UtcNow.AddDays(3),
            EndAt = DateTime.UtcNow.AddDays(4)
        };

        var response = await _client.PutAsync(
            $"/events/{Guid.NewGuid()}",
            JsonContent(payload),
            TestContext.Current.CancellationToken);

        await AssertStatusCodeAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutEvents_WithUserToken_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.UserToken);

        var payload = new UpdateEventRequest
        {
            Title = "Updated",
            StartAt = DateTime.UtcNow.AddDays(3),
            EndAt = DateTime.UtcNow.AddDays(4)
        };

        var response = await _client.PutAsync(
            $"/events/{Guid.NewGuid()}",
            JsonContent(payload),
            TestContext.Current.CancellationToken);

        await AssertStatusCodeAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteEvents_WithoutToken_Returns401()
    {
        var response = await _client.DeleteAsync(
            $"/events/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        await AssertStatusCodeAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteEvents_WithUserToken_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.UserToken);

        var response = await _client.DeleteAsync(
            $"/events/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        await AssertStatusCodeAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetEvents_WithoutToken_Returns200()
    {
        var response = await _client.GetAsync("/events", TestContext.Current.CancellationToken);

        await AssertStatusCodeAsync(response, HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetEventById_WithoutToken_Returns404()
    {
        var response = await _client.GetAsync(
            $"/events/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        await AssertStatusCodeAsync(response, HttpStatusCode.NotFound);
    }

    private static StringContent JsonContent<T>(T payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }

    private static async Task AssertStatusCodeAsync(HttpResponseMessage response, HttpStatusCode expectedStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(expectedStatusCode, "response body was: {0}", body);
    }
}
