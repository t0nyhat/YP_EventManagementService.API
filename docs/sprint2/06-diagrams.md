# Диаграммы и визуализация sprint 2

В этом разделе приведены Mermaid-диаграммы, которые помогают быстро увидеть, как после sprint 2 устроены:

- слои приложения;
- обработка ошибок;
- фильтрация и пагинация;
- тестовый слой.

## 1. Общая архитектура после sprint 2

```mermaid
graph TD
    Client[HTTP клиент] --> Middleware[ExceptionHandlingMiddleware]
    Middleware --> Controller[EventsController]
    Controller --> Service[IEventService / EventService]
    Service --> Storage[List<Event> in memory]

    Controller --> DTO[DTO: Request/Response]
    Service --> Exceptions[BusinessValidationException / NotFoundException]
    Exceptions --> Middleware

    Tests[EventManagementService.API.Tests] --> Service
```

**Пояснение:**  
Клиент проходит через middleware, затем попадает в контроллер. Контроллер вызывает сервис. Сервис работает с in-memory хранилищем и при ошибках бросает доменные исключения, которые снова обрабатываются middleware. Тесты проверяют сервис напрямую.

## 2. Конвейер HTTP-запроса с ошибками

```mermaid
sequenceDiagram
    participant Client
    participant Middleware
    participant Controller
    participant Service

    Client->>Middleware: HTTP Request
    Middleware->>Controller: Передача запроса дальше
    Controller->>Service: Вызов бизнес-логики

    alt Успешный сценарий
        Service-->>Controller: Результат
        Controller-->>Middleware: HTTP 200/201/204
        Middleware-->>Client: Response
    else BusinessValidationException / NotFoundException / Exception
        Service-->>Controller: throw exception
        Controller-->>Middleware: exception bubbles up
        Middleware->>Middleware: log + map status + ProblemDetails
        Middleware-->>Client: application/problem+json
    end
```

**Пояснение:**  
Контроллер не занимается ручным `try/catch`. Исключение поднимается вверх по pipeline и обрабатывается единым middleware.

## 3. Отдельный путь model validation

```mermaid
sequenceDiagram
    participant Client
    participant ASP as ASP.NET Core Model Binding
    participant Program as InvalidModelStateResponseFactory
    participant Controller

    Client->>ASP: POST/PUT c DTO
    ASP->>ASP: DataAnnotations validation

    alt DTO невалиден
        ASP->>Program: InvalidModelStateResponseFactory
        Program-->>Client: 400 ValidationProblemDetails
    else DTO валиден
        ASP->>Controller: Передача в action
    end
```

**Пояснение:**  
Не все ошибки проходят через middleware. Ошибки model validation могут завершить запрос ещё до вызова action, поэтому для них нужен отдельный factory в `Program.cs`.

## 4. Фильтрация и пагинация в `EventService`

```mermaid
flowchart TD
    Start[GetEvents(query)] --> Validate[ValidateQuery]
    Validate --> Snapshot[Снять snapshot списка под lock]
    Snapshot --> Title{Title задан?}
    Title -->|Да| FilterTitle[Where Title Contains]
    Title -->|Нет| FromCheck
    FilterTitle --> FromCheck{From задан?}
    FromCheck -->|Да| FilterFrom[Where StartAt >= From]
    FromCheck -->|Нет| ToCheck
    FilterFrom --> ToCheck{To задан?}
    ToCheck -->|Да| FilterTo[Where EndAt <= To]
    ToCheck -->|Нет| Sort
    FilterTo --> Sort[OrderBy StartAt]
    Sort --> Count[Count filtered]
    Count --> SkipTake[Skip + Take]
    SkipTake --> Result[PaginatedResult]
```

**Пояснение:**  
Запрос строится поэтапно. Каждый фильтр применяется только при наличии параметра. В конце результат сортируется и только потом пагинируется.

## 5. Диаграмма классов ключевых компонентов sprint 2

