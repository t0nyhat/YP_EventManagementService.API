# Тестирование и запуск sprint 8

## 1. Что покрыто тестами

Новые правила и сценарии авторизации закрыты на двух уровнях.

### Unit-тесты (`tests/EventManagementService.API.Tests`)

- **Домен:** создание брони с `userId`, отказ при пустом `userId`, отмена `Pending`/`Confirmed`, защита от повторной отмены (`BookingDomainRulesTests`); валидация `User.Create` (`UserTests`).
- **Бизнес-правила брони:** запрет брони прошедшего события, превышение лимита активных броней, **независимость лимитов разных пользователей**, отмена владельцем подтверждённой брони, отказ при отмене чужой брони (`BookingServiceTests`).
- **Сервис пользователей:** успешный вход с выдачей токена, регистронезависимый логин, `404` при неизвестном логине и неверном пароле, нормализация и хеширование при регистрации, отказ при дубликате логина (`UserServiceTests`).
- **Security-примитивы:** round-trip хеша и отклонение неверного пароля, состав claims в JWT (`SecurityPrimitivesTests`).
- **Чтение claims:** валидный/пустой/битый `NameIdentifier`, дефолт роли (`ClaimsPrincipalExtensionsTests`).

### Integration-тесты (`tests/EventManagementService.API.IntegrationTests`)

Поднимают реальный PostgreSQL через Testcontainers и проверяют схему и авторизацию end-to-end:

- `401` без токена на защищённых эндпоинтах;
- `403` для обычного пользователя на управлении событиями и при отмене чужой брони;
- успешная админская отмена;
- применение миграции на чистой базе, уникальность логина, внешний ключ.

## 2. Команды

Сборка:

```bash
dotnet build
```

Все unit-тесты:

```bash
dotnet test tests/EventManagementService.API.Tests/EventManagementService.API.Tests.csproj
```

Integration-тесты (нужен запущенный Docker — поднимется контейнер PostgreSQL):

```bash
dotnet test tests/EventManagementService.API.IntegrationTests/EventManagementService.API.IntegrationTests.csproj
```

Запуск приложения:

```bash
dotnet run --project src/EventManagementService.Presentation/EventManagementService.Presentation.csproj
```

При старте автоматически применяются миграции и засевается технический пользователь `system`.

## 3. Swagger и кнопка `Authorize`

В `AddSwaggerGen` добавлены security definition и requirement для схемы `Bearer`, поэтому в Swagger UI доступна кнопка **Authorize**. Порядок ручной проверки:

1. `POST /api/auth/register` — зарегистрировать пользователя (можно с `"role": "Admin"`).
2. `POST /api/auth/login` — получить JWT-токен.
3. Нажать **Authorize** и вставить `Bearer <jwt>`.
4. Вызывать защищённые эндпоинты уже с токеном.

## 4. Ручной end-to-end сценарий

Сценарий ниже проверяет роли, владение бронью и аутентификацию (значения кодов — ожидаемые):

| Шаг | Запрос | Ожидаемый код |
| --- | --- | --- |
| Регистрация | `POST /api/auth/register` | `204` |
| Логин | `POST /api/auth/login` | `200` + токен |
| Неверный пароль | `POST /api/auth/login` | `404` |
| Логин в верхнем регистре | `POST /api/auth/login` | `200` |
| Создание события админом | `POST /api/events` | `201` |
| Бронь без токена | `POST /api/events/{id}/book` | `401` |
| Бронь с токеном | `POST /api/events/{id}/book` | `202` |
| Чтение брони | `GET /api/bookings/{id}` | `200` |
| Обычный юзер создаёт событие | `POST /api/events` | `403` |
| Юзер отменяет чужую бронь | `DELETE /api/bookings/{id}` | `403` |
| Владелец отменяет свою бронь | `DELETE /api/bookings/{id}` | `204` |

Пример вызова через `curl` (получение токена и создание события):

```bash
B=http://localhost:5248
TOKEN=$(curl -s -X POST $B/api/auth/login -H 'Content-Type: application/json' \
  -d '{"login":"admin","password":"secret123"}' | sed -E 's/.*"token":"([^"]+)".*/\1/')

curl -s -X POST $B/api/events -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"title":"Demo","startAt":"2026-09-01T10:00:00Z","endAt":"2026-09-01T12:00:00Z","totalSeats":5}'
```

## 5. Критерии готовности спринта

- `dotnet build`, `dotnet run`, `dotnet test` отрабатывают без ошибок.
- Защищённые эндпоинты отклоняют запросы без токена (`401`).
- Управление событиями доступно только `Admin` (`403` для остальных).
- Отмена чужой брони без прав администратора возвращает `403`.
- Пароли хранятся хешем; неверный вход даёт единое сообщение и `404`.
- Новые доменные правила и сценарии авторизации покрыты тестами и проходят.

---

[Далее: Диаграммы →](06-diagrams.md)
