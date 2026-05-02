# Диаграммы sprint 4: Синхронизация и параллелизм

## 1. Диаграмма потоков при конкурентных запросах на создание брони

### Без синхронизации (race condition)

```
Thread 1: Check availableSeats = 5 ✓
Thread 2: Check availableSeats = 5 ✓    (Thread 1 не изменила значение)
Thread 3: Check availableSeats = 5 ✓    (никто не изменял)
...
Thread 20: Check availableSeats = 5 ✓

Thread 1: Reserve availableSeats -= 1 → 4
Thread 2: Reserve availableSeats -= 1 → 4    (перезаписала!)
Thread 3: Reserve availableSeats -= 1 → 4    (перезаписала!)
...
Thread 20: Reserve availableSeats -= 1 → 4   (перезаписала!)

РЕЗУЛЬТАТ: availableSeats = 4 (неправильно! должно быть -15)
OVEROVERBOOKING! Все 20 получили место, хотя мест было 5.
```

### С lock (правильно)

```
Lock status: [FREE]

Thread 1: lock (_bookingLock) ← ACQUIRE → Lock: [LOCKED by Thread 1]
  Check availableSeats = 5 ✓
  Reserve availableSeats -= 1 → 4
  Save booking
  Exit lock → Lock: [FREE]

Thread 2: lock (_bookingLock) ← ACQUIRE → Lock: [LOCKED by Thread 2]
  Check availableSeats = 4 ✓
  Reserve availableSeats -= 1 → 3
  Save booking
  Exit lock → Lock: [FREE]

Thread 3: lock (_bookingLock) ← ACQUIRE → Lock: [LOCKED by Thread 3]
  Check availableSeats = 3 ✓
  Reserve availableSeats -= 1 → 2
  Save booking
  Exit lock → Lock: [FREE]

Thread 4: lock (_bookingLock) ← ACQUIRE → Lock: [LOCKED by Thread 4]
  Check availableSeats = 2 ✓
  Reserve availableSeats -= 1 → 1
  Save booking
  Exit lock → Lock: [FREE]

Thread 5: lock (_bookingLock) ← ACQUIRE → Lock: [LOCKED by Thread 5]
  Check availableSeats = 1 ✓
  Reserve availableSeats -= 1 → 0
  Save booking
  Exit lock → Lock: [FREE]

Thread 6: lock (_bookingLock) ← WAIT (очередь)
Thread 7: lock (_bookingLock) ← WAIT (очередь)
...
Thread 20: lock (_bookingLock) ← WAIT (очередь)

Thread 6: lock (_bookingLock) ← ACQUIRE → Lock: [LOCKED by Thread 6]
  Check availableSeats = 0 ✗
  Throw NoAvailableSeatsException
  Exit lock → Lock: [FREE]

... (Threads 7-20 все получат NoAvailableSeatsException)

РЕЗУЛЬТАТ: availableSeats = 0
5 успешных броней, 15 ошибок. ОВЕРБУКИНГА НЕТ ✓
```

## 2. Диаграмма обработки в BackgroundService: Параллельная задержка

### Неправильно: задержка внутри семафора

```
Time 0s:  Semaphore: [1 token]

Booking 1: WaitAsync(semaphore) ← [0 tokens] (захвачен)
  Task.Delay(2s)
  Задержка... (семафор ВСЕ ЕЩЕ занят!)
  ...

Booking 2: WaitAsync(semaphore) ← WAIT (очередь)
Booking 3: WaitAsync(semaphore) ← WAIT (очередь)
...
Booking 10: WaitAsync(semaphore) ← WAIT (очередь)

Time 2s:  Booking 1 пишет в store (~1ms)
Time 2.001s: Release() ← Booking 2 может войти
  Task.Delay(2s)
  Задержка... (семафор ВСЕ ЕЩЕ занят!)
  ...

Time 4s:  Booking 2 пишет в store
Time 4.001s: Release() ← Booking 3 может войти
  ...

Итого для 10 bookings: 10 * 2 сек = 20 СЕКУНД ❌
```

### Правильно: задержка ВНЕ семафора

```
Time 0s:  Semaphore: [1 token]

Booking 1: Task.Delay(2s) вне семафора → Начало ожидания
Booking 2: Task.Delay(2s) вне семафора → Начало ожидания (параллельно!)
Booking 3: Task.Delay(2s) вне семафора → Начало ожидания (параллельно!)
...
Booking 10: Task.Delay(2s) вне семафора → Начало ожидания (параллельно!)

Time 0-2s: Все 10 дёргают Task.Delay одновременно

Time 2s:   Все 10 готовы → Попадают на WaitAsync(semaphore)
  Booking 1: WaitAsync ← [0 tokens] (захвачен)
  Booking 2: WaitAsync ← WAIT (очередь)
  Booking 3: WaitAsync ← WAIT (очередь)
  ...
  Booking 10: WaitAsync ← WAIT (очередь)

Time 2s:    Booking 1 пишет (~1ms)
Time 2.001s: Release() → Booking 2 может войти
  
Time 2.002s: Booking 2 пишет (~1ms)
Time 2.003s: Release() → Booking 3 может войти

...

Time 2.010s: Все 10 обработаны

Итого для 10 bookings: 2 сек (задержки) + 0.01 сек (writes) ≈ 2 СЕКУНДЫ ✓
ЭКОНОМИЯ: 20 → 2 сек = 10x УСКОРЕНИЕ!
```

