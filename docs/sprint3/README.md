# Документация проекта EventManagementService.API. Sprint 3

Эта папка содержит подробную документацию по реализации третьего спринта учебного проекта **EventManagementService.API**.

В sprint 3 проект перестал быть только CRUD API для событий и получил второй рабочий сценарий:

- создание бронирования для события;
- быстрый HTTP-ответ `202 Accepted`;
- отложенную обработку через `BackgroundService`;
- отдельное in-memory хранилище для разделяемого состояния бронирований;
- расширенный тестовый слой с unit- и integration-сценариями.

Документация построена по тому же принципу, что и `docs/sprint1` и `docs/sprint2`: от общего обзора к архитектуре, затем к прикладным деталям реализации, тестам и диаграммам.

## Структура документации

1. **[Введение и обзор спринта](01-introduction.md)**  
   Что именно добавлено в sprint 3, как изменился проект относительно sprint 2 и чему этот спринт учит с инженерной точки зрения.

2. **[Архитектурные решения](02-architecture.md)**  
   Почему для бронирований введён отдельный store, зачем понадобился `BackgroundService`, почему `BookingService` зависит от `IEventService`, и как теперь устроены lifetimes в DI.

3. **[Модель бронирований, store и сервис](03-booking-model-store-service.md)**  
   Подробный разбор `Booking`, `BookingStatus`, DTO, маппинга, `IBookingStore`, `InMemoryBookingStore`, `IBookingService` и `BookingService`.

4. **[Эндпоинты и фоновая обработка](04-endpoints-background-processing.md)**  
   Как работают `POST /api/events/{id}/book`, `GET /api/bookings/{id}`, что означает `202 Accepted`, как выставляется `Location`, и как pending-брони обрабатываются worker'ом.

5. **[Тестирование и запуск](05-testing-and-run.md)**  
   Как устроен тестовый проект после sprint 3, зачем подключён `FluentAssertions`, какие сценарии покрыты, и как запускать приложение и проверять сценарий вручную.

6. **[Диаграммы и визуализация](06-diagrams.md)**  
   Mermaid-диаграммы для общей архитектуры, HTTP-потока создания брони, фоновой обработки, store и тестового слоя.

## Дополнительные материалы

- **[Текст задания](sprint3-task.md)** — исходные требования третьего спринта.
- **[План реализации](sprint3-implementation-plan.md)** — поэтапный план, на основе которого велась реализация и принимались архитектурные решения.

## Как лучше читать эту документацию

- Если нужно понять sprint 3 целиком, начните с [введения](01-introduction.md), затем переходите к [архитектуре](02-architecture.md).
- Если важнее понять устройство `Booking`, store и сервиса, сразу открывайте [раздел по доменной модели и сервису](03-booking-model-store-service.md).
- Если основной интерес — `202 Accepted`, `Location` и `BackgroundService`, переходите в [раздел про endpoint-ы и фоновую обработку](04-endpoints-background-processing.md).
- Если нужно проверить соответствие заданию через тесты, используйте [раздел тестирования и запуска](05-testing-and-run.md).

## Связь документации с кодом

Все разделы опираются на реальные файлы проекта:

- `Program.cs`
- `Controllers/EventsController.cs`
- `Controllers/EventBookingsController.cs`
- `Controllers/BookingsController.cs`
- `Models/Booking.cs`
- `Models/BookingStatus.cs`
- `Stores/IBookingStore.cs`
- `Stores/InMemoryBookingStore.cs`
- `Services/IBookingService.cs`
- `Services/BookingService.cs`
- `BackgroundServices/BookingProcessingBackgroundService.cs`
- `EventManagementService.API.Tests/...`

Документация не дублирует код “для красоты”, а помогает читать именно эту реализацию и понимать, почему она устроена так, а не иначе.

## Что особенно важно понять в sprint 3

1. `202 Accepted` означает не “операция уже завершена”, а “операция принята и будет завершена позже”.
2. `BackgroundService` вводит в проект отдельный жизненный цикл, отличный от обычного запроса-к-ответу.
3. Появление `IBookingStore` — это не просто “ещё один слой”, а способ безопасно разделить одно состояние между API и worker'ом.
4. Тесты теперь проверяют не только CRUD и валидацию, но и асинхронный сценарий изменения состояния ресурса.

---

**EventManagementService.API**  
[Корневой README](../../README.md) | [Документация Sprint 2](../sprint2/README.md)