```mermaid
classDiagram
    class EventsController {
        +GetAllEvents(GetEventsQuery)
        +GetEventById(Guid)
        +CreateEvent(CreateEventRequest)
        +UpdateEvent(Guid, UpdateEventRequest)
        +DeleteEvent(Guid)
        -MapToResponse(Event)
    }

    class IEventService {
        <<interface>>
        +GetAllEvents()
        +GetEvents(GetEventsQuery)
        +GetEventById(Guid)
        +CreateEvent(Event)
        +UpdateEvent(Guid, Event)
        +DeleteEvent(Guid)
    }

    class EventService {
        -List~Event~ _events
        -object _lock
        +GetAllEvents()
        +GetEvents(GetEventsQuery)
        +GetEventById(Guid)
        +CreateEvent(Event)
        +UpdateEvent(Guid, Event)
        +DeleteEvent(Guid)
        -ValidateEvent(Event)
        -ValidateQuery(GetEventsQuery)
    }

    class ExceptionHandlingMiddleware {
        +InvokeAsync(HttpContext)
        -WriteProblemDetailsAsync(HttpContext, Exception)
        -MapException(Exception)
    }

    class GetEventsQuery {
        +string? Title
        +DateTime? From
        +DateTime? To
        +int Page
        +int PageSize
    }

    class PaginatedResult~T~ {
        +Items
        +Page
        +Count
        +TotalCount
    }

    class BusinessValidationException
    class NotFoundException

    EventsController --> IEventService
    EventService ..|> IEventService
    EventsController --> GetEventsQuery
    EventService --> GetEventsQuery
    EventService --> PaginatedResult~T~
    EventService --> BusinessValidationException
    EventService --> NotFoundException
    ExceptionHandlingMiddleware --> BusinessValidationException
    ExceptionHandlingMiddleware --> NotFoundException
```

**Пояснение:**  
Диаграмма показывает, что логика распределена между контроллером, сервисом и middleware, а DTO и исключения играют роль контрактов между слоями.

## 6. Покрытие тестами

```mermaid
graph LR
    Tests[EventManagementService.API.Tests]
    Tests --> Crud[CRUD happy path]
    Tests --> Query[Filtering and pagination]
    Tests --> Validation[Failure and validation scenarios]

    Crud --> Create[CreateEvent]
    Crud --> Read[GetAllEvents / GetEventById]
    Crud --> Update[UpdateEvent]
    Crud --> Delete[DeleteEvent]

    Query --> TitleFilter[Title filter]
    Query --> DateFilter[Date filter]
    Query --> Combined[Combined filters]
    Query --> Paging[Paging]

    Validation --> NotFound[NotFoundException]
    Validation --> Rules[BusinessValidationException]
    Validation --> QueryValidation[Invalid page and pageSize]
```

**Пояснение:**  
Тесты покрывают не один узкий участок, а весь сервисный контракт sprint 2: операции, чтение списка и ошибки.

## 7. Жизненный цикл данных в памяти

```mermaid
stateDiagram-v2
    [*] --> Empty : Запуск приложения
    Empty --> HasEvents : CreateEvent
    HasEvents --> HasEvents : UpdateEvent
    HasEvents --> HasEvents : GetEvents / GetEventById
    HasEvents --> HasEvents : DeleteEvent (остались элементы)
    HasEvents --> Empty : DeleteEvent (удалён последний элемент)
    Empty --> [*] : Остановка приложения
```

**Пояснение:**  
Хранилище остаётся in-memory, поэтому все данные существуют только в течение жизни процесса.

## Как использовать диаграммы

1. Читать вместе с кодом в `Program.cs`, `EventService.cs` и `ExceptionHandlingMiddleware.cs`.
2. Открывать в Markdown preview с поддержкой Mermaid.
3. Использовать как быстрый “map of the system”, когда не хочется сразу читать исходники.

Диаграммы особенно полезны перед code review или перед повторным входом в проект после перерыва.

---

[Назад: Тестирование и запуск](05-testing-and-run.md) | [К оглавлению документации](README.md)
