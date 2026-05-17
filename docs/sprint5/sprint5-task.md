Задание
В этом спринте вы замените хранение данных в памяти приложения на полноценную работу с базой данных PostgreSQL через Entity Framework Core.
Вы настроите DbContext и DbSet для сущностей Event и Booking, опишете маппинг в таблицы с помощью Fluent API и интегрируете слой данных в существующую бизнес-логику приложения. Сервисы будут работать с AppDbContext напрямую.
В результате выполнения задания вы:

настроите подключение к PostgreSQL через Entity Framework Core;
реализуете DbContext с DbSet для сущностей Event и Booking;
опишете маппинг сущностей в таблицы БД с помощью Fluent API (Configurations);
интегрируете AppDbContext в существующие сервисы, заменив in-memory коллекции;
обновите фоновый сервис для корректной работы со scoped-зависимостями;
адаптируете юнит-тесты для работы с новой архитектурой.
Требования и функциональность
Проект использует C#, .NET 8 и выше, ASP.NET Core Web API.
Код приложения хранится в том же публичном GitHub-репозитории.
Проект собирается и запускается стандартными средствами (dotnet build, dotnet run).
Тесты запускаются командой dotnet test и успешно проходят.
Swagger доступен и корректно отображает все эндпоинты.
Данные хранятся в PostgreSQL, а не в памяти приложения.
Для взаимодействия с БД используется Entity Framework Core.
Маппинг сущностей настроен через Fluent API (IEntityTypeConfiguration<T>).
Сервисы работают с AppDbContext напрямую и зарегистрированы в DI-контейнере с корректным жизненным циклом.
Фоновый сервис правильно работает со scoped-сервисами через IServiceScopeFactory.
Как работать над заданием
Этап 1. Подготовка к работе

Убедитесь, что проектная работа четвёртого спринта успешно завершена и принята.
Переключитесь на ветку main вашего репозитория и выполните git pull для синхронизации.
Создайте новую ветку sprint-5 из ветки main и переключитесь на неё.
Всю работу по этому проекту ведите в ветке sprint-5.
Запустите docker compose-файл для работы базы данных.
Этап 2. Подключение NuGet-пакетов

Добавьте в основной проект пакеты: 
Microsoft.EntityFrameworkCore — ядро EF Core;
Npgsql.EntityFrameworkCore.PostgreSQL — провайдер для PostgreSQL.
Добавьте в тестовый проект пакет: 
Microsoft.EntityFrameworkCore.InMemory — InMemory-провайдер для юнит-тестов.
Убедитесь, что проект собирается без ошибок (dotnet build).
Этап 3. Подготовка сущностей для EF Core

EF Core использует рефлексию для создания экземпляров сущностей при чтении данных из БД. Для этого ему необходим приватный конструктор без параметров.
Добавьте в сущности Event и Booking приватные конструкторы без параметров. Если у сущности есть строковые свойства с required/non-nullable — инициализируйте их через null!, чтобы избежать предупреждений компилятора.
Добавьте навигационные свойства для связи между сущностями: 
в Event — коллекцию Booking;
в Booking — ссылку на Event.
Этап 4. Настройка DbContext

Создайте класс AppDbContext в папке DataAccess:
наследуется от DbContext;
принимает DbContextOptions<AppDbContext> в конструкторе;
содержит два DbSet: Events и Bookings;
переопределяет OnModelCreating и вызывает ApplyConfigurationsFromAssembly для автоматического подключения всех конфигураций из сборки.
internal sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
} 
Добавьте строку подключения в appsettings.json:
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=eventapi;Username=postgres;Password=postgres"
  }
} 
Этап 5. Описание маппинга через Fluent API

Создайте для каждой сущности конфигурации, реализующие интерфейс IEntityTypeConfiguration<T>. Рекомендуем размещать их в папке DataAccess/Configurations.
Создайте EventConfiguration: 
укажите имя таблицы;
настройте первичный ключ по Id с ValueGeneratedNever() (идентификатор генерируется в коде);
настройте ограничения для свойств: IsRequired(), HasMaxLength() и т. д.;
настройте связь «один–ко–многим» с Booking через навигационные свойства.
Создайте BookingConfiguration: 
укажите имя таблицы;
настройте первичный ключ по Id с ValueGeneratedNever();
настройте хранение Status как строки в БД через HasConversion<string>();
настройте связь с Event через HasOne/WithMany и внешний ключ EventId.
Этап 6. Интеграция DbContext в сервисы

