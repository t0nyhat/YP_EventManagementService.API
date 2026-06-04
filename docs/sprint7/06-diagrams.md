# Диаграммы sprint 7

## 1. Зависимости production-проектов

```mermaid
flowchart TD
    Presentation[EventManagementService.Presentation]
    Infrastructure[EventManagementService.Infrastructure]
    Application[EventManagementService.Application]
    Domain[EventManagementService.Domain]

    Presentation --> Application
    Presentation --> Infrastructure
    Infrastructure --> Application
    Infrastructure --> Domain
    Application --> Domain
```

Запрещенное направление:

```text
Domain -> outward
Application -> Infrastructure
Application -> Presentation
```

## 2. HTTP request flow

```mermaid
sequenceDiagram
    participant Client
    participant Controller as Presentation Controller
    participant AppService as Application Service
    participant Port as Repository Port
    participant Repo as Infrastructure Repository
    participant Db as PostgreSQL

    Client->>Controller: HTTP request
    Controller->>AppService: call use case
    AppService->>Port: repository contract
    Port->>Repo: implemented by DI adapter
    Repo->>Db: EF Core query/save
    Db-->>Repo: data
    Repo-->>AppService: domain model/result
    AppService-->>Controller: use case result
    Controller-->>Client: HTTP response
```

## 3. Booking creation flow

```mermaid
sequenceDiagram
    participant Client
    participant Controller as EventBookingsController
    participant BookingService
    participant EventRepo as IEventRepository
    participant BookingRepo as IBookingRepository
    participant Event as Event aggregate
    participant Booking as Booking entity

    Client->>Controller: POST /api/events/{id}/book
    Controller->>BookingService: CreateBookingAsync(eventId)
    BookingService->>EventRepo: GetByIdAsync(eventId)
    EventRepo-->>BookingService: Event
    BookingService->>Event: TryReserveSeats()
    BookingService->>Booking: CreatePending(eventId)
    BookingService->>BookingRepo: AddAsync(booking)
    BookingService->>BookingRepo: SaveChangesAsync()
    BookingService-->>Controller: Booking
    Controller-->>Client: 202 Accepted + Location
```

## 4. Background booking processing

```mermaid
sequenceDiagram
    participant Worker as Presentation BackgroundService
    participant BookingRepo as IBookingRepository
    participant Processing as Application BookingProcessingService
    participant EventRepo as IEventRepository
    participant Booking
    participant Event

    Worker->>BookingRepo: GetPendingIdsAsync()
    BookingRepo-->>Worker: pending ids
    Worker->>Worker: Task.WhenAll(ids)
    Worker->>Processing: ProcessPendingBookingAsync(id)
    Processing->>BookingRepo: GetByIdAsync(id)
    BookingRepo-->>Processing: Booking
    Processing->>EventRepo: GetByIdAsync(booking.EventId)
    EventRepo-->>Processing: Event or null

    alt event exists
        Processing->>Booking: Confirm()
        Processing->>BookingRepo: SaveChangesAsync()
    else event deleted
        Processing->>Booking: Reject()
        Processing->>BookingRepo: SaveChangesAsync()
    end
```

## 5. Test architecture

```mermaid
flowchart TD
    UnitTests[EventManagementService.API.Tests]
    IntegrationTests[EventManagementService.API.IntegrationTests]
    TestServer[TestServer API pipeline]
    Testcontainers[PostgreSQL Testcontainers]

    UnitTests --> Domain[Domain]
    UnitTests --> Application[Application]
    UnitTests --> Infrastructure[Infrastructure/InMemory]
    UnitTests --> Presentation[Presentation for HTTP pipeline]

    IntegrationTests --> Infrastructure
    IntegrationTests --> Testcontainers
    Testcontainers --> PostgreSQL[(PostgreSQL)]
    TestServer --> Presentation
```

## 6. EF Core migration flow

```mermaid
flowchart LR
    Developer[Developer]
    EF[dotnet ef]
    Infra[Infrastructure project]
    Presentation[Presentation startup project]
    Db[(PostgreSQL)]

    Developer --> EF
    EF --> Infra
    EF --> Presentation
    Infra --> Db
```

Команда:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/EventManagementService.Infrastructure/EventManagementService.Infrastructure.csproj \
  --startup-project src/EventManagementService.Presentation/EventManagementService.Presentation.csproj
```

---

[К началу sprint 7 docs](README.md)
