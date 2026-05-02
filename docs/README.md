# EventManagementService.API — Полная учебная документация

Этот проект демонстрирует разработку REST API на ASP.NET Core, от базового CRUD до многопоточной синхронизации и параллельной обработки.

## Структура проекта и спринты

Разработка разделена на 4 спринта, каждый добавляющий новый уровень сложности:

### Sprint 1: Основы CRUD и архитектура

📁 **[docs/sprint1/](sprint1/)**

**Цель**: Создать базовый REST API для управления событиями.

**Что реализовано**:
- Полный CRUD для сущности Event
- Архитектура: контроллеры → сервисы → модели
- Валидация данных (DataAnnotations)
- Swagger/OpenAPI документация
- In-memory хранилище
- Базовая потокобезопасность (lock в EventService)

**Ключевые концепции**:
- Layered architecture
- Dependency Injection
- DTO vs. доменные модели
- Валидация на разных уровнях

**Файлы документации**:
- [01-introduction.md](sprint1/01-introduction.md) — назначение и обзор
- [02-architecture.md](sprint1/02-architecture.md) — архитектурные решения
- [03-models-dto-validation.md](sprint1/03-models-dto-validation.md) — модели и валидация
- [04-controller-services.md](sprint1/04-controller-services.md) — контроллеры и сервисы
- [05-testing-deployment.md](sprint1/05-testing-deployment.md) — тестирование

---

### Sprint 2: Фильтрация, пагинация и обработка ошибок

📁 **[docs/sprint2/](sprint2/)**

**Цель**: Добавить фильтрацию/пагинацию и правильную обработку ошибок.

**Что реализовано**:
- Фильтрация по названию и диапазону дат
- Пагинация (page, pageSize)
- ProblemDetails для унифицированных ошибок (RFC 7807)
- Middleware для централизованной обработки исключений
- Статусные коды (400, 404, 500)
- Расширенная валидация

**Ключевые концепции**:
- Запрос-ответ контракты
- Фильтрация и сортировка на уровне сервиса
- Обработка исключений через middleware
- ProblemDetails стандарт

**Файлы документации**:
- [01-introduction.md](sprint2/01-introduction.md)
- [02-architecture.md](sprint2/02-architecture.md)
- [03-error-handling-validation.md](sprint2/03-error-handling-validation.md)
- [04-filtering-pagination.md](sprint2/04-filtering-pagination.md)
- [05-testing-and-run.md](sprint2/05-testing-and-run.md)

---

### Sprint 3: Бронирование и фоновая обработка

📁 **[docs/sprint3/](sprint3/)**

**Цель**: Добавить систему бронирования с асинхронной фоновой обработкой.

**Что реализовано**:
- Модель Booking с состояниями (Pending, Confirmed, Rejected)
- Асинхронный HTTP-контракт (202 Accepted)
- Отдельный store для бронирований (InMemoryBookingStore)
- BackgroundService для периодической обработки
- Инъекция зависимостей для worker'а
- Тесты на поведение брони и worker'а

**Ключевые концепции**:
- Жизненный цикл асинхронной операции
- Отдельный слой хранения (store)
- BackgroundService vs. контроллеры
- Singleton lifetime для общего состояния

**Файлы документации**:
- [01-introduction.md](sprint3/01-introduction.md)
- [02-architecture.md](sprint3/02-architecture.md)
- [03-booking-model-store-service.md](sprint3/03-booking-model-store-service.md)
- [04-endpoints-background-processing.md](sprint3/04-endpoints-background-processing.md)
- [05-testing-and-run.md](sprint3/05-testing-and-run.md)

---

### Sprint 4: Синхронизация и параллельная обработка ⭐

📁 **[docs/sprint4/](sprint4/)**

**Цель**: Обеспечить потокобезопасность при конкурентных запросах и параллельную обработку.

**Что реализовано**:
- Ограничение по количеству мест (TotalSeats, AvailableSeats)
- Защита критической секции в BookingService через `lock`
- Параллельная обработка в BackgroundService через `Task.WhenAll`
- Асинхронная синхронизация через `SemaphoreSlim`
- Новое исключение NoAvailableSeatsException (409 Conflict)
- Конкурентные тесты (5 мест, 20 запросов)

**Ключевые концепции**:
- Race condition и как их избежать
- Примитивы синхронизации (`lock`, `SemaphoreSlim`)
- Атомарность операций
- Параллельная обработка vs. последовательная
- Обработка ошибок при параллелизме

