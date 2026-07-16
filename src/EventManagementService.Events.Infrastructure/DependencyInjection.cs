using EventManagementService.Events.Application.Abstractions.Caching;
using EventManagementService.Events.Application.Abstractions.Messaging;
using EventManagementService.Events.Application.Abstractions.Repositories;
using EventManagementService.Events.Application.Configuration;
using EventManagementService.Events.Infrastructure.Caching;
using EventManagementService.Events.Infrastructure.Configuration;
using EventManagementService.Events.Infrastructure.DataAccess;
using EventManagementService.Events.Infrastructure.Messaging;
using EventManagementService.Events.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EventManagementService.Events.Infrastructure;

/// <summary>
/// Registers infrastructure-layer services with the dependency injection container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds infrastructure-layer services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<EventsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingConfirmedHandler, BookingConfirmedHandler>();

        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));

        services.AddSingleton<KafkaDeadLetterPublisher>();
        services.AddSingleton<BookingConfirmedConsumerService>();
        services.AddHostedService<KafkaTopicInitializer>();
        services.AddHostedService(sp => sp.GetRequiredService<BookingConfirmedConsumerService>());

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Redis:ConnectionString must be a non-empty string.")
            .ValidateOnStart();

        services.AddOptions<CacheOptions>()
            .Bind(configuration.GetSection(CacheOptions.SectionName))
            .Validate(
                options => options.EventTtl > TimeSpan.Zero,
                "Cache:EventTtl must be greater than zero.")
            .Validate(
                options => options.TopEventsTtl > TimeSpan.Zero,
                "Cache:TopEventsTtl must be greater than zero.")
            .ValidateOnStart();

        // One multiplexer per process: it is thread-safe and manages its own connections.
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var redisOptions = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
            var configurationOptions = ConfigurationOptions.Parse(redisOptions.ConnectionString);

            // The API must start and serve traffic even when Redis is down:
            // keep retrying in the background instead of failing the first connect.
            configurationOptions.AbortOnConnectFail = false;

            return ConnectionMultiplexer.Connect(configurationOptions);
        });

        // Stateless adapter over the thread-safe multiplexer, so a singleton is safe.
        services.AddSingleton<ICacheService, RedisCacheService>();

        return services;
    }
}