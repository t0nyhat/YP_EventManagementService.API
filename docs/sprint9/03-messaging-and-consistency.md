# Обмен сообщениями и согласованность

## 1. Почему нельзя «просто опубликовать» событие

Наивная реализация подтверждения брони выглядит так:

```csharp
booking.Confirm();
await _context.SaveChangesAsync();      // (1) статус в БД
await _producer.ProduceAsync(message);  // (2) событие в Kafka
```

Между (1) и (2) есть окно отказа: если процесс упал или Kafka недоступна, бронь подтверждена, а Events об этом никогда не узнает — места не уменьшатся. Обратный порядок ещё хуже: событие улетело, а транзакция откатилась.

Это классическая проблема **dual write**: две системы (БД и брокер) нельзя изменить одной транзакцией.

## 2. Паттерн Outbox (Bookings)

Решение — записывать событие в ту же базу, в той же транзакции, что и бизнес-изменение:

1. [`BookingProcessingService`](../../src/EventManagementService.Bookings.Application/Services/BookingProcessingService.cs) подтверждает бронь и добавляет строку в `booking_outbox` — **один `SaveChanges`, одна транзакция**;
2. фоновый [`BookingOutboxPublisherBackgroundService`](../../src/EventManagementService.Bookings.Infrastructure/Messaging/BookingOutboxPublisherBackgroundService.cs) раз в секунду выбирает неопубликованные строки (батч до 50) и передаёт их [`BookingOutboxPublisher`](../../src/EventManagementService.Bookings.Infrastructure/Messaging/BookingOutboxPublisher.cs);
3. успех → `published_at_utc` заполняется; ошибка → `publish_attempts++`, `last_error`, строка остаётся в очереди и будет ретраиться;
4. опубликованные строки старше 7 дней раз в час удаляются (`PurgePublishedAsync`), чтобы таблица не росла бесконечно.

Свойства:

- бизнес-факт и намерение публикации атомарны — событие **не может потеряться**;
- публикация — **at-least-once**: при падении между `ProduceAsync` и пометкой строки событие уйдёт повторно. Дубликаты — забота получателя (см. Inbox).

В outbox-строке хранится готовый JSON (`payload`, сериализованный через общий `KafkaJson.Options`) плюс ключевые идентификаторы для логов и ключа сообщения.

## 3. Ключ сообщения и порядок

[`KafkaBookingConfirmedPublisher`](../../src/EventManagementService.Bookings.Infrastructure/Messaging/KafkaBookingConfirmedPublisher.cs) отправляет сообщение с ключом `EventId.ToString("D")`. Сообщения с одним ключом попадают в один partition и читаются строго по порядку — все подтверждения по одному событию обрабатываются последовательно, что исключает гонки за `available_seats` даже при нескольких partition'ах.

Продюсер настроен на надёжность: `Acks.All` (подтверждение всех реплик) и `EnableIdempotence = true` (брокер отбрасывает дубли при ретраях продюсера). Это тяжёлый потокобезопасный объект — регистрируется как **singleton** и освобождается при остановке приложения (`IDisposable`, с `Flush` в `Dispose`).

## 4. Паттерн Inbox и идемпотентность (Events)

At-least-once означает: Events обязан переживать повторную доставку. Для этого — таблица `booking_confirmed_inbox` с **первичным ключом `booking_id`**.

Алгоритм [`BookingConfirmedHandler`](../../src/EventManagementService.Events.Infrastructure/Messaging/BookingConfirmedHandler.cs):

| Шаг | Условие | Действие | `result` в inbox |
|---|---|---|---|
| 1 | `booking_id` уже в inbox | ничего не делаем (no-op) | — |
| 2 | событие не найдено | warning в лог, пропуск | `EventNotFound` |
| 3 | событие уже началось | warning в лог, пропуск | `EventAlreadyStarted` |
| 4 | мест не хватает | warning в лог, пропуск | `NotEnoughSeats` |
| 5 | всё корректно | `available_seats -= seats` | `Processed` |

Ключевая деталь: уменьшение мест и запись inbox-строки сохраняются **одним `SaveChanges`** — одной транзакцией. Дубль сообщения либо увидит inbox-строку (шаг 1), либо упадёт на уникальности PK — но места дважды не спишутся ни при каком раскладе.

Inbox-строки сознательно **не удаляются**: это история идемпотентности; удаление окна дедупликации разрешило бы «очень поздним» дублям списать места повторно.

## 5. Гарантии консюмера

[`BookingConfirmedConsumerService`](../../src/EventManagementService.Events.Infrastructure/Messaging/BookingConfirmedConsumerService.cs) работает с ручным управлением оффсетами (`EnableAutoCommit = false`):

- **успех** → `Commit(result)` после обработки, счётчик попыток для этого оффсета сбрасывается;
- **ошибка обработки** (например, БД временно недоступна) → `Seek` на упавший оффсет + пауза 5 секунд, без коммита: позиция консюмера уже сдвинулась при `Consume`, и без `Seek` следующий успешный `Commit` навсегда пропустил бы сообщение;
- **лимит попыток исчерпан** (`Kafka:MaxHandlerAttempts`, по умолчанию 5) или **сообщение в принципе невалидно** (битый JSON, `Seats <= 0`) → сообщение уходит в Dead Letter Topic (раздел 6), офсет коммитится.

