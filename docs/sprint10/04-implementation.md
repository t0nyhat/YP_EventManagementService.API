# Реализация sprint 10 в коде

## 1. Порт кеша в Application

[`ICacheService`](../../src/EventManagementService.Events.Application/Abstractions/Caching/ICacheService.cs) предоставляет минимальный generic API:

```csharp
Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    where T : class;

Task SetAsync<T>(string key, T value, TimeSpan timeToLive,
    CancellationToken cancellationToken = default)
    where T : class;

Task RemoveAsync(string key, CancellationToken cancellationToken = default);
```

Интерфейс не раскрывает `IDatabase`, `RedisValue` и другие типы StackExchange.Redis. Это позволяет Application формулировать use case через порт и тестировать его без внешнего сервера.

## 2. EventService: два Cache-Aside read-пути

[`EventService`](../../src/EventManagementService.Events.Application/Services/EventService.cs) теперь возвращает `EventResponse` для чтения по id: DTO может прийти как из Redis, так и из доменной сущности после repository miss. `GET /events` с фильтрацией и пагинацией остаётся без кеша.

Для топа сервис хранит константу `TopEventsCount = 10`, вызывает `GetTopEventsAsync(10)`, маппит результат в массив и оборачивает его через `Array.AsReadOnly`. Свежий массив создаётся после десериализации или mapping, поэтому вызывающая сторона не может изменить коллекцию, которую вернул сервис.

Write paths имеют одинаковую форму:

```csharp
await repository.SaveChangesAsync();
await cache.RemoveAsync(EventCacheKeys.ForEvent(id));
```

Ключ `events:top10` в create/update/delete не удаляется.

## 3. Централизация ключей и TTL

[`EventCacheKeys`](../../src/EventManagementService.Events.Application/Caching/EventCacheKeys.cs) содержит:

```csharp
public const string Top10 = "events:top10";
public static string ForEvent(Guid id) => $"event:{id:D}";
```

[`CacheOptions`](../../src/EventManagementService.Events.Application/Configuration/CacheOptions.cs) привязывается к секции `Cache` и задаёт defaults: 10 минут для карточки, 1 минута для топа.

## 4. RedisCacheService

[`RedisCacheService`](../../src/EventManagementService.Events.Infrastructure/Caching/RedisCacheService.cs) реализует порт через три Redis-команды:

| Метод порта | StackExchange.Redis |
|---|---|
| `GetAsync<T>` | `StringGetAsync` + JSON deserialize |
| `SetAsync<T>` | `StringSetAsync` с точным TTL |
| `RemoveAsync` | `KeyDeleteAsync` |

StackExchange.Redis не принимает `CancellationToken` в используемых async-операциях, поэтому задачи оборачиваются в `WaitAsync(cancellationToken)`. Фильтр исключений намеренно не перехватывает `OperationCanceledException`, но логирует и поглощает инфраструктурные ошибки.

Повреждённый JSON считается miss. Адаптер пытается удалить такую запись, чтобы она не отравляла следующие чтения; ошибка cleanup также только логируется.

## 5. Общий JSON-формат кеша

[`CacheJson`](../../src/EventManagementService.Events.Infrastructure/Caching/CacheJson.cs) содержит один экземпляр `JsonSerializerOptions(JsonSerializerDefaults.Web)`. И чтение, и запись `RedisCacheService` используют его, поэтому naming policy и будущие converters меняются в одном месте.

Это локальный контракт хранилища Events, в отличие от `KafkaJson` в общем проекте Contracts: Kafka-формат разделяют Bookings и Events, а Redis-payload нужен только Events.

## 6. Top query в PostgreSQL

[`EventRepository.GetTopEventsAsync`](../../src/EventManagementService.Events.Infrastructure/Repositories/EventRepository.cs) строит один LINQ-запрос с `AsNoTracking`, сортировкой и `Take(count)`. EF Core переводит расчёт ratio и tie-breakers в SQL, поэтому в память загружаются только итоговые строки.

Метод отвергает `count <= 0` через `ArgumentOutOfRangeException.ThrowIfNegativeOrZero`. Защитная проверка `TotalSeats > 0` исключает деление на ноль для повреждённых данных.

## 7. Инвалидация из Kafka handler

[`BookingConfirmedHandler`](../../src/EventManagementService.Events.Infrastructure/Messaging/BookingConfirmedHandler.cs) получил `ICacheService`. Только ветка `Processed`, реально уменьшающая места, удаляет `event:{eventId}` — строго после `SaveChangesAsync`, который атомарно сохраняет Event и Inbox.

Дубликаты и skipped-результаты не инвалидируют ключ: состояние события в этих ветках не меняется. Топ по-прежнему обновится после истечения минутного TTL.

## 8. DI и валидация конфигурации

[`DependencyInjection.AddInfrastructureServices`](../../src/EventManagementService.Events.Infrastructure/DependencyInjection.cs) выполняет:

- binding `RedisOptions` из `Redis` и проверку непустой строки соединения;
- binding `CacheOptions` из `Cache` и проверку обоих TTL `> 0`;
- singleton `IConnectionMultiplexer` с `AbortOnConnectFail = false`;
- singleton `ICacheService -> RedisCacheService`.

Валидация выполняется на старте (`ValidateOnStart`), поэтому опечатка в обязательной настройке обнаруживается сразу, а не при первом запросе.

## 9. Конфигурация и Docker Compose

Локальные настройки Events:

```json
"Redis": {
  "ConnectionString": "localhost:6379"
},
"Cache": {
  "EventTtl": "00:10:00",
  "TopEventsTtl": "00:01:00"
}
```

В [`docker-compose.yml`](../../docker-compose.yml) добавлен `redis:7.2-alpine` с healthcheck. Events получает `Redis__ConnectionString=redis:6379`; двойное подчёркивание превращается в разделитель секции ASP.NET Core Configuration.

`events-api` не имеет жёсткого `depends_on` от Redis. Это намеренно проверяет архитектурный принцип: кеш улучшает производительность, но не является условием доступности API.

---

[Далее: Тестирование и запуск →](05-testing-and-run.md)
