using EventManagementService.Users.Domain.Exceptions;
using EventManagementService.Users.Domain.Models;
using FluentAssertions;

namespace EventManagementService.Users.Tests.Models;

public class UserTests
{
    [Fact]
    public void Create_WhenLoginPasswordAndRoleAreProvided_CreatesUser()
    {
        var user = User.Create("admin", "hash", UserRole.Admin);

        user.Id.Should().NotBe(Guid.Empty);
        user.Login.Should().Be("admin");
        user.PasswordHash.Should().Be("hash");
        user.Role.Should().Be(UserRole.Admin);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenLoginIsInvalid_ThrowsBusinessValidationException(string login)
    {
        var action = () => User.Create(login, "hash");

        action.Should().Throw<BusinessValidationException>()
            .WithMessage("Логин пользователя не должен быть пустым.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenPasswordHashIsInvalid_ThrowsBusinessValidationException(string passwordHash)
    {
        var action = () => User.Create("user", passwordHash);

        action.Should().Throw<BusinessValidationException>()
            .WithMessage("Хеш пароля пользователя не должен быть пустым.");
    }
}