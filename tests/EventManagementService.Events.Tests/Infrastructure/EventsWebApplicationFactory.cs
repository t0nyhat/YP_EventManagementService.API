using System.Text;
using EventManagementService.Events.Application.Abstractions.Services;
using EventManagementService.Events.Application.Dtos;
using EventManagementService.Events.Domain.Exceptions;
using EventManagementService.Events.Domain.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EventManagementService.Events.Tests.Infrastructure;

public class EventsWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string JwtSigningKey = "замените_на_сильный_ключ_на_проде_32_байта";
    private static readonly string JwtIssuer = "EventManagementService.API";
    private static readonly string JwtAudience = "EventManagementService.API";

    public string AdminToken { get; private set; } = null!;
    public string UserToken { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
        builder.UseSetting("Jwt:SigningKey", JwtSigningKey);
        builder.UseSetting("SkipDatabaseMigration", "true");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEventService>();
            services.RemoveAll<IHostedService>();

            services.AddScoped<IEventService, TestEventService>();
        });
    }

    public ValueTask InitializeAsync()
    {
        var _ = Server;

        AdminToken = GenerateToken(Guid.NewGuid(), "admin", "Admin");
        UserToken = GenerateToken(Guid.NewGuid(), "user", "User");

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

    private sealed class TestEventService : IEventService
    {
        public Task<PaginatedResult<Event>> GetEventsAsync(GetEventsQuery query)
        {
            return Task.FromResult(new PaginatedResult<Event>
            {
                Items = Array.Empty<Event>(),
                Page = query.Page,
                Count = 0,
                TotalCount = 0
            });
        }

        public Task<Event> GetEventByIdAsync(Guid id)
        {
            throw new NotFoundException($"Событие с id {id} не найдено.");
        }

        public Task<Event> CreateEventAsync(Event newEvent)
        {
            return Task.FromResult(newEvent);
        }

        public Task<Event> UpdateEventAsync(Guid id, UpdateEventRequest request)
        {
            throw new NotFoundException($"Событие с id {id} не найдено.");
        }

        public Task DeleteEventAsync(Guid id)
        {
            throw new NotFoundException($"Событие с id {id} не найдено.");
        }
    }
}
