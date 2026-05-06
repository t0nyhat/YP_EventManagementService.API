# EventManagementService.API

REST API для управления событиями на ASP.NET Core Web API.

## Требования

- .NET SDK 10.0+

## Запуск

```bash
dotnet restore
dotnet build
dotnet run --project src/EventManagementService.API/EventManagementService.API.csproj
```

## Запуск тестов

```bash
dotnet test EventManagementService.API.sln
```

## Swagger / OpenAPI

В режиме `Development` доступны:

- Swagger UI: `http://localhost:5248/swagger`
- OpenAPI JSON: `http://localhost:5248/openapi/v1.json`

## Эндпоинты

Эндпоинты событий:

- `GET /api/events` — получить список событий с фильтрацией и пагинацией
- `GET /api/events/{id}` — получить событие по `id`
- `POST /api/events` — создать событие
- `PUT /api/events/{id}` — обновить событие
- `DELETE /api/events/{id}` — удалить событие
- `POST /api/events/{id}/book` — создать бронирование для события

Эндпоинты бронирований:

- `GET /api/bookings/{id}` — получить текущее состояние бронирования по `id`

## Фильтрация и пагинация

`GET /api/events` поддерживает query-параметры:

- `title` — поиск по названию, регистронезависимый, частичное совпадение
- `from` — вернуть события, которые начинаются не раньше указанной даты
- `to` — вернуть события, которые заканчиваются не позже указанной даты
- `page` — номер страницы, по умолчанию `1`
- `pageSize` — размер страницы, по умолчанию `10`

Пример запроса:

```http
GET /api/events?title=dotnet&from=2026-05-01T00:00:00&page=1&pageSize=2
```

Пример ответа:

```json
{
  "items": [
    {
      "id": "0c6bbd2b-4f64-4fb9-8d73-dcd7f6f36611",
      "title": "DotNet Advanced",
      "description": "Продвинутый курс",
      "startAt": "2026-05-02T10:00:00",
      "endAt": "2026-05-02T13:00:00",
      "totalSeats": 50,
      "availableSeats": 47
    },
    {
      "id": "7a5fd9f7-6425-4f31-9dd4-2597f431ce92",
      "title": "DotNet Meetup",
      "description": "Встреча сообщества",
      "startAt": "2026-05-04T18:00:00",
      "endAt": "2026-05-04T20:00:00",
      "totalSeats": 100,
      "availableSeats": 100
    }
  ],
  "page": 1,
  "count": 2,
  "totalCount": 2
}
```

## Пример тела запроса

`POST /api/events` принимает тело:

```json
{
  "title": "Конференция .NET",
  "description": "Технологическое мероприятие",
  "startAt": "2026-04-10T10:00:00",
  "endAt": "2026-04-10T18:00:00",
  "totalSeats": 50
}
```

`PUT /api/events/{id}` принимает тело без поля `totalSeats` — вместимость не меняется при обновлении:

```json
{
  "title": "Конференция .NET (обновлено)",
  "description": "Технологическое мероприятие",
  "startAt": "2026-04-10T10:00:00",
  "endAt": "2026-04-10T18:00:00"
}
```

`POST /api/events/{id}/book` не принимает JSON-тело. Идентификатор события передаётся через route-параметр `{id}`.

## Бронирования

Модель `Booking` содержит поля:

- `id` — идентификатор бронирования
- `eventId` — идентификатор события
- `status` — статус бронирования
- `createdAt` — время создания
- `processedAt` — время обработки, `null` до завершения фоновой обработки

Статусы `BookingStatus`:

- `Pending` — бронирование создано и ожидает обработки
- `Confirmed` — бронирование подтверждено
- `Rejected` — бронирование отклонено

Пример ответа для `POST /api/events/{id}/book` и `GET /api/bookings/{id}`:

```json
{
  "id": "5b178c2f-247d-4e6f-bf64-c40aeb9f95ef",
  "eventId": "0c6bbd2b-4f64-4fb9-8d73-dcd7f6f36611",
  "status": "Pending",
  "createdAt": "2026-04-03T12:00:00Z",
  "processedAt": null
}
```

`POST /api/events/{id}/book` возвращает:

