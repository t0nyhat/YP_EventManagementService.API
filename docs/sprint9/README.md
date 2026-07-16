# Sprint 9: Документация

Полная учебная документация по спринту 9: декомпозиция монолита на три микросервиса (Users, Events, Bookings), у каждого своя база PostgreSQL, и асинхронный обмен сообщениями через Apache Kafka с паттернами Outbox и Inbox.

## Структура документации

1. **[01-introduction.md](01-introduction.md)** — Обзор спринта, цели и мотивация
   - Зачем делить монолит и по каким границам
   - Что такое событийно-ориентированное взаимодействие и eventual consistency
   - Что осталось прежним внутри каждого сервиса

2. **[02-architecture.md](02-architecture.md)** — Архитектура решения
   - Три сервиса × четыре слоя Clean Architecture + общий проект контрактов
   - Правила зависимостей между проектами и запрет прямых HTTP-вызовов
   - Разделение данных: три базы, связи только по идентификаторам

3. **[03-messaging-and-consistency.md](03-messaging-and-consistency.md)** — Обмен сообщениями и согласованность
   - Контракт `BookingConfirmed`, имя топика и общие настройки сериализации
   - Паттерн Outbox в Bookings и паттерн Inbox (идемпотентность) в Events
   - Гарантии доставки: at-least-once, порядок по ключу, повтор при ошибке, лимит попыток
   - Dead Letter Topic: изоляция невалидных и систематически падающих сообщений
   - Конкурентность: concurrency token на статусе брони и advisory lock на лимите

4. **[04-implementation.md](04-implementation.md)** — Реализация в коде
   - Продюсер Kafka (singleton, идемпотентность), консюмер на `BackgroundService` со scope на сообщение
   - Инициализация топика при старте, обработка ошибок фоновых циклов
   - Проверка общего JWT в трёх сервисах, нормализация дат к UTC

5. **[05-testing-and-run.md](05-testing-and-run.md)** — Тестирование и запуск
   - Тестовые проекты по границам сервисов: unit, InMemory, Testcontainers, integration
   - `docker compose up` — полный стек (Zookeeper, Kafka, три БД, три API)
   - Ручной end-to-end сценарий через Swagger

6. **[06-diagrams.md](06-diagrams.md)** — Диаграммы
   - Контейнерная схема системы
   - Поток `BookingConfirmed`: от брони до уменьшения мест
   - Логика обработчика сообщения (идемпотентность и крайние случаи)
   - Гонка «отмена во время подтверждения»

## Как читать эту документацию

### Для обзора и защиты решения

1. [01-introduction.md](01-introduction.md)
2. [02-architecture.md](02-architecture.md)
3. [06-diagrams.md](06-diagrams.md)

### Для работы с кодом

1. [03-messaging-and-consistency.md](03-messaging-and-consistency.md)
2. [04-implementation.md](04-implementation.md)
3. [05-testing-and-run.md](05-testing-and-run.md)

### Для сверки с требованиями

1. [sprint9-task.md](sprint9-task.md)
2. [03-messaging-and-consistency.md](03-messaging-and-consistency.md)
3. [05-testing-and-run.md](05-testing-and-run.md)

## Что принципиально изменилось относительно sprint 8

- Монолит разделён на три самостоятельных сервиса: **Users/Auth**, **Events**, **Bookings** — каждый со своими слоями Domain / Application / Infrastructure / Presentation.
- У каждого сервиса **своя база PostgreSQL** и своя миграция EF Core; FK между «чужими» сущностями и навигационные свойства убраны — связь только по `Guid`-идентификаторам.
- Появился разделяемый проект `EventManagementService.Contracts`: имя топика, record `BookingConfirmed`, общие настройки JSON-сериализации.
- Bookings при подтверждении брони **публикует событие в Kafka** через паттерн Outbox; Events **подписан на топик** и уменьшает `available_seats`, обеспечивая идемпотентность через Inbox.
- Сервисы **не вызывают друг друга по HTTP** — система согласована в конечном счёте (eventual consistency): Bookings не проверяет существование события и места при создании брони.
- Events изолирует необрабатываемые сообщения в **Dead Letter Topic** (`booking-confirmed.DLT`) после исчерпания лимита попыток — партиция не блокируется навсегда одним «отравленным» сообщением.
- JWT-токен по-прежнему выдаёт только Users; Events и Bookings проверяют тот же токен (общие секрет, издатель, аудитория).
- Вся система поднимается одной командой `docker compose up`: Zookeeper, Kafka, три БД, три API с multi-stage Dockerfile.

---

[Назад к документации по спринтам](../README.md)
