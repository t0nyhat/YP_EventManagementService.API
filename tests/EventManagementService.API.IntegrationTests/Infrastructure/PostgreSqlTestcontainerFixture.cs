using EventManagementService.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace EventManagementService.API.IntegrationTests.Infrastructure;

public sealed class PostgreSqlTestcontainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgreSqlTestcontainerFixture()
    {
        var databasePassword = $"tc_{Guid.NewGuid():N}";

        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("event_management_tests")
            .WithUsername("postgres")
            .WithPassword(databasePassword)
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateDbContext();
        // Осознанное отличие от учебного примера:
        // проверяем реальный путь через миграции (Migrate), а не EnsureCreated.
        await context.Database.MigrateAsync();
        await SeedLegacySystemUserAsync(context);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    internal AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }

    public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Осознанное отличие от учебного примера:
        // динамически очищаем все пользовательские таблицы, чтобы тесты
        // оставались устойчивыми при расширении схемы.
        const string truncateSql = """
            DO $$
            DECLARE
                table_record RECORD;
            BEGIN
                FOR table_record IN
                    SELECT tablename
                    FROM pg_tables
                    WHERE schemaname = 'public'
                      AND tablename <> '__EFMigrationsHistory'
                LOOP
                    EXECUTE format('TRUNCATE TABLE public.%I RESTART IDENTITY CASCADE', table_record.tablename);
                END LOOP;
            END $$;
            """;

        await using var command = new NpgsqlCommand(truncateSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var context = CreateDbContext();
        await SeedLegacySystemUserAsync(context, cancellationToken);
    }

    private static async Task SeedLegacySystemUserAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Users.AnyAsync(user => user.Id == EventManagementService.Domain.Models.User.SystemUserId, cancellationToken))
        {
            return;
        }

        context.Users.Add(EventManagementService.Domain.Models.User.Create(
            "system",
            "system-hash",
            EventManagementService.Domain.Models.UserRole.User,
            EventManagementService.Domain.Models.User.SystemUserId));

        await context.SaveChangesAsync(cancellationToken);
    }
}
