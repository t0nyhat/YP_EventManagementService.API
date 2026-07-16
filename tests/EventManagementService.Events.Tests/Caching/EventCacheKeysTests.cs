using EventManagementService.Events.Application.Caching;
using FluentAssertions;

namespace EventManagementService.Events.Tests.Caching;

public class EventCacheKeysTests
{
    [Fact]
    public void ForEvent_WhenGuidIsKnown_ReturnsKeyWithDFormatGuid()
    {
        var id = Guid.Parse("0F8FAD5B-D9CB-469F-A165-70867728950E");

        var key = EventCacheKeys.ForEvent(id);

        key.Should().Be("event:0f8fad5b-d9cb-469f-a165-70867728950e");
    }

    [Fact]
    public void ForEvent_WhenCalledTwiceWithSameGuid_ReturnsSameKey()
    {
        var id = Guid.NewGuid();

        var first = EventCacheKeys.ForEvent(id);
        var second = EventCacheKeys.ForEvent(id);

        second.Should().Be(first);
    }

    [Fact]
    public void Top10_ReturnsExpectedKey()
    {
        EventCacheKeys.Top10.Should().Be("events:top10");
    }
}
