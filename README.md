# EventManagementService.API

Простой REST API для управления событиями (CRUD) на ASP.NET Core Web API.

## Требования

- .NET SDK 10.0+

## Запуск

```bash
dotnet restore
dotnet build
dotnet run
```

После запуска API смотрите адрес в консоли (например: `http://localhost:5248`).

## Swagger / OpenAPI

В режиме Development доступны:

- Swagger UI: `http://localhost:5248/swagger`
- OpenAPI JSON: `http://localhost:5248/openapi/v1.json`

## Эндпоинты

Базовый префикс: `/api/events`

- `GET /api/events` — получить список событий
- `GET /api/events/{id}` — получить событие по id
- `POST /api/events` — создать событие
- `PUT /api/events/{id}` — обновить событие
- `DELETE /api/events/{id}` — удалить событие

## Пример тела запроса (POST/PUT)

```json
{
  "title": "Бронирование",
  "description": "Домик №4",
  "startAt": "2026-01-15T10:00:00",
  "endAt": "2026-01-16T17:00:00"
}
```

## Валидация

- Обязательные поля: `title`, `startAt`, `endAt`
- `endAt` должен быть позже `startAt`

## Хранение данных

Данные хранятся в памяти приложения (in-memory) и очищаются при перезапуске.