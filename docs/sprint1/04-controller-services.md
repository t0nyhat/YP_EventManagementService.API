# Контроллер и сервисы

В этом разделе детально разбираются два ключевых компонента приложения: контроллер (`EventsController`) и сервис (`EventService`). Рассматриваются их обязанности, взаимодействие, применяемые паттерны и причины выбора конкретных реализаций.

## Контроллер (`EventsController`)

Контроллер находится в файле `Controllers/EventsController.cs` и является единственной точкой входа для HTTP-запросов, связанных с событиями.

### Обязанности контроллера

1. **Маршрутизация** — определение того, какой метод должен быть вызван для данного URL и HTTP-метода (через атрибуты `[HttpGet]`, `[HttpPost]` и т.д.).
2. **Валидация входных данных** — проверка корректности DTO (с помощью атрибутов `[Required]` и ручных проверок).
3. **Преобразование данных** — маппинг между DTO и доменной моделью (и наоборот).
4. **Вызов бизнес-логики** — делегирование операций сервисному слою (`IEventService`).
5. **Формирование HTTP-ответа** — возврат соответствующих кодов состояния (200, 201, 400, 404) и тел ответов.

### Структура контроллера

Контроллер наследует от `ControllerBase` (базовый класс для API-контроллеров) и использует **внедрение зависимостей через конструктор**:

```csharp
public EventsController(IEventService eventService) : ControllerBase
```

Это обеспечивает слабую связность и упрощает тестирование.

### Методы контроллера

#### 1. `GetAllEvents`

```csharp
[HttpGet]
public ActionResult<IEnumerable<EventResponse>> GetAllEvents()
{
    var events = eventService.GetAllEvents();
    var response = events.Select(MapToResponse).ToArray();
    return Ok(response);
}
```

- **HTTP-метод**: GET
- **Маршрут**: `/api/events`
- **Конструкция метода**: `public ActionResult<IEnumerable<EventResponse>> GetAllEvents()`
  - `ActionResult<T>` — обёртка, позволяющая возвращать как данные (`T`), так и HTTP-статус (через `Ok()`, `NotFound()` и т.д.).
  - `IEnumerable<EventResponse>` — тип данных, возвращаемых в теле ответа при успешном выполнении.
- **Возвращает**: массив `EventResponse` с кодом 200.
- **Особенности**:  
  - Не принимает параметров.  
  - Преобразует каждую доменную модель в DTO ответа.  
  - Использует `.ToArray()` для материализации коллекции (альтернативно можно вернуть `IEnumerable<EventResponse>` для ленивого выполнения).

#### 2. `GetEventById`

```csharp
[HttpGet("{id:guid}")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public ActionResult<EventResponse> GetEventById(Guid id)
{
    var eventItem = eventService.GetEventById(id);
    if (eventItem is null)
    {
        return NotFound(new { message = $"Событие с id {id} не найдено." });
    }
    return Ok(MapToResponse(eventItem));
}
```

- **Параметр**: `id` типа `Guid` (валидируется framework’ом как корректный GUID).
- **Конструкция метода**: `public ActionResult<EventResponse> GetEventById(Guid id)`
  - `ActionResult<EventResponse>` — указывает, что метод возвращает либо `EventResponse` (с кодом 200), либо другой статус (404).
  - Параметр `id` извлекается из маршрута благодаря шаблону `{id:guid}` в атрибуте `[HttpGet]`.
- **Атрибуты `[ProducesResponseType]`** используются для документирования возможных ответов в Swagger.
- **Логика**: если сервис возвращает `null`, контроллер отвечает 404 с понятным сообщением.

#### 3. `CreateEvent`

```csharp
[HttpPost]
[ProducesResponseType(StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public ActionResult<EventResponse> CreateEvent([FromBody] CreateEventRequest request)
{
    // Дополнительная валидация: EndAt должен быть после StartAt
    if (request.EndAt <= request.StartAt)
    {
        return BadRequest(new { message = "Дата окончания должна быть позже даты начала события." });
    }

    var eventItem = new Event
    {
        Title = request.Title,
        Description = request.Description,
        StartAt = request.StartAt!.Value,
        EndAt = request.EndAt!.Value
    };

    var createdEvent = eventService.CreateEvent(eventItem);
    var response = MapToResponse(createdEvent);
    return CreatedAtAction(nameof(GetEventById), new { id = createdEvent.Id }, response);
}
```

- **HTTP-метод**: POST
- **Конструкция метода**: `public ActionResult<EventResponse> CreateEvent([FromBody] CreateEventRequest request)`
  - `[FromBody]` указывает, что параметр `request` извлекается из тела HTTP-запроса (в формате JSON).
  - `ActionResult<EventResponse>` — возвращает созданное событие с кодом 201 (Created) или ошибку 400.
