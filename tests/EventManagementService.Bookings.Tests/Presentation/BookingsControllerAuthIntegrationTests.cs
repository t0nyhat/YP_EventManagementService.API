using System.Net;
using System.Net.Http.Headers;
using EventManagementService.Bookings.Tests.Infrastructure;
using FluentAssertions;

namespace EventManagementService.Bookings.Tests.Presentation;

public class BookingsControllerAuthIntegrationTests : IClassFixture<BookingsWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly BookingsWebApplicationFactory _factory;

    public BookingsControllerAuthIntegrationTests(BookingsWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateBooking_WithoutToken_Returns401()
    {
        var response = await _client.PostAsync(
            $"/events/{Guid.NewGuid()}/book",
            content: null,
            TestContext.Current.CancellationToken);

        await AssertStatusCodeAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateBooking_WithUserToken_Returns202AndLocation()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.UserToken);

        var response = await _client.PostAsync(
            $"/events/{Guid.NewGuid()}/book",
            content: null,
            TestContext.Current.CancellationToken);

        await AssertStatusCodeAsync(response, HttpStatusCode.Accepted);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.AbsolutePath.Should().StartWith("/bookings/");
    }

    [Fact]
    public async Task GetBooking_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync(
            $"/bookings/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        await AssertStatusCodeAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteBooking_WithoutToken_Returns401()
    {
        var response = await _client.DeleteAsync(
            $"/bookings/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        await AssertStatusCodeAsync(response, HttpStatusCode.Unauthorized);
    }

    private static async Task AssertStatusCodeAsync(HttpResponseMessage response, HttpStatusCode expectedStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(expectedStatusCode, "response body was: {0}", body);
    }
}
