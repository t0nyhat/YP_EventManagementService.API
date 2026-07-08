# Диаграммы sprint 9

## 1. Контейнерная схема системы

```mermaid
flowchart LR
    Client((Клиент))

    subgraph compose [docker compose]
        Users[Users API :5101]
        Events[Events API :5102]
        Bookings[Bookings API :5103]

        UsersDb[(users_db :5433)]
        EventsDb[(events_db :5434)]
        BookingsDb[(bookings_db :5435)]

        ZK[Zookeeper]
        Kafka[[Kafka topic booking-confirmed]]
    end

    Client -- "POST /auth/*" --> Users
    Client -- "JWT: /events*" --> Events
    Client -- "JWT: /bookings*" --> Bookings

    Users --- UsersDb
    Events --- EventsDb
    Bookings --- BookingsDb

    Bookings -- publish --> Kafka
    Kafka -- consume --> Events
    ZK --- Kafka
```

Между сервисами нет ни одной HTTP-стрелки: единственный канал — Kafka. Один JWT (общие secret/issuer/audience) принимают все три API.

## 2. Поток BookingConfirmed: от брони до уменьшения мест

```mermaid
sequenceDiagram
    participant Client
    participant BC as EventBookingsController
    participant BP as BookingProcessingService (фон)
    participant BDB as bookings_db
    participant OP as BookingOutboxPublisher (фон)
    participant K as Kafka
    participant C as BookingConfirmedConsumerService
    participant H as BookingConfirmedHandler
    participant EDB as events_db

    Client->>BC: POST /events/{id}/book + JWT
    BC->>BDB: INSERT booking (Pending)
    BC-->>Client: 202 Accepted + Location

    Note over BP: поллинг Pending раз в 1 с, задержка 2 с
    BP->>BDB: Confirm + INSERT booking_outbox (одна транзакция)

    Note over OP: поллинг outbox раз в 1 с
    OP->>K: Produce(key=EventId, value=payload)
    K-->>OP: ack (Acks.All)
    OP->>BDB: published_at_utc = now

    K-->>C: BookingConfirmed
    C->>H: HandleAsync (новый DI-scope)
    H->>EDB: available_seats -= 1 + INSERT inbox (одна транзакция)
    C->>K: Commit offset
```

Две локальные транзакции (в Bookings и в Events) связаны сообщением — глобальной транзакции нет, согласованность достигается в конечном счёте.

## 3. Логика обработчика сообщения (идемпотентность и крайние случаи)

```mermaid
flowchart TD
    Msg[BookingConfirmed из Kafka] --> Dup{booking_id уже в inbox?}
    Dup -- да --> Skip1[no-op, Commit]
    Dup -- нет --> Found{Событие найдено?}
    Found -- нет --> I1[inbox: EventNotFound] --> Commit1[Commit]
    Found -- да --> Started{Событие уже началось?}
    Started -- да --> I2[inbox: EventAlreadyStarted] --> Commit1
    Started -- нет --> Seats{Мест хватает?}
    Seats -- нет --> I3[inbox: NotEnoughSeats] --> Commit1
    Seats -- да --> Dec[available_seats -= seats + inbox: Processed] --> Commit1

    Msg -.ошибка обработки, попытка меньше MaxHandlerAttempts.-> Seek[Seek на тот же offset + пауза 5 с] -.-> Msg
    Msg -.лимит попыток исчерпан.-> DLT1[Dead Letter Topic + Commit]
    Msg -.битый JSON / seats <= 0.-> DLT2[Dead Letter Topic + Commit, без ретраев]
```

Каждая ветка с записью inbox — одна транзакция БД. `Commit` оффсета выполняется только после успешного сохранения, поэтому сбой БД не теряет сообщение — оно будет повторено через `Seek`, пока не исчерпается лимит попыток; после этого (или сразу для заведомо невалидных сообщений) — изоляция в Dead Letter Topic (см. [03-messaging-and-consistency.md](03-messaging-and-consistency.md#6-dead-letter-topic)).

## 4. Гонка «отмена во время подтверждения»

```mermaid
sequenceDiagram
    participant U as Пользователь (DELETE /bookings/{id})
    participant P as BookingProcessingService (фон)
    participant DB as bookings_db (status - concurrency token)

    P->>DB: SELECT booking (status = Pending)
    U->>DB: UPDATE ... SET status = Cancelled WHERE status = Pending
    DB-->>U: 1 row - отмена записана (204)
    P->>DB: UPDATE ... SET status = Confirmed WHERE status = Pending
    DB-->>P: 0 rows - DbUpdateConcurrencyException
    Note over P: ConcurrencyConflictException - подтверждение пропущено,<br/>outbox-строка не сохраняется
```

Симметричный случай (подтверждение победило) обрабатывается на стороне отмены: бронь перечитывается и отменяется повторно — отмена из `Confirmed` допустима.

## 5. Создание брони: лимит под advisory lock

```mermaid
flowchart TD
    Start[POST /events/id/book + JWT] --> Auth{Токен валиден?}
    Auth -- нет --> R401[401 Unauthorized]
    Auth -- да --> Create[Booking.CreatePending - валидация Guid]
    Create --> Tx[BEGIN + pg_advisory_xact_lock userId]
    Tx --> Limit{Активных броней >= 10?}
    Limit -- да --> R409[409 TooManyActiveBookings, ROLLBACK]
    Limit -- нет --> Ins[INSERT booking + COMMIT]
    Ins --> R202[202 Accepted + Location]
```

В отличие от монолита, существование события и наличие мест здесь **не проверяются** — это ответственность Events при обработке `BookingConfirmed`.

---

[Назад к README спринта](README.md)
