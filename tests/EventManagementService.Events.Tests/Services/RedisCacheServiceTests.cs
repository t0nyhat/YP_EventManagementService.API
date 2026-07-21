using EventManagementService.Events.Application.Dtos;
using EventManagementService.Events.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace EventManagementService.Events.Tests.Services;

public class RedisCacheServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _multiplexer = new();
    private readonly Mock<IDatabase> _database = new();
    private readonly Mock<ILogger<RedisCacheService>> _logger = new();

    public RedisCacheServiceTests()
    {
        _multiplexer
            .Setup(multiplexer => multiplexer.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(_database.Object);
    }

    private RedisCacheService CreateService() => new(_multiplexer.Object, _logger.Object);

    [Fact]
    public async Task GetAsync_WhenValueContainsValidJson_ReturnsDeserializedValue()
    {
        var id = Guid.NewGuid();
        var json =
            $"{{\"id\":\"{id}\",\"title\":\"Concert\",\"description\":\"Live show\"," +
            "\"startAt\":\"2026-08-01T10:00:00Z\",\"endAt\":\"2026-08-01T12:00:00Z\"," +
            "\"totalSeats\":100,\"availableSeats\":58}";
        _database
            .Setup(db => db.StringGetAsync((RedisKey)$"event:{id:D}", It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)json);

        var result = await CreateService().GetAsync<EventResponse>(
            $"event:{id:D}", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.Title.Should().Be("Concert");
        result.Description.Should().Be("Live show");
        result.TotalSeats.Should().Be(100);
        result.AvailableSeats.Should().Be(58);
    }

    [Fact]
    public async Task GetAsync_WhenValueIsMissing_ReturnsNull()
    {
        _database
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await CreateService().GetAsync<EventResponse>(
            "events:top10", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenRedisThrows_ReturnsNullAndLogsWarning()
    {
        _database
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is down"));

        var result = await CreateService().GetAsync<EventResponse>(
            "events:top10", TestContext.Current.CancellationToken);

        result.Should().BeNull("infrastructure failures must be treated as a cache miss");
        VerifyWarningLogged(Times.Once());
    }

    [Fact]
    public async Task GetAsync_WhenValueContainsMalformedJson_ReturnsNullAndRemovesCorruptedEntry()
    {
        _database
            .Setup(db => db.StringGetAsync((RedisKey)"events:top10", It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"{not-valid-json");
        _database
            .Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await CreateService().GetAsync<EventResponse>(
            "events:top10", TestContext.Current.CancellationToken);

        result.Should().BeNull();
        _database.Verify(
            db => db.KeyDeleteAsync((RedisKey)"events:top10", It.IsAny<CommandFlags>()),
            Times.Once,
            "a corrupted entry must be removed so it cannot poison later reads");
        VerifyWarningLogged(Times.Once());
    }

    [Fact]
    public async Task GetAsync_WhenCorruptedEntryRemovalFails_StillReturnsNull()
    {
        _database
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"{not-valid-json");
        _database
            .Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("delete failed"));

        var result = await CreateService().GetAsync<EventResponse>(
            "events:top10", TestContext.Current.CancellationToken);

        result.Should().BeNull("the cleanup is best-effort and its failure must not surface");
    }

    [Fact]
    public async Task GetAsync_WhenKeyIsEmpty_ThrowsArgumentException()
    {
        var action = async () => await CreateService().GetAsync<EventResponse>(
            "", TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetAsync_WhenTokenAlreadyCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var action = async () => await CreateService().GetAsync<EventResponse>("events:top10", cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>(
            "cancellation must never be converted into a cache miss");
        _database.Verify(
            db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAsync_WhenCancelledWhileRedisCallIsPending_PropagatesCancellation()
    {
        var pendingRedisCall = new TaskCompletionSource<RedisValue>();
        _database
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(pendingRedisCall.Task);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var action = async () => await CreateService().GetAsync<EventResponse>("events:top10", cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>(
            "a caller-requested cancellation must not be swallowed as a miss");
    }

    [Fact]
    public async Task SetAsync_PassesExactKeyAndTtlToRedis()
    {
        var timeToLive = TimeSpan.FromMinutes(7);
        _database
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await CreateService().SetAsync(
            "events:top10", CreateResponse(), timeToLive, TestContext.Current.CancellationToken);

        _database.Verify(
            db => db.StringSetAsync(
                (RedisKey)"events:top10",
                It.Is<RedisValue>(value => value.ToString().Contains("\"title\":\"Concert\"")),
                timeToLive,
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once,
            "the entry must be stored under the exact key, serialized with web (camelCase) naming, with exactly the requested TTL");
    }

    [Fact]
    public async Task SetAsync_WhenRedisThrows_DoesNotThrowAndLogsWarning()
    {
        _database
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisTimeoutException("timeout", CommandStatus.Unknown));

        var action = async () => await CreateService().SetAsync(
            "events:top10", CreateResponse(), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        await action.Should().NotThrowAsync("cache write failures must degrade to a logged no-op");
        VerifyWarningLogged(Times.Once());
    }

    [Fact]
    public async Task SetAsync_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var action = async () => await CreateService().SetAsync<EventResponse>(
            "events:top10", null!, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SetAsync_WhenTokenAlreadyCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var action = async () => await CreateService().SetAsync(
            "events:top10", CreateResponse(), TimeSpan.FromMinutes(1), cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _database.Verify(
            db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveAsync_DeletesExactKey()
    {
        var id = Guid.NewGuid();
        _database
            .Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await CreateService().RemoveAsync($"event:{id:D}", TestContext.Current.CancellationToken);

        _database.Verify(
            db => db.KeyDeleteAsync((RedisKey)$"event:{id:D}", It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_WhenRedisThrows_DoesNotThrowAndLogsWarning()
    {
        _database
            .Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Redis is down"));

        var action = async () => await CreateService().RemoveAsync(
            "events:top10", TestContext.Current.CancellationToken);

        await action.Should().NotThrowAsync("cache removal failures must degrade to a logged no-op");
        VerifyWarningLogged(Times.Once());
    }

    private static EventResponse CreateResponse() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Concert",
        Description = "Live show",
        StartAt = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc),
        EndAt = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc),
        TotalSeats = 100,
        AvailableSeats = 58
    };

    private void VerifyWarningLogged(Times times) =>
        _logger.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, type) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
}
