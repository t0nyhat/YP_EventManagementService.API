using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EventManagementService.Bookings.Application.Abstractions.Services;
using EventManagementService.Bookings.Domain.Exceptions;
using EventManagementService.Bookings.Domain.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace EventManagementService.Bookings.Tests.Infrastructure;

public sealed class BookingsWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string JwtSigningKey = "0123456789abcdef0123456789abcdef";
    private const string JwtIssuer = "EventManagementService.API";
    private const string JwtAudience = "EventManagementService.API";

    public string UserToken { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Port=5435;Database=bookings_test;Username=postgres;Password=postgres");
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
        builder.UseSetting("Jwt:SigningKey", JwtSigningKey);
        builder.UseSetting("SkipDatabaseMigration", "true");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBookingService>();
            services.RemoveAll<IHostedService>();

            services.AddScoped<IBookingService, TestBookingService>();
        });
    }

    public ValueTask InitializeAsync()
    {
        var _ = Server;
        UserToken = GenerateToken(Guid.NewGuid(), "user", UserRole.User.ToString());
        return ValueTask.CompletedTask;
    }

    private static string GenerateToken(Guid userId, string login, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, login),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, login)
        };

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class TestBookingService : IBookingService
    {
        public Task<Booking> CreateBookingAsync(
            Guid eventId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Booking.CreatePending(eventId, userId));
        }

        public Task<Booking> GetBookingByIdAsync(
            Guid bookingId,
            Guid requesterUserId,
            UserRole requesterRole,
            CancellationToken cancellationToken = default)
        {
            throw new NotFoundException($"Бронирование с id {bookingId} не найдено.");
        }

        public Task CancelBookingAsync(
            Guid bookingId,
            Guid requesterUserId,
            UserRole requesterRole,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
