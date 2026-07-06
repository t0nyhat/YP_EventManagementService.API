using EventManagementService.Users.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EventManagementService.Users.Infrastructure.DataAccess;

public sealed class UsersDbContext : DbContext
{
    public UsersDbContext(DbContextOptions<UsersDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new Configurations.UserConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}