## 3. Диаграмма потоков выполнения ProcessBookingAsync

```
ProcessBookingAsync(bookingId) called

├─ [PARALLEL] Task.Delay(2s) ← Все bookings дёргают одновременно
│  ├─ Booking 1
│  ├─ Booking 2
│  ├─ Booking 3
│  └─ ...
│
├─ [SERIAL] WaitAsync(_processingSemaphore)
│  ├─ Booking 1 acquires → [enter critical section]
│  ├─ Check: booking is null? no. Status is Pending? yes.
│  ├─ Check: event exists?
│  │  ├─ yes → call TrySetStatus(Confirmed)
│  │  └─ no → call TrySetStatus(Rejected)
│  ├─ Finally: Release()
│  │
│  └─ [QUEUE]
│     ├─ Booking 2 ← waiting for semaphore
│     ├─ Booking 3 ← waiting for semaphore
│     └─ ...
│
├─ [SERIAL] → Booking 2 acquires → [enter critical section]
│  ├─ ... (same logic)
│  └─ Finally: Release()
│
├─ [SERIAL] → Booking 3 acquires → [enter critical section]
│  ├─ ... (same logic)
│  └─ Finally: Release()
│
└─ [QUEUE] → ... (остальные)
```

## 4. Диаграмма состояний брони

```
                    ┌────────────┐
                    │   START    │
                    └─────┬──────┘
                          │
                          ▼
                  ┌─────────────────┐
                  │  Pending        │
                  │                 │
        ┌─────────│ CreatePending() │◄──────────┐
        │         │ ProcessedAt: null
        │         └─────────────────┘         │
        │                 │                    │
        │                 │ (background        │
        │                 │  processing or     │ (reject during
        │                 │  error recovery)   │  overbooking)
        │                 ▼                    │
        │         ┌─────────────────┐         │
        │         │   Confirmed     │         │
        │         │                 │         │
        │         │ Status changed  │         │
        │         │ ProcessedAt set │         │
        │         └─────────────────┘         │
        │                                     │
        │                                     ▼
        │                         ┌─────────────────┐
        │                         │   Rejected      │
        │                         │                 │
        └──────────────────────── │ Status changed  │
                                  │ ProcessedAt set │
                                  └─────────────────┘
                                          │
                                          ▼
                                  ┌──────────────┐
                                  │  TERMINAL    │
                                  │  (no more    │
                                  │  transitions)│
                                  └──────────────┘

Правило: После Pending → Confirmed или Rejected, других переходов нет.
         Попытка обработать дважды выбросит InvalidOperationException.
```

## 5. Диаграмма архитектуры с синхронизацией

```
┌─────────────────────────────────────────────────────────────────┐
│                         HTTP Requests (async)                   │
│  POST /api/events/{id}/book           GET /api/bookings/{id}   │
│                                                                 │
│                         (3 parallel requests with 5 seats)      │
└──────────────┬──────────────────────────────────────────────────┘
               │
               ▼
   ┌───────────────────────────────────┐
   │  EventBookingsController          │
   │  .CreateBooking()                 │
   └──────────────┬────────────────────┘
                  │
                  ▼
   ┌─────────────────────────────────────────────────────────────┐
   │           BookingService                                    │
   │                                                             │
   │  private readonly object _bookingLock = new();             │
   │                                                             │
   │  CreateBookingAsync(eventId)                              │
   │  {                                                         │
   │    lock (_bookingLock) {   ◄─── CRITICAL SECTION          │
   │      TryReserveSeats()      ◄─ Атомарная проверка+резерв  │
   │      → bool reserved        │                             │
   │      if (!reserved)         │                             │
   │        throw NoAvailableSeatsException                    │
   │      CreateBooking()        │                             │
   │      Save()                 │                             │
   │    }  ◄──────────────────── Release lock                  │
   │  }                                                         │
   │                                                             │
   │  Защита от: race condition, overoverbooking              │
   └──────┬─────────────────────────────────────────────────────┘
          │
          ├──────────────────────────┐
          │                          │
          ▼                          ▼
  ┌───────────────────┐   ┌─────────────────────────────┐
  │  EventService     │   │  InMemoryBookingStore       │
  │                   │   │                             │
  │ lock (_lock)      │   │  Add(booking)               │
  │ ├─ GetEventById   │   │  GetById(id)                │
  │ ├─ TryReserveSeats│   │  GetPendingIds()            │
  │ ├─ ReleaseSeats   │   │  TrySetStatus(...)          │
  │ └─ ... CRUD       │   │                             │
  └─────────┬─────────┘   └──────────────┬──────────────┘
            │                           │
            └─────────┬─────────────────┘
                      │
                      ▼
            ┌──────────────────────┐
            │  Booking/Event Data  │
            │  (in-memory objects) │
            └──────────────────────┘


Параллельный поток: Background Worker
┌───────────────────────────────────────────────────────────────┐
│         BookingProcessingBackgroundService                    │
│                                                               │
│  while (!cancellation)                                       │
│  {                                                           │
│    pendingIds = GetPendingIds()                             │
│                                                              │
│    // ПАРАЛЛЕЛЬНО                                           │
│    tasks = pendingIds.Select(id =>                         │
│      ProcessBookingAsync(id))                              │
│    await Task.WhenAll(tasks)                               │
│                                                              │
│      each task:                                             │
│      ├─ await Task.Delay(2s) [ПАРАЛЛЕЛЬНО]                │
│      │                                                      │
│      ├─ await _processingSemaphore.WaitAsync()  [SERIAL]   │
│      │  ├─ GetBookingById(id)                              │
│      │  ├─ GetEventById(eventId) ← может быть null        │
│      │  ├─ if (event == null) TrySetStatus(Rejected)      │
│      │  └─ else TrySetStatus(Confirmed)                   │
│      ├─ finally: Release()                                 │
│      │                                                      │
│      └─ catch: TrySetStatus(Rejected), ReleaseSeats()      │
│                                                              │
│    await Task.Delay(1s polling)                            │
│  }                                                           │
└───────────────────────────────────────────────────────────────┘
```

