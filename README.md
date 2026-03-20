# EventManagementService.API

REST API для управления событиями на ASP.NET Core Web API.

## Требования

- .NET SDK 10.0+

## Запуск

```bash
dotnet restore
dotnet build
dotnet run
```

## Запуск тестов

```bash
dotnet test
```

## Swagger / OpenAPI

В режиме `Development` доступны:

- Swagger UI: `http://localhost:5248/swagger`
- OpenAPI JSON: `http://localhost:5248/openapi/v1.json`

## Эндпоинты

Базовый префикс: `/api/events`

- `GET /api/events` — получить список событий с фильтрацией и пагинацией
- `GET /api/events/{id}` — получить событие по `id`
- `POST /api/events` — создать событие
- `PUT /api/events/{id}` — обновить событие
- `DELETE /api/events/{id}` — удалить событие

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
      "endAt": "2026-05-02T13:00:00"
    },
    {
      "id": "7a5fd9f7-6425-4f31-9dd4-2597f431ce92",
      "title": "DotNet Meetup",
      "description": "Встреча сообщества",
      "startAt": "2026-05-04T18:00:00",
      "endAt": "2026-05-04T20:00:00"
    }
  ],
  "page": 1,
  "count": 2,
  "totalCount": 2
}
```

## Пример тела запроса

`POST /api/events` и `PUT /api/events/{id}` принимают тело:

```json
{
  "title": "Конференция .NET",
  "description": "Технологическое мероприятие",
  "startAt": "2026-04-10T10:00:00",
  "endAt": "2026-04-10T18:00:00"
}
```

## Валидация

- обязательные поля: `title`, `startAt`, `endAt`
- `title` не должен быть пустым или состоять только из пробелов
- `endAt` должен быть позже `startAt`
- `page` должен быть не меньше `1`
- `pageSize` должен быть в диапазоне от `1` до `100`

## Обработка ошибок

Приложение использует глобальный middleware и возвращает ошибки в формате `ProblemDetails` (`application/problem+json`).

Коды ответа:

- `400 Bad Request` — ошибки валидации
- `404 Not Found` — ресурс не найден
- `500 Internal Server Error` — непредвиденная ошибка

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

## Хранение данных

Данные хранятся в памяти приложения и очищаются при перезапуске.
