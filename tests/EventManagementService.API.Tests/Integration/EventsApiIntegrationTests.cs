using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using EventManagementService.Presentation.BackgroundServices;
using EventManagementService.Presentation.Controllers;
using EventManagementService.Application;
using EventManagementService.Application.Dtos;
using EventManagementService.Presentation.Middleware;
using EventManagementService.Domain.Models;
using EventManagementService.Application.Abstractions.Repositories;
using EventManagementService.Infrastructure.DataAccess;
using EventManagementService.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Encodings.Web;

namespace EventManagementService.API.Tests.Integration;

public class EventsApiIntegrationTests : IClassFixture<ApiTestServerFixture>
{
    private static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000101");
    private static readonly Guid UserUserId = Guid.Parse("00000000-0000-0000-0000-000000000102");
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

        using var createEventResponse = await PostAsJsonAsync("/api/events", createEventRequest, AdminUserId, UserRole.Admin, cancellationToken);
        createEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdEvent = await createEventResponse.Content.ReadFromJsonAsync<EventResponse>(cancellationToken);
        createdEvent.Should().NotBeNull();
        createdEvent!.TotalSeats.Should().Be(3);
        createdEvent.AvailableSeats.Should().Be(3);

        // Act
        using var createBookingResponse = await PostAsync($"/api/events/{createdEvent!.Id}/book", UserUserId, UserRole.User, cancellationToken);

        // Assert
        createBookingResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        createBookingResponse.Headers.Location.Should().NotBeNull();

        var createdBooking = await createBookingResponse.Content.ReadFromJsonAsync<BookingResponse>(cancellationToken);
        createdBooking.Should().NotBeNull();
        createdBooking!.EventId.Should().Be(createdEvent.Id);
        createdBooking.Status.Should().Be(BookingStatus.Pending);
        createdBooking.ProcessedAt.Should().BeNull();
        createBookingResponse.Headers.Location!.AbsolutePath.Should().Be($"/api/bookings/{createdBooking.Id}");

        var pendingBooking = await GetFromJsonAsync<BookingResponse>($"/api/bookings/{createdBooking.Id}", UserUserId, UserRole.User, cancellationToken);
        pendingBooking.Should().NotBeNull();
        pendingBooking!.Status.Should().Be(BookingStatus.Pending);
        pendingBooking.ProcessedAt.Should().BeNull();

        var confirmedBooking = await WaitForBookingStatusAsync(createdBooking.Id, BookingStatus.Confirmed, UserUserId, UserRole.User, TimeSpan.FromSeconds(6));
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
        using var response = await PostAsJsonAsync("/api/events", request, AdminUserId, UserRole.Admin, cancellationToken);

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
        using var response = await PostAsJsonAsync("/api/events", request, AdminUserId, UserRole.Admin, cancellationToken);

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

        using var createEventResponse = await PostAsJsonAsync("/api/events", createEventRequest, AdminUserId, UserRole.Admin, cancellationToken);
        createEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdEvent = await createEventResponse.Content.ReadFromJsonAsync<EventResponse>(cancellationToken);
        createdEvent.Should().NotBeNull();

        using var firstBookingResponse = await PostAsync($"/api/events/{createdEvent!.Id}/book", UserUserId, UserRole.User, cancellationToken);
        firstBookingResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Act
        using var secondBookingResponse = await PostAsync($"/api/events/{createdEvent.Id}/book", Guid.NewGuid(), UserRole.User, cancellationToken);

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

    [Fact]
    public async Task CreateBooking_WhenEventAlreadyStarted_ReturnsBadRequestProblemDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var createEventRequest = new CreateEventRequest
        {
            Title = "Already started event",
            Description = "Booking should be rejected",
            StartAt = DateTime.UtcNow.AddHours(-1),
            EndAt = DateTime.UtcNow.AddHours(1),
            TotalSeats = 3
        };

        using var createEventResponse = await PostAsJsonAsync("/api/events", createEventRequest, AdminUserId, UserRole.Admin, cancellationToken);
        createEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdEvent = await createEventResponse.Content.ReadFromJsonAsync<EventResponse>(cancellationToken);
        createdEvent.Should().NotBeNull();

        using var response = await PostAsync($"/api/events/{createdEvent!.Id}/book", UserUserId, UserRole.User, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = payload.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(400);
        root.GetProperty("title").GetString().Should().Be("Validation error");
        root.GetProperty("detail").GetString().Should().Be("Нельзя бронировать событие, которое уже началось.");
        root.GetProperty("instance").GetString().Should().Be($"/api/events/{createdEvent.Id}/book");
    }

    [Fact]
    public async Task CreateEvent_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var request = new CreateEventRequest
        {
            Title = "Unauthorized event",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(1).AddHours(1),
            TotalSeats = 10
        };