- `202 Accepted`
- тело с созданным бронированием
- заголовок `Location` со ссылкой на ресурс бронирования: `/api/bookings/{bookingId}`

## Вместимость события

Каждое событие имеет ограниченное количество мест:

- `totalSeats` — общая вместимость, задаётся при создании, не меняется при обновлении
- `availableSeats` — текущее количество свободных мест; при создании равно `totalSeats`, уменьшается с каждым успешным бронированием

При создании бронирования место резервируется атомарно. Если свободных мест нет, API возвращает `409 Conflict`.

## Валидация

- обязательные поля при создании события: `title`, `startAt`, `endAt`, `totalSeats`
- `title` не должен быть пустым или состоять только из пробелов
- `endAt` должен быть позже `startAt`
- `totalSeats` должен быть больше нуля
- `from` не должен быть позже `to`
- `page` должен быть не меньше `1`
- `pageSize` должен быть в диапазоне от `1` до `100`

## Обработка ошибок

Приложение использует глобальный middleware и возвращает ошибки в формате `ProblemDetails` (`application/problem+json`).

Коды ответа:

- `400 Bad Request` — ошибки валидации
- `404 Not Found` — ресурс не найден
- `409 Conflict` — нет свободных мест на событие
- `500 Internal Server Error` — непредвиденная ошибка

Пример ответа при исчерпании мест:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "Нет свободных мест на данное событие.",
  "instance": "/api/events/0c6bbd2b-4f64-4fb9-8d73-dcd7f6f36611/book",
  "traceId": "00-5c5f0e5f8b6dd5f1d8e51051a335f12e-8aef5f5ec0c86219-00"
}
```

Пример ответа при ошибке:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Resource not found",
  "status": 404,
  "detail": "Событие с id 8a1d2c54-0c43-4db6-bd7f-0e6d6f9191f8 не найдено.",
  "instance": "/api/events/8a1d2c54-0c43-4db6-bd7f-0e6d6f9191f8",
  "traceId": "00-5c5f0e5f8b6dd5f1d8e51051a335f12e-8aef5f5ec0c86219-00"
}
```

## Фоновая обработка бронирований

После создания бронирование попадает в in-memory хранилище со статусом `Pending`.

Фоновый сервис `BookingProcessingBackgroundService`:

- периодически получает список `Pending`-броней
- запускает обработку всех броней **параллельно** через `Task.WhenAll`
- для каждой брони выполняет искусственную задержку `Task.Delay(2s)` вне семафора — задержки перекрываются
- запись в хранилище (изменение статуса) **сериализована** через `SemaphoreSlim(1,1)` — исключает race condition при сохранении
- если событие было удалено до обработки — бронь переводится в `Rejected`
- если в процессе обработки возникла непредвиденная ошибка — бронь переводится в `Rejected`, занятое место возвращается через `ReleaseSeats`
- `OperationCanceledException` при остановке сервиса обрабатывается корректно

При создании бронирования `BookingService` защищает критическую секцию через `lock`:
- получение события
- резервирование места через `TryReserveSeats()`
- сохранение брони

Это исключает овербукинг при конкурентных запросах.

## Пример сценария: успешное бронирование

1. Создать событие через `POST /api/events` с `totalSeats: 3`.
2. Создать бронирование через `POST /api/events/{id}/book`.
3. Получить `202 Accepted` и `Location: /api/bookings/{bookingId}`.
4. Сразу вызвать `GET /api/bookings/{bookingId}` — увидеть статус `Pending`.
5. Подождать несколько секунд и повторить `GET /api/bookings/{bookingId}`.
6. Убедиться, что статус изменился на `Confirmed`, а `processedAt` заполнено.

## Пример сценария: овербукинг

1. Создать событие с `totalSeats: 3`.
2. Создать три бронирования — все вернут `202 Accepted`.
3. Попытаться создать четвёртое бронирование.
4. Получить `409 Conflict` с телом:
   ```json
   {
     "status": 409,
     "title": "Conflict",
     "detail": "Нет свободных мест на данное событие."
   }
   ```
5. Проверить, что у события `availableSeats: 0`.

## Хранение данных

Данные о событиях и бронированиях хранятся в памяти приложения и очищаются при перезапуске.
