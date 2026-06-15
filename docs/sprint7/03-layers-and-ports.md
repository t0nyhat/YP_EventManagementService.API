# Слои, порты и адаптеры

## 1. Domain layer

Domain отвечает за правила предметной области.

### `Event`

`Event` инкапсулирует:

- создание валидного события;
- обновление изменяемых полей;
- резервирование мест;
- освобождение мест.

Создание идет через фабричный метод:

```csharp
Event.Create(title, startAt, endAt, totalSeats, description)
```

Это не отдельный Factory-класс, а простой доменный factory method. Такой вариант лучше соответствует текущему размеру модели и не добавляет лишнюю абстракцию.

### `Booking`

`Booking` инкапсулирует:

- создание pending-брони;
- перевод в `Confirmed`;
- перевод в `Rejected`;
- запрет повторной обработки уже обработанной брони.

Создание идет через:

```csharp
Booking.CreatePending(eventId)
```

### Доменные исключения

В Domain находятся исключения, отражающие бизнес-состояния:

- `BusinessValidationException`;
- `NoAvailableSeatsException`;
- `NotFoundException`.

Presentation превращает их в HTTP-ответы, но сами исключения не знают об HTTP.

## 2. Application layer

Application содержит use cases.

### `EventService`

Отвечает за:

- получение списка событий с фильтрацией и пагинацией;
- получение события по id;
- создание события;
- обновление события;
- удаление события.

Сервис использует `IEventRepository`, а не EF Core.

### `BookingService`

Отвечает за:

- создание бронирования;
- атомарную проверку и резервирование места;
- получение бронирования по id.

Для защиты от конкурентного овербукинга используется `SemaphoreSlim` вокруг критической секции check-reserve-save.

### `BookingProcessingService`

Отвечает за обработку одной pending-брони:

- пропускает бронь, если она уже не `Pending`;
- отклоняет бронь, если событие удалено;
- подтверждает бронь при успешной обработке;
- при ошибке отклоняет бронь и освобождает место у события, если событие найдено.

Это бизнес-решения, поэтому они находятся в Application, а не в hosted service.

## 3. Ports

Порты определены в Application:

```text
Application/Abstractions/Repositories/
  IEventRepository.cs
  IBookingRepository.cs
```

Порт описывает, что нужно use case-слою:

- найти событие;
- добавить событие;
- удалить событие;
- найти бронь;
- получить pending booking ids;
- сохранить изменения.

Порт не описывает, как именно данные хранятся.

## 4. Infrastructure adapters

Infrastructure реализует порты:

```text
Infrastructure/Repositories/
  EventRepository.cs
  BookingRepository.cs
```

Эти классы являются Adapter-паттерном:

```text
Application port -> Infrastructure adapter -> EF Core/PostgreSQL
```

Репозитории не возвращают наружу `DbContext`, `DbSet` или `IQueryable`, чтобы EF Core не протекал во внутренние слои.

## 5. Presentation layer

Presentation содержит HTTP-адаптеры:

- controllers принимают route/query/body;
- вызывают Application-сервисы;
- маппят domain/application результат в HTTP response;
- не содержат бизнес-логики.

HTTP-specific типы (`ActionResult`, `ProblemDetails`, route attributes, status codes) остаются во внешнем слое.

---

[Далее: Реализация в коде →](04-implementation.md)