## 6. Сравнение timeline: Последовательная vs. Параллельная обработка

### Sprint 3: Последовательная обработка

```
Time 0s:   GetPendingIds() → [B1, B2, B3, B4, B5]

Time 0s:   ProcessBooking(B1)
           ├─ Task.Delay(2s)
Time 2s:   └─ Store.Update()

Time 2s:   ProcessBooking(B2)
           ├─ Task.Delay(2s)
Time 4s:   └─ Store.Update()

Time 4s:   ProcessBooking(B3)
           ├─ Task.Delay(2s)
Time 6s:   └─ Store.Update()

Time 6s:   ProcessBooking(B4)
           ├─ Task.Delay(2s)
Time 8s:   └─ Store.Update()

Time 8s:   ProcessBooking(B5)
           ├─ Task.Delay(2s)
Time 10s:  └─ Store.Update()

────────────────────────────────────────
TOTAL: 10 seconds ❌
```

### Sprint 4: Параллельная обработка

```
Time 0s:   GetPendingIds() → [B1, B2, B3, B4, B5]

           Parallel Task.Delay(2s)
Time 0s:   ├─ ProcessBooking(B1): Task.Delay(2s)
           ├─ ProcessBooking(B2): Task.Delay(2s)
           ├─ ProcessBooking(B3): Task.Delay(2s)
           ├─ ProcessBooking(B4): Task.Delay(2s)
           └─ ProcessBooking(B5): Task.Delay(2s)

Time 2s:   WaitAsync(semaphore) + Serial Writes
           ├─ B1: Store.Update() ~1ms
           ├─ B2: Store.Update() ~1ms
           ├─ B3: Store.Update() ~1ms
           ├─ B4: Store.Update() ~1ms
           └─ B5: Store.Update() ~1ms

Time 2.005s: All done

────────────────────────────────────────
TOTAL: 2 seconds ✓
SPEEDUP: 5x faster!
```

## 7. Диаграмма обработки ошибок

```
ProcessBookingAsync(bookingId)
│
├─ await Task.Delay()
│  │
│  └─ OperationCanceledException?
│     ├─ YES → throw (propagate cancellation)
│     └─ NO → continue
│
├─ await _semaphore.WaitAsync()
│
├─ try {
│  │
│  ├─ booking = GetById()
│  │  ├─ null? → log & return (already processed)
│  │  └─ continue
│  │
│  ├─ event = GetEventById()
│  │  │
│  │  └─ catch NotFoundException
│  │     ├─ Set event = null
│  │     └─ → Code below handles it
│  │
│  ├─ if (event == null)
│  │  ├─ TrySetStatus(Rejected)
│  │  ├─ Log warning
│  │  └─ return
│  │
│  └─ else (event exists)
│     ├─ TrySetStatus(Confirmed)
│     └─ Log info
│
├─ catch (OperationCanceledException when IsCancellationRequested)
│  ├─ throw (propagate cancellation)
│
├─ catch (Exception ex)
│  ├─ Log error
│  ├─ TrySetStatus(Rejected)
│  ├─ ReleaseSeats()  ← ★ ОТКАТ МЕСТА
│  └─ (continue, don't rethrow)
│
└─ finally
   └─ Release()  ← Всегда освобождаем семафор
```

Ключевой момент: При ANY ошибке место возвращается через `ReleaseSeats()`.

---

[Назад к документации спринта](README.md)
