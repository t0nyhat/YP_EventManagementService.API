# Архитектура решения sprint 10

## 1. Изменения сосредоточены в Events

Состав микросервисов sprint 9 сохранён. Redis добавляется как внешняя инфраструктура только для Events:

```text
Client -> Events.Presentation -> Events.Application -> IEventRepository -> PostgreSQL
                                      |
                                      +-> ICacheService <- RedisCacheService -> Redis

Kafka -> BookingConfirmedConsumer -> BookingConfirmedHandler -> PostgreSQL + ICacheService
```

Users и Bookings не получают ссылок на Redis и не зависят от доступности кеша.

## 2. Распределение ответственности по слоям

### Application

- [`ICacheService`](../../src/EventManagementService.Events.Application/Abstractions/Caching/ICacheService.cs) — порт с операциями `GetAsync`, `SetAsync`, `RemoveAsync`; контракт объявляет кеш best-effort.
- [`EventCacheKeys`](../../src/EventManagementService.Events.Application/Caching/EventCacheKeys.cs) — единый источник форматов `event:{id}` и `events:top10`.
- [`CacheOptions`](../../src/EventManagementService.Events.Application/Configuration/CacheOptions.cs) — типизированная политика TTL.
- [`EventService`](../../src/EventManagementService.Events.Application/Services/EventService.cs) — оркестрирует Cache-Aside и инвалидацию после CRUD.
- `IEventRepository.GetTopEventsAsync(count)` — порт запроса рейтинга без знания HTTP и Redis.

Application зависит только от абстракции кеша и не содержит `StackExchange.Redis`, connection string или Redis-команд.

### Infrastructure

- [`RedisCacheService`](../../src/EventManagementService.Events.Infrastructure/Caching/RedisCacheService.cs) — адаптер `ICacheService` над StackExchange.Redis.
- [`CacheJson`](../../src/EventManagementService.Events.Infrastructure/Caching/CacheJson.cs) — общие настройки JSON для всех кеш-payload.
- [`RedisOptions`](../../src/EventManagementService.Events.Infrastructure/Configuration/RedisOptions.cs) — типизированная секция соединения.
- [`EventRepository`](../../src/EventManagementService.Events.Infrastructure/Repositories/EventRepository.cs) — SQL-транслируемое ранжирование топа.
- [`BookingConfirmedHandler`](../../src/EventManagementService.Events.Infrastructure/Messaging/BookingConfirmedHandler.cs) — удаляет ключ затронутого события после commit Event + Inbox.
- [`DependencyInjection`](../../src/EventManagementService.Events.Infrastructure/DependencyInjection.cs) — binding/validation настроек и singleton-регистрация Redis-клиента.

### Presentation

- [`EventsController`](../../src/EventManagementService.Events.Presentation/Controllers/EventsController.cs) публикует анонимный `GET /events/top`.
- `appsettings*.json` задают локальное соединение и TTL.
- Composition root подключает Infrastructure, но не реализует кеш-логику в контроллере.

## 3. Правила зависимостей

Направление Clean Architecture не изменилось:

```text
Events.Domain <- Events.Application <- Events.Infrastructure <- Events.Presentation
```

Ключевой dependency inversion:

```text
EventService --> ICacheService <-- RedisCacheService
```

Application определяет нужный ему порт; Infrastructure предоставляет технологическую реализацию. Благодаря этому unit-тесты `EventService` используют Moq и не запускают Redis.

## 4. Жизненный цикл Redis-компонентов

`IConnectionMultiplexer` регистрируется как singleton. Это тяжёлый потокобезопасный клиент, который сам управляет соединениями и предназначен для переиспользования на протяжении жизни процесса.

При создании клиента:

```csharp
var options = ConfigurationOptions.Parse(redisOptions.ConnectionString);
options.AbortOnConnectFail = false;
return ConnectionMultiplexer.Connect(options);
```

`AbortOnConnectFail = false` позволяет API стартовать без доступного Redis и переподключаться в фоне. `RedisCacheService` не хранит mutable state поверх multiplexer и также безопасно зарегистрирован singleton.

`EventsDbContext`, `IEventRepository` и `BookingConfirmedHandler` остаются scoped.

## 5. Новый read use case: топ-10

`GET /events/top` возвращает до десяти событий по доле проданных мест:

```text
soldRatio = (TotalSeats - AvailableSeats) / TotalSeats
```

Вычисление и сортировка выполняются в PostgreSQL, а не после загрузки всех событий в память. Основной порядок — ratio по убыванию; tie-breakers обеспечивают стабильный результат:

1. число проданных мест — по убыванию;
2. `StartAt` — по возрастанию;
3. `Id` — по возрастанию.

Приведение к `double` внутри LINQ необходимо для дробного деления: целочисленное `5 / 10` дало бы `0` и разрушило рейтинг. Защитная ветка `TotalSeats <= 0` присваивает ratio `0`, хотя доменная модель не допускает такое состояние.

## 6. Формат кешируемых данных

В Redis сохраняется `EventResponse`, а для топа — массив `EventResponse[]`. Доменные EF-сущности не кешируются: это исключает tracking-состояние, навигации и зависимость внешнего формата от persistence-модели.

JSON сериализуется через единые [`CacheJson.Options`](../../src/EventManagementService.Events.Infrastructure/Caching/CacheJson.cs) на базе `JsonSerializerDefaults.Web` — имена свойств camelCase. Если контракт станет несовместимым между версиями API, настройки меняются централизованно; одновременно потребуется версия в ключе (`v2:event:...`) либо очистка старых записей.

## 7. Runtime-топология

Docker Compose теперь поднимает девять контейнеров:

- Zookeeper и Kafka;
- PostgreSQL для Users, Events и Bookings;
- Redis 7.2;
- Users API, Events API и Bookings API.

В compose-сети Events подключается к `redis:6379` через `Redis__ConnectionString`. Redis имеет healthcheck `redis-cli ping`, но `events-api` намеренно не зависит от его healthy-состояния: недоступный кеш не должен блокировать запуск бизнес-сервиса.

---

[Далее: Стратегия кеширования и согласованность →](03-cache-strategy.md)