**Файлы документации**:
- [01-introduction.md](sprint4/01-introduction.md) — обзор проблем и решений
- [02-architecture.md](sprint4/02-architecture.md) — архитектурные решения
- [03-synchronization.md](sprint4/03-synchronization.md) — примитивы синхронизации
- [04-implementation.md](sprint4/04-implementation.md) — деталь каждого компонента
- [05-testing.md](sprint4/05-testing.md) — стратегия тестирования
- [06-diagrams.md](sprint4/06-diagrams.md) — диаграммы потоков и состояний

---

## Как использовать эту документацию

### Для изучения с нуля

**Рекомендуемый путь**:

1. **Sprint 1**: Начните с понимания базовой архитектуры
   - Прочитайте `01-introduction` каждого спринта
   - Изучите `02-architecture`
   - Посмотрите исходный код

2. **Sprint 2**: Добавьте сложность
   - Поймите, как расширяется архитектура
   - Изучите обработку ошибок

3. **Sprint 3**: Введение в асинхронность
   - Поймите отдельный store
   - BackgroundService и жизненный цикл

4. **Sprint 4**: Многопоточность (самое сложное)
   - Сначала посмотрите диаграммы (06-diagrams)
   - Затем изучите примитивы синхронизации (03-synchronization)
   - Потом разбирайте реализацию (04-implementation)
   - Обязательно напишите свои конкурентные тесты

### Для быстрого поиска

**Ищете информацию о**:
- Race condition → Sprint 4, 06-diagrams и 03-synchronization
- Фильтрация → Sprint 2, 04-filtering-pagination
- Фоновая обработка → Sprint 3, 04-endpoints-background-processing
- Бронирование → Sprint 3, 03-booking-model-store-service
- Обработка ошибок → Sprint 2, 03-error-handling-validation
- Синхронизация потоков → Sprint 4, полная документация

### Для подготовки к собеседованию

**Обязательно пройти**:
1. Sprint 1: основы архитектуры и CRUD
2. Sprint 2: фильтрация и обработка ошибок (ProblemDetails)
3. Sprint 3: асинхронность (async/await, BackgroundService)
4. Sprint 4: многопоточность (lock, SemaphoreSlim, race conditions)

**Рекомендуется уметь объяснить**:
- Почему нельзя использовать `lock` с `await`?
- Как происходит race condition при 20 параллельных запросах на 5 мест?
- Почему нужна отдельная система store для бронирований?
- Как параллельная обработка экономит время (2 сек вместо 20)?

---

## Запуск проекта

### Требования

- .NET SDK 10.0+
- Visual Studio Code или другой IDE

### Подготовка

```bash
cd EventManagementService.API
dotnet restore
dotnet build
```

### Запуск приложения

```bash
dotnet run
```

Приложение запустится на `https://localhost:5248`.

**Swagger доступен**: `http://localhost:5248/swagger`

### Запуск тестов

```bash
dotnet test
```

Все тесты должны пройти. Особое внимание на **конкурентные тесты** в Sprint 4:
- `CreateBookingAsync_WhenRequestedConcurrently_DoesNotExceedTotalSeats`
- `ExecuteAsync_WhenMultiplePendingBookingsExist_ProcessesThemAllInParallel`

---

## Основные компоненты проекта

```
EventManagementService.API/
├── Controllers/
│   ├── EventsController.cs
│   ├── EventBookingsController.cs
│   └── BookingsController.cs
├── Services/
│   ├── IEventService.cs
│   ├── EventService.cs (lock для потокобезопасности)
│   ├── IBookingService.cs
│   └── BookingService.cs (lock для критической секции)
├── Models/
│   ├── Event.cs (TotalSeats, AvailableSeats, методы синхронизации)
│   ├── Booking.cs (состояния: Pending, Confirmed, Rejected)
│   └── BookingStatus.cs
├── Stores/
│   ├── IBookingStore.cs
│   └── InMemoryBookingStore.cs (общее хранилище для API и worker'а)
├── BackgroundServices/
│   └── BookingProcessingBackgroundService.cs (Task.WhenAll, SemaphoreSlim)
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs (маппирование ошибок → HTTP статусы)
├── Exceptions/
│   ├── BusinessValidationException.cs
│   ├── NotFoundException.cs
│   └── NoAvailableSeatsException.cs (409 Conflict)
├── Dtos/
│   ├── CreateEventRequest.cs / EventResponse.cs
│   ├── CreateBookingRequest.cs / BookingResponse.cs
│   └── GetEventsQuery.cs
├── Mappings/
│   ├── EventMappings.cs
│   └── BookingMappings.cs
├── Validation/
│   └── GetEventsQueryValidation.cs
├── Program.cs (DI, middleware, hosting services)
├── docs/
│   ├── sprint1/
│   ├── sprint2/
│   ├── sprint3/
│   └── sprint4/ ← ВЫ ЗДЕСЬ
├── EventManagementService.API.Tests/
│   ├── Models/
│   ├── Services/
│   ├── BackgroundServices/
│   ├── Integration/
│   └── Stores/
└── README.md (основной readme проекта)
```

