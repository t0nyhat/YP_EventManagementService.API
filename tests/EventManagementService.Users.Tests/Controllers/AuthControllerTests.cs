using EventManagementService.Users.Application.Dtos;
using EventManagementService.Users.Application.Services;
using EventManagementService.Users.Domain.Exceptions;
using EventManagementService.Users.Domain.Models;
using EventManagementService.Users.Presentation.Controllers;
using FluentAssertions;
using Moq;

namespace EventManagementService.Users.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IUserService> _userService = new();

    private AuthController CreateController() => new(_userService.Object);

    [Fact]
    public async Task Register_WhenRoleIsAdmin_PassesAdminRoleToService()
    {
        var request = new RegisterUserRequest { Login = "admin", Password = "secret123", Role = "Admin" };

        await CreateController().Register(request);

        _userService.Verify(
            service => service.RegisterAsync("admin", "secret123", UserRole.Admin),
            Times.Once);
    }

    [Fact]
    public async Task Register_WhenRoleIsOmitted_DefaultsToUserRole()
    {
        var request = new RegisterUserRequest { Login = "user", Password = "secret123" };

        await CreateController().Register(request);

        _userService.Verify(
            service => service.RegisterAsync("user", "secret123", UserRole.User),
            Times.Once);
    }

    [Fact]
    public async Task Register_WhenRoleIsUnknown_ThrowsBusinessValidationException()
    {
        var request = new RegisterUserRequest { Login = "user", Password = "secret123", Role = "SuperAdmin" };

        var action = async () => await CreateController().Register(request);

        await action.Should().ThrowAsync<BusinessValidationException>();
        _userService.Verify(
            service => service.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UserRole>()),
            Times.Never);
    }
}
