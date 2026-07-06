# Реализация в коде sprint 8

Документ показывает, как доменные правила и security-примитивы соединяются в работающие use cases, persistence и HTTP-пайплайн.

## 1. Бизнес-правила в `BookingService`

### Создание брони

```csharp
public async Task<Booking> CreateBookingAsync(Guid eventId, Guid userId)
{
    if (userId == Guid.Empty)
        throw new ArgumentException("Идентификатор пользователя должен быть указан.", nameof(userId));

    await BookingLock.WaitAsync();
    try
    {
        var eventItem = await _eventRepository.GetByIdAsync(eventId)
            ?? throw new NotFoundException($"Событие с id {eventId} не найдено.");

        if (eventItem.StartAt <= DateTime.UtcNow)
            throw new BookingInPastException();

        var activeBookings = await _bookingRepository.CountActiveByUserAsync(userId);
        if (activeBookings >= MaxActiveBookingsPerUser)
            throw new TooManyActiveBookingsException(MaxActiveBookingsPerUser);

        if (!eventItem.TryReserveSeats())
            throw new NoAvailableSeatsException("Нет свободных мест на данное событие.");

        var booking = Booking.CreatePending(eventId, userId);
        await _bookingRepository.AddAsync(booking);
        await _bookingRepository.SaveChangesAsync();
        return booking;
    }
    finally
    {
        BookingLock.Release();
    }
}
```

Решения:

- лимит вынесен в именованную константу `MaxActiveBookingsPerUser = 10` (значение из задания) — не «магическое число»;
- «активная» бронь — это `Pending` или `Confirmed` (запрос `CountActiveByUserAsync`), а `Cancelled`/`Rejected` в лимит не входят;
- проверки прошлого события и лимита идут **до** резервирования места, чтобы не занимать место зря;
- весь блок «проверить → зарезервировать → сохранить» выполняется под `BookingLock`, что защищает счётчик мест от гонки параллельных запросов.

### Отмена брони

```csharp
public async Task CancelBookingAsync(Guid bookingId, Guid requesterUserId, UserRole requesterRole)
{
    await BookingLock.WaitAsync();
    try
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new NotFoundException($"Бронирование с id {bookingId} не найдено.");

        EnsureAccess(booking, requesterUserId, requesterRole);
        booking.Cancel();

        var eventItem = await _eventRepository.GetByIdAsync(booking.EventId)
            ?? throw new NotFoundException($"Событие с id {booking.EventId} не найдено.");
        eventItem.ReleaseSeats();

        await _bookingRepository.SaveChangesAsync();
    }
    finally
    {
        BookingLock.Release();
    }
}

private static void EnsureAccess(Booking booking, Guid requesterUserId, UserRole requesterRole)
{
    if (booking.UserId != requesterUserId && requesterRole != UserRole.Admin)
        throw new ForbiddenOperationException();
}
```

Решения:

- проверка прав — `EnsureAccess` — единый приватный метод, переиспользуется и при чтении, и при отмене;
- отмена тоже идёт под `BookingLock`, потому что `ReleaseSeats()` меняет счётчик мест — иначе отмена конкурировала бы с созданием за один и тот же `AvailableSeats`;
- порядок: сначала `404` (нет брони), затем `403` (нет прав), затем доменная отмена.

## 2. `UserService`: регистрация и вход

```csharp
public async Task RegisterAsync(string login, string password, UserRole role = UserRole.User)
{
    // ... валидация login/password
    var normalizedLogin = NormalizeLogin(login);
    if (await _userRepository.GetByLoginAsync(normalizedLogin) is not null)
        throw new BusinessValidationException($"Пользователь с логином {normalizedLogin} уже существует.");

    var user = User.Create(normalizedLogin, _passwordHasher.Hash(password), role);
    await _userRepository.AddAsync(user);
    await _userRepository.SaveChangesAsync();
}

public async Task<string> LoginAsync(string login, string password)
{
    if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        throw new NotFoundException("Неверный логин или пароль.");

    var user = await _userRepository.GetByLoginAsync(NormalizeLogin(login))
        ?? throw new NotFoundException("Неверный логин или пароль.");

    if (!_passwordHasher.Verify(password, user.PasswordHash))
        throw new NotFoundException("Неверный логин или пароль.");

    return _jwtTokenGenerator.GenerateToken(user.Id, user.Login, user.Role);
}

private static string NormalizeLogin(string login) => login.Trim().ToLowerInvariant();
```

