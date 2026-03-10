# Диаграммы и визуализация

В этом разделе представлены диаграммы, которые помогают визуализировать архитектуру проекта, поток данных и взаимодействие компонентов. Диаграммы созданы с использованием языка [Mermaid](https://mermaid.js.org/), который поддерживается многими платформами (GitHub, GitLab, VS Code с плагином).

## 1. Общая архитектура слоёв

```mermaid
graph TD
    Client[Клиент (HTTP)] --> Controller[Контроллер EventsController]
    Controller --> Service[Сервис IEventService]
    Service --> Storage[In‑memory хранилище List<Event>]
    
    Controller --> DTO[DTO слой]
    DTO --> Client
    
    subgraph "Слои приложения"
        Controller
        Service
        Storage
        DTO
    end
```

**Пояснение:**  
Клиент отправляет HTTP-запросы к контроллеру. Контроллер использует сервис для выполнения бизнес-логики. Сервис работает с in‑memory хранилищем. Контроллер преобразует данные в DTO и возвращает клиенту.

## 2. Поток данных при создании события (POST)

```mermaid
sequenceDiagram
    participant Client
    participant Controller
    participant Service
    participant Storage

    Client->>Controller: POST /api/events (CreateEventRequest)
    Controller->>Controller: Валидация (DataAnnotations + бизнес-правила)
    Controller->>Controller: Маппинг CreateEventRequest → Event
    Controller->>Service: CreateEvent(event)
    Service->>Storage: Генерация Id, добавление в List<Event>
    Service-->>Controller: Event (с Id)
    Controller->>Controller: Маппинг Event → EventResponse
    Controller-->>Client: 201 Created (EventResponse + Location header)
```

**Пояснение:**  
Последовательность шагов от получения запроса до ответа. Обратите внимание на два этапа маппинга (DTO→Модель и Модель→DTO) и генерацию Id на стороне сервиса.

## 3. Взаимосвязь компонентов (зависимости)

```mermaid
classDiagram
    class EventsController {
        -IEventService _eventService
        +GetAllEvents()
        +GetEventById()
        +CreateEvent()
        +UpdateEvent()
        +DeleteEvent()
        -MapToResponse()
    }
    
    class IEventService {
        <<interface>>
        +GetAllEvents()
        +GetEventById()
        +CreateEvent()
        +UpdateEvent()
        +DeleteEvent()
    }
    
    class EventService {
        -List~Event~ _events
        -object _lock
        +GetAllEvents()
        +GetEventById()
        +CreateEvent()
        +UpdateEvent()
        +DeleteEvent()
    }
    
    class Event {
        +Guid Id
        +string Title
        +string? Description
        +DateTime StartAt
        +DateTime EndAt
    }
    
    class CreateEventRequest {
        +string Title
        +string? Description
        +DateTime? StartAt
        +DateTime? EndAt
    }
    
    class EventResponse {
        +Guid Id
        +string Title
        +string? Description
        +DateTime StartAt
        +DateTime EndAt
    }
    
    EventsController --> IEventService
    EventService ..|> IEventService
    EventService --> Event
    EventsController --> CreateEventRequest
    EventsController --> EventResponse
```

**Пояснение:**  
Диаграмма классов показывает зависимости между основными компонентами. Контроллер зависит от интерфейса `IEventService`, а `EventService` реализует этот интерфейс и работает с моделью `Event`. DTO используются только контроллером.

## 4. Конвейер HTTP-запросов (Middleware pipeline)

```mermaid
graph LR
    Request[HTTP Request] --> Swagger{Режим Development?}
    Swagger -->|Да| SwaggerMiddleware[Swagger/OpenAPI Middleware]
    Swagger -->|Нет| HttpsRedirect[HTTPS Redirection]
    SwaggerMiddleware --> HttpsRedirect
    HttpsRedirect --> Routing[Маршрутизация контроллеров]
    Routing --> Controller[EventsController]
    Controller --> Response[HTTP Response]
```

**Пояснение:**  
В development-режиме запрос сначала проходит через Swagger Middleware (для обслуживания UI и OpenAPI). Затем применяется перенаправление на HTTPS (если запрос пришёл по HTTP). Далее запрос маршрутизируется к соответствующему методу контроллера.

## 5. Состояние данных в памяти

```mermaid
stateDiagram-v2
    [*] --> Empty : Запуск приложения
    Empty --> HasEvents : Создание первого события
    HasEvents --> HasEvents : Добавление / обновление / удаление
    HasEvents --> Empty : Удаление всех событий
    Empty --> [*] : Остановка приложения
```

**Пояснение:**  
In‑memory хранилище начинается с пустого состояния. При создании событий переходит в состояние «есть события». Все операции CRUD происходят внутри этого состояния. Если все события удалены, хранилище снова становится пустым. При остановке приложения данные теряются.

## 6. Альтернативная архитектура (с репозиторием)

Для сравнения приведём диаграмму улучшенной архитектуры, которая могла бы быть использована в production:

```mermaid
graph TD
    Client --> Controller
    Controller --> Service
    Service --> IRepository[[IRepository<Event>]]
    IRepository --> InMemoryRepo[InMemoryRepository]
    IRepository --> DbRepo[DatabaseRepository]
    
    Service --> IUnitOfWork[[IUnitOfWork]]
    IUnitOfWork --> Transaction[Транзакции]
    
    Controller --> AutoMapper[[AutoMapper]]
    AutoMapper --> DTO
```

**Пояснение:**  
Введение репозитория абстрагирует доступ к данным, позволяя подменять реализацию (in‑memory, база данных). Unit of Work управляет транзакциями. AutoMapper автоматизирует маппинг между объектами.

## Как использовать эти диаграммы

1. **В VS Code** установите расширение "Markdown Preview Mermaid Support" для просмотра диаграмм прямо в preview.
2. **На GitHub** диаграммы Mermaid отображаются автоматически в файлах `.md`.
3. **Для презентаций** можно скопировать код диаграммы в [Mermaid Live Editor](https://mermaid.live/) и экспортировать как изображение.

Диаграммы помогают быстро понять структуру проекта, не погружаясь в код, и служат отличным дополнением к текстовой документации.

---

[К оглавлению документации](../README.md) | [Назад: Тестирование и запуск](05-testing-deployment.md)