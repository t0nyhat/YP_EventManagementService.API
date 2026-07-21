Задание
Представьте: ваша распределённая система работает в проде, но что-то пошло не так — один из запросов стал отвечать медленно, а ошибки стали появляться чаще. Где искать проблему? Без инструментов наблюдаемости остаётся только строить догадки.
В этом спринте вы добавите такие инструменты в сервисы: интегрируете OpenTelemetry SDK для сбора трейсов и метрик, настроите Prometheus и Jaeger как хранилища, приведёте логи к единому формату и создадите дашборд с базовыми метриками.
В результате выполнения проектной работы вы:
интегрируете OpenTelemetry SDK во все сервисы и настроите автоматический сбор телеметрии по HTTP-запросам и запросам к базе данных;
настроите экспорт метрик в Prometheus и убедитесь, что они корректно собираются;
настроите экспорт трейсов в Jaeger и проверите появление трейсов с HTTP- и SQL-спанами;
приведёте логи к единому структурированному формату;
создадите дашборд в Grafana с базовыми техническими метриками (latency, throughput, error rate).
Требования и функциональность
В проекте используются C#, .NET 8 и выше, а также ASP.NET Core Web API.
Код приложения хранится в том же публичном GitHub-репозитории.
OpenTelemetry SDK подключён во всех трёх сервисах.
Метрики каждого сервиса доступны по эндпоинту /metrics и собираются Prometheus.
Трейсы отправляются в Jaeger; в интерфейсе Jaeger видны трейсы каждого сервиса с корректным именем сервиса и спанами HTTP-запросов и SQL-запросов к БД.
Логи сервисов выводятся в структурированном формате (JSON).
В docker-compose.yml добавлены контейнеры Prometheus, Jaeger и Grafana.
В Grafana создан дашборд с метриками latency, throughput и error rate хотя бы для одного сервиса.
В README описано, какие инструменты добавлены, как запустить стек мониторинга и на каких портах доступны UI Prometheus, Jaeger и Grafana.
Как работать над заданием
Этап 1. Подготовка к работе
Убедитесь, что проектная работа десятого спринта успешно завершена.
Переключитесь на ветку main вашего репозитория и выполните git pull для синхронизации.
Создайте новую ветку sprint-11 из ветки main и переключитесь на неё.
Всю работу по этому проекту ведите в ветке sprint-11.
Этап 2. Подключение NuGet-пакетов
Добавьте пакеты OpenTelemetry в каждый сервис. Пакеты нужны в проекте Presentation каждого сервиса:
OpenTelemetry.Extensions.Hosting
OpenTelemetry.Instrumentation.AspNetCore
OpenTelemetry.Instrumentation.Http
OpenTelemetry.Instrumentation.Runtime
OpenTelemetry.Instrumentation.EntityFrameworkCore
OpenTelemetry.Exporter.Prometheus.AspNetCore
OpenTelemetry.Exporter.OpenTelemetryProtocol
Для структурированного логирования добавьте Serilog:
Serilog.AspNetCore
Serilog.Formatting.Compact
Этап 3. Настройка OpenTelemetry в сервисах
В каждом сервисе зарегистрируйте OpenTelemetry через builder.Services.AddOpenTelemetry(). Настройте три сигнала:
Трейсы — автоматическая инструментация входящих HTTP-запросов, исходящих HTTP-запросов и запросов EF Core. Экспорт в Jaeger через OTLP:
.WithTracing(tracing => tracing
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddEntityFrameworkCoreInstrumentation()
    .AddOtlpExporter(o => o.Endpoint = new Uri(configuration["Otlp:Endpoint"]!)))
Метрики — инструментация ASP.NET Core (latency, throughput, error rate) и метрики рантайма .NET. Экспорт через Prometheus:
.WithMetrics(metrics => metrics
    .AddAspNetCoreInstrumentation()
    .AddRuntimeInstrumentation()
    .AddPrometheusExporter())
