using EventManagementService.API.Services;

namespace EventManagementService.API.Tests.Services;

public class EventServiceSmokeTests
{
    [Fact]
    public void GetAllEvents_WhenServiceIsNew_ReturnsEmptyCollection()
    {
        // Arrange
        var service = new EventService();

        // Act
        var events = service.GetAllEvents();

        // Assert
        Assert.Empty(events);
    }
}
