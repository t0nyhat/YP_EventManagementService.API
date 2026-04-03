# Диаграммы и визуализация sprint 3

В этом разделе приведены Mermaid-диаграммы, которые помогают быстро увидеть, как после sprint 3 устроены:

- слои приложения;
- создание брони с `202 Accepted`;
- фоновая обработка pending-броней;
- взаимодействие store, сервиса и worker'а;
- тестовый слой.

## 1. Общая архитектура после sprint 3

```mermaid
graph TD
    Client[HTTP клиент] --> Middleware[ExceptionHandlingMiddleware]
    Middleware --> EventsController[EventsController]
    Middleware --> EventBookingsController[EventBookingsController]
    Middleware --> BookingsController[BookingsController]

    EventsController --> EventService[IEventService / EventService]
    EventBookingsController --> BookingService[IBookingService / BookingService]
    BookingsController --> BookingService

    BookingService --> EventService
    BookingService --> BookingStore[IBookingStore / InMemoryBookingStore]
    Worker[BookingProcessingBackgroundService] --> BookingStore

    EventsController --> EventDto[Event DTO / Mapping]
    EventBookingsController --> BookingDto[Booking DTO / Mapping]
    BookingsController --> BookingDto

    EventService --> EventStorage[List<Event> in memory]
    BookingStore --> BookingStorage[Dictionary<Guid, Booking> in memory]

    EventService --> Exceptions[NotFoundException / BusinessValidationException]
    BookingService --> Exceptions
    Exceptions --> Middleware

    Tests[EventManagementService.API.Tests] --> EventService
    Tests --> BookingService
    Tests --> BookingStore
    Tests --> Worker
```

**Пояснение:**  
После sprint 3 у проекта появляется второй контур логики: бронирования. `BookingService` зависит от `IEventService` и `IBookingStore`, а `BookingProcessingBackgroundService` использует тот же самый store. Это и есть центральная архитектурная идея спринта.

## 2. HTTP-сценарий создания брони

```mermaid
sequenceDiagram
    participant Client
    participant Middleware
    participant EventBookingsController
    participant BookingService
    participant EventService
    participant BookingStore

    Client->>Middleware: POST /api/events/{id}/book
    Middleware->>EventBookingsController: Передача запроса
    EventBookingsController->>BookingService: CreateBookingAsync(eventId)
    BookingService->>EventService: GetEventById(eventId)
    EventService-->>BookingService: Event найден
    BookingService->>BookingStore: Add(Booking.CreatePending(...))
    BookingStore-->>BookingService: stored booking
    BookingService-->>EventBookingsController: Booking
    EventBookingsController-->>Middleware: 202 Accepted + Location + BookingResponse
    Middleware-->>Client: HTTP Response
```

**Пояснение:**  
В момент ответа клиент получает уже существующий ресурс брони, но ещё не конечный статус обработки. Поэтому используется `202 Accepted`, а не `201 Created`.

## 3. Асинхронная обработка брони worker'ом

```mermaid
sequenceDiagram
    participant Worker as BookingProcessingBackgroundService
    participant Store as InMemoryBookingStore

    loop Пока приложение работает
        Worker->>Store: GetPendingIds()
        Store-->>Worker: snapshot pending ids

        loop Для каждой pending-брони
            Worker->>Worker: Task.Delay(2s)
            Worker->>Store: TrySetStatus(id, Confirmed, DateTime.UtcNow)
            alt Бронь всё ещё Pending
                Store-->>Worker: true
                Worker->>Worker: log Confirmed
            else Уже обработана или отсутствует
                Store-->>Worker: false
                Worker->>Worker: log skip
            end
        end

        Worker->>Worker: Task.Delay(1s)
    end
```

**Пояснение:**  
Worker последовательно обрабатывает pending-записи, делает искусственную задержку и затем переводит бронь в `Confirmed`. Между итерациями цикла есть дополнительная пауза опроса.

## 4. Поток чтения состояния брони

```mermaid
sequenceDiagram
    participant Client
    participant BookingsController
    participant BookingService
    participant Store as InMemoryBookingStore

    Client->>BookingsController: GET /api/bookings/{id}
    BookingsController->>BookingService: GetBookingByIdAsync(id)
    BookingService->>Store: GetById(id)

    alt Бронь найдена
        Store-->>BookingService: Booking snapshot
        BookingService-->>BookingsController: Booking
        BookingsController-->>Client: 200 OK + BookingResponse
    else Бронь не найдена
        Store-->>BookingService: null
        BookingService-->>BookingsController: throw NotFoundException
        BookingsController-->>Client: 404 ProblemDetails
    end
```

