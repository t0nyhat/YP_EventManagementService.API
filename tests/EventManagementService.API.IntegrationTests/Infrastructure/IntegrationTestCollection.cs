namespace EventManagementService.API.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgreSqlTestcontainerFixture>
{
    public const string Name = "PostgreSql integration collection";
}
