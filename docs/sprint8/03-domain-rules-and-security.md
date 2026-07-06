# Доменные правила и компоненты безопасности sprint 8

Этот документ описывает «ядро» спринта: новые доменные типы и правила, а также два независимых security-компонента — хеширование паролей и генерацию JWT.

## 1. Роли: `UserRole`

```csharp
public enum UserRole
{
    User,
    Admin
}
```

Две роли — минимум, который требует задание. `User` бронирует и отменяет свои брони, `Admin` дополнительно управляет событиями и отменяет любые брони.

## 2. Сущность `User`

`User` следует тем же принципам инкапсуляции, что и `Event`/`Booking`: приватные сеттеры, приватный конструктор, создание через фабрику.

```csharp
public static User Create(string login, string passwordHash, UserRole role = UserRole.User, Guid? id = null)
{
    if (string.IsNullOrWhiteSpace(login))
        throw new BusinessValidationException("Логин пользователя не должен быть пустым.");
    if (string.IsNullOrWhiteSpace(passwordHash))
        throw new BusinessValidationException("Хеш пароля пользователя не должен быть пустым.");

    return new User(id ?? Guid.NewGuid(), login.Trim(), passwordHash, role);
}
```

Важные решения:

- хранится **хеш** пароля (`PasswordHash`), а не пароль — домен принципиально не работает с открытым паролем;
- `Create` принимает уже готовый хеш: хеширование — задача Application/Infrastructure, а не домена;
- `SystemUserId` — зарезервированный технический пользователь, к которому привязываются ранее созданные брони при миграции (см. [04-implementation.md](04-implementation.md)).

## 3. Связь брони с пользователем

`Booking` получил поле `UserId`, а фабрика `CreatePending` теперь требует идентификатор пользователя:

```csharp
public static Booking CreatePending(Guid eventId, Guid userId, DateTime? createdAt = null)
{
    if (eventId == Guid.Empty)
        throw new ArgumentException("Идентификатор события должен быть указан.", nameof(eventId));
    if (userId == Guid.Empty)
        throw new ArgumentException("Идентификатор пользователя должен быть указан.", nameof(userId));
    // ...
}
```

Пустой `userId` — это ошибка программиста (контроллер обязан подставить пользователя из claims), поэтому здесь `ArgumentException`, а не бизнес-исключение.

## 4. Статус `Cancelled` и политика отмены

В `BookingStatus` добавлено значение `Cancelled`. Метод отмены устроен так:

```csharp
public void Cancel(DateTime? processedAt = null)
{
    if (Status is BookingStatus.Rejected or BookingStatus.Cancelled)
        throw new BookingAlreadyProcessedException(
            "Отмена недоступна для бронирования в текущем статусе.");

    Status = BookingStatus.Cancelled;
    ProcessedAt = processedAt ?? DateTime.UtcNow;
}
```

Решение по политике отмены:

- отменить можно бронь в статусе **`Pending` или `Confirmed`** — это естественный сценарий «передумал, освобождаю место»;
- **`Rejected` и `Cancelled` отменить нельзя** — это защита от повторной отмены и от отмены уже отклонённой брони;
- запрет выражен отдельным доменным исключением `BookingAlreadyProcessedException`, а не общим `InvalidOperationException` — чтобы на краю системы его можно было однозначно отобразить в `409 Conflict`.

## 5. Доменные исключения новых правил

Все новые исключения наследуют `BusinessValidationException`, но несут **разную** семантику кода ответа:

| Исключение | Когда | HTTP |
| --- | --- | --- |
| `BookingInPastException` | событие уже началось | `400` |
| `TooManyActiveBookingsException(limit)` | превышен лимит активных броней | `409` |
| `ForbiddenOperationException` | нет прав на операцию | `403` |
| `BookingAlreadyProcessedException` | отмена недоступна в текущем статусе | `409` |

`TooManyActiveBookingsException` включает само значение лимита в сообщение:

```csharp
public TooManyActiveBookingsException(int limit)
    : base($"Превышен лимит активных броней. Максимум: {limit}.") { }
```

Поскольку `Forbidden` (403) и `TooMany`/`AlreadyProcessed` (409) являются наследниками `BusinessValidationException` (которая по умолчанию = 400), в маппинге их обязательно обрабатывать **до** базового типа. Об этом — в [04-implementation.md](04-implementation.md).

## 6. Хеширование паролей: `Sha256PasswordHasher`

Задание явно предписывает SHA-256 из стандартной библиотеки:

```csharp
public string Hash(string password)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(password);
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
    return Convert.ToHexString(bytes);
}

public bool Verify(string password, string passwordHash)
    => Hash(password).Equals(passwordHash, StringComparison.OrdinalIgnoreCase);
```

Что важно понимать (учебный контекст):

- хеш делает невозможным хранение пароля в открытом виде — это и есть требование задания;
- **ограничение** простого SHA-256: он без соли и без «фактора работы», поэтому быстрый перебор и одинаковые пароли дают одинаковые хеши. В продакшене для паролей применяют PBKDF2/bcrypt/Argon2 с пер-юзерной солью;
- для учебной задачи SHA-256 достаточно и соответствует прямому указанию задания — это сознательный, документированный компромисс.

## 7. Генерация JWT: `JwtTokenGenerator`

Сервис формирует подписанный токен по данным пользователя:

```csharp
public string GenerateToken(Guid userId, string login, UserRole role)
{
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
        new Claim(JwtRegisteredClaimNames.UniqueName, login),
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(ClaimTypes.Name, login),
        new Claim(ClaimTypes.Role, role.ToString())
    };
    // HmacSha256 поверх SigningKey из конфигурации
}
```

Решения:

- в токене есть всё, что нужно авторизации: идентификатор (`sub`/`NameIdentifier`), логин и роль (`Role`);
- конструктор падает сразу, если `SigningKey` не задан — fail-fast на старте лучше, чем ошибка при первом логине;
- параметры (`Issuer`, `Audience`, `SigningKey`, `LifetimeMinutes`) приходят через `JwtOptions`, а не зашиты в коде.

## 8. Конфигурация токена: `JwtOptions` и `appsettings.json`

```json
"Jwt": {
  "Issuer": "EventManagementService.API",
  "Audience": "EventManagementService.API",
  "SigningKey": "замените_на_сильный_на_проде",
  "LifetimeMinutes": 60
}
```

Значение `SigningKey` в репозитории — заведомо плейсхолдер. В реальном окружении его переопределяют переменной окружения `Jwt__SigningKey`; секрет не должен жить в коде или в публичном конфиге.

---

[Далее: Реализация в коде →](04-implementation.md)
