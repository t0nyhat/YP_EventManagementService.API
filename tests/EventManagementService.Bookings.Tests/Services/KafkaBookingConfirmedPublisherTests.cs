using Confluent.Kafka;
using EventManagementService.Bookings.Infrastructure.Messaging;
using EventManagementService.Contracts;
using FluentAssertions;
using Moq;

namespace EventManagementService.Bookings.Tests.Services;

public class KafkaBookingConfirmedPublisherTests
{
    [Fact]
    public async Task PublishAsync_UsesBookingConfirmedTopicAndEventIdAsMessageKey()
    {
        var producer = new Mock<IProducer<string, string>>();
        producer
            .Setup(item => item.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                Status = PersistenceStatus.Persisted
            });

        var publisher = new KafkaBookingConfirmedPublisher(producer.Object);
        var eventId = Guid.NewGuid();
        const string payload = "{\"bookingId\":\"test\"}";

        await publisher.PublishAsync(eventId, payload, TestContext.Current.CancellationToken);

        producer.Verify(item => item.ProduceAsync(
            KafkaTopics.BookingConfirmed,
            It.Is<Message<string, string>>(message =>
                message.Key == eventId.ToString("D") &&
                message.Value == payload),
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenPayloadIsEmpty_ThrowsArgumentException()
    {
        var producer = new Mock<IProducer<string, string>>();
        var publisher = new KafkaBookingConfirmedPublisher(producer.Object);

        var action = async () => await publisher.PublishAsync(
            Guid.NewGuid(),
            "",
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>();
    }
}
