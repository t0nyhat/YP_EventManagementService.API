using EventManagementService.Application;
using EventManagementService.Infrastructure.DataAccess;
using EventManagementService.Infrastructure.Repositories;
using EventManagementService.Application.Abstractions.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventManagementService.API.Tests.Infrastructure;

internal static class TestDbContextFactory
{
    public static AppDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    public static ServiceProvider CreateServiceProvider(string? databaseName = null)
    {
        var effectiveDatabaseName = databaseName ?? Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(effectiveDatabaseName));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddApplicationServices();

        return services.BuildServiceProvider();
    }
}