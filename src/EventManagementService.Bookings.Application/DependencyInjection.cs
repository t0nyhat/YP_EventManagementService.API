using EventManagementService.Bookings.Application.Abstractions.Services;
using EventManagementService.Bookings.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventManagementService.Bookings.Application;

/// <summary>
/// Registers application-layer services with the dependency injection container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IBookingProcessingService, BookingProcessingService>();

        return services;
    }
}
