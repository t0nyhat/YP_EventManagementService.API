# Тестирование и запуск

В этом разделе рассматриваются вопросы тестирования приложения, его запуска в различных средах, а также даются рекомендации по развёртыванию и мониторингу. Поскольку проект учебный, акцент сделан на понимании процессов, а не на production-готовых решениях.

## Тестирование

Тестирование — неотъемлемая часть разработки, обеспечивающая корректность работы кода и предотвращающая регрессии. В проекте можно выделить несколько уровней тестирования.

### 1. Модульное тестирование (Unit Testing)

Модульные тесты проверяют отдельные компоненты изолированно. В контексте EventManagementService.API наиболее подходящими кандидатами являются:

- **Сервис `EventService`** — тестирование CRUD-операций на изолированной коллекции.
- **Контроллер `EventsController`** — тестирование методов с подменой `IEventService` (mock).

#### Пример теста для `EventService`

```csharp
using Xunit;
using EventManagementService.API.Services;
using EventManagementService.API.Models;

public class EventServiceTests
{
    [Fact]
    public void CreateEvent_GeneratesUniqueId()
    {
        var service = new EventService();
        var newEvent = new Event
        {
            Title = "Test",
            StartAt = DateTime.Now,
            EndAt = DateTime.Now.AddHours(1)
        };

        var created = service.CreateEvent(newEvent);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Single(service.GetAllEvents());
    }

    [Fact]
    public void GetEventById_ReturnsNull_WhenNotFound()
    {
        var service = new EventService();
        var result = service.GetEventById(Guid.NewGuid());

        Assert.Null(result);
    }
}
```

**Фреймворки:** xUnit, NUnit или MSTest. В проекте пока нет тестов, но их легко добавить, создав отдельный проект `EventManagementService.API.Tests`.

#### Пример теста для `EventsController`

```csharp
using Moq;
using Microsoft.AspNetCore.Mvc;
using EventManagementService.API.Controllers;
using EventManagementService.API.Services;
using EventManagementService.API.Models;
using EventManagementService.API.Dtos;

public class EventsControllerTests
{
    [Fact]
    public void GetEventById_Returns404_WhenEventMissing()
    {
        var mockService = new Mock<IEventService>();
        mockService.Setup(s => s.GetEventById(It.IsAny<Guid>()))
                   .Returns((Event?)null);

        var controller = new EventsController(mockService.Object);
        var result = controller.GetEventById(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
```

**Mock-библиотеки:** Moq, NSubstitute или FakeItEasy.

### 2. Интеграционное тестирование (Integration Testing)

Интеграционные тесты проверяют взаимодействие нескольких компонентов (например, контроллер + сервис + база данных). В данном проекте можно протестировать:

- **Работу in‑memory хранилища** через реальный экземпляр `EventService`.
- **Маршрутизацию и сериализацию** с помощью `WebApplicationFactory` (ASP.NET Core).

#### Пример интеграционного теста

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text.Json;
using EventManagementService.API.Dtos;