- **Тело запроса**: `CreateEventRequest` (валидируется автоматически).
- **Кастомная валидация**: проверка, что `EndAt > StartAt`. Если нарушено — возвращается 400.
- **Статус 201 Created**: используется `CreatedAtAction`, который:
  - Возвращает код 201.
  - Добавляет в заголовок `Location` URL созданного ресурса (например, `/api/events/{id}`).
  - Возвращает в теле ответа созданное событие.

#### 4. `UpdateEvent`

```csharp
[HttpPut("{id:guid}")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public ActionResult<EventResponse> UpdateEvent(Guid id, [FromBody] UpdateEventRequest request)
{
    // Сначала проверяем существование ресурса
    var existingEvent = eventService.GetEventById(id);
    if (existingEvent is null)
    {
        return NotFound(new { message = $"Событие с id {id} не найдено." });
    }

    // Затем валидируем данные
    if (request.EndAt <= request.StartAt)
    {
        return BadRequest(new { message = "Дата окончания должна быть позже даты начала события." });
    }

    var eventItem = new Event
    {
        Title = request.Title,
        Description = request.Description ?? existingEvent.Description,
        StartAt = request.StartAt!.Value,
        EndAt = request.EndAt!.Value
    };

    var updatedEvent = eventService.UpdateEvent(id, eventItem);
    return Ok(MapToResponse(updatedEvent!));
}
```

- **Конструкция метода**: `public ActionResult<EventResponse> UpdateEvent(Guid id, [FromBody] UpdateEventRequest request)`
  - `ActionResult<EventResponse>` — возвращает обновлённое событие с кодом 200 или ошибку (400, 404).
  - Параметр `id` извлекается из маршрута, `request` — из тела запроса.
- **Подход «сначала существование, потом валидация»**: если ресурс не найден, возвращается 404, и дальнейшая проверка данных не выполняется. Это экономит процессорное время и соответствует принципу «быстрый отказ» (fail-fast).
- **Сохранение описания**: если `Description` не передан (`null`), используется существующее значение (`existingEvent.Description`). Это позволяет частично обновлять ресурс (хотя формально PUT предполагает полную замену, в учебном проекте сделано упрощение).

#### 5. `DeleteEvent`

```csharp
[HttpDelete("{id:guid}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public ActionResult DeleteEvent(Guid id)
{
    var isDeleted = eventService.DeleteEvent(id);
    if (!isDeleted)
    {
        return NotFound(new { message = $"Событие с id {id} не найдено." });
    }
    return NoContent();
}
```

- **Конструкция метода**: `public ActionResult DeleteEvent(Guid id)`
  - `ActionResult` (без generic) используется, когда метод не возвращает данные в теле ответа (например, при коде 204 NoContent).
  - Параметр `id` извлекается из маршрута.
- **Статус 204 No Content**: при успешном удалении тело ответа отсутствует.
- **Логика**: сервис возвращает `bool`, что позволяет отличить «удалено» от «не найдено».

### Приватный метод `MapToResponse`

```csharp
private static EventResponse MapToResponse(Event eventItem)
{
    return new EventResponse
    {
        Id = eventItem.Id,
        Title = eventItem.Title,
        Description = eventItem.Description,
        StartAt = eventItem.StartAt,
        EndAt = eventItem.EndAt
    };
}
```

Вынесен в отдельный метод для избежания дублирования кода. Является статическим, т.к. не зависит от состояния контроллера.

## Сервис (`EventService`)

Сервис находится в файле `Services/EventService.cs` и реализует интерфейс `IEventService`. Это **единственный компонент, который работает с данными**.

### Обязанности сервиса

1. **Управление состоянием** — хранение коллекции событий в памяти.
2. **Реализация бизнес-логики** — генерация Id, обновление полей, проверка условий (хотя в данном проекте большая часть проверок вынесена в контроллер).
3. **Обеспечение потокобезопасности** — использование `lock` для защиты коллекции от одновременных модификаций.
4. **Абстракция доступа к данным** — если в будущем потребуется заменить in‑memory хранилище на базу данных, изменения будут локализованы в этом классе.

### Внутреннее устройство

#### Хранилище

```csharp
private readonly List<Event> _events = [];
private readonly object _lock = new object();
```

- `_events` — обычный `List<Event>`. Данные теряются при перезапуске приложения.
- `_lock` — объект для синхронизации. Все публичные методы оборачивают работу с `_events` в `lock (_lock)`.

#### Методы сервиса

##### `GetAllEvents`

