# Тестирование и запуск sprint 6

## 1. Стратегия тестирования

В sprint 6 используется двухуровневая модель:

- unit-тесты для сервисов и фоновой логики;
- интеграционные тесты репозиториев/схемы на реальном PostgreSQL.

## 2. Интеграционные тесты через Testcontainers

В проекте `EventManagementService.API.IntegrationTests`:

1. поднимается PostgreSQL-контейнер;
2. применяются миграции;
3. перед тестами выполняется reset пользовательских таблиц;
4. тесты выполняются с общим fixture и отключенной параллелизацией в коллекции.

Это исключает гонки за одну БД в рамках коллекции и делает результаты воспроизводимыми.

## 3. Какие сценарии покрыты

### Репозитории

- CRUD-поведение `EventRepository`;
- фильтрация и пагинация событий;
- read/write контракты `BookingRepository`;
- выборка pending ID для worker-сценариев.

### Схема и ограничения

- наличие таблиц и ключевых колонок;
- FK между `bookings` и `events`;
- каскадное удаление;
- ограничения длины и FK-ошибки.

## 4. Команды запуска

### Полный прогон

```bash
dotnet test EventManagementService.API.sln
```

### Только integration tests

```bash
dotnet test tests/EventManagementService.API.IntegrationTests/EventManagementService.API.IntegrationTests.csproj
```

### Запуск API

```bash
dotnet run --project src/EventManagementService.API/EventManagementService.API.csproj
```

## 5. Частые проблемы

1. Testcontainers не стартует:
- проверить, что Docker запущен и доступен текущему пользователю.

2. Ошибки `DateTime Kind` в PostgreSQL:
- в интеграционных тестах использовать UTC-времена.

3. Ложноположительный assert на записи:
- проверять результат через отдельный `DbContext` (`verify-context`).

4. Схема не совпадает с ожиданиями:
- убедиться, что миграции применяются (`Database.Migrate()`) и тест не пропускает reset.

---

[Далее: Диаграммы →](06-diagrams.md)
