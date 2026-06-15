# Архитектура решения sprint 7

## 1. Production-проекты

После sprint 7 solution содержит четыре production-проекта:

```text
src/
  EventManagementService.Domain/
  EventManagementService.Application/
  EventManagementService.Infrastructure/
  EventManagementService.Presentation/
```

Каждый проект имеет отдельную ответственность.

## 2. Domain

`EventManagementService.Domain` содержит:

- `Event`;
- `Booking`;
- `BookingStatus`;
- `BusinessValidationException`;
- `NoAvailableSeatsException`;
- `NotFoundException`.

Domain не содержит `PackageReference` и не зависит от ASP.NET Core, EF Core, PostgreSQL или DI.

Основные бизнес-правила находятся рядом с данными:

- событие нельзя создать без названия;
- `EndAt` должен быть позже `StartAt`;
- вместимость должна быть больше нуля;
- нельзя зарезервировать больше мест, чем доступно;
- обработанное бронирование нельзя обработать повторно.

## 3. Application

`EventManagementService.Application` содержит:

- application-сервисы `EventService`, `BookingService`, `BookingProcessingService`;
- интерфейсы сервисов;
- DTO;
- validation для query-параметров;
- порты репозиториев `IEventRepository`, `IBookingRepository`;
- `AddApplicationServices`.

Application зависит только от Domain.

Это значит, что use cases работают с абстракциями:

```text
Application service -> repository port -> domain model
```

Application не знает о `AppDbContext`, `DbSet`, migrations или Npgsql.

## 4. Infrastructure

`EventManagementService.Infrastructure` содержит:

- `AppDbContext`;
- EF Core configurations;
- migrations;
- `EventRepository`;
- `BookingRepository`;
- `AddInfrastructureServices`.

Infrastructure зависит от Application и Domain:

- от Application — чтобы реализовать repository ports;
- от Domain — чтобы маппить доменные сущности в EF Core.

Репозитории являются адаптерами между use cases и PostgreSQL.

## 5. Presentation

`EventManagementService.Presentation` содержит:

- controllers;
- HTTP mapping;
- middleware обработки исключений;
- Swagger/OpenAPI;
- `BookingProcessingBackgroundService` как hosted service adapter;
- `Program.cs` как composition root;
- `appsettings*.json`.

Presentation зависит от Application и Infrastructure. Это внешний слой, поэтому он имеет право собрать все зависимости в DI-контейнере.

## 6. Composition root

`Program.cs` выполняет роль composition root:

```csharp
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddHostedService<BookingProcessingBackgroundService>();
```

Также в Presentation остаются:

- настройка controllers;
- Swagger;
- `ProblemDetails`;
- HTTP middleware;
- автоматическое применение migrations на старте.

## 7. Направление зависимостей

Разрешенная схема:

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

Запрещенные зависимости:

- Domain -> Application;
- Domain -> Infrastructure;
- Domain -> Presentation;
- Application -> Infrastructure;
- Application -> Presentation.

Проверка:

```bash
rg "Infrastructure" src/EventManagementService.Application
```

Ожидаемый результат — нет совпадений.

---

[Далее: Слои, порты и адаптеры →](03-layers-and-ports.md)
