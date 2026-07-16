namespace EventManagementService.Users.Application.Services;

public interface IUserService
{
    Task RegisterAsync(string login, string password, Domain.Models.UserRole role = Domain.Models.UserRole.User);

    Task<string> LoginAsync(string login, string password);
}