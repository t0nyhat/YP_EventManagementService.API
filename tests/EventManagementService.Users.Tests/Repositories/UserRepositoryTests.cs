using EventManagementService.Users.Domain.Models;
using EventManagementService.Users.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using EventManagementService.Users.Tests.Infrastructure;

namespace EventManagementService.Users.Tests.Repositories;

/// <summary>
/// Requires a running Docker daemon (Testcontainers spins up a real PostgreSQL instance).
/// Excluded from Docker-less runs via `dotnet test --filter "Category!=RequiresDocker"`.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "RequiresDocker")]
public class UserRepositoryTests
{
    private readonly PostgreSqlTestcontainerFixture _fixture;

    public UserRepositoryTests(PostgreSqlTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveChangesAsync_WhenLoginAlreadyExists_ThrowsDbUpdateException()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        await using (var seedContext = _fixture.CreateDbContext())
        {
            var seedRepository = new UserRepository(seedContext);
            await seedRepository.AddAsync(User.Create("duplicate-login", "hash-1"), cancellationToken);
            await seedRepository.SaveChangesAsync(cancellationToken);
        }

        await using var actContext = _fixture.CreateDbContext();
        var repository = new UserRepository(actContext);
        await repository.AddAsync(User.Create("duplicate-login", "hash-2"), cancellationToken);

        var act = async () => await repository.SaveChangesAsync(cancellationToken);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task GetByLoginAsync_WhenUserExists_ReturnsUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var user = User.Create("testuser", "hash", UserRole.User);

        await using (var seedContext = _fixture.CreateDbContext())
        {
            var seedRepository = new UserRepository(seedContext);
            await seedRepository.AddAsync(user, cancellationToken);
            await seedRepository.SaveChangesAsync(cancellationToken);
        }

        await using var readContext = _fixture.CreateDbContext();
        var repository = new UserRepository(readContext);
        var found = await repository.GetByLoginAsync("testuser", cancellationToken);

        found.Should().NotBeNull();
        found!.Id.Should().Be(user.Id);
        found.Login.Should().Be("testuser");
        found.Role.Should().Be(UserRole.User);
    }

    [Fact]
    public async Task GetByLoginAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        await using var context = _fixture.CreateDbContext();
        var repository = new UserRepository(context);
        var found = await repository.GetByLoginAsync("nonexistent", cancellationToken);

        found.Should().BeNull();
    }
}