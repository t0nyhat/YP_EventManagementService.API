using System.ComponentModel.DataAnnotations;

namespace EventManagementService.Application.Dtos;

public class RegisterUserRequest
{
    [Required(ErrorMessage = "Логин пользователя обязателен")]
    public required string Login { get; set; }

    [Required(ErrorMessage = "Пароль пользователя обязателен")]
    public required string Password { get; set; }

    public string? Role { get; set; }
}
