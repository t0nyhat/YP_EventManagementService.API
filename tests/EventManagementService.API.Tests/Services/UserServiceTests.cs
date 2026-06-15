using EventManagementService.Application.Abstractions.Repositories;
using EventManagementService.Application.Abstractions.Security;
using EventManagementService.Application.Services;
using EventManagementService.Domain.Models;
using EventManagementService.Infrastructure.Security;
using FluentAssertions;
using Moq;

namespace EventManagementService.API.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IJwtTokenGenerator> _tokenGenerator = new();
    private readonly Sha256PasswordHasher _passwordHasher = new();

    private UserService CreateService() =>
        new(_userRepository.Object, _passwordHasher, _tokenGenerator.Object);

    [Fact]
    public async Task LoginAsync_WhenCredentialsAreValid_ReturnsGeneratedToken()
    {
        var user = User.Create("admin", _passwordHasher.Hash("secret"), UserRole.Admin);
        _userRepository.Setup(repo => repo.GetByLoginAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _tokenGenerator.Setup(gen => gen.GenerateToken(user.Id, user.Login, user.Role))
            .Returns("jwt-token");

        var token = await CreateService().LoginAsync("admin", "secret");

        token.Should().Be("jwt-token");
    }

    [Fact]
    public async Task LoginAsync_WhenLoginDiffersOnlyByCase_FindsUserCaseInsensitively()
    {
        var user = User.Create("admin", _passwordHasher.Hash("secret"), UserRole.Admin);
        _userRepository.Setup(repo => repo.GetByLoginAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _tokenGenerator.Setup(gen => gen.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<UserRole>()))
            .Returns("jwt-token");

        var token = await CreateService().LoginAsync("  ADMIN  ", "secret");

        token.Should().Be("jwt-token");
        _userRepository.Verify(repo => repo.GetByLoginAsync("admin", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WhenUserDoesNotExist_ThrowsUnauthorizedAccessException()
    {
        _userRepository.Setup(repo => repo.GetByLoginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var action = async () => await CreateService().LoginAsync("ghost", "secret");

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Неверный логин или пароль.");
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIsWrong_ThrowsUnauthorizedAccessException()
    {
        var user = User.Create("admin", _passwordHasher.Hash("secret"), UserRole.Admin);
        _userRepository.Setup(repo => repo.GetByLoginAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var action = async () => await CreateService().LoginAsync("admin", "wrong");

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Неверный логин или пароль.");
    }

    [Fact]
    public async Task RegisterAsync_WhenLoginIsFree_NormalizesLoginAndStoresHashedPassword()
    {
        User? savedUser = null;
        _userRepository.Setup(repo => repo.GetByLoginAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository.Setup(repo => repo.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => savedUser = user)
            .Returns(Task.CompletedTask);

        await CreateService().RegisterAsync("  ADMIN  ", "secret", UserRole.Admin);

        savedUser.Should().NotBeNull();
        savedUser!.Login.Should().Be("admin");
        savedUser.Role.Should().Be(UserRole.Admin);
        savedUser.PasswordHash.Should().NotBe("secret");
        _passwordHasher.Verify("secret", savedUser.PasswordHash).Should().BeTrue();
        _userRepository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenLoginAlreadyExists_ThrowsBusinessValidationException()
    {
        var existing = User.Create("admin", _passwordHasher.Hash("secret"), UserRole.Admin);
        _userRepository.Setup(repo => repo.GetByLoginAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var action = async () => await CreateService().RegisterAsync("ADMIN", "secret");

        await action.Should().ThrowAsync<Domain.Exceptions.BusinessValidationException>();
        _userRepository.Verify(repo => repo.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
