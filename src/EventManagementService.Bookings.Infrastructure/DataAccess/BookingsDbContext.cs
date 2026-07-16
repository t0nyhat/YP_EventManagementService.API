using EventManagementService.Bookings.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EventManagementService.Bookings.Infrastructure.DataAccess;

public sealed class BookingsDbContext : DbContext
{
    public BookingsDbContext(DbContextOptions<BookingsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookingOutboxMessage> BookingOutbox => Set<BookingOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingsDbContext).Assembly);
    }
}