public class EventsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EventsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task POST_events_Returns201()
    {
        var request = new CreateEventRequest
        {
            Title = "Integration Test",
            StartAt = DateTime.Now,
            EndAt = DateTime.Now.AddHours(2)
        };

        var response = await _client.PostAsJsonAsync("/api/events", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
```

### 3. Тестирование валидации

Поскольку валидация выполняется автоматически framework’ом, важно убедиться, что атрибуты `[Required]` и кастомные проверки работают корректно. Это можно сделать через unit-тесты `ModelState` или интеграционные тесты.

### Почему в проекте нет тестов?

Проект создавался как учебный, сфокусированный на демонстрации базовых концепций ASP.NET Core. Добавление тестов увеличило бы объём кода и могло бы отвлечь от основной цели. Однако в production-проекте тесты обязательны.

## Запуск приложения

### Требования

- **.NET SDK 10.0** или выше (можно проверить командой `dotnet --version`).
- **IDE или редактор** (например, Visual Studio 2022, VS Code, Rider) — опционально.

### Шаги запуска

1. **Восстановление зависимостей**

   ```bash
   dotnet restore
   ```

2. **Сборка проекта**

   ```bash
   dotnet build
   ```

   (или `dotnet build --configuration Release` для production-сборки)

3. **Запуск**

   ```bash
   dotnet run
   ```

   По умолчанию приложение запускается на `http://localhost:5248` (порт может отличаться, смотрите вывод в консоли).

### Альтернативные способы запуска

- **Запуск через IDE** — открыть решение `EventManagementService.API.sln` в Visual Studio и нажать F5.
- **Запуск с указанием порта**

  ```bash
  dotnet run --urls "http://localhost:5000"
  ```

- **Запуск в режиме watch** (автоматическая пересборка при изменениях)

  ```bash
  dotnet watch run
  ```

## Swagger UI

В режиме Development (`app.Environment.IsDevelopment()`) автоматически подключается Swagger UI.

- **URL Swagger UI**: `http://localhost:5248/swagger`
- **OpenAPI спецификация**: `http://localhost:5248/openapi/v1.json`

Через Swagger можно:
- просматривать все endpoint’ы,
- отправлять тестовые запросы,
- изучать схемы запросов и ответов.

**Важно:** В production Swagger обычно отключается (удаляется условие `if (app.Environment.IsDevelopment())`), чтобы не暴露 внутреннюю структуру API.

## Конфигурация

Конфигурационные параметры хранятся в файлах `appsettings.json` и `appsettings.Development.json`.

### Пример `appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Добавление собственных настроек

Можно добавить, например, настройку порта:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5000"
      }
    }
  }
}
```

Затем прочитать её в `Program.cs` через `builder.Configuration`.

## Развёртывание (Deployment)

### 1. Публикация (Publish)

Создать самодостаточное (self-contained) или зависимое от framework (framework-dependent) развёртывание:

```bash
dotnet publish -c Release -o ./publish
```

В папке `./publish` появятся все необходимые файлы, включая `EventManagementService.API.dll`.

### 2. Запуск опубликованного приложения

```bash
cd publish
dotnet EventManagementService.API.dll
```

Или, если создано self-contained развёртывание для конкретной ОС, можно запустить исполняемый файл напрямую.

### 3. Контейнеризация (Docker)

Создать `Dockerfile` в корне проекта:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["EventManagementService.API.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EventManagementService.API.dll"]
```

Сборка и запуск контейнера:

```bash
docker build -t event-management-api .
docker run -p 8080:80 event-management-api
```

### 4. Развёртывание в облаке

- **Azure** — использовать Azure App Service (поддержка .NET 10).
- **AWS** — развернуть на EC2 или Elastic Beanstalk.
- **Heroku** — через контейнеры или buildpack.

## Мониторинг и логирование

### Встроенное логирование

ASP.NET Core использует интерфейс `ILogger<T>`. Чтобы добавить логирование в сервис или контроллер, нужно внедрить `ILogger<EventService>` и вызывать методы `LogInformation`, `LogWarning` и т.д.

Пример:

```csharp
public class EventService : IEventService
{
    private readonly ILogger<EventService> _logger;

    public EventService(ILogger<EventService> logger)
    {
        _logger = logger;
    }

    public Event CreateEvent(Event newEvent)
    {
        _logger.LogInformation("Creating event with title {Title}", newEvent.Title);
        // ...
    }
}
```

Логи выводятся в консоль, файл или внешние системы (Application Insights, Seq) в зависимости от конфигурации.

### Health Checks

Для мониторинга работоспособности API можно добавить endpoint здоровья:

```csharp
builder.Services.AddHealthChecks();
// ...
app.MapHealthChecks("/health");
```

Затем внешний мониторинг может периодически запрашивать `GET /health`.

## Рекомендации для production

1. **Заменить in‑memory хранилище на базу данных** (SQL Server, PostgreSQL, MongoDB).
2. **Добавить аутентификацию и авторизацию** (JWT, OAuth2).
3. **Настроить централизованное логирование** (Serilog + Elasticsearch).
4. **Внедрить кэширование** (Redis) для часто запрашиваемых событий.
5. **Написать полный набор тестов** (unit, integration, e2e).
6. **Настроить CI/CD** (GitHub Actions, GitLab CI, Azure DevOps).
7. **Использовать конфигурацию из переменных окружения** (для чувствительных данных).
8. **Включить rate limiting** для защиты от DDoS.

## Заключение

Проект EventManagementService.API готов к запуску в development-среде и может быть использован как основа для более сложных решений. Понимание процессов тестирования и развёртывания позволит уверенно масштабировать приложение под реальные нагрузки.

---

[Далее: Диаграммы и визуализация →](06-diagrams.md) (опционально)