using EventManagementService.Bookings.Application.Abstractions.Repositories;
using EventManagementService.Bookings.Infrastructure.DataAccess;
using EventManagementService.Bookings.Infrastructure.Messaging;
using EventManagementService.Bookings.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventManagementService.Bookings.Infrastructure;

/// <summary>
/// Registers infrastructure-layer services with the dependency injection container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<BookingsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingOutboxRepository, BookingOutboxRepository>();
        services.AddScoped<BookingOutboxPublisher>();

        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddSingleton<IBookingConfirmedPublisher, KafkaBookingConfirmedPublisher>();
        services.AddHostedService<BookingOutboxPublisherBackgroundService>();

        return services;
    }
}
