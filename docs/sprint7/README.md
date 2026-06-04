# Sprint 7: Документация

Полная учебная документация по спринту 7: реорганизация сервиса бронирований в четыре production-проекта по принципам Clean Architecture.

## Структура документации

1. **[01-introduction.md](01-introduction.md)** — Обзор спринта, цели и мотивация
   - Почему монолитный Web API-проект стал ограничением
   - Какие границы появились между слоями
   - Что изменилось для разработки и ревью

2. **[02-architecture.md](02-architecture.md)** — Архитектура решения
   - Domain, Application, Infrastructure, Presentation
   - Направление зависимостей
   - Composition root и dependency inversion

3. **[03-layers-and-ports.md](03-layers-and-ports.md)** — Слои, порты и адаптеры
   - Доменные сущности и исключения
   - Application-сервисы и DTO
   - Repository ports и EF Core adapters

4. **[04-implementation.md](04-implementation.md)** — Реализация в коде
   - Перенос файлов по проектам
   - DI extension-методы
   - Фоновая обработка бронирований после выделения use case

5. **[05-testing-and-run.md](05-testing-and-run.md)** — Тестирование и запуск
   - Unit и integration test references
   - Команды build/test/run
   - Swagger и ручная проверка API

6. **[06-diagrams.md](06-diagrams.md)** — Диаграммы
   - Зависимости проектов
   - Поток HTTP-запроса
   - Поток фоновой обработки бронирования

## Как читать эту документацию

### Для обзора и защиты решения

1. [01-introduction.md](01-introduction.md)
2. [02-architecture.md](02-architecture.md)
3. [06-diagrams.md](06-diagrams.md)

### Для работы с кодом

1. [03-layers-and-ports.md](03-layers-and-ports.md)
2. [04-implementation.md](04-implementation.md)
3. [05-testing-and-run.md](05-testing-and-run.md)

### Для сверки с требованиями

1. [sprint7-task.md](sprint7-task.md)
2. [02-architecture.md](02-architecture.md)
3. [05-testing-and-run.md](05-testing-and-run.md)

## Что принципиально изменилось относительно sprint 6

- Production-код разделен на четыре отдельные сборки: Domain, Application, Infrastructure, Presentation.
- Доменные модели и доменные исключения вынесены из Web API-проекта в Domain.
- Use cases, DTO и порты репозиториев находятся в Application.
- EF Core, `AppDbContext`, migrations и реализации репозиториев находятся в Infrastructure.
- Controllers, middleware, Swagger, hosted service adapter и `Program.cs` находятся в Presentation.
- `Application` не зависит от `Infrastructure`; инфраструктура подключается через интерфейсы портов.
- Тестовые проекты ссылаются на конкретные слои, а не на монолитный проект.

---

[Назад к документации по спринтам](../README.md)
