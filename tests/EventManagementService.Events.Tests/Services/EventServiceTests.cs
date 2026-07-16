using EventManagementService.Events.Application.Abstractions.Caching;
using EventManagementService.Events.Application.Abstractions.Repositories;
using EventManagementService.Events.Application.Abstractions.Services;
using EventManagementService.Events.Application.Caching;
using EventManagementService.Events.Application.Configuration;
using EventManagementService.Events.Application.Dtos;
using EventManagementService.Events.Application.Services;
using EventManagementService.Events.Domain.Exceptions;
using EventManagementService.Events.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace EventManagementService.Events.Tests.Services;

public class EventServiceTests
{
    // Deliberately different from the CacheOptions defaults to prove
    // the service takes TTLs from the injected options, not from constants.
    private static readonly TimeSpan EventTtl = TimeSpan.FromMinutes(7);
    private static readonly TimeSpan TopEventsTtl = TimeSpan.FromSeconds(42);

    private readonly Mock<IEventRepository> _repository = new();

    // Loose mock: GetAsync<EventResponse> returns null by default (a cache miss), but for
    // array payloads Moq's empty default provider returns an EMPTY array — the service would
    // read that as a valid hit, so top-miss tests arrange the miss explicitly.
    private readonly Mock<ICacheService> _cache = new();

    private IEventService CreateService() => new EventService(
        _repository.Object,
        _cache.Object,
        Options.Create(new CacheOptions { EventTtl = EventTtl, TopEventsTtl = TopEventsTtl }));

    [Fact]
    public async Task GetEventsAsync_WhenQueryIsValid_ReturnsPaginatedResult()
    {
        var events = new[] { Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100) };
        var paginatedResult = new PaginatedResult<Event>
        {
            Items = events,
            Page = 1,
            Count = 1,
            TotalCount = 1
        };

