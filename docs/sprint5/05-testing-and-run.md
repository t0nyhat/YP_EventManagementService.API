# Тестирование и запуск sprint 5

## 1. Стратегия тестирования

После перехода на EF Core тесты проверяют ту же бизнес-логику, но через инфраструктуру `DbContext`.

### Что важно

- InMemory БД создаётся с уникальным именем на тестовый класс.
- В конкурентных сценариях каждый параллельный запрос использует отдельный scope.
- Integration тесты поднимают тестовый host с `AppDbContext` на `UseInMemoryDatabase`.

## 2. Как запускать тесты

```bash
dotnet test
```

Ожидаемый результат: все тесты зелёные.

## 3. Запуск PostgreSQL через Docker

Из корня репозитория:

```bash
docker compose up -d
```

Проверка:

```bash
docker compose ps
```

Остановка:

```bash
docker compose down
```

Удаление данных volume (если нужно начать с нуля):

```bash
docker compose down -v
```

## 4. Запуск приложения

```bash
dotnet run --project src/EventManagementService.API/EventManagementService.API.csproj
```

При первом запуске таблицы будут созданы автоматически через `EnsureCreated`.

## 5. Ручная проверка через Swagger

1. Открыть `http://localhost:5248/swagger`.
2. Создать событие `POST /api/events`.
3. Создать бронь `POST /api/events/{id}/book`.
4. Проверить `GET /api/bookings/{id}` до и после обработки worker'ом.

## 6. Частые проблемы и диагностика

1. Ошибка подключения к БД:
- проверить, что контейнер PostgreSQL запущен;
- проверить строку подключения в `appsettings.json`.

2. Таблицы не появились:
- убедиться, что `EnsureCreated` вызывается после `builder.Build()`.

3. Нестабильные конкурентные тесты:
- проверить, что в каждой параллельной задаче создаётся отдельный scope.

---

[Далее: Диаграммы →](06-diagrams.md)