```csharp
public IEnumerable<Event> GetAllEvents()
{
    lock (_lock)
    {
        return _events.ToList();
    }
}
```

Возвращает **копию** списка (`ToList()`), чтобы внешний код не мог изменить внутреннюю коллекцию.

##### `GetEventById`

```csharp
public Event? GetEventById(Guid id)
{
    lock (_lock)
    {
        return _events.FirstOrDefault(item => item.Id == id);
    }
}
```

Использует `FirstOrDefault`, возвращает `null` если событие не найдено.

##### `CreateEvent`

```csharp
public Event CreateEvent(Event newEvent)
{
    lock (_lock)
    {
        newEvent.Id = Guid.NewGuid();
        _events.Add(newEvent);
        return newEvent;
    }
}
```

- **Генерация Id** — всегда выполняется на сервере, игнорируя возможное значение `newEvent.Id` (если бы клиент его указал).
- **Добавление в коллекцию** — после генерации Id событие добавляется в `_events`.

##### `UpdateEvent`

```csharp
public Event? UpdateEvent(Guid id, Event updatedEvent)
{
    lock (_lock)
    {
        var existingEvent = _events.FirstOrDefault(item => item.Id == id);
        if (existingEvent is null)
        {
            return null;
        }

        existingEvent.Title = updatedEvent.Title;
        existingEvent.Description = updatedEvent.Description;
        existingEvent.StartAt = updatedEvent.StartAt;
        existingEvent.EndAt = updatedEvent.EndAt;

        return existingEvent;
    }
}
```

- **Поиск по Id** — если событие не найдено, возвращается `null`.
- **Изменение полей** — обновляются только mutable свойства (`Title`, `Description`, `StartAt`, `EndAt`). `Id` остаётся неизменным.
- **Возврат** — возвращается обновлённый объект (тот же экземпляр, что хранится в коллекции).

##### `DeleteEvent`

```csharp
public bool DeleteEvent(Guid id)
{
    lock (_lock)
    {
        var existingEvent = _events.FirstOrDefault(item => item.Id == id);
        if (existingEvent is null)
        {
            return false;
        }
        return _events.Remove(existingEvent);
    }
}
```

- `Remove` возвращает `true` если элемент был удалён. В данном случае это всегда `true`, т.к. элемент найден.
- Возврат `false` означает, что события с таким Id не существовало.

### Почему сервис не использует исключения?

Вместо выбрасывания исключений при отсутствии ресурса сервис возвращает `null` или `false`. Это сознательное решение:

- **Исключения — для исключительных ситуаций**. Отсутствие события при запросе по Id — это ожидаемый сценарий (клиент мог ошибиться), а не ошибка в работе приложения.
- **Упрощение кода контроллера**. Контроллер может просто проверить `null` и вернуть 404, не занимаясь обработкой исключений.
- **Производительность**. Выбрасывание и перехват исключений дороже, чем возврат `null`.

Если бы требовалось сообщить о нарушении бизнес-правил (например, попытка создать событие с уже существующим Id), можно было бы использовать исключения типа `InvalidOperationException`.

## Взаимодействие контроллера и сервиса

1. Контроллер получает HTTP-запрос.
2. Framework валидирует DTO (если есть тело) и привязывает параметры.
3. Контроллер выполняет дополнительную валидацию (бизнес-правила).
4. Контроллер преобразует DTO в доменную модель.
5. Контроллер вызывает соответствующий метод сервиса.
6. Сервис работает с данными (в памяти) и возвращает результат.
7. Контроллер преобразует результат в DTO ответа и формирует HTTP-ответ.

Такое разделение позволяет:

- **Тестировать сервис отдельно от HTTP-контекста** (unit-тесты).
- **Заменять реализацию сервиса** (например, на версию с базой данных) без изменения контроллера.
- **Легко добавлять новые endpoint’ы**, повторно используя существующие методы сервиса.

## Альтернативные подходы

- **Использование MediatR** — можно было бы отправлять команды и запросы через MediatR, что полностью отделило бы контроллер от сервиса. Это увеличило бы гибкость, но добавило бы сложности.
- **Реализация репозитория** — выделить слой доступа к данным (`IRepository<Event>`) и инкапсулировать в нём работу с коллекцией. Сервис тогда работал бы с репозиторием, а не напрямую с `List<Event>`.
- **Асинхронные методы** — все методы сервиса и контроллера можно сделать `async`/`await`, даже если они работают с памятью. Это подготовило бы приложение к переходу на базу данных.

В учебном проекте эти усложнения не применялись, чтобы сохранить фокус на основных концепциях.

---

[Далее: Тестирование и запуск →](05-testing-deployment.md)