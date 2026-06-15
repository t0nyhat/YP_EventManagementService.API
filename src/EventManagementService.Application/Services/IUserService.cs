namespace EventManagementService.Application.Services;

public interface IUserService
{
    Task RegisterAsync(string login, string password, EventManagementService.Domain.Models.UserRole role = EventManagementService.Domain.Models.UserRole.User);

    Task<string> LoginAsync(string login, string password);
}
