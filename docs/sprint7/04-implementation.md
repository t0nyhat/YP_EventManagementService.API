# Реализация в коде

## 1. Перенос файлов по проектам

### Domain

```text
src/EventManagementService.Domain/
  Models/
  Exceptions/
```

Сюда перенесены доменные сущности и доменные исключения.

### Application

```text
src/EventManagementService.Application/
  Abstractions/Repositories/
  Dtos/
  Services/
  Validation/
  DependencyInjection.cs
```

Сюда перенесены use cases, DTO, validation и интерфейсы портов.

### Infrastructure

```text
src/EventManagementService.Infrastructure/
  DataAccess/
  Migrations/
  Repositories/
  DependencyInjection.cs
```

Сюда перенесены EF Core, PostgreSQL repository adapters и migrations.

### Presentation

```text
src/EventManagementService.Presentation/
  Controllers/
  Mappings/
  Middleware/
  BackgroundServices/
  Program.cs
  appsettings*.json
```

Сюда перенесена HTTP-обвязка и composition root.

## 2. Регистрация Application

Application регистрирует только свои сервисы:

```csharp
services.AddScoped<IEventService, EventService>();
services.AddScoped<IBookingService, BookingService>();
services.AddScoped<IBookingProcessingService, BookingProcessingService>();
```

Этот extension-метод не знает об Infrastructure.

## 3. Регистрация Infrastructure

Infrastructure регистрирует EF Core и реализации портов:

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

services.AddScoped<IEventRepository, EventRepository>();
services.AddScoped<IBookingRepository, BookingRepository>();
```

Так Application получает зависимости через интерфейсы, а Presentation связывает интерфейсы с реализациями.

## 4. Program.cs как composition root

`Program.cs` находится в Presentation и собирает приложение:

- ProblemDetails;
- OpenAPI/Swagger;
- controllers;
- Application services;
- Infrastructure services;
- hosted service;
- exception middleware;
- `Database.Migrate()`.

Это допустимо, потому что Presentation — внешний слой и точка входа приложения.

## 5. Controllers после рефакторинга

Controllers стали тонкими:

- принимают HTTP-запрос;
- вызывают Application-сервис;
- возвращают HTTP-ответ;
- используют mapping extensions для response DTO.

Пример потока:

```text
POST /api/events
  -> EventsController
  -> request.ToModel()
  -> IEventService.CreateEventAsync()
  -> CreatedAtAction(...)
```

## 6. Фоновая обработка бронирований

До sprint 7 hosted service сам принимал бизнес-решения. После рефакторинга:

- `BookingProcessingBackgroundService` остался в Presentation;
- он управляет polling loop, delay, scope и parallel dispatch;
- бизнес-обработка одной брони вынесена в `BookingProcessingService` в Application.

Это разделяет:

- hosting concerns;
- business decisions.

## 7. Миграции после разделения

Migrations находятся в Infrastructure, а startup project — Presentation.

Создать миграцию:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/EventManagementService.Infrastructure/EventManagementService.Infrastructure.csproj \
  --startup-project src/EventManagementService.Presentation/EventManagementService.Presentation.csproj
```

Применить миграции:

```bash
dotnet ef database update \
  --project src/EventManagementService.Infrastructure/EventManagementService.Infrastructure.csproj \
  --startup-project src/EventManagementService.Presentation/EventManagementService.Presentation.csproj
```

---

[Далее: Тестирование и запуск →](05-testing-and-run.md)
