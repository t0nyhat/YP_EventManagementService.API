using EventManagementService.Users.Application.Dtos;
using EventManagementService.Users.Application.Services;
using EventManagementService.Users.Domain.Exceptions;
using EventManagementService.Users.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventManagementService.Users.Presentation.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        await _userService.RegisterAsync(request.Login, request.Password, ParseRole(request.Role));
        return NoContent();
    }

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginUserRequest request)
    {
        var token = await _userService.LoginAsync(request.Login, request.Password);
        return Ok(new LoginResponse { Token = token });
    }

    private static UserRole ParseRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return UserRole.User;
        }

        if (Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsedRole))
        {
            return parsedRole;
        }

        throw new BusinessValidationException("Недопустимая роль пользователя.");
    }
}