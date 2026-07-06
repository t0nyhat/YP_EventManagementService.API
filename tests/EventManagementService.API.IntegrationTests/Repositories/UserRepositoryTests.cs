using EventManagementService.API.IntegrationTests.Infrastructure;
using EventManagementService.Domain.Models;
using EventManagementService.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EventManagementService.API.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
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
}
