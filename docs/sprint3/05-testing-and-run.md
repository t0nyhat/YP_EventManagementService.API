# Тестирование и запуск sprint 3

Этот раздел объясняет:

- как после sprint 3 устроен тестовый проект;
- какие именно сценарии теперь покрываются;
- зачем подключён `FluentAssertions`;
- как запускать приложение и что проверять вручную.

## Зачем в sprint 3 понадобилось больше тестов

Во втором спринте основной фокус тестов был на `EventService`.

В третьем спринте логика стала богаче сразу по нескольким причинам:

- появилась новая сущность `Booking`;
- добавился отдельный store;
- появилось асинхронное изменение состояния во времени;
- добавился worker, который живёт отдельно от HTTP-запроса.

Если такую логику оставить без тестов, регрессии будут возникать намного легче, чем во втором спринте.

## Что изменилось в тестовом проекте

Тестовый проект по-прежнему расположен в:

`EventManagementService.API.Tests`

Но теперь он покрывает не только события, а весь новый контур бронирований.

## Почему подключён `FluentAssertions`

Файл: `EventManagementService.API.Tests/EventManagementService.API.Tests.csproj`

В sprint 3 тестовый проект получил зависимость:

- `FluentAssertions`

### Почему это полезно именно сейчас

В третьем спринте в тестах стало заметно больше:

- проверок статусов;
- проверок временных полей;
- проверок коллекций;
- проверок исключений;
- integration-проверок JSON- и HTTP-контракта.

С `FluentAssertions` эти проверки читаются проще:

```csharp
booking.Status.Should().Be(BookingStatus.Pending);
booking.ProcessedAt.Should().BeNull();
```

Именно поэтому sprint 3 стал подходящим моментом перевести тестовый проект на единый стиль assert'ов.

## Структура тестового слоя после sprint 3

### Существующие тесты событий

- `EventManagementService.API.Tests/Services/EventServiceCrudTests.cs`
- `EventManagementService.API.Tests/Services/EventServiceQueryTests.cs`
- `EventManagementService.API.Tests/Services/EventServiceValidationTests.cs`
- `EventManagementService.API.Tests/Integration/EventsApiIntegrationTests.cs`

Они не удалены, а сохранены и переведены на fluent-style.

### Новые тесты sprint 3

- `EventManagementService.API.Tests/Services/BookingServiceTests.cs`
- `EventManagementService.API.Tests/Models/BookingTests.cs`
- `EventManagementService.API.Tests/Stores/InMemoryBookingStoreTests.cs`
- `EventManagementService.API.Tests/BackgroundServices/BookingProcessingBackgroundServiceTests.cs`

Это важный момент: новая функциональность покрывается на нескольких уровнях, а не только через один “общий” сценарий.

## Какие уровни тестирования есть теперь

### 1. Unit-тесты доменной модели `Booking`

Файл: `EventManagementService.API.Tests/Models/BookingTests.cs`

Они проверяют:

- создание pending-брони;
- запрет пустого `eventId`;
- переход в `Confirmed`;
- переход в `Rejected`;
- запрет повторной обработки уже завершённой брони.

### Зачем это нужно

Эти тесты закрепляют доменные правила независимо от store, сервиса и HTTP.

## 2. Unit-тесты `InMemoryBookingStore`

Файл: `EventManagementService.API.Tests/Stores/InMemoryBookingStoreTests.cs`

Они проверяют:

- добавление брони;
- чтение по `Id`;
- работу snapshot'ов;
- выборку pending-броней;
- безопасное изменение статуса только для `Pending`;
- защиту от дублирующего `Id`.

### Зачем это нужно

Store — это общий mutable state sprint 3.  
Если его поведение не закрепить отдельно, проблемы потом будут выглядеть как “странные баги сервиса” или “странный worker”.

## 3. Unit-тесты `BookingService`

Файл: `EventManagementService.API.Tests/Services/BookingServiceTests.cs`

Это обязательный минимум по чек-листу sprint 3.

Покрыты сценарии:

- создание брони для существующего события;
- создание нескольких броней для одного события;
- получение брони по `Id`;
- получение брони после смены статуса;
- попытка создать бронь для несуществующего события;
- попытка создать бронь для удалённого события;
- попытка получить несуществующую бронь.