**Пояснение:**  
`GET /api/bookings/{id}` нужен именно для наблюдения за асинхронным жизненным циклом брони.

## 5. Диаграмма store и snapshot-логики

```mermaid
flowchart TD
    Add[Add booking] --> Lock1[lock]
    Lock1 --> CloneOnWrite[Snapshot before store]
    CloneOnWrite --> Save[Save in dictionary]
    Save --> ReturnCopy[Return snapshot to caller]

    GetById[GetById] --> Lock2[lock]
    Lock2 --> Read[Read from dictionary]
    Read --> ReturnSnapshot[Return detached snapshot]

    GetPendingIds[GetPendingIds] --> Lock3[lock]
    Lock3 --> FilterPending[Filter Status == Pending]
    FilterPending --> ReturnIds[Return only ids]

    TrySetStatus[TrySetStatus] --> Lock4[lock]
    Lock4 --> CheckPending{Exists and Pending?}
    CheckPending -->|No| False[Return false]
    CheckPending -->|Yes| Update[Confirm / Reject + set ProcessedAt]
    Update --> True[Return true]
```

**Пояснение:**  
Store не отдаёт наружу внутренние объекты как есть. Вместо этого он работает через snapshot'ы и тем самым отделяет внутреннее состояние от внешнего кода.

## 6. Диаграмма классов основных компонентов sprint 3

```mermaid
classDiagram
    class Booking {
        -Guid Id
        -Guid EventId
        -BookingStatus Status
        -DateTime CreatedAt
        -DateTime? ProcessedAt
        +CreatePending(Guid, DateTime?)
        +Confirm(DateTime?)
        +Reject(DateTime?)
        +Snapshot()
    }

    class BookingStatus {
        <<enumeration>>
        Pending
        Confirmed
        Rejected
    }

    class IBookingStore {
        <<interface>>
        +Add(Booking) Booking
        +GetById(Guid) Booking?
        +GetPendingIds() IReadOnlyCollection<Guid>
        +TrySetStatus(Guid, BookingStatus, DateTime) bool
    }

    class InMemoryBookingStore {
        -Dictionary<Guid, Booking> _bookings
        -object _lock
        +Add(Booking) Booking
        +GetById(Guid) Booking?
        +GetPendingIds() IReadOnlyCollection<Guid>
        +TrySetStatus(Guid, BookingStatus, DateTime) bool
    }

    class IBookingService {
        <<interface>>
        +CreateBookingAsync(Guid) Task~Booking~
        +GetBookingByIdAsync(Guid) Task~Booking~
    }

    class BookingService {
        -IBookingStore _bookingStore
        -IEventService _eventService
        +CreateBookingAsync(Guid) Task~Booking~
        +GetBookingByIdAsync(Guid) Task~Booking~
    }

    class EventBookingsController {
        +CreateBooking(CreateBookingRequest)
    }

    class BookingsController {
        +GetBookingById(Guid)
    }

    class BookingProcessingBackgroundService {
        -IBookingStore _bookingStore
        -ILogger _logger
        +ExecuteAsync(CancellationToken)
    }

    Booking --> BookingStatus
    InMemoryBookingStore ..|> IBookingStore
    BookingService ..|> IBookingService
    BookingService --> IBookingStore
    BookingService --> IEventService
    EventBookingsController --> IBookingService
    BookingsController --> IBookingService
    BookingProcessingBackgroundService --> IBookingStore
```

**Пояснение:**  
Это основной “срез классов” sprint 3. Здесь хорошо видно, что `Booking` — это доменная модель, `BookingService` — прикладной сервис, `InMemoryBookingStore` — shared state, а worker — отдельный фоновой компонент.

## 7. Тестовый слой после sprint 3

```mermaid
graph TD
    BookingTests[BookingTests] --> Booking
    StoreTests[InMemoryBookingStoreTests] --> Store[InMemoryBookingStore]
    BookingServiceTests[BookingServiceTests] --> BookingService
    BookingServiceTests --> EventService
    BookingServiceTests --> Store
    WorkerTests[BookingProcessingBackgroundServiceTests] --> Worker[BookingProcessingBackgroundService]
    WorkerTests --> Store
    IntegrationTests[EventsApiIntegrationTests] --> Controllers
    IntegrationTests --> Middleware
    IntegrationTests --> Worker
    IntegrationTests --> Store
```

**Пояснение:**  
Тестовый слой теперь покрывает проект на нескольких уровнях: модель, store, сервис, worker и HTTP-контракт целиком.
