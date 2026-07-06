using EventManagementService.Users.Application.Abstractions.Repositories;
using EventManagementService.Users.Application.Abstractions.Security;
using EventManagementService.Users.Domain.Exceptions;
using EventManagementService.Users.Domain.Models;

namespace EventManagementService.Users.Application.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public UserService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _jwtTokenGenerator = jwtTokenGenerator ?? throw new ArgumentNullException(nameof(jwtTokenGenerator));
    }

    public async Task RegisterAsync(string login, string password, UserRole role = UserRole.User)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            throw new BusinessValidationException("Логин пользователя не должен быть пустым.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new BusinessValidationException("Пароль пользователя не должен быть пустым.");
        }

        var normalizedLogin = NormalizeLogin(login);
        var existingUser = await _userRepository.GetByLoginAsync(normalizedLogin);
        if (existingUser is not null)
        {
            throw new BusinessValidationException($"Пользователь с логином {normalizedLogin} уже существует.");
        }

        var passwordHash = _passwordHasher.Hash(password);
        var user = User.Create(normalizedLogin, passwordHash, role);

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();
    }

    public async Task<string> LoginAsync(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            throw new NotFoundException("Неверный логин или пароль.");
        }

        var normalizedLogin = NormalizeLogin(login);
        var user = await _userRepository.GetByLoginAsync(normalizedLogin)
            ?? throw new NotFoundException("Неверный логин или пароль.");

        if (!_passwordHasher.Verify(password, user.PasswordHash))
        {
            throw new NotFoundException("Неверный логин или пароль.");
        }

        return _jwtTokenGenerator.GenerateToken(user.Id, user.Login, user.Role);
    }

    private static string NormalizeLogin(string login)
    {
        return login.Trim().ToLowerInvariant();
    }
}