Замените зависимости на in-memory хранилища в сервисах на AppDbContext. Сервисы будут работать с DbSet<T> напрямую.
Обновите EventService: 
принимайте AppDbContext через конструктор;
используйте _context.Events (DbSet<Event>) для всех операций с данными;
сделайте методы асинхронными (используйте await вместо синхронных обёрток);
вызывайте SaveChangesAsync() после операций, изменяющих данные (добавление, обновление, удаление).
Обновите BookingService: 
принимайте AppDbContext через конструктор;
используйте _context.Events и _context.Bookings для работы с данными;
обратите внимание: если вы использовали lock для защиты критической секции, его необходимо заменить на SemaphoreSlim, поскольку внутри критической секции теперь используются await-вызовы (нельзя использовать await внутри lock);
учтите, что DbContext — scoped, а значит сервис тоже должен быть scoped; если семафор нужен для синхронизации между экземплярами — сделайте его static;
один вызов SaveChangesAsync() сохраняет и новую бронь, и изменение AvailableSeats у события, поскольку оба объекта отслеживаются одним контекстом.
Убедитесь, что сервисы зарегистрированы в DI-контейнере как scoped (поскольку DbContext — scoped).
Удалите in-memory хранилища, которые больше не используются.
Этап 7. Обновление фонового сервиса

BackgroundService — синглтон, а DbContext — scoped. Нельзя внедрить scoped-зависимость в синглтон напрямую.
Замените прямые зависимости на IServiceScopeFactory.
Для получения списка необработанных бронирований создайте scope, получите AppDbContext и извлеките идентификаторы Pending-бронирований, затем scope закрывается.
Для обработки каждой брони создайте отдельный scope — так каждая задача получит свой DbContext.
Поскольку каждая задача теперь работает со своим экземпляром контекста, пересмотрите необходимость существующих примитивов синхронизации в фоновом сервисе.
Этап 8. Создание схемы базы данных

Для создания объектов БД используйте метод EnsureCreated — EF Core автоматически создаст таблицы при первом запуске, если их ещё нет.
Добавьте в Program.cs блок после builder.Build() и перед маппингом эндпоинтов:
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
} 
EnsureCreated создаёт базу данных и все таблицы, если их не существует. При повторных запусках метод ничего не делает, если схема уже создана.
Примечание: EnsureCreated не совместим с миграциями — если потом добавить миграции, потребуется либо пересоздать БД, либо использовать Migrate() вместо EnsureCreated(). Для учебных проектов EnsureCreated — самый простой вариант.
Миграции понадобятся в следующих проектах.
Запустите приложение и убедитесь, что таблицы events и bookings создались в PostgreSQL автоматически.
Этап 9. Обновление юнит-тестов

Обновите тесты для EventService и BookingService:
используйте ServiceCollection для настройки DI с InMemory-провайдером EF Core;
зарегистрируйте AppDbContext и сервисы;
для каждого тестового класса создайте уникальную InMemory-базу, чтобы тесты не влияли друг на друга.
var dbName = Guid.NewGuid().ToString();
var services = new ServiceCollection();
services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase(dbName)); 
Важно: имя базы данных нужно вынести в переменную. Если вызвать Guid.NewGuid() прямо в лямбде, каждый scope получит новую базу, и данные между scope не будут общими.
Для тестов на конкурентность создавайте отдельный scope для каждого параллельного запроса:
var tasks = Enumerable.Range(0, concurrentRequests)
    .Select(_ => Task.Run(async () =>
    {
        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        // ...
    })); 
Если у вас были тесты для in-memory хранилищ — удалите их или адаптируйте для работы через сервисы с AppDbContext.
Убедитесь, что все тесты проходят успешно (dotnet test).
Этап 10. Проверка и слияние

Протестируйте сценарий через Swagger: 
создайте событие через POST /events;
создайте бронь через POST /events/{id}/book;
проверьте статус брони через GET /bookings/{id};
убедитесь, что данные сохраняются в PostgreSQL и доступны после перезапуска приложения.
Обновите README.md: 
укажите требование PostgreSQL для запуска приложения;
добавьте инструкцию по настройке строки подключения;
укажите, что схема БД создаётся автоматически при запуске через EnsureCreated;
укажите использование InMemory-провайдера в тестах.
Убедитесь, что: 
проект собирается и запускается без ошибок;
все тесты проходят успешно.
Сделайте финальный commit и push всех изменений в ветку sprint-5 на GitHub.
Проверьте результат с помощью авторского решения ниже.
После того как убедитесь, что ваше решение корректно и соответствует требованиям, выполните слияние изменений в ветку main.