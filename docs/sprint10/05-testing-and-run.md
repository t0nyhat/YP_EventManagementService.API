# Тестирование и запуск sprint 10

## 1. Итоговый набор тестов

Полный solution содержит 131 тест:

```text
Bookings: 25
Users:    21
Events:   85
Total:   131
```

Тесты используют xUnit v3, FluentAssertions и Moq; PostgreSQL integration-тесты запускаются через Testcontainers.

## 2. Тесты кеширования Events

### EventServiceTests — Cache-Aside и write paths

Порт `ICacheService` и репозиторий подменяются Moq-объектами. Проверяются:

- hit по `event:{id}` возвращает DTO без вызова репозитория;
- miss читает репозиторий ровно один раз и записывает DTO с `EventTtl`;
- 404 не кешируется;
- hit топа не вызывает `GetTopEventsAsync` репозитория;
- miss топа запрашивает ровно десять событий и пишет с `TopEventsTtl`;
- пустой топ кешируется как пустой массив;
- create/update/delete удаляют только `event:{id}` и только после успешного save;
- ошибка save не запускает инвалидацию;
- ключ `events:top10` при CRUD не удаляется.

### RedisCacheServiceTests — инфраструктурный адаптер

`IConnectionMultiplexer` и `IDatabase` подменяются. Проверяются сериализация/deserialization, точный ключ и TTL, hit/miss, удаление повреждённого JSON, логирование Redis-ошибок, no-op при сбоях записи/удаления, валидация аргументов и распространение cancellation.

### EventCacheKeysTests

Фиксируют стабильный формат GUID (`D`, lowercase), детерминированность `ForEvent` и константу `events:top10`.

### BookingConfirmedHandlerTests

Помимо Inbox-идемпотентности проверяют кеш-инварианты:

- успешное уменьшение мест инвалидирует ровно ключ изменённого события;
- удаление происходит после сохранения Event + Inbox;
- для инвалидации после commit используется неотменяемый token;
- duplicate и skipped-ветки не трогают кеш;
- ключ топа не удаляется.

## 3. Тесты топа и degraded mode

[`EventRepositoryTests`](../../tests/EventManagementService.Events.Tests/Repositories/EventRepositoryTests.cs) используют реальный PostgreSQL в Testcontainers и проверяют дробный sold ratio, все tie-breakers, лимит количества, пустую БД и защиту от некорректного `count`.

[`DegradedRedisIntegrationTests`](../../tests/EventManagementService.Events.Tests/Presentation/DegradedRedisIntegrationTests.cs) поднимают полный DI-граф Events с реальным PostgreSQL и заведомо недоступным Redis. Через HTTP проверяется, что:

- `GET /events/top` возвращает `200` и данные из PostgreSQL;
- повторный запрос также успешен, хотя кеш не прогрелся;
- `GET /events/{id}` возвращает событие из БД;
- отсутствующее событие по-прежнему даёт `404`.

[`EventsControllerAuthIntegrationTests`](../../tests/EventManagementService.Events.Tests/Presentation/EventsControllerAuthIntegrationTests.cs) подтверждают, что `GET /events/top` остаётся анонимным и возвращает детерминированный результат.

## 4. Команды сборки и тестов

```bash
# сборка всего решения
dotnet build EventManagementService.API.sln

# полный прогон, включая PostgreSQL Testcontainers — Docker должен быть запущен
dotnet test EventManagementService.API.sln

# без Docker: исключить тесты с Testcontainers
dotnet test EventManagementService.API.sln --filter "Category!=RequiresDocker"
```

Категорией `RequiresDocker` помечены PostgreSQL integration-тесты Users и Events. Unit-тесты Redis не требуют настоящего Redis: протокол проверяется через mocks, а отказоустойчивый HTTP-сценарий использует недоступный адрес Redis.

## 5. Запуск полного стека

```bash
docker compose up --build -d
docker compose ps
```

Ожидаемые публичные адреса:

| Сервис | URL |
|---|---|
| Users Swagger | `http://localhost:5101/swagger` |
| Events Swagger | `http://localhost:5102/swagger` |
| Bookings Swagger | `http://localhost:5103/swagger` |
| Kafka с хоста | `localhost:29092` |

Redis доступен внутри compose-сети как `redis:6379`. Для диагностики с хоста используется `docker compose exec redis redis-cli`.

## 6. Ручная проверка топа

После создания событий и бронирований через Swagger:

```bash
# начать с cache miss
docker compose exec redis redis-cli DEL events:top10

# первый запрос читает PostgreSQL и прогревает Redis
curl -i http://localhost:5102/events/top

# ключ существует и имеет TTL не более 60 секунд
docker compose exec redis redis-cli EXISTS events:top10
docker compose exec redis redis-cli TTL events:top10

# следующий запрос должен быть cache hit
curl -i http://localhost:5102/events/top
```

Порядок результата: сначала больший процент проданных мест, затем большее абсолютное число продаж, более ранний `StartAt`, меньший `Id`.

## 7. Ручная проверка карточки и инвалидации

Подставьте GUID существующего события:

```bash
EVENT_ID=00000000-0000-0000-0000-000000000000

docker compose exec redis redis-cli DEL "event:$EVENT_ID"
curl -i "http://localhost:5102/events/$EVENT_ID"
docker compose exec redis redis-cli EXISTS "event:$EVENT_ID"
docker compose exec redis redis-cli TTL "event:$EVENT_ID"
```

После `PUT /events/{id}`, `DELETE /events/{id}` или успешного `BookingConfirmed` команда `EXISTS "event:$EVENT_ID"` должна вернуть `0`. Ключ `events:top10` при этом остаётся до истечения собственного TTL.

## 8. Ручная проверка деградации

```bash
docker compose stop redis

# оба запроса продолжают обслуживаться из PostgreSQL
curl -i http://localhost:5102/events/top
curl -i "http://localhost:5102/events/$EVENT_ID"

docker compose start redis
```

Events API не нужно перезапускать: singleton multiplexer продолжает попытки подключения. После восстановления Redis следующий miss снова прогреет кеш.

## 9. Частые проблемы

- **Testcontainers не стартует** — проверьте, что Docker Desktop запущен и доступен текущему пользователю.
- **`redis-cli` с хоста не подключается к `localhost:6379`** — compose не публикует Redis-порт; используйте `docker compose exec redis redis-cli`.
- **После PUT виден старый топ** — это ожидаемо: `events:top10` обновляется только по минутному TTL.
- **После остановки Redis в логах warning** — это ожидаемая деградация, запрос должен завершиться через PostgreSQL.
- **API не стартует из-за Cache options** — оба TTL обязаны быть больше нуля; проверьте формат `hh:mm:ss`.

---

[Далее: Диаграммы →](06-diagrams.md)