---

## Ключевые файлы для каждого спринта

| Файл | Sprint 1 | Sprint 2 | Sprint 3 | Sprint 4 |
|------|----------|----------|----------|----------|
| Models/Event.cs | CRUD | + validate dates | CRUD | + seats management |
| Services/EventService.cs | lock + CRUD | + filters | CRUD | + TryReserveSeats/ReleaseSeats |
| Program.cs | DI setup | middleware | + worker | + exception mapping |
| Controllers/ | EventsController | (same) | + EventBookingsController | (same) |
| Services/BookingService.cs | — | — | CRUD | + lock for critical section |
| BackgroundServices/ | — | — | sequential foreach | Task.WhenAll + SemaphoreSlim |
| Tests/ | basic CRUD | validation | booking states | concurrency tests ⭐ |

---

## Особенности этого проекта

### ✓ Используемые паттерны

- **Layered Architecture**: Controllers → Services → Models → Stores
- **Dependency Injection**: встроенный DI контейнер
- **Store Pattern**: отдельный слой хранения
- **DTO Pattern**: разделение контракта и модели
- **Middleware**: централизованная обработка ошибок
- **BackgroundService**: асинхронная фоновая обработка
- **Synchronization Primitives**: lock и SemaphoreSlim

### ✓ Важные концепции

- **Потокобезопасность**: lock в сервисах
- **Асинхронность**: async/await, Task.WhenAll
- **Валидация**: на разных уровнях
- **Обработка ошибок**: ProblemDetails + middleware
- **Конкурентность**: защита от race conditions

### ✓ Тестируемость

- Все сервисы имеют интерфейсы (IEventService, IBookingService)
- In-memory хранилище для тестов
- Конкурентные тесты (самые важные!)
- Тесты на состояния и переходы

---

## Часто задаваемые вопросы

**Q: С чего начать?**
A: Начните с Sprint 1, прочитайте `01-introduction.md`, затем `02-architecture.md`.

**Q: Почему так много документации?**
A: Потому что на примере этого проекта можно изучить все аспекты разработки на ASP.NET Core: от базового CRUD до многопоточной синхронизации.

**Q: Какой спринт самый сложный?**
A: Sprint 4. Здесь нужно понять race conditions, lock, SemaphoreSlim и как они взаимодействуют.

**Q: Можно ли использовать это как шаблон для реального проекта?**
A: Это учебный проект. Для production используйте:
- Настоящую БД вместо in-memory
- Entity Framework Core для ORM
- Более сложные паттерны (Unit of Work, Repository)
- Caching, async operations, etc.

**Q: Почему нельзя использовать lock с await?**
A: Потому что `await` может передать управление другому потоку, а `lock` требует полного контроля над текущим потоком.

**Q: Как тестировать конкурентность?**
A: Запустите много параллельных операций (`Task.WhenAll`), проверьте инварианты (availableSeats не отрицательный, нет дублей Id).

---

## Контрольный список для изучения

- [ ] Sprint 1: CRUD работает, Swagger показывает эндпоинты
- [ ] Sprint 2: Фильтрация работает, ошибки возвращают 400/404/500
- [ ] Sprint 3: Брони создаются в статусе Pending, через 2 сек переходят в Confirmed
- [ ] Sprint 4: 20 параллельных запросов на 5 мест → 5 успешных, 15 ошибок (409 Conflict)
- [ ] Все тесты проходят (`dotnet test`)
- [ ] Вы можете объяснить, почему нужна синхронизация
- [ ] Вы можете написать конкурентный тест

---

## Дополнительные ресурсы

- [Microsoft Docs: async/await](https://docs.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/)
- [Microsoft Docs: Threading](https://docs.microsoft.com/en-us/dotnet/standard/threading/)
- [C# Threading Best Practices](https://github.com/davidfowl/AspNetCoreDiagnosticScenarios)
- [Concurrency in C#](https://www.oreilly.com/library/view/concurrency-in-c/9781491927281/)

---

**Удачи в изучении!** Если у вас возникнут вопросы, обратитесь к соответствующей документации спринта.

[Вернуться к Sprint 4 →](sprint4/)
