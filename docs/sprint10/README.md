# Sprint 10: Документация

Полная учебная документация по спринту 10: Redis в сервисе Events, паттерн Cache-Aside для чтения события и топ-10, инвалидация кеша после записей и безопасная деградация при недоступном кеш-сервере.

## Структура документации

1. **[01-introduction.md](01-introduction.md)** — Обзор спринта, цели и мотивация
   - Зачем кешировать публичные read-пути Events
   - Почему Redis остаётся ускорителем, а PostgreSQL — источником истины
   - Что изменилось относительно sprint 9

2. **[02-architecture.md](02-architecture.md)** — Архитектура решения
   - Порт `ICacheService` в Application и Redis-адаптер в Infrastructure
   - Границы ответственности слоёв и DI-регистрация singleton-компонентов
   - Новый endpoint топа и SQL-ранжирование событий

3. **[03-cache-strategy.md](03-cache-strategy.md)** — Стратегия кеширования и согласованность
   - Cache-Aside для `event:{id}` и `events:top10`
   - Разные TTL и матрица инвалидации
   - Порядок «сначала commit БД, затем кеш» и влияние Kafka
   - Деградация, сериализация и осознанные ограничения

4. **[04-implementation.md](04-implementation.md)** — Реализация в коде
   - `EventService`, `EventCacheKeys`, `CacheOptions`
   - `RedisCacheService`, `CacheJson`, `RedisOptions`
   - Инвалидация из `BookingConfirmedHandler`, конфигурация и Docker Compose

5. **[05-testing-and-run.md](05-testing-and-run.md)** — Тестирование и запуск
   - Unit-, PostgreSQL/Testcontainers- и HTTP integration-тесты
   - Полный прогон с Docker и запуск без Docker
   - Ручная проверка hit/miss, инвалидации и degraded mode

6. **[06-diagrams.md](06-diagrams.md)** — Диаграммы
   - Место Redis в архитектуре Events
   - Cache-Aside для отдельного события и топ-10
   - Инвалидация после CRUD и Kafka-сообщения
   - Работа API при недоступном Redis

## Как читать эту документацию

### Для обзора и защиты решения

1. [01-introduction.md](01-introduction.md)
2. [02-architecture.md](02-architecture.md)
3. [06-diagrams.md](06-diagrams.md)

### Для работы с кодом

1. [03-cache-strategy.md](03-cache-strategy.md)
2. [04-implementation.md](04-implementation.md)
3. [05-testing-and-run.md](05-testing-and-run.md)

### Для сверки с требованиями

1. [sprint10-task.md](sprint10-task.md)
2. [03-cache-strategy.md](03-cache-strategy.md)
3. [05-testing-and-run.md](05-testing-and-run.md)

## Что принципиально изменилось относительно sprint 9

- В runtime-стек добавлен Redis 7.2; кеш использует только сервис Events.
- В Application появился инфраструктурно-независимый порт `ICacheService`, единые ключи `EventCacheKeys` и типизированная TTL-политика `CacheOptions`.
- `GET /events/{id}` и новый публичный `GET /events/top` работают по Cache-Aside: hit не обращается к PostgreSQL, miss читает БД и прогревает кеш.
- Репозиторий Events рассчитывает топ-10 по доле проданных мест целиком в PostgreSQL с детерминированным порядком при равенстве рейтинга.
- Ключ отдельного события удаляется после успешных create/update/delete и после успешной обработки `BookingConfirmed`; агрегат топа обновляется только по TTL.
- Ошибки Redis логируются и не ломают API: read становится cache miss, write/delete — no-op; отмена запроса при этом не маскируется.
- Настройки JSON кеша вынесены в `CacheJson.Options`, а ключи — в `EventCacheKeys`, чтобы формат хранилища не расходился между читателями и писателями.
- Тесты Events расширены сценариями hit, miss, TTL, порядка инвалидации, ранжирования топа и работы с недоступным Redis.

---

[Назад к документации по спринтам](../README.md)
