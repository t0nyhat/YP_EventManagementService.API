# Архитектура решения sprint 8

Sprint 8 не вводит новых проектов — он добавляет новые типы в существующие четыре слоя из sprint 7 и сохраняет прежнее направление зависимостей. Ниже — где живёт каждый кусок и почему именно там.

## 1. Domain — правила и инварианты

`EventManagementService.Domain` пополнился:

- `UserRole` — перечисление ролей (`User`, `Admin`);
- `User` — сущность с `Id`, `Login`, `PasswordHash`, `Role` и фабрикой `Create`;
- `Booking.UserId` — связь брони с пользователем;
- `BookingStatus.Cancelled` — новый статус;
- `Booking.Cancel(...)` — операция отмены с защитой от повторной отмены;
- доменные исключения: `BookingInPastException`, `TooManyActiveBookingsException`, `ForbiddenOperationException`, `BookingAlreadyProcessedException`.

Domain по-прежнему **не имеет** зависимостей от ASP.NET Core, EF Core или JWT-библиотек. Хранение пароля как **хеша** — это поле `PasswordHash`; сам алгоритм хеширования домену не известен.

## 2. Application — use cases, порты и контракты безопасности

`EventManagementService.Application` содержит:

- обновлённый `BookingService` (принимает `userId`, проверяет прошлое событие, лимит, права на отмену);
- новый `UserService` (регистрация и вход);
- порт `IUserRepository`;
- абстракции безопасности `IPasswordHasher`, `IJwtTokenGenerator`;
- DTO регистрации/логина (`RegisterUserRequest`, `LoginUserRequest`, `LoginResponse`).

Ключевое архитектурное решение: **бизнес-решения о доступе принимаются здесь, а не в контроллерах**. Например, проверка «владелец или админ» живёт в `BookingService.CancelBookingAsync`, поэтому правило соблюдается независимо от того, какой контроллер или тест вызвал use case.

Application зависит только от Domain и не знает ни про `AppDbContext`, ни про `JwtSecurityToken`, ни про HTTP.

## 3. Infrastructure — адаптеры и реализация безопасности

`EventManagementService.Infrastructure` содержит:

- `UserRepository` — адаптер порта `IUserRepository` к EF Core;
- `UserConfiguration` — маппинг `User` (таблица `users`, уникальный индекс на `login`, роль строкой);
- обновлённую `BookingConfiguration` (внешний ключ `bookings.user_id → users.id`);
- новую миграцию `Sprint8AddUsersAndBookingUserId`;
- `Sha256PasswordHasher` — реализация `IPasswordHasher`;
- `JwtTokenGenerator` — реализация `IJwtTokenGenerator`;
- `JwtOptions` — типизированные параметры токена из конфигурации.

Реализации security вынесены в Infrastructure намеренно: они зависят от внешних библиотек (`System.IdentityModel.Tokens.Jwt`, `System.Security.Cryptography`), а Application должен оставаться чистым.

## 4. Presentation — auth-пайплайн и edge-маппинг

`EventManagementService.Presentation` содержит:

- `AuthController` (`/api/auth/register`, `/api/auth/login`);
- `[Authorize]` на эндпоинтах броней и `[Authorize(Roles = "Admin")]` на управлении событиями;
- `ClaimsPrincipalExtensions` — чтение `userId` и роли из claims в одном месте;
- настройку JWT Bearer в `Program.cs` (валидация issuer/audience/ключа/времени жизни);
- `ExceptionHandlingMiddleware` — единственная точка, где доменные исключения превращаются в HTTP-коды;
- Swagger с security definition (кнопка `Authorize`).

Presentation — внешний слой, поэтому именно он собирает identity из HTTP-запроса и отдаёт её в Application как простые значения (`Guid userId`, `UserRole role`).

## 5. Направление зависимостей не изменилось

```text
Domain
  no project references

Application
  -> Domain

Infrastructure
  -> Application
  -> Domain

Presentation
  -> Application
  -> Infrastructure
```

Новые типы аккуратно «легли» в эту схему:

- абстракции `IPasswordHasher` / `IJwtTokenGenerator` — в Application;
- их реализации — в Infrastructure;
- значит `Application` по-прежнему не зависит от `Infrastructure`.

Проверка отсутствия запрещённой зависимости:

```bash
rg "Infrastructure" src/EventManagementService.Application
```

Ожидаемый результат — нет совпадений.

## 6. Где «секрет» и почему он в конфигурации

Подписывающий ключ JWT, issuer, audience и время жизни вынесены в `appsettings.json` (секция `Jwt`) и читаются через `JwtOptions`. Захардкоженный в коде секрет формально работал бы, но нарушал бы требование задания и плохо масштабировался бы на окружения. В проде значение переопределяется переменной окружения `Jwt__SigningKey` без пересборки.

## 7. Где принимаются решения и где они становятся кодами ответа

| Решение | Где принимается | Как становится HTTP-кодом |
| --- | --- | --- |
| Прошлое событие | `BookingService` → `BookingInPastException` | middleware → `400` |
| Превышен лимит | `BookingService` → `TooManyActiveBookingsException` | middleware → `409` |
| Нет прав на отмену | `BookingService` → `ForbiddenOperationException` | middleware → `403` |
| Повторная отмена | `Booking.Cancel` → `BookingAlreadyProcessedException` | middleware → `409` |
| Неверные креды | `UserService` → `NotFoundException` | middleware → `404` |
| Нет/невалидный токен | JWT middleware | `401` |
| Не та роль | `[Authorize(Roles)]` | `403` |

---

[Далее: Доменные правила и компоненты безопасности →](03-domain-rules-and-security.md)
