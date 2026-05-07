# Диаграммы sprint 5

## 1. Архитектура после миграции на EF Core

```mermaid
flowchart TD
    Client[HTTP Client] --> Controllers[Controllers]
    Controllers --> EventSvc[IEventService]
    Controllers --> BookingSvc[IBookingService]

    EventSvc --> DbCtx[AppDbContext]
    BookingSvc --> DbCtx

    DbCtx --> PG[(PostgreSQL)]

    Worker[BookingProcessingBackgroundService] --> ScopeFactory[IServiceScopeFactory]
    ScopeFactory --> DbCtx
```

## 2. Поток создания бронирования

```mermaid
sequenceDiagram
    participant C as Client
    participant BC as EventBookingsController
    participant BS as BookingService
    participant DB as AppDbContext/PostgreSQL

    C->>BC: POST /api/events/{id}/book
    BC->>BS: CreateBookingAsync(eventId)
    BS->>DB: Load Event by Id
    DB-->>BS: Event
    BS->>BS: TryReserveSeats()
    BS->>DB: Add Booking + SaveChangesAsync()
    DB-->>BS: persisted
    BS-->>BC: Booking
    BC-->>C: 202 Accepted + Location
```

## 3. Работа фонового обработчика

```mermaid
flowchart TD
    A[Timer Tick] --> B[Create scope]
    B --> C[Read pending booking IDs]
    C --> D[For each ID create processing task]
    D --> E[Create scope per booking]
    E --> F[Load booking and related event]
    F --> G{Event exists?}
    G -- Yes --> H[Confirm booking]
    G -- No --> I[Reject booking]
    H --> J[SaveChangesAsync]
    I --> J
```

## 4. Тест конкурентного бронирования

```mermaid
flowchart LR
    Start[Start test] --> Seed[Seed event with N seats]
    Seed --> Parallel[Run M parallel tasks]
    Parallel --> Scope[Each task creates its own scope]
    Scope --> Call[Call CreateBookingAsync]
    Call --> Assert[Assert: success count == N]
    Assert --> Done[Assert: AvailableSeats == 0]
```

## 5. Контрольные вопросы

1. Почему `DbContext` должен быть scoped?
2. Зачем `BackgroundService` нужен `IServiceScopeFactory`?
3. Почему конкурентные тесты создают отдельный scope на запрос?
4. Как `EnsureCreated` помогает в учебном проекте?

---

[Назад к README спринта](README.md)
