using EventManagementService.Events.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EventManagementService.Events.Infrastructure.DataAccess;

public sealed class EventsDbContext : DbContext
{
    public EventsDbContext(DbContextOptions<EventsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Event> Events => Set<Event>();

    public DbSet<BookingConfirmedInbox> BookingConfirmedInbox => Set<BookingConfirmedInbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventsDbContext).Assembly);
    }
}