После регистрации добавьте эндпоинт для скрейпинга:
app.MapPrometheusScrapingEndpoint(); // доступен по /metrics
Именование сервиса — чтобы в Jaeger и Prometheus сервисы различались, задайте имя ресурса:
.ConfigureResource(r => r.AddService(serviceName: "events-service"))
Этап 4. Структурированное логирование
Настройте Serilog как провайдер логирования с выводом в JSON-формате:
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Console(new CompactJsonFormatter()));
Это позволит агрегировать логи из всех сервисов в единой системе сбора — каждая строка лога будет полноценным JSON-объектом с полями уровня, временной метки, источника и сообщения.
Этап 5. Конфигурация
Вынесите OTLP endpoint и имя сервиса в appsettings.json:
{
  "Otlp": {
    "Endpoint": "http://localhost:4317"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
Этап 6. Обновление Docker Compose
Добавьте в docker-compose.yml три новых контейнера: Prometheus, Jaeger и Grafana.
Вот что нужно добавить в docker-compose.yml:
services:
  # ... существующие сервисы ...

  prometheus:
    image: prom/prometheus:v2.51.0
    container_name: eventapi-prometheus
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro
    ports:
      - "9090:9090"

  jaeger:
    image: jaegertracing/all-in-one:1.56
    container_name: eventapi-jaeger
    ports:
      - "16686:16686"   # UI
      - "4317:4317"     # OTLP gRPC
    environment:
      COLLECTOR_OTLP_ENABLED: "true"

  grafana:
    image: grafana/grafana:10.4.2
    container_name: eventapi-grafana
    ports:
      - "3000:3000"
    environment:
      GF_SECURITY_ADMIN_PASSWORD: admin
    volumes:
      - grafana-data:/var/lib/grafana

volumes:
  # ... существующие тома ...
  grafana-data:
Для каждого сервиса передайте OTLP endpoint через переменную окружения — внутри сети Docker Jaeger доступен по имени контейнера:
events-service:
  environment:
    Otlp__Endpoint: http://jaeger:4317
Рядом с docker-compose.yml создайте файл prometheus.yml, в котором опишите, с каких сервисов Prometheus будет собирать метрики:
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: events-service
    static_configs:
      - targets: ["events-service:8080"]
    metrics_path: /metrics

  - job_name: bookings-service
    static_configs:
      - targets: ["bookings-service:8080"]
    metrics_path: /metrics

  - job_name: users-service
    static_configs:
      - targets: ["users-service:8080"]
    metrics_path: /metrics
Этап 7. Дашборд в Grafana
Запустите стек через docker compose up.
Откройте Grafana на http://localhost:3000 (логин admin, пароль admin).
Добавьте источник данных Prometheus с адресом http://prometheus:9090.
Создайте новый дашборд и добавьте панели с метриками. Для ASP.NET Core автоматически доступны метрики:
Метрика	Что отображает
http_server_request_duration_seconds	Latency (p50, p95, p99)
http_server_active_requests	Текущее количество запросов в обработке
http_server_request_duration_seconds_count	Throughput (RPS)
Сохраните дашборд и экспортируйте его в JSON (Dashboard → Share → Export). Добавьте JSON-файл дашборда в репозиторий.
Этап 8. Проверка

Убедитесь, что эндпоинт /metrics каждого сервиса возвращает данные в формате Prometheus.
Откройте Jaeger UI (http://localhost:16686) и убедитесь, что трейсы от сервисов появляются и содержат корректные спаны.
Убедитесь, что Prometheus успешно скрейпит все три сервиса (раздел Status → Targets).
Убедитесь, что дашборд в Grafana отображает данные.
Этап 9. Оформление и сдача
Добавьте в README раздел о наблюдаемости: какие инструменты добавлены, на каких портах доступны их UI, как запустить стек мониторинга.
Добавьте в репозиторий файлы prometheus.yml и экспортированный JSON дашборда Grafana.
Сделайте финальный коммит и пуш всех изменений в ветку sprint-11 на GitHub.
Создайте Pull Request из ветки sprint-11 в ветку main вашего репозитория.
Скопируйте ссылку на созданный Pull Request.
В уроке с проектом вставьте ссылку на Pull Request в поле для ссылки и нажмите «Отправить».
Ревьюер проверит вашу работу. Он оставит комментарии в Pull Request, если потребуются правки.
После того как правки по всем комментариям будут внесены, ревьюер одобрит Pull Request.
Выполните слияние изменений в ветку main.
Хорошие практики (необязательно для зачёта):

Grafana настроена через provisioning (datasource и dashboard добавлены через конфиг-файлы, не вручную).
Метрики рантайма .NET (GC, thread pool) добавлены на дашборд.