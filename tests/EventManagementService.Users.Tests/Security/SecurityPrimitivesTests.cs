using System.IdentityModel.Tokens.Jwt;
using EventManagementService.Users.Domain.Models;
using EventManagementService.Users.Infrastructure.Configuration;
using EventManagementService.Users.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EventManagementService.Users.Tests.Security;

public class SecurityPrimitivesTests
{
    [Fact]
    public void Pbkdf2PasswordHasher_HashAndVerify_RoundTripWorks()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var hash = hasher.Hash("P@ssw0rd");

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().Contain(":"); // versioned format
        hasher.Verify("P@ssw0rd", hash).Should().BeTrue();
        hasher.Verify("wrong-password", hash).Should().BeFalse();
    }

    [Fact]
    public void Pbkdf2PasswordHasher_SamePassword_DifferentHashes()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var hash1 = hasher.Hash("P@ssw0rd");
        var hash2 = hasher.Hash("P@ssw0rd");

        hash1.Should().NotBe(hash2); // salt ensures uniqueness
    }

    [Fact]
    public void Pbkdf2PasswordHasher_Verify_InvalidFormat_ReturnsFalse()
    {
        var hasher = new Pbkdf2PasswordHasher();

        hasher.Verify("password", "not-a-valid-format").Should().BeFalse();
        hasher.Verify("password", "2:salt:hash").Should().BeFalse(); // unknown version
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