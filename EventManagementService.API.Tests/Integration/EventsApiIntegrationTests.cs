using System.Net;
using System.Text.Json;
using EventManagementService.API.Controllers;
using EventManagementService.API.Middleware;
using EventManagementService.API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EventManagementService.API.Tests.Integration;

public class EventsApiIntegrationTests : IClassFixture<ApiTestServerFixture>
{
    private readonly HttpClient _client;

    public EventsApiIntegrationTests(ApiTestServerFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetEvents_WhenPageIsLessThanOne_ReturnsValidationProblemDetails()
    {
        // Act
        using var response = await _client.GetAsync("/api/events?page=0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("Validation error", root.GetProperty("title").GetString());
        Assert.True(root.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.True(errors.EnumerateObject().Any());
    }

    [Fact]
    public async Task GetEvents_WhenFromIsLaterThanTo_ReturnsValidationProblemDetails()
    {
        // Act
        using var response = await _client.GetAsync("/api/events?from=2026-11-05T00:00:00&to=2026-11-04T23:59:59");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("Validation error", root.GetProperty("title").GetString());
        Assert.Equal("Дата начала диапазона не должна быть позже даты окончания.", root.GetProperty("detail").GetString());
        Assert.Equal("/api/events", root.GetProperty("instance").GetString());
        Assert.True(root.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
    }

    [Fact]
    public async Task GetEventById_WhenEventDoesNotExist_ReturnsNotFoundProblemDetails()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        using var response = await _client.GetAsync($"/api/events/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;

        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("Resource not found", root.GetProperty("title").GetString());
        Assert.Equal($"Событие с id {id} не найдено.", root.GetProperty("detail").GetString());
        Assert.Equal($"/api/events/{id}", root.GetProperty("instance").GetString());
        Assert.True(root.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
    }
}

public sealed class ApiTestServerFixture : IAsyncLifetime
{
    private IHost _host = default!;

    public HttpClient Client { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddLogging();
                    services.AddProblemDetails();
                    services.AddControllers()
                        .AddApplicationPart(typeof(EventsController).Assembly);
                    services.Configure<ApiBehaviorOptions>(options =>
                    {
                        options.InvalidModelStateResponseFactory = context =>
                        {
                            var problemDetails = new ValidationProblemDetails(context.ModelState)
                            {
                                Status = StatusCodes.Status400BadRequest,
                                Title = "Validation error",
                                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                                Detail = "One or more validation errors occurred.",
                                Instance = context.HttpContext.Request.Path
                            };

                            problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                            return new BadRequestObjectResult(problemDetails)
                            {
                                ContentTypes = { "application/problem+json" }
                            };
                        };
                    });
                    services.AddSingleton<IEventService, EventService>();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseMiddleware<ExceptionHandlingMiddleware>();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .StartAsync();

        Client = _host.GetTestClient();
        Client.BaseAddress = new Uri("http://localhost");
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }
}
