using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventManagementService.API.BackgroundServices;
using EventManagementService.API.Controllers;
using EventManagementService.API.DataAccess;
using EventManagementService.Application.Dtos;
using EventManagementService.API.Middleware;
using EventManagementService.Domain.Models;
using EventManagementService.API.Repositories;
using EventManagementService.Application.Abstractions.Repositories;
using EventManagementService.Application.Services;
using Microsoft.EntityFrameworkCore;
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
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        using var response = await _client.GetAsync("/api/events?page=0", cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
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
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        using var response = await _client.GetAsync("/api/events?from=2026-11-05T00:00:00&to=2026-11-04T23:59:59", cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
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
        var cancellationToken = TestContext.Current.CancellationToken;

        // Arrange
        var id = Guid.NewGuid();

        // Act
        using var response = await _client.GetAsync($"/api/events/{id}", cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
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
        var cancellationToken = TestContext.Current.CancellationToken;

        // Arrange
        var createEventRequest = new CreateEventRequest
        {
            Title = "Sprint 3 integration event",
            Description = "Booking workflow check",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(1).AddHours(2),
            TotalSeats = 3
        };

        using var createEventResponse = await _client.PostAsJsonAsync("/api/events", createEventRequest, cancellationToken);
        createEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdEvent = await createEventResponse.Content.ReadFromJsonAsync<EventResponse>(cancellationToken);
        createdEvent.Should().NotBeNull();
        createdEvent!.TotalSeats.Should().Be(3);
        createdEvent.AvailableSeats.Should().Be(3);

        // Act
        using var createBookingResponse = await _client.PostAsync($"/api/events/{createdEvent!.Id}/book", content: null, cancellationToken);

        // Assert
        createBookingResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        createBookingResponse.Headers.Location.Should().NotBeNull();

        var createdBooking = await createBookingResponse.Content.ReadFromJsonAsync<BookingResponse>(cancellationToken);
        createdBooking.Should().NotBeNull();
        createdBooking!.EventId.Should().Be(createdEvent.Id);
        createdBooking.Status.Should().Be(BookingStatus.Pending);
        createdBooking.ProcessedAt.Should().BeNull();
        createBookingResponse.Headers.Location!.AbsolutePath.Should().Be($"/api/bookings/{createdBooking.Id}");

        var pendingBooking = await _client.GetFromJsonAsync<BookingResponse>($"/api/bookings/{createdBooking.Id}", cancellationToken);
        pendingBooking.Should().NotBeNull();
        pendingBooking!.Status.Should().Be(BookingStatus.Pending);
        pendingBooking.ProcessedAt.Should().BeNull();

        var confirmedBooking = await WaitForBookingStatusAsync(createdBooking.Id, BookingStatus.Confirmed, TimeSpan.FromSeconds(6));
        confirmedBooking.Status.Should().Be(BookingStatus.Confirmed);
        confirmedBooking.ProcessedAt.Should().NotBeNull();
        confirmedBooking.ProcessedAt!.Value.Should().BeAfter(createdBooking.CreatedAt);
    }

    [Fact]
    public async Task CreateEvent_WhenRequestIsValid_ReturnsCreatedEventWithSeatFields()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Arrange
        var request = new CreateEventRequest
        {
            Title = "Sprint 4 seats event",
            Description = "Seat contract check",
            StartAt = DateTime.UtcNow.AddDays(2),
            EndAt = DateTime.UtcNow.AddDays(2).AddHours(1),
            TotalSeats = 25
        };

        // Act
        using var response = await _client.PostAsJsonAsync("/api/events", request, cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdEvent = await response.Content.ReadFromJsonAsync<EventResponse>(cancellationToken);
        createdEvent.Should().NotBeNull();
        createdEvent!.TotalSeats.Should().Be(25);
        createdEvent.AvailableSeats.Should().Be(25);
    }

    [Fact]
    public async Task CreateEvent_WhenTotalSeatsIsInvalid_ReturnsValidationProblemDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Arrange
        var request = new CreateEventRequest
        {
            Title = "Invalid seats event",
            Description = "Seat validation check",
            StartAt = DateTime.UtcNow.AddDays(2),
            EndAt = DateTime.UtcNow.AddDays(2).AddHours(1),
            TotalSeats = 0
        };

        // Act
        using var response = await _client.PostAsJsonAsync("/api/events", request, cancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = payload.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(400);
        root.GetProperty("title").GetString().Should().Be("Validation error");
        root.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.TryGetProperty("TotalSeats", out var totalSeatsErrors).Should().BeTrue();
        totalSeatsErrors.EnumerateArray().Select(item => item.GetString())
            .Should().Contain(message => message == "Количество мест должно быть больше нуля.");
    }

    [Fact]
    public async Task CreateBooking_WhenNoSeatsAreAvailable_ReturnsConflictProblemDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Arrange
        var createEventRequest = new CreateEventRequest
        {
            Title = "Sold out event",
            Description = "No seats should remain",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(1).AddHours(2),
            TotalSeats = 1
        };

        using var createEventResponse = await _client.PostAsJsonAsync("/api/events", createEventRequest, cancellationToken);
        createEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdEvent = await createEventResponse.Content.ReadFromJsonAsync<EventResponse>(cancellationToken);
        createdEvent.Should().NotBeNull();

        using var firstBookingResponse = await _client.PostAsync($"/api/events/{createdEvent!.Id}/book", content: null, cancellationToken);
        firstBookingResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Act
        using var secondBookingResponse = await _client.PostAsync($"/api/events/{createdEvent.Id}/book", content: null, cancellationToken);

        // Assert
        secondBookingResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        secondBookingResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var payload = JsonDocument.Parse(await secondBookingResponse.Content.ReadAsStringAsync(cancellationToken));
        var root = payload.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(409);
        root.GetProperty("title").GetString().Should().Be("Conflict");
        root.GetProperty("detail").GetString().Should().Be("Нет свободных мест на данное событие.");
        root.GetProperty("instance").GetString().Should().Be($"/api/events/{createdEvent.Id}/book");
    }

    private async Task<BookingResponse> WaitForBookingStatusAsync(Guid bookingId, BookingStatus expectedStatus, TimeSpan timeout)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var deadline = DateTime.UtcNow.Add(timeout);
        BookingResponse? latestBooking = null;

        while (DateTime.UtcNow <= deadline)
        {
            latestBooking = await _client.GetFromJsonAsync<BookingResponse>($"/api/bookings/{bookingId}", cancellationToken);

            if (latestBooking?.Status == expectedStatus)
            {
                return latestBooking;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        throw new TimeoutException(
            $"Бронирование с id {bookingId} не достигло статуса {expectedStatus} за {timeout.TotalSeconds} секунд.");
    }
}

public sealed class ApiTestServerFixture : IAsyncLifetime
{
    private IHost _host = default!;

    public HttpClient Client { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        var databaseName = Guid.NewGuid().ToString();
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
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));
                    services.AddScoped<IEventRepository, EventRepository>();
                    services.AddScoped<IBookingRepository, BookingRepository>();
                    services.AddScoped<IEventService, EventService>();
                    services.AddScoped<IBookingService, BookingService>();
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

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }
}
