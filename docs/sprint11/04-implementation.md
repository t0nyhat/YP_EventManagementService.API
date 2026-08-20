# Реализация

## 1. NuGet-пакеты

В каждом Presentation-проекте используется одинаковый набор зависимостей:

| Пакет | Версия | Назначение |
|---|---:|---|
| `OpenTelemetry.Extensions.Hosting` | 1.17.0 | интеграция OpenTelemetry с Generic Host |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.17.0 | входящие HTTP-спаны и метрики |
| `OpenTelemetry.Instrumentation.Http` | 1.17.0 | исходящие HTTP-спаны |
| `OpenTelemetry.Instrumentation.Runtime` | 1.17.0 | метрики .NET runtime |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.17.0 | экспорт трейсов по OTLP |
| `OpenTelemetry.Instrumentation.EntityFrameworkCore` | 1.17.0-beta.1 | SQL-спаны EF Core |
| `OpenTelemetry.Exporter.Prometheus.AspNetCore` | 1.17.0-beta.1 | endpoint `/metrics` |
| `Serilog.AspNetCore` | 10.0.0 | интеграция Serilog с ASP.NET Core |
| `Serilog.Formatting.Compact` | 3.0.0 | компактный JSON в stdout |

Preview-версии двух пакетов зафиксированы явно, поскольку на выбранной версии OpenTelemetry их стабильные варианты отсутствуют. Версии должны обновляться согласованно во всех трех сервисах.

## 2. Проверка конфигурации при старте

Каждый `Program.cs` читает имя сервиса и OTLP endpoint до регистрации telemetry pipeline:

```csharp
var serviceName = builder.Configuration["OpenTelemetry:ServiceName"];
if (string.IsNullOrWhiteSpace(serviceName))
{
    throw new InvalidOperationException(
        "OpenTelemetry:ServiceName must be configured.");
}

var otlpEndpointValue = builder.Configuration["Otlp:Endpoint"];
if (!Uri.TryCreate(otlpEndpointValue, UriKind.Absolute, out var otlpEndpoint)
    || otlpEndpoint.Scheme is not ("http" or "https"))
{
    throw new InvalidOperationException(
        "Otlp:Endpoint must be an absolute HTTP or HTTPS URI.");
}
```

Fail-fast поведение лучше скрытой деградации: ошибка обнаруживается при развертывании, а не после попытки найти отсутствующий trace.

## 3. Serilog

Логгер собирается из общей конфигурации, контекста и статичного имени сервиса:

```csharp
builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("service.name", serviceName)
        .WriteTo.Console(new CompactJsonFormatter()));
```

Консольный sink является единственным обязательным транспортом. Благодаря этому одинаковый JSON получается при локальном запуске, в Docker и в CI.

## 4. OpenTelemetry

Регистрация строится вокруг общего Resource:

```csharp
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
            options.Filter = context => context.Request.Path != "/metrics")
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = otlpEndpoint;
            options.Protocol = OtlpExportProtocol.Grpc;
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());
```

Ключевые свойства конфигурации:

- один `service.name` используется во всех сигналах;
- HTTP instrumentation не создает trace для `/metrics`;
- EF Core instrumentation создает дочерние SQL-спаны;
- runtime instrumentation не требует изменений бизнес-кода;
- OTLP exporter отправляет только трейсы, метрики забирает Prometheus.

## 5. Порядок middleware

Технический endpoint отделен условными ветвями:

```csharp
app.UseWhen(
    context => context.Request.Path != "/metrics",
    branch => branch.UseSerilogRequestLogging());

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseWhen(
    context => context.Request.Path != "/metrics",
    branch => branch.UseHttpsRedirection());

app.UseAuthentication();
app.UseAuthorization();

app.MapPrometheusScrapingEndpoint()
    .AllowAnonymous()
    .DisableHttpMetrics();

app.MapControllers();
```

Request logging охватывает обработчик исключений, поэтому записывает фактический статус ответа. HTTPS redirection не включен в ветку `/metrics`, и работоспособность scrape не зависит от наличия HTTPS-порта в контейнере.

## 6. Настройки приложений

В `appsettings.json` каждого API добавлены секции:

```json
{
  "OpenTelemetry": {
    "ServiceName": "events-service"
  },
  "Otlp": {
    "Endpoint": "http://localhost:4317"
  }
}
```

В Docker Compose endpoint переопределяется стандартным синтаксисом конфигурации .NET:

```yaml
environment:
  OpenTelemetry__ServiceName: events-service
  Otlp__Endpoint: http://jaeger:4317
```

Локальный адрес удобен для запуска API с хоста, а имя контейнера Jaeger — для общей Docker-сети.

## 7. Docker Compose

В [docker-compose.yml](../../docker-compose.yml) добавлены:

- Prometheus `2.51.0` на порту `9090`;
- Jaeger all-in-one `1.56` с OTLP gRPC `4317` и UI `16686`;
- Grafana `10.4.2` на порту `3000`;
- volume для данных Grafana;
- read-only mounts для Prometheus и Grafana provisioning.

Jaeger запускается с `COLLECTOR_OTLP_ENABLED=true`. API зависят от Jaeger только на уровне порядка запуска контейнеров: telemetry backend не становится бизнес-зависимостью приложения.

Runtime-образы API устанавливают `libgssapi-krb5-2`. Библиотека требуется PostgreSQL-драйверу в Linux-контейнере и предотвращает неструктурированный аварийный вывод до инициализации приложения.

## 8. Prometheus

Файл [prometheus.yml](../../prometheus.yml) монтируется в контейнер read-only. Перед запуском его можно проверить штатной утилитой `promtool` из образа Prometheus. Все targets используют внутренний порт API `8080`, а не опубликованные хост-порты.

## 9. Grafana provisioning

Provisioning состоит из трех частей:

| Файл | Назначение |
|---|---|
| [prometheus.yml](../../grafana/provisioning/datasources/prometheus.yml) | создает datasource Prometheus с UID `prometheus` |
| [dashboards.yml](../../grafana/provisioning/dashboards/dashboards.yml) | объявляет provider и каталог дашбордов |
| [event-management-observability.json](../../grafana/dashboards/event-management-observability.json) | описывает переменную `$service` и девять панелей |

Provider проверяет каталог раз в 10 секунд. Дашборд управляется файлами, поэтому изменения в UI не считаются источником истины и должны переноситься обратно в JSON.

## 10. Управление зависимостями

Для устранения известной уязвимости транзитивная зависимость `System.Security.Cryptography.Xml` зафиксирована на безопасной версии `10.0.10`. После изменения выполняется аудит всех проектов командой `dotnet list package --vulnerable --include-transitive`.

Далее: [проверка и запуск](05-testing-and-run.md).
