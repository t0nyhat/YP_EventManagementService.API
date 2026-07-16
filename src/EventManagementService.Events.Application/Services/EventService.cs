using EventManagementService.Events.Application.Abstractions.Caching;
using EventManagementService.Events.Application.Abstractions.Repositories;
using EventManagementService.Events.Application.Abstractions.Services;
using EventManagementService.Events.Application.Caching;
using EventManagementService.Events.Application.Configuration;
using EventManagementService.Events.Application.Dtos;
using EventManagementService.Events.Application.Mappings;
using EventManagementService.Events.Application.Validation;
using EventManagementService.Events.Domain.Exceptions;
using EventManagementService.Events.Domain.Models;
using Microsoft.Extensions.Options;

namespace EventManagementService.Events.Application.Services;

/// <summary>
/// Application service implementation of <see cref="IEventService"/>.
/// Read paths use cache-aside over <see cref="ICacheService"/>; the cache is
/// best-effort by contract, so its failures never break the database path.
/// Write paths invalidate the per-event key only after a successful save.
/// </summary>
public sealed class EventService : IEventService
{
    /// <summary>
    /// Size of the top events projection. The use case is fixed to ten entries,
    /// so the constant lives here rather than in the repository.
    /// </summary>
    private const int TopEventsCount = 10;

    private readonly IEventRepository _repository;
    private readonly ICacheService _cache;
    private readonly CacheOptions _cacheOptions;

    public EventService(
        IEventRepository repository,
        ICacheService cache,
        IOptions<CacheOptions> cacheOptions)
    {
        ArgumentNullException.ThrowIfNull(cacheOptions);

        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheOptions = cacheOptions.Value;
    }

    /// <inheritdoc />
    public async Task<PaginatedResult<Event>> GetEventsAsync(GetEventsQuery query)
    {
        ValidateQuery(query);
        return await _repository.GetEventsAsync(query);
    }

    /// <inheritdoc />
    public async Task<EventResponse> GetEventByIdAsync(Guid id)
    {
        var cacheKey = EventCacheKeys.ForEvent(id);

        var cached = await _cache.GetAsync<EventResponse>(cacheKey);
        if (cached is not null)
        {
            return cached;
        }

        var existingEvent = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Событие с id {id} не найдено.");

        var response = existingEvent.ToResponse();

        // 404 никогда не кэшируется: при промахе в кэш пишется только найденное событие.
        await _cache.SetAsync(cacheKey, response, _cacheOptions.EventTtl);

        return response;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<EventResponse>> GetTopEventsAsync()
    {
        var cached = await _cache.GetAsync<EventResponse[]>(EventCacheKeys.Top10);
        if (cached is not null)
        {
            return AsReadOnly(cached);
        }

        var topEvents = await _repository.GetTopEventsAsync(TopEventsCount);

        var response = topEvents.Select(EventMappings.ToResponse).ToArray();

        // Пустой топ — валидный кэшируемый результат, а не промах.
        await _cache.SetAsync(EventCacheKeys.Top10, response, _cacheOptions.TopEventsTtl);

        return AsReadOnly(response);
    }

    /// <inheritdoc />
    public async Task<Event> CreateEventAsync(Event newEvent)
    {
        ArgumentNullException.ThrowIfNull(newEvent);

        await _repository.AddAsync(newEvent);
        await _repository.SaveChangesAsync();

        // Защитная инвалидация: 404 не кэшируется, поэтому под свежим id ничего
        // лежать не должно, но единое правило для всех путей записи проще проверять.
        await _cache.RemoveAsync(EventCacheKeys.ForEvent(newEvent.Id));

        return newEvent;
    }

    /// <inheritdoc />
    public async Task<Event> UpdateEventAsync(Guid id, UpdateEventRequest request)
    {
        var existingEvent = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Событие с id {id} не найдено.");

        existingEvent.Update(request.Title, request.StartAt!.Value, request.EndAt!.Value, request.Description);
        await _repository.SaveChangesAsync();

        // Инвалидируем строго после успешного сохранения: кэш не должен опережать
        // базу, если сохранение откатилось. Ключ топа истекает только по TTL.
        await _cache.RemoveAsync(EventCacheKeys.ForEvent(id));

        return existingEvent;
    }

    /// <inheritdoc />
    public async Task DeleteEventAsync(Guid id)
    {
        var existingEvent = await _repository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Событие с id {id} не найдено.");

        _repository.Remove(existingEvent);
        await _repository.SaveChangesAsync();

        // Инвалидируем строго после успешного сохранения: кэш не должен опережать
        // базу, если сохранение откатилось. Ключ топа истекает только по TTL.
        await _cache.RemoveAsync(EventCacheKeys.ForEvent(id));
    }

    /// <summary>
    /// Wraps the array in a <see cref="System.Collections.ObjectModel.ReadOnlyCollection{T}"/>
    /// so callers cannot cast the result back to <c>EventResponse[]</c> and mutate
    /// the collection the service produced. A wrapper is enough here: the array is
    /// created fresh per call (deserialized or mapped) and is not shared state.
    /// </summary>
    private static IReadOnlyCollection<EventResponse> AsReadOnly(EventResponse[] events)
        => Array.AsReadOnly(events);

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
