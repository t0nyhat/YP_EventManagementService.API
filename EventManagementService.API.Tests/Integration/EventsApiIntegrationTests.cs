using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventManagementService.API.BackgroundServices;
using EventManagementService.API.Controllers;
using EventManagementService.API.Dtos;
using EventManagementService.API.Middleware;
using EventManagementService.API.Models;
using EventManagementService.API.Services;
using EventManagementService.API.Stores;
using FluentAssertions;
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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(400);
        root.GetProperty("title").GetString().Should().Be("Validation error");
        root.TryGetProperty("traceId", out var traceId).Should().BeTrue();
        string.IsNullOrWhiteSpace(traceId.GetString()).Should().BeFalse();
        root.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetEvents_WhenFromIsLaterThanTo_ReturnsValidationProblemDetails()
    {
        // Act
        using var response = await _client.GetAsync("/api/events?from=2026-11-05T00:00:00&to=2026-11-04T23:59:59");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(400);
        root.GetProperty("title").GetString().Should().Be("Validation error");
        root.GetProperty("detail").GetString().Should().Be("Дата начала диапазона не должна быть позже даты окончания.");
        root.GetProperty("instance").GetString().Should().Be("/api/events");
        root.TryGetProperty("traceId", out var traceId).Should().BeTrue();
        string.IsNullOrWhiteSpace(traceId.GetString()).Should().BeFalse();
    }

    [Fact]
    public async Task GetEventById_WhenEventDoesNotExist_ReturnsNotFoundProblemDetails()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        using var response = await _client.GetAsync($"/api/events/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(404);
        root.GetProperty("title").GetString().Should().Be("Resource not found");
        root.GetProperty("detail").GetString().Should().Be($"Событие с id {id} не найдено.");
        root.GetProperty("instance").GetString().Should().Be($"/api/events/{id}");
        root.TryGetProperty("traceId", out var traceId).Should().BeTrue();
        string.IsNullOrWhiteSpace(traceId.GetString()).Should().BeFalse();
    }

    [Fact]
    public async Task CreateBooking_WhenEventExists_ReturnsAcceptedAndEventuallyConfirmed()
    {
        // Arrange
        var createEventRequest = new CreateEventRequest
        {
            Title = "Sprint 3 integration event",
            Description = "Booking workflow check",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(1).AddHours(2)
        };

        using var createEventResponse = await _client.PostAsJsonAsync("/api/events", createEventRequest);
        createEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdEvent = await createEventResponse.Content.ReadFromJsonAsync<EventResponse>();
        createdEvent.Should().NotBeNull();

        // Act
        using var createBookingResponse = await _client.PostAsync($"/api/events/{createdEvent!.Id}/book", content: null);

        // Assert
        createBookingResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        createBookingResponse.Headers.Location.Should().NotBeNull();

        var createdBooking = await createBookingResponse.Content.ReadFromJsonAsync<BookingResponse>();
        createdBooking.Should().NotBeNull();
        createdBooking!.EventId.Should().Be(createdEvent.Id);
        createdBooking.Status.Should().Be(BookingStatus.Pending);
        createdBooking.ProcessedAt.Should().BeNull();
        createBookingResponse.Headers.Location!.AbsolutePath.Should().Be($"/api/bookings/{createdBooking.Id}");

        var pendingBooking = await _client.GetFromJsonAsync<BookingResponse>($"/api/bookings/{createdBooking.Id}");
        pendingBooking.Should().NotBeNull();
        pendingBooking!.Status.Should().Be(BookingStatus.Pending);
        pendingBooking.ProcessedAt.Should().BeNull();

        var confirmedBooking = await WaitForBookingStatusAsync(createdBooking.Id, BookingStatus.Confirmed, TimeSpan.FromSeconds(6));
        confirmedBooking.Status.Should().Be(BookingStatus.Confirmed);
        confirmedBooking.ProcessedAt.Should().NotBeNull();
        confirmedBooking.ProcessedAt!.Value.Should().BeAfter(createdBooking.CreatedAt);
    }

    private async Task<BookingResponse> WaitForBookingStatusAsync(Guid bookingId, BookingStatus expectedStatus, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        BookingResponse? latestBooking = null;

        while (DateTime.UtcNow <= deadline)
        {
            latestBooking = await _client.GetFromJsonAsync<BookingResponse>($"/api/bookings/{bookingId}");

            if (latestBooking?.Status == expectedStatus)
            {
                return latestBooking;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(
            $"Бронирование с id {bookingId} не достигло статуса {expectedStatus} за {timeout.TotalSeconds} секунд.");
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
                    services.AddSingleton<IBookingStore, InMemoryBookingStore>();
                    services.AddSingleton<IBookingService, BookingService>();
                    services.AddHostedService<BookingProcessingBackgroundService>();
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
