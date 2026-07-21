# Диаграммы sprint 10

## 1. Redis в архитектуре Events

```mermaid
flowchart LR
    Client[Клиент] --> API[Events Presentation]
    API --> Service[EventService]
    Service --> CachePort[ICacheService]
    Service --> RepoPort[IEventRepository]
    CachePort --> RedisAdapter[RedisCacheService]
    RedisAdapter --> Redis[(Redis)]
    RepoPort --> Repo[EventRepository]
    Repo --> Postgres[(events_db)]

    Kafka[(Kafka)] --> Consumer[BookingConfirmedConsumer]
    Consumer --> Handler[BookingConfirmedHandler]
    Handler --> Postgres
    Handler --> CachePort
```

Application зависит от `ICacheService`, а не от StackExchange.Redis. Redis остаётся производной копией PostgreSQL-данных.

## 2. `GET /events/{id}`: Cache-Aside

```mermaid
sequenceDiagram
    participant C as Client
    participant S as EventService
    participant R as Redis
    participant DB as PostgreSQL

    C->>S: GET /events/{id}
    S->>R: GET event:{id}
    alt cache hit
        R-->>S: EventResponse JSON
        S-->>C: 200 EventResponse
    else cache miss или Redis недоступен
        R-->>S: null
        S->>DB: SELECT event WHERE id
        alt событие найдено
            DB-->>S: Event
            S->>R: SET event:{id} EventResponse EX 600
            Note over S,R: ошибка SET логируется и не ломает ответ
            S-->>C: 200 EventResponse
        else событие отсутствует
            DB-->>S: null
            S-->>C: 404 Not Found
            Note over S,R: 404 не кешируется
        end
    end
```

## 3. `GET /events/top`: топ-10 и TTL

```mermaid
flowchart TD
    Start[GET /events/top] --> Get[GET events:top10]
    Get --> Hit{Значение найдено?}
    Hit -- да --> Return[200: кешированный EventResponse array]
    Hit -- нет --> Query[PostgreSQL: ORDER BY sold ratio DESC + tie-breakers + LIMIT 10]
    Query --> Map[Map в EventResponse array]
    Map --> Set[SET events:top10 с TTL 60 секунд]
    Set --> ReturnFresh[200: свежий результат]
```

Пустой массив также проходит через `SET`. CRUD и Kafka не удаляют `events:top10`; актуальность ограничивается минутным TTL.

## 4. HTTP-мутация: commit перед инвалидацией

```mermaid
sequenceDiagram
    participant C as Admin client
    participant S as EventService
    participant DB as PostgreSQL
    participant R as Redis

    C->>S: POST / PUT / DELETE event
    S->>DB: изменить Event + SaveChanges
    alt save failed
        DB-->>S: exception / rollback
        Note over S,R: кеш не изменяется
        S-->>C: error response
    else commit successful
        DB-->>S: committed
        S->>R: DEL event:{id}
        Note over S,R: ошибка DEL = warning + no-op
        S-->>C: success response
    end
```

Удаление ключа до commit запрещено: cache miss между `DEL` и `SaveChanges` мог бы заново прогреть старое значение.

## 5. Kafka: изменение мест и кеш

```mermaid
flowchart TD
    Msg[BookingConfirmed] --> Dup{booking_id уже в Inbox?}
    Dup -- да --> Noop[no-op: кеш не менять]
    Dup -- нет --> Valid{Event найден, не начался, мест хватает?}
    Valid -- нет --> Skip[записать skipped Inbox result: кеш не менять]
    Valid -- да --> Tx[уменьшить AvailableSeats + записать Inbox + commit]
    Tx --> Del[DEL event:{eventId} с CancellationToken.None]
    Del --> CommitOffset[Kafka consumer commit offset]

    Del -. не затрагивает .-> Top[events:top10 живёт до TTL]
```

Инвалидация стоит после транзакции Event + Inbox, но до успешного завершения handler. Неотменяемый token не позволяет остановке host пропустить единственную попытку удалить ключ после commit.

## 6. Деградация Redis

```mermaid
flowchart LR
    Request[GET request] --> Cache[RedisCacheService.GetAsync]
    Cache --> Available{Redis доступен?}
    Available -- да, hit --> Cached[Ответ из кеша]
    Available -- да, miss --> DB[Чтение PostgreSQL]
    Available -- нет --> Log[Warning + вернуть null]
    Log --> DB
    DB --> Write[Best-effort SetAsync]
    Write --> Response[HTTP response]
```

Отказ Redis меняет только производительность: путь через PostgreSQL и HTTP-результат сохраняются.

## 7. TTL и инвалидация

```mermaid
timeline
    title Жизненный цикл event:{id}
    Cache miss : чтение PostgreSQL
               : SET с TTL 10 минут
    Cache hit  : ответы без PostgreSQL
    Event write: commit в PostgreSQL
               : DEL ключа
    Next read  : повторный прогрев свежим DTO
```

Для `events:top10` этап `DEL` отсутствует: после `SET` ключ живёт не более одной минуты и затем пересчитывается при следующем запросе.

---

[Назад к README спринта](README.md)
