using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EventManagementService.Events.Tests.Infrastructure;

/// <summary>
/// Boots Events.Presentation on its REAL production DI graph — EventService,
/// RedisCacheService, EventRepository and the singleton IConnectionMultiplexer with
/// AbortOnConnectFail=false — against a live PostgreSQL Testcontainer and a
/// deliberately unreachable Redis endpoint. Unlike <see cref="EventsWebApplicationFactory"/>,
/// nothing in the service/cache pipeline is replaced; only the Kafka hosted services
/// are removed so the host does not wait for a broker.
/// </summary>
public sealed class DegradedRedisWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly string JwtSigningKey = "замените_на_сильный_ключ_на_проде_32_байта";
    private static readonly string JwtIssuer = "EventManagementService.API";
    private static readonly string JwtAudience = "EventManagementService.API";

    /// <summary>
    /// A local port with no listener (verified free), plus tight timeouts and a single
    /// connect retry so every cache operation fails within milliseconds instead of
    /// hanging on the default five-second connect timeout. The multiplexer still gets
    /// created because production DI sets AbortOnConnectFail=false.
    /// </summary>
    private const string DeadRedisConnectionString =
        "localhost:6399,connectTimeout=250,syncTimeout=250,asyncTimeout=250,connectRetry=1";

    private readonly string _postgresConnectionString;

    public DegradedRedisWebApplicationFactory(string postgresConnectionString)
    {
        _postgresConnectionString = postgresConnectionString
            ?? throw new ArgumentNullException(nameof(postgresConnectionString));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
        builder.UseSetting("Jwt:SigningKey", JwtSigningKey);

        // Миграции применяются один раз в PostgreSqlTestcontainerFixture.
        builder.UseSetting("SkipDatabaseMigration", "true");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgresConnectionString);
        builder.UseSetting("Redis:ConnectionString", DeadRedisConnectionString);

        builder.ConfigureTestServices(services =>
        {
            // Та же обрезка, что в EventsWebApplicationFactory, но ТОЛЬКО эта часть:
            // убираем hosted services Kafka-консьюмера и инициализатора топиков.
            // IEventService и ICacheService остаются боевыми реализациями.
            services.RemoveAll<IHostedService>();
        });
    }
}