        _repository.Setup(repo => repo.GetEventsAsync(It.IsAny<GetEventsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        var result = await CreateService().GetEventsAsync(new GetEventsQuery());

        result.Items.Should().HaveCount(1);
        result.Page.Should().Be(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetEventsAsync_WhenFromIsAfterTo_ThrowsBusinessValidationException()
    {
        var query = new GetEventsQuery
        {
            From = DateTime.UtcNow.AddDays(5),
            To = DateTime.UtcNow.AddDays(1)
        };

        var action = async () => await CreateService().GetEventsAsync(query);

        await action.Should().ThrowAsync<BusinessValidationException>()
            .WithMessage("Дата начала диапазона не должна быть позже даты окончания.");
    }

    [Fact]
    public async Task GetEventByIdAsync_WhenCacheHit_ReturnsCachedResponseWithoutRepositoryCall()
    {
        var id = Guid.NewGuid();
        var cachedResponse = new EventResponse
        {
            Id = id,
            Title = "Cached",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 100,
            AvailableSeats = 40
        };

        _cache.Setup(cache => cache.GetAsync<EventResponse>($"event:{id:D}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var result = await CreateService().GetEventByIdAsync(id);

        result.Should().BeSameAs(cachedResponse);
        _repository.Verify(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.VerifyNoOtherCalls();
        _cache.Verify(
            cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<EventResponse>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetEventByIdAsync_WhenCacheMiss_ReadsRepositoryOnceAndCachesResponseWithEventTtl()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _repository.Setup(repo => repo.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ev);

        var result = await CreateService().GetEventByIdAsync(ev.Id);

        result.Id.Should().Be(ev.Id);
        result.Title.Should().Be(ev.Title);
        result.TotalSeats.Should().Be(ev.TotalSeats);
        result.AvailableSeats.Should().Be(ev.AvailableSeats);
        _repository.Verify(repo => repo.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(
            cache => cache.SetAsync(
                $"event:{ev.Id:D}",
                It.Is<EventResponse>(response => response.Id == ev.Id && response.Title == ev.Title),
                EventTtl,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetEventByIdAsync_WhenEventDoesNotExist_ThrowsNotFoundExceptionAndCachesNothing()
    {
        _repository.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var action = async () => await CreateService().GetEventByIdAsync(Guid.NewGuid());

        await action.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*не найдено*");
        _cache.Verify(
            cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<EventResponse>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetTopEventsAsync_WhenCacheHit_ReturnsCachedTopWithoutRepositoryCall()
    {
        var cachedTop = new[]
        {
            new EventResponse
            {
                Id = Guid.NewGuid(),
                Title = "First",
                StartAt = DateTime.UtcNow.AddDays(1),
                EndAt = DateTime.UtcNow.AddDays(2),
                TotalSeats = 100,
                AvailableSeats = 0
            },
            new EventResponse
            {
                Id = Guid.NewGuid(),
                Title = "Second",
                StartAt = DateTime.UtcNow.AddDays(3),
                EndAt = DateTime.UtcNow.AddDays(4),
                TotalSeats = 50,
                AvailableSeats = 25
            }
        };

        _cache.Setup(cache => cache.GetAsync<EventResponse[]>("events:top10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedTop);

        var result = await CreateService().GetTopEventsAsync();

        result.Should().BeEquivalentTo(cachedTop, options => options.WithStrictOrdering());
        (result is EventResponse[]).Should().BeFalse("the service must not leak the mutable cached array to callers");
        _repository.Verify(repo => repo.GetTopEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetTopEventsAsync_WhenCacheMiss_ReadsTopTenFromRepositoryAndCachesWithTopTtl()
    {
        var events = new[]
        {
            Event.Create("First", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100),
            Event.Create("Second", DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4), 50)
        };
        ArrangeTopCacheMiss();
        _repository.Setup(repo => repo.GetTopEventsAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        var result = await CreateService().GetTopEventsAsync();

        result.Select(item => item.Id).Should().Equal(events[0].Id, events[1].Id);
        (result is EventResponse[]).Should().BeFalse("the service must not leak the mutable cached array to callers");
        _repository.Verify(repo => repo.GetTopEventsAsync(10, It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(
            cache => cache.SetAsync(
                "events:top10",
                It.Is<EventResponse[]>(payload =>
                    payload.Length == 2 && payload[0].Id == events[0].Id && payload[1].Id == events[1].Id),
                TopEventsTtl,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTopEventsAsync_WhenTopIsEmpty_CachesEmptyArrayAndReturnsEmpty()
    {
        ArrangeTopCacheMiss();
        _repository.Setup(repo => repo.GetTopEventsAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Event>());

        var result = await CreateService().GetTopEventsAsync();

        result.Should().BeEmpty();
        _cache.Verify(
            cache => cache.SetAsync(
                "events:top10",
                It.Is<EventResponse[]>(payload => payload.Length == 0),
                TopEventsTtl,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateEventAsync_AddsEventAndSaves()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);

        var result = await CreateService().CreateEventAsync(ev);

        result.Should().Be(ev);
        _repository.Verify(repo => repo.AddAsync(ev, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateEventAsync_InvalidatesEventKeyOnlyAfterSuccessfulSave()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        var journal = TrackSaveAndInvalidateOrder();

        await CreateService().CreateEventAsync(ev);

        journal.Should().Equal("save", $"invalidate:event:{ev.Id:D}");
        _cache.Verify(cache => cache.RemoveAsync(EventCacheKeys.Top10, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateEventAsync_WhenSaveFails_DoesNotInvalidateCache()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _repository.Setup(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database failure"));

        var action = async () => await CreateService().CreateEventAsync(ev);

        await action.Should().ThrowAsync<InvalidOperationException>();
        _cache.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateEventAsync_WhenEventExists_UpdatesAndSaves()
    {
        var ev = Event.Create("Original", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _repository.Setup(repo => repo.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ev);

        var request = new UpdateEventRequest
        {
            Title = "Updated",
            StartAt = DateTime.UtcNow.AddDays(3),
            EndAt = DateTime.UtcNow.AddDays(4),
            Description = "New description"
        };

        var result = await CreateService().UpdateEventAsync(ev.Id, request);

        result.Title.Should().Be("Updated");
        _repository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateEventAsync_InvalidatesEventKeyOnlyAfterSuccessfulSave()
    {
        var ev = Event.Create("Original", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _repository.Setup(repo => repo.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ev);
        var journal = TrackSaveAndInvalidateOrder();

        var request = new UpdateEventRequest
        {
            Title = "Updated",
            StartAt = DateTime.UtcNow.AddDays(3),
            EndAt = DateTime.UtcNow.AddDays(4)
        };

        await CreateService().UpdateEventAsync(ev.Id, request);

        journal.Should().Equal("save", $"invalidate:event:{ev.Id:D}");
        _cache.Verify(cache => cache.RemoveAsync(EventCacheKeys.Top10, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateEventAsync_WhenSaveFails_DoesNotInvalidateCache()
    {
        var ev = Event.Create("Original", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _repository.Setup(repo => repo.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ev);
        _repository.Setup(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database failure"));

        var request = new UpdateEventRequest
        {
            Title = "Updated",
            StartAt = DateTime.UtcNow.AddDays(3),
            EndAt = DateTime.UtcNow.AddDays(4)
        };

        var action = async () => await CreateService().UpdateEventAsync(ev.Id, request);

        await action.Should().ThrowAsync<InvalidOperationException>();
        _cache.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateEventAsync_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        _repository.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var request = new UpdateEventRequest
        {
            Title = "Updated",
            StartAt = DateTime.UtcNow.AddDays(3),
            EndAt = DateTime.UtcNow.AddDays(4)
        };

        var action = async () => await CreateService().UpdateEventAsync(Guid.NewGuid(), request);

        await action.Should().ThrowAsync<NotFoundException>();
        _cache.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteEventAsync_WhenEventExists_RemovesAndSaves()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _repository.Setup(repo => repo.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ev);

        await CreateService().DeleteEventAsync(ev.Id);

        _repository.Verify(repo => repo.Remove(ev), Times.Once);
        _repository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteEventAsync_InvalidatesEventKeyOnlyAfterSuccessfulSave()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _repository.Setup(repo => repo.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ev);
        var journal = TrackSaveAndInvalidateOrder();

        await CreateService().DeleteEventAsync(ev.Id);

        journal.Should().Equal("save", $"invalidate:event:{ev.Id:D}");
        _cache.Verify(cache => cache.RemoveAsync(EventCacheKeys.Top10, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteEventAsync_WhenSaveFails_DoesNotInvalidateCache()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _repository.Setup(repo => repo.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ev);
        _repository.Setup(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database failure"));

        var action = async () => await CreateService().DeleteEventAsync(ev.Id);

        await action.Should().ThrowAsync<InvalidOperationException>();
        _cache.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteEventAsync_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        _repository.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var action = async () => await CreateService().DeleteEventAsync(Guid.NewGuid());

        await action.Should().ThrowAsync<NotFoundException>();
        _cache.Verify(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Arranges an explicit top cache miss: for array payloads Moq's default provider
    /// returns an empty array (not null), which the service would treat as a cached hit.
    /// </summary>
    private void ArrangeTopCacheMiss() =>
        _cache.Setup(cache => cache.GetAsync<EventResponse[]>("events:top10", It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventResponse[]?)null);

    /// <summary>
    /// Records the relative order of repository saves and cache invalidations,
    /// proving "save first, invalidate second" instead of relying on independent Times.Once checks.
    /// The journal also captures the exact invalidated key.
    /// </summary>
    private List<string> TrackSaveAndInvalidateOrder()
    {
        var journal = new List<string>();

        _repository.Setup(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => journal.Add("save"))
            .Returns(Task.CompletedTask);

        _cache.Setup(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => journal.Add($"invalidate:{key}"))
            .Returns(Task.CompletedTask);

        return journal;
    }
}
