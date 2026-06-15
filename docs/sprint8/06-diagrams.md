# Диаграммы sprint 8

## 1. Регистрация и логин

```mermaid
sequenceDiagram
    participant Client
    participant Auth as AuthController
    participant Users as UserService
    participant Repo as IUserRepository
    participant Hasher as IPasswordHasher
    participant Jwt as IJwtTokenGenerator

    Client->>Auth: POST /api/auth/register {login, password, role}
    Auth->>Users: RegisterAsync(login, password, role)
    Users->>Repo: GetByLoginAsync(normalizedLogin)
    Repo-->>Users: null (свободен)
    Users->>Hasher: Hash(password)
    Hasher-->>Users: passwordHash
    Users->>Repo: AddAsync(User) + SaveChanges
    Auth-->>Client: 204 No Content

    Client->>Auth: POST /api/auth/login {login, password}
    Auth->>Users: LoginAsync(login, password)
    Users->>Repo: GetByLoginAsync(normalizedLogin)
    Repo-->>Users: User
    Users->>Hasher: Verify(password, user.PasswordHash)
    Hasher-->>Users: true
    Users->>Jwt: GenerateToken(id, login, role)
    Jwt-->>Users: JWT
    Auth-->>Client: 200 OK + token
```

При неверных учётных данных `LoginAsync` бросает `NotFoundException` → `404` с единым сообщением (защита от перебора пользователей).

## 2. Создание брони с проверкой правил

```mermaid
flowchart TD
    Start[POST /api/events/id/book + JWT] --> Auth{Токен валиден?}
    Auth -- нет --> R401[401 Unauthorized]
    Auth -- да --> UserId[userId из claims]
    UserId --> Lock[Войти в BookingLock]
    Lock --> Found{Событие найдено?}
    Found -- нет --> R404[404 Not Found]
    Found -- да --> Past{StartAt в прошлом?}
    Past -- да --> R400[400 BookingInPast]
    Past -- нет --> Limit{Активных броней >= 10?}
    Limit -- да --> R409a[409 TooManyActiveBookings]
    Limit -- нет --> Seats{Есть свободные места?}
    Seats -- нет --> R409b[409 NoAvailableSeats]
    Seats -- да --> Create[CreatePending + Save]
    Create --> R202[202 Accepted]
```

## 3. Отмена брони и решение об авторизации

```mermaid
sequenceDiagram
    participant Client
    participant Ctrl as BookingsController
    participant Svc as BookingService
    participant BRepo as IBookingRepository
    participant ERepo as IEventRepository

    Client->>Ctrl: DELETE /api/bookings/{id} + JWT
    Ctrl->>Ctrl: User.TryGetUserId / GetUserRole
    Ctrl->>Svc: CancelBookingAsync(id, userId, role)
    Svc->>BRepo: GetByIdAsync(id)
    BRepo-->>Svc: Booking (или null -> 404)
    Svc->>Svc: EnsureAccess(booking, userId, role)
    alt не владелец и не Admin
        Svc-->>Ctrl: ForbiddenOperationException (403)
    else владелец или Admin
        Svc->>Svc: booking.Cancel()
        Svc->>ERepo: GetByIdAsync(eventId) + ReleaseSeats
        Svc->>BRepo: SaveChangesAsync
        Svc-->>Ctrl: ok
        Ctrl-->>Client: 204 No Content
    end
```

Правило «владелец или администратор» проверяется в Application (`EnsureAccess`), а не в контроллере, поэтому соблюдается при любом способе вызова use case.

## 4. Авторизация по ролям на событиях

```mermaid
flowchart LR
    Req[POST/PUT/DELETE /api/events] --> Token{Токен есть?}
    Token -- нет --> N401[401]
    Token -- да --> Role{Роль = Admin?}
    Role -- нет --> N403[403]
    Role -- да --> Handler[Контроллер выполняет операцию]
```

`[Authorize(Roles = "Admin")]` отдаёт `401` без токена и `403` для обычного пользователя ещё до входа в контроллер.

## 5. Маппинг исключений в HTTP-коды

```mermaid
flowchart TD
    Ex[Исключение из Application/Domain] --> MW[ExceptionHandlingMiddleware]
    MW --> F[ForbiddenOperationException -> 403]
    MW --> T[TooManyActiveBookingsException -> 409]
    MW --> A[BookingAlreadyProcessedException -> 409]
    MW --> B[BusinessValidationException -> 400]
    MW --> S[NoAvailableSeatsException -> 409]
    MW --> N[NotFoundException -> 404]
    MW --> D[прочее -> 500]
```

Наследники `BusinessValidationException` (`Forbidden`, `TooMany`, `AlreadyProcessed`) стоят в `switch` выше базового типа, иначе их коды схлопнулись бы в `400`.

## 6. Связь таблиц после миграции

```mermaid
erDiagram
    users ||--o{ bookings : "user_id (FK, Restrict)"
    events ||--o{ bookings : "event_id"

    users {
        uuid id PK
        string login "unique"
        string password_hash
        string role
    }
    bookings {
        uuid id PK
        uuid event_id FK
        uuid user_id FK
        string status
        timestamp created_at
        timestamp processed_at
    }
```

---

[К началу sprint 8 docs](README.md)
