using System.Text.Json;
using EventManagementService.Contracts;
using FluentAssertions;

namespace EventManagementService.Bookings.Tests.Contracts;

public class BookingConfirmedSerializationTests
{
    // Тот же экземпляр опций, что используют продюсер и подписчик.
    private static readonly JsonSerializerOptions JsonOptions = KafkaJson.Options;

    [Fact]
    public void BookingConfirmed_WhenSerializedWithWebDefaults_UsesCamelCaseAndRoundTrips()
    {
        var message = new BookingConfirmed(
            BookingId: Guid.NewGuid(),
            EventId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Seats: 1,
            ConfirmedAtUtc: DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(message, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<BookingConfirmed>(json, JsonOptions);

        json.Should().Contain("\"bookingId\"");
        json.Should().Contain("\"eventId\"");
        json.Should().Contain("\"userId\"");
        json.Should().Contain("\"confirmedAtUtc\"");
        deserialized.Should().Be(message);
    }
}
