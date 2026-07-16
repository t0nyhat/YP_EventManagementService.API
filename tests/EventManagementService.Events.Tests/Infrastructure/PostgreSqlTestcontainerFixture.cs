using EventManagementService.Events.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EventManagementService.Events.Tests.Infrastructure;

/// <summary>
/// Fixture that manages a PostgreSQL Testcontainer for integration tests.
/// Resets database state via TRUNCATE between tests.
/// </summary>
public sealed class PostgreSqlTestcontainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    public PostgreSqlTestcontainerFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("events_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // Apply migrations once at fixture startup.
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Creates a new <see cref="EventsDbContext"/> connected to the test container.
    /// </summary>
    public EventsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EventsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new EventsDbContext(options);
    }

    /// <summary>
    /// Clears all tables without dropping and recreating them.
    /// Uses TRUNCATE with RESTART IDENTITY and CASCADE to handle any future FK references.
    /// </summary>
    public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateDbContext();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE events, booking_confirmed_inbox RESTART IDENTITY CASCADE;",
            cancellationToken);
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgreSqlTestcontainerFixture>
{
    public const string Name = "PostgreSQL Integration Tests";
}
