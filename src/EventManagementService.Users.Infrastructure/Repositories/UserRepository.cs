using EventManagementService.Users.Application.Abstractions.Repositories;
using EventManagementService.Users.Domain.Models;
using EventManagementService.Users.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace EventManagementService.Users.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly UsersDbContext _context;

    public UserRepository(UsersDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        return _context.Users.FirstOrDefaultAsync(user => user.Login == login, cancellationToken);
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        return _context.Users.AddAsync(user, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}