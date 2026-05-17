# Диаграммы sprint 6

## 1. Архитектура после перехода на репозитории и миграции

```mermaid
flowchart TD
    Client[HTTP Client] --> Controllers[Controllers]
    Controllers --> EventSvc[IEventService]
    Controllers --> BookingSvc[IBookingService]

    EventSvc --> EventRepo[IEventRepository]
    BookingSvc --> EventRepo
    BookingSvc --> BookingRepo[IBookingRepository]

    EventRepo --> DbCtx[AppDbContext]
    BookingRepo --> DbCtx
    DbCtx --> PG[(PostgreSQL)]

    Worker[BookingProcessingBackgroundService] --> ScopeFactory[IServiceScopeFactory]
    ScopeFactory --> EventRepo
    ScopeFactory --> BookingRepo
```

## 2. Поток создания бронирования

```mermaid
sequenceDiagram
    participant C as Client
    participant BC as EventBookingsController
    participant BS as BookingService
    participant ER as IEventRepository
    participant BR as IBookingRepository
    participant DB as PostgreSQL

    C->>BC: POST /api/events/{id}/book
    BC->>BS: CreateBookingAsync(eventId)
    BS->>ER: GetByIdAsync(eventId)
    ER->>DB: SELECT event
    DB-->>ER: event
    ER-->>BS: event

    BS->>BS: TryReserveSeats()
    BS->>BR: AddAsync(booking)
    BS->>BR: SaveChangesAsync()
    BR->>DB: UPDATE event + INSERT booking
    DB-->>BR: committed
    BR-->>BS: ok
    BS-->>BC: BookingResponse
    BC-->>C: 202 Accepted + Location
```

## 3. Поток integration tests с Testcontainers

```mermaid
flowchart TD
    Start[Test start] --> Up[Start PostgreSQL container]
    Up --> Migrate[Apply EF migrations]
    Migrate --> Reset[Reset user tables before test]
    Reset --> Seed[Arrange seed data]
    Seed --> Act[Call repository method]
    Act --> Verify[Assert via separate verify-context]
    Verify --> Next[Next test]
```

## 4. Проверка схемы и ограничений

```mermaid
flowchart LR
    A[SchemaTests] --> B[Check tables and columns]
    A --> C[Check PK and FK]
    A --> D[Check ON DELETE CASCADE]
    A --> E[Check FK violation exception]
    A --> F[Check max length violation exception]
```

## 5. Контрольные вопросы

1. Почему migration-first лучше `EnsureCreated` для развивающейся схемы?
2. Что дает слой репозиториев между сервисом и `DbContext`?
3. Зачем нужен verify-context в интеграционных тестах записи?
4. Какие ошибки можно поймать только на реальном PostgreSQL, но не в InMemory?

---

[Назад к README спринта](README.md)
