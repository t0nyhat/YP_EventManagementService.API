using EventManagementService.Events.Application.Abstractions.Repositories;
using EventManagementService.Events.Application.Abstractions.Services;
using EventManagementService.Events.Application.Dtos;
using EventManagementService.Events.Application.Validation;
using EventManagementService.Events.Domain.Exceptions;
using EventManagementService.Events.Domain.Models;

namespace EventManagementService.Events.Application.Services;

/// <summary>
/// Application service implementation of <see cref="IEventService"/>.
/// </summary>
public sealed class EventService : IEventService
{
    private readonly IEventRepository _repository;

    public EventService(IEventRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    public async Task<PaginatedResult<Event>> GetEventsAsync(GetEventsQuery query)
    {
        ValidateQuery(query);
        return await _repository.GetEventsAsync(query);
    }

    /// <inheritdoc />
    public async Task<Event> GetEventByIdAsync(Guid id)
    {
        return await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Событие с id {id} не найдено.");
    }

    /// <inheritdoc />
    public async Task<Event> CreateEventAsync(Event newEvent)
    {
        ArgumentNullException.ThrowIfNull(newEvent);

        await _repository.AddAsync(newEvent);
        await _repository.SaveChangesAsync();
        return newEvent;
    }

    /// <inheritdoc />
    public async Task<Event> UpdateEventAsync(Guid id, UpdateEventRequest request)
    {
        var existingEvent = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Событие с id {id} не найдено.");

        existingEvent.Update(request.Title, request.StartAt!.Value, request.EndAt!.Value, request.Description);
        await _repository.SaveChangesAsync();

        return existingEvent;
    }

    /// <inheritdoc />
    public async Task DeleteEventAsync(Guid id)
    {
        var existingEvent = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Событие с id {id} не найдено.");

        _repository.Remove(existingEvent);
        await _repository.SaveChangesAsync();
    }

    private static void ValidateQuery(GetEventsQuery query)
    {
        if (query.From.HasValue && query.To.HasValue && query.From.Value > query.To.Value)
        {
            throw new BusinessValidationException("Дата начала диапазона не должна быть позже даты окончания.");
        }

        var error = GetEventsQueryValidation.Validate(query).FirstOrDefault();
        if (error is not null)
        {
            throw new BusinessValidationException(error.ErrorMessage!);
        }
    }
}