Так достигается инвариант: **подтверждённая бронь рано или поздно уменьшит места**, если событие существует, места есть и ошибка была временной — а систематическая ошибка не блокирует остальные сообщения бесконечно.

## 6. Dead Letter Topic

Kafka не даёт «пропустить» одно сообщение и продолжить читать со следующего без коммита — коммит оффсета означает «всё до этой позиции прочитано». Поэтому при неустранимой ошибке решение нужно принять **до коммита**: либо ждать бесконечно (блокируя партицию), либо изолировать сообщение и двигаться дальше. Второй вариант — паттерн **Dead Letter Topic** (DLT), отдельный топик `booking-confirmed.DLT` (константа [`KafkaTopics.BookingConfirmedDeadLetter`](../../src/EventManagementService.Contracts/BookingConfirmed.cs)).

Различаются два рода неустранимых ошибок:

- **сообщение невалидно в принципе** — битый JSON или `Seats <= 0`: повтор ничего не изменит, поэтому такое сообщение уходит в DLT **немедленно**, без ретраев;
- **обработчик систематически падает** (не разовый сбой БД, а стабильная ошибка) — сначала консюмер честно пробует `MaxHandlerAttempts` раз через `Seek` (транзиентные сбои чинятся сами за это время), и только исчерпав лимит, изолирует сообщение в DLT. Уходить в DLT при первой же ошибке было бы неверно: короткий блип БД во время деплоя навсегда сослал бы валидное сообщение в DLT, требуя ручного разбора там, где хватило бы одного повтора.

[`KafkaDeadLetterPublisher`](../../src/EventManagementService.Events.Infrastructure/Messaging/KafkaDeadLetterPublisher.cs) публикует **оригинальный payload нетронутым** в `Value` (чтобы сообщение можно было буквально переиграть обратно в основной топик) и диагностику — в заголовках, а не в теле:

| Заголовок | Источник | Назначение |
|---|---|---|
| `error-reason` | текст исключения / причина валидации | диагностика |
| `error-source-topic` | `TopicPartitionOffset.Topic` | из какого топика пришло |
| `error-source-partition` | `TopicPartitionOffset.Partition` | точная локализация |
| `error-source-offset` | `TopicPartitionOffset.Offset` | точная локализация |
| `error-timestamp` | `DateTimeOffset.UtcNow` | хронология инцидента |

Топик `booking-confirmed.DLT` создаётся тем же [`KafkaTopicInitializer`](../../src/EventManagementService.Events.Infrastructure/Messaging/KafkaTopicInitializer.cs), что и основной — при старте, идемпотентно, без падения сервиса при неудаче. Если публикация в DLT сама не удалась (например, брокер временно недоступен), офсет **не коммитится** — at-least-once отработает и на следующей итерации попытка повторится, вместо того чтобы молча потерять сообщение.

DLT — обычный топик; отдельный консьюмер для него (автоматический реплей с задержкой, ручной разбор оператором, алерт на ненулевой consumer lag) в рамках спринта не реализован — это следующий шаг эксплуатации, за пределами scope учебного проекта.

## 7. Конкурентность внутри Bookings

Асинхронная модель добавила две внутренние гонки, решённые на уровне БД (а не блокировками в памяти — они не работают при нескольких инстансах сервиса):

**Гонка «отмена во время подтверждения».** Пользователь отменяет бронь, пока фоновый обработчик её подтверждает. Решение — `Status` объявлен **concurrency token** ([`BookingConfiguration`](../../src/EventManagementService.Bookings.Infrastructure/DataAccess/Configurations/BookingConfiguration.cs)): EF Core добавляет в `UPDATE` условие `WHERE status = <прочитанный>`. Проигравшая сторона получает `DbUpdateConcurrencyException`, транслируемую в доменную `ConcurrencyConflictException`:

- проиграло подтверждение → оно просто пропускается (отмена победила, outbox-строка не сохраняется);
- проиграла отмена → бронь перечитывается и отменяется повторно (отмена из `Confirmed` допустима).

**Лимит активных броней при параллельных запросах.** Проверка «посчитать активные → вставить» без синхронизации позволяет двум параллельным запросам обойти лимит. Решение — [`BookingRepository.AddWithActiveLimitAsync`](../../src/EventManagementService.Bookings.Infrastructure/Repositories/BookingRepository.cs): транзакция + `pg_advisory_xact_lock(hashtextextended(userId, 0))`. Лок сериализует создание броней **одного пользователя** между запросами и инстансами сервиса, не задевая остальных, и снимается автоматически при завершении транзакции.

## 7. Крайние случаи и осознанные ограничения

Модель eventual consistency оставляет сценарии, где бронь в Bookings остаётся `Confirmed`, хотя место не выделено (`EventNotFound`, `EventAlreadyStarted`, `NotEnoughSeats`), и отмена брони не возвращает место в Events. Компенсация потребовала бы обратного события (`BookingRejected`/`BookingCancelled`) — задание спринта явно ограничивает обмен одним `BookingConfirmed`, поэтому ограничения зафиксированы в [README](../../README.md) и покрыты тестами обработчика.

---

[Далее: Реализация в коде →](04-implementation.md)
