using EventManagementService.Users.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EventManagementService.Users.Tests.Infrastructure;

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
            .WithDatabase("users_test")
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
    /// Creates a new <see cref="UsersDbContext"/> connected to the test container.
    /// </summary>
    public UsersDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new UsersDbContext(options);
    }

    /// <summary>
    /// Clears all tables without dropping and recreating them.
    /// Uses TRUNCATE with RESTART IDENTITY and CASCADE to handle any future FK references.
    /// </summary>
    public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateDbContext();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE users RESTART IDENTITY CASCADE;",
            cancellationToken);
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgreSqlTestcontainerFixture>
{
    public const string Name = "PostgreSQL Integration Tests";
}