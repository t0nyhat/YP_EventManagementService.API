using EventManagementService.Events.Application.Abstractions.Repositories;
using EventManagementService.Events.Application.Abstractions.Services;
using EventManagementService.Events.Application.Dtos;
using EventManagementService.Events.Application.Services;
using EventManagementService.Events.Domain.Exceptions;
using EventManagementService.Events.Domain.Models;
using FluentAssertions;
using Moq;

namespace EventManagementService.Events.Tests.Services;

public class EventServiceTests
{
    private readonly Mock<IEventRepository> _repository = new();

    private IEventService CreateService() => new EventService(_repository.Object);

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
    public async Task GetEventByIdAsync_WhenEventExists_ReturnsEvent()
    {
        var ev = Event.Create("Test", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);
        _repository.Setup(repo => repo.GetByIdAsync(ev.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ev);

        var result = await CreateService().GetEventByIdAsync(ev.Id);

        result.Should().NotBeNull();
        result.Id.Should().Be(ev.Id);
    }

    [Fact]
    public async Task GetEventByIdAsync_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        _repository.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var action = async () => await CreateService().GetEventByIdAsync(Guid.NewGuid());

        await action.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*не найдено*");
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
    public async Task DeleteEventAsync_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        _repository.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var action = async () => await CreateService().DeleteEventAsync(Guid.NewGuid());

        await action.Should().ThrowAsync<NotFoundException>();
    }
}