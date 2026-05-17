using EventManagementService.API.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EventManagementService.API.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class SmokeTests
{
    private readonly PostgreSqlTestcontainerFixture _fixture;

    public SmokeTests(PostgreSqlTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PostgreSqlContainer_WhenStarted_AllowsConnectionAndHasMigrationsApplied()
    {
        // Базовый smoke-тест инфраструктуры.
        // Применяем паттерн:
        // Arrange (seed) -> Act (вызов контракта репозитория) -> Assert через отдельный verify-context.
        var cancellationToken = TestContext.Current.CancellationToken;

        await _fixture.ResetDatabaseAsync(cancellationToken);
        await using var context = _fixture.CreateDbContext();

        var canConnect = await context.Database.CanConnectAsync(cancellationToken);
        canConnect.Should().BeTrue();

        // Проверяем историю миграций, чтобы убедиться, что схема создается
        // через миграции, а не через авто-создание.
        var hasMigrationsHistory = await context.Database
            .SqlQueryRaw<int>("SELECT 1 FROM \"__EFMigrationsHistory\" LIMIT 1")
            .AnyAsync(cancellationToken);

        hasMigrationsHistory.Should().BeTrue();
    }
}