Решения:

- логин нормализуется в нижний регистр при регистрации и входе — `Admin` и `admin` считаются одним пользователем (иначе уникальный индекс был бы регистрозависимым);
- при неверных учётных данных — `NotFoundException` → `404` (как описано в задании), причём **всегда одно и то же сообщение** «Неверный логин или пароль», чтобы нельзя было перебором узнать, существует ли логин;
- пароль хешируется при регистрации; обратно не расшифровывается — вход проверяет совпадение хешей.

## 3. Persistence и миграция

`UserConfiguration` задаёт таблицу `users`, уникальный индекс на `login` и роль строкой:

```csharp
builder.HasIndex(user => user.Login).IsUnique();
builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(20);
```

`BookingConfiguration` добавляет внешний ключ `bookings.user_id → users.id`.

Миграция `Sprint8AddUsersAndBookingUserId`:

- создаёт таблицу `users` и уникальный индекс по логину;
- добавляет колонку `user_id` в `bookings` с внешним ключом (`onDelete: Restrict`);
- **засевает технического пользователя** `system` (`...0001`) и проставляет его как `defaultValue` для `user_id`, чтобы существующие брони не нарушили `NOT NULL` и внешний ключ.

`onDelete: Restrict` выбран сознательно: удаление пользователя с бронями должно быть явной операцией, а не каскадно стирать историю броней.

## 4. JWT-пайплайн в `Program.cs`

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT settings are not configured.");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();
// ...
app.UseAuthentication();
app.UseAuthorization();
```

`ClockSkew = TimeSpan.Zero` убирает стандартный 5-минутный «зазор», чтобы время жизни токена было предсказуемым в тестах. `UseAuthentication` стоит перед `UseAuthorization` — порядок middleware обязателен.

## 5. Защита эндпоинтов и чтение пользователя из claims

- управление событиями — только `Admin`:

```csharp
[Authorize(Roles = nameof(UserRole.Admin))]   // POST/PUT/DELETE /api/events
```

- брони требуют аутентификации (`[Authorize]`), а идентификатор берётся из claims через общий helper:

```csharp
public static bool TryGetUserId(this ClaimsPrincipal user, out Guid userId)
{
    userId = Guid.Empty;
    return Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}

public static UserRole GetUserRole(this ClaimsPrincipal user)
    => Enum.TryParse<UserRole>(user.FindFirstValue(ClaimTypes.Role), true, out var role)
        ? role : UserRole.User;
```

`ClaimsPrincipalExtensions` вынесен отдельно, чтобы не дублировать парсинг claims в каждом контроллере. Контроллер остаётся тонким:

```csharp
if (!User.TryGetUserId(out var currentUserId))
    return Unauthorized();
await bookingService.CancelBookingAsync(id, currentUserId, User.GetUserRole());
```

## 6. Маппинг исключений в HTTP-коды

`ExceptionHandlingMiddleware` — единственное место, где доменные исключения становятся `ProblemDetails`:

```csharp
return exception switch
{
    ForbiddenOperationException        => (403, "Forbidden", ForbiddenType),
    TooManyActiveBookingsException     => (409, "Conflict", ConflictType),
    BookingAlreadyProcessedException   => (409, "Conflict", ConflictType),
    BusinessValidationException        => (400, "Validation error", ValidationType),
    NoAvailableSeatsException          => (409, "Conflict", ConflictType),
    NotFoundException                  => (404, "Resource not found", NotFoundType),
    _                                  => (500, "Internal server error", ServerErrorType)
};
```

Порядок веток критичен: `ForbiddenOperationException`, `TooManyActiveBookingsException` и `BookingAlreadyProcessedException` — наследники `BusinessValidationException`, поэтому стоят **выше** неё, иначе их `403`/`409` ошибочно схлопнулись бы в `400`.

## 7. Сериализация статуса брони

`BookingStatus` помечен `[JsonConverter(typeof(JsonStringEnumConverter<BookingStatus>))]`, поэтому API отдаёт читаемое `"status": "Pending"` вместо `0`. Атрибут на самом типе обеспечивает симметрию: и сериализация в API, и десериализация в тестах используют строку без дополнительной глобальной настройки.

---

[Далее: Тестирование и запуск →](05-testing-and-run.md)
