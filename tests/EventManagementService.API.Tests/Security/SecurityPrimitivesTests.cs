using System.IdentityModel.Tokens.Jwt;
using EventManagementService.Domain.Models;
using EventManagementService.Infrastructure.Configuration;
using EventManagementService.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EventManagementService.API.Tests.Security;

public class SecurityPrimitivesTests
{
    [Fact]
    public void Sha256PasswordHasher_HashAndVerify_RoundTripWorks()
    {
        var hasher = new Sha256PasswordHasher();

        var hash = hasher.Hash("P@ssw0rd");

        hash.Should().NotBeNullOrWhiteSpace();
        hasher.Verify("P@ssw0rd", hash).Should().BeTrue();
        hasher.Verify("wrong-password", hash).Should().BeFalse();
    }

    [Fact]
    public void JwtTokenGenerator_GenerateToken_ContainsExpectedClaims()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "EventManagementService.API",
            Audience = "EventManagementService.API",
            SigningKey = "0123456789abcdef0123456789abcdef",
            LifetimeMinutes = 60
        });

        var generator = new JwtTokenGenerator(options);
        var userId = Guid.NewGuid();
        const string login = "admin";

        var token = generator.GenerateToken(userId, login, UserRole.Admin);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Issuer.Should().Be("EventManagementService.API");
        jwt.Audiences.Should().Contain("EventManagementService.API");
        jwt.Claims.Should().Contain(claim => claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == userId.ToString());
        jwt.Claims.Should().Contain(claim => claim.Type == JwtRegisteredClaimNames.UniqueName && claim.Value == login);
        jwt.Claims.Should().Contain(claim => claim.Type == System.Security.Claims.ClaimTypes.NameIdentifier && claim.Value == userId.ToString());
        jwt.Claims.Should().Contain(claim => claim.Type == System.Security.Claims.ClaimTypes.Role && claim.Value == UserRole.Admin.ToString());
    }
}