### Почему именно эти тесты самые важные

Потому что они проверяют основной контракт новой бизнес-логики, не завися от HTTP и Swagger.

## 4. Изолированный тест на `BookingProcessingBackgroundService`

Файл: `EventManagementService.API.Tests/BackgroundServices/BookingProcessingBackgroundServiceTests.cs`

Этот тест:

- создаёт реальный `InMemoryBookingStore`;
- кладёт в него pending-бронь;
- запускает worker через `StartAsync`;
- ждёт смены статуса;
- проверяет `Confirmed` и `ProcessedAt`;
- корректно останавливает worker через `StopAsync`.

### Почему этот тест полезен

Он проверяет фоновую обработку отдельно от HTTP и `TestServer`, то есть именно как изолированный компонент.

## 5. Integration-тесты HTTP-контракта

Файл: `EventManagementService.API.Tests/Integration/EventsApiIntegrationTests.cs`

После sprint 3 integration-набор покрывает:

- `ProblemDetails` для event endpoint-ов;
- сквозной booking-сценарий:
  - создать событие;
  - создать бронь;
  - увидеть `202 Accepted`;
  - проверить `Location`;
  - прочитать `Pending`;
  - дождаться `Confirmed`.

### Почему integration-тест здесь особенно полезен

Он одновременно проверяет:

- контроллеры;
- DTO;
- DI;
- middleware;
- worker;
- shared state между API и hosted service.

То есть фактически проверяет весь связанный сценарий sprint 3 целиком.

## Сколько тестов в проекте сейчас

На текущем этапе покрыто 47 test methods:

- event CRUD, query и validation;
- HTTP integration по событиям;
- booking service;
- booking model;
- booking store;
- booking worker;
- booking integration flow.

Это уже полноценный учебный тестовый слой, а не только “минимальный smoke test”.

## Почему важен принцип AAA

Тесты в проекте написаны в стиле:

- **Arrange**
- **Act**
- **Assert**

Это особенно полезно в sprint 3, потому что сценарии стали длиннее и многослойнее.

AAA помогает сразу видеть:

- какое состояние готовится;
- какое действие выполняется;
- что именно считается контрактом.

## Как запускать проект

Из корня репозитория:

```bash
dotnet restore
dotnet build
dotnet run
```

## Как запускать тесты

Из корня репозитория:

```bash
dotnet test
```

Если проект запускается локально вне sandbox-среды, этого достаточно.  
Внутри изолированных сред может потребоваться перенаправление NuGet cache, но это уже техническая особенность окружения, а не самого проекта.

## Что проверить вручную через Swagger

### Сценарий событий

1. Создать событие через `POST /api/events`
2. Убедиться, что вернулся `201 Created`
3. Проверить `GET /api/events/{id}`

### Сценарий бронирований

1. Создать событие
2. Выполнить `POST /api/events/{id}/book`
3. Убедиться, что вернулся `202 Accepted`
4. Посмотреть заголовок `Location`
5. Сразу выполнить `GET /api/bookings/{id}` и увидеть `Pending`
6. Подождать несколько секунд
7. Повторить `GET /api/bookings/{id}` и увидеть `Confirmed`

### Сценарии ошибок

Полезно дополнительно проверить:

- `POST /api/events/{missingId}/book`
- `GET /api/bookings/{missingId}`
- `GET /api/events?page=0`

Это позволяет увидеть, что sprint 3 не сломал общую стратегию ошибок sprint 2.

## Что особенно полезно изучить в тестовом слое sprint 3

### 1. Комбинацию unit- и integration-тестов

В проекте видно, как разные уровни тестирования отвечают за разные вопросы:

- unit-тесты проверяют локальные правила;
- integration-тесты подтверждают, что всё реально работает вместе.

### 2. Проверку асинхронного перехода состояния

Тесты sprint 3 демонстрируют, что изменение ресурса можно проверять не только “сразу после метода”, но и через ожидание целевого состояния.

### 3. Практическую роль `FluentAssertions`

Sprint 3 — хороший пример, когда assertion-библиотека начинает действительно упрощать поддержку тестов, а не просто меняет синтаксис.
