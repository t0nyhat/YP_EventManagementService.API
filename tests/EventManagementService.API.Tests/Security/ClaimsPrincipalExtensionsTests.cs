using System.Security.Claims;
using EventManagementService.Domain.Models;
using EventManagementService.Presentation.Security;
using FluentAssertions;

namespace EventManagementService.API.Tests.Security;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void TryGetUserId_WhenNameIdentifierClaimIsValidGuid_ReturnsTrueAndId()
    {
        var userId = Guid.NewGuid();
        var principal = CreatePrincipal((ClaimTypes.NameIdentifier, userId.ToString()));

        var result = principal.TryGetUserId(out var parsedId);

        result.Should().BeTrue();
        parsedId.Should().Be(userId);
    }

    [Fact]
    public void TryGetUserId_WhenNameIdentifierClaimIsMissing_ReturnsFalseAndEmpty()
    {
        var principal = CreatePrincipal();

        var result = principal.TryGetUserId(out var parsedId);

        result.Should().BeFalse();
        parsedId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void TryGetUserId_WhenNameIdentifierClaimIsNotAGuid_ReturnsFalseAndEmpty()
    {
        var principal = CreatePrincipal((ClaimTypes.NameIdentifier, "not-a-guid"));

        var result = principal.TryGetUserId(out var parsedId);

        result.Should().BeFalse();
        parsedId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void GetUserRole_WhenRoleClaimIsValid_ReturnsParsedRole()
    {
        var principal = CreatePrincipal((ClaimTypes.Role, nameof(UserRole.Admin)));

        principal.GetUserRole().Should().Be(UserRole.Admin);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-role")]
    public void GetUserRole_WhenRoleClaimIsMissingOrInvalid_DefaultsToUser(string? roleValue)
    {
        var principal = roleValue is null
            ? CreatePrincipal()
            : CreatePrincipal((ClaimTypes.Role, roleValue));

        principal.GetUserRole().Should().Be(UserRole.User);
    }

    private static ClaimsPrincipal CreatePrincipal(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(claims.Select(claim => new Claim(claim.Type, claim.Value)));
        return new ClaimsPrincipal(identity);
    }
}
