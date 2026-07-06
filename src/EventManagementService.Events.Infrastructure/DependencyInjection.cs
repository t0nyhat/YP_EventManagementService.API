using EventManagementService.Events.Application.Abstractions.Messaging;
using EventManagementService.Events.Application.Abstractions.Repositories;
using EventManagementService.Events.Infrastructure.DataAccess;
using EventManagementService.Events.Infrastructure.Messaging;
using EventManagementService.Events.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddSingleton<BookingConfirmedConsumerService>();
        services.AddHostedService<KafkaTopicInitializer>();
        services.AddHostedService(sp => sp.GetRequiredService<BookingConfirmedConsumerService>());

        return services;
    }
}