        using var response = await _client.PostAsJsonAsync("/api/events", request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateEvent_WhenNonAdminUser_ReturnsForbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var request = new CreateEventRequest
        {
            Title = "Forbidden event",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(1).AddHours(1),
            TotalSeats = 10
        };

        using var response = await PostAsJsonAsync("/api/events", request, UserUserId, UserRole.User, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateBooking_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var eventId = await CreateEventAsync("Unauthorized booking event", 3, cancellationToken);

        using var response = await _client.PostAsync($"/api/events/{eventId}/book", content: null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBookingById_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var eventId = await CreateEventAsync("Unauthorized booking read event", 3, cancellationToken);
        var bookingId = await CreateBookingAsync(eventId, UserUserId, UserRole.User, cancellationToken);

        using var response = await _client.GetAsync($"/api/bookings/{bookingId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBookingById_WhenRequesterIsNotOwnerAndNotAdmin_ReturnsForbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var eventId = await CreateEventAsync("Forbidden booking read event", 3, cancellationToken);
        var ownerId = Guid.NewGuid();
        var bookingId = await CreateBookingAsync(eventId, ownerId, UserRole.User, cancellationToken);

        using var request = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/bookings/{bookingId}", Guid.NewGuid(), UserRole.User);
        using var response = await _client.SendAsync(request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelBooking_WhenRequesterIsNotOwnerAndNotAdmin_ReturnsForbidden()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var eventId = await CreateEventAsync("Forbidden booking cancel event", 3, cancellationToken);
        var ownerId = Guid.NewGuid();
        var bookingId = await CreateBookingAsync(eventId, ownerId, UserRole.User, cancellationToken);

        using var response = await DeleteAsync($"/api/bookings/{bookingId}", Guid.NewGuid(), UserRole.User, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelBooking_WhenRequesterIsAdmin_ReturnsNoContent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var eventId = await CreateEventAsync("Admin booking cancel event", 3, cancellationToken);
        var ownerId = Guid.NewGuid();
        var bookingId = await CreateBookingAsync(eventId, ownerId, UserRole.User, cancellationToken);

        using var response = await DeleteAsync($"/api/bookings/{bookingId}", AdminUserId, UserRole.Admin, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var cancelledBooking = await GetFromJsonAsync<BookingResponse>($"/api/bookings/{bookingId}", AdminUserId, UserRole.Admin, cancellationToken);
        cancelledBooking.Should().NotBeNull();
        cancelledBooking!.Status.Should().Be(BookingStatus.Cancelled);
    }

    private async Task<BookingResponse> WaitForBookingStatusAsync(Guid bookingId, BookingStatus expectedStatus, Guid userId, UserRole role, TimeSpan timeout)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var deadline = DateTime.UtcNow.Add(timeout);
        BookingResponse? latestBooking = null;

        while (DateTime.UtcNow <= deadline)
        {
            latestBooking = await GetFromJsonAsync<BookingResponse>($"/api/bookings/{bookingId}", userId, role, cancellationToken);

            if (latestBooking?.Status == expectedStatus)
            {
                return latestBooking;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        throw new TimeoutException(
            $"Бронирование с id {bookingId} не достигло статуса {expectedStatus} за {timeout.TotalSeconds} секунд.");
    }

    private async Task<T?> GetFromJsonAsync<T>(string url, Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, url, userId, role);
        using var response = await _client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private async Task<HttpResponseMessage> PostAsJsonAsync<T>(
        string url,
        T payload,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, url, userId, role);
        request.Content = JsonContent.Create(payload);
        return await _client.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> PostAsync(string url, Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, url, userId, role);
        return await _client.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> DeleteAsync(string url, Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Delete, url, userId, role);
        return await _client.SendAsync(request, cancellationToken);
    }

    private async Task<Guid> CreateEventAsync(string title, int totalSeats, CancellationToken cancellationToken)
    {
        var request = new CreateEventRequest
        {
            Title = title,
            StartAt = DateTime.UtcNow.AddDays(2),
            EndAt = DateTime.UtcNow.AddDays(2).AddHours(2),
            TotalSeats = totalSeats
        };

        using var response = await PostAsJsonAsync("/api/events", request, AdminUserId, UserRole.Admin, cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdEvent = await response.Content.ReadFromJsonAsync<EventResponse>(cancellationToken);
        createdEvent.Should().NotBeNull();
        return createdEvent!.Id;
    }

    private async Task<Guid> CreateBookingAsync(Guid eventId, Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        using var response = await PostAsync($"/api/events/{eventId}/book", userId, role, cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var createdBooking = await response.Content.ReadFromJsonAsync<BookingResponse>(cancellationToken);
        createdBooking.Should().NotBeNull();
        return createdBooking!.Id;
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string url, Guid userId, UserRole role)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, role.ToString());
        return request;
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
                    services.AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                    services.AddAuthorization();
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
                    services.AddApplicationServices();
                    services.AddHostedService<BookingProcessingBackgroundService>();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseMiddleware<ExceptionHandlingMiddleware>();
                    app.UseAuthentication();
                    app.UseAuthorization();
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

internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string UserIdHeader = "X-Test-UserId";
    public const string RoleHeader = "X-Test-Role";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userIdHeader)
            || !Guid.TryParse(userIdHeader, out var userId))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing or invalid user id header."));
        }

        var role = Request.Headers.TryGetValue(RoleHeader, out var roleHeader)
            ? roleHeader.ToString()
            : UserRole.User.ToString();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"test-{userId:N}"),
            new Claim(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
