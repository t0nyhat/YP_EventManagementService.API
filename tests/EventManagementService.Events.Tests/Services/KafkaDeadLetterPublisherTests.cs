using System.Text;
using Confluent.Kafka;
using EventManagementService.Contracts;
using EventManagementService.Events.Infrastructure.Messaging;
using FluentAssertions;
using Moq;

namespace EventManagementService.Events.Tests.Services;

public class KafkaDeadLetterPublisherTests
{
    [Fact]
    public async Task PublishAsync_SendsOriginalPayloadToDeadLetterTopicWithDiagnosticHeaders()
    {
        var producer = new Mock<IProducer<string, string>>();
        Message<string, string>? capturedMessage = null;
        producer
            .Setup(item => item.ProduceAsync(
                KafkaTopics.BookingConfirmedDeadLetter,
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((_, message, _) => capturedMessage = message)
            .ReturnsAsync(new DeliveryResult<string, string> { Status = PersistenceStatus.Persisted });

        var publisher = new KafkaDeadLetterPublisher(producer.Object);
        const string payload = "{\"bookingId\":\"broken\"}";
        var source = new TopicPartitionOffset(KafkaTopics.BookingConfirmed, new Partition(0), new Offset(42));

        await publisher.PublishAsync(
            "event-key",
            payload,
            "Failed to deserialize BookingConfirmed message.",
            source,
            TestContext.Current.CancellationToken);

        producer.Verify(item => item.ProduceAsync(
            KafkaTopics.BookingConfirmedDeadLetter,
            It.IsAny<Message<string, string>>(),
            TestContext.Current.CancellationToken), Times.Once);

        capturedMessage.Should().NotBeNull();
        capturedMessage!.Key.Should().Be("event-key");
        capturedMessage.Value.Should().Be(payload, "the original payload must reach the dead letter topic untouched");

        var headers = capturedMessage.Headers.ToDictionary(h => h.Key, h => Encoding.UTF8.GetString(h.GetValueBytes()));
        headers["error-reason"].Should().Be("Failed to deserialize BookingConfirmed message.");
        headers["error-source-topic"].Should().Be(KafkaTopics.BookingConfirmed);
        headers["error-source-partition"].Should().Be("0");
        headers["error-source-offset"].Should().Be("42");
        headers.Should().ContainKey("error-timestamp");
    }

    [Fact]
    public async Task PublishAsync_WhenPayloadIsEmpty_ThrowsArgumentException()
    {
        var producer = new Mock<IProducer<string, string>>();
        var publisher = new KafkaDeadLetterPublisher(producer.Object);
        var source = new TopicPartitionOffset(KafkaTopics.BookingConfirmed, new Partition(0), new Offset(1));

        var action = async () => await publisher.PublishAsync(
            "key",
            "",
            "reason",
            source,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>();
    }
}
