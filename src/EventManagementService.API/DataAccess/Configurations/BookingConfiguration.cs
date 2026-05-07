using EventManagementService.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventManagementService.API.DataAccess.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");

        builder.HasKey(booking => booking.Id);

        builder.Property(booking => booking.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(booking => booking.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(booking => booking.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(booking => booking.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(booking => booking.ProcessedAt)
            .HasColumnName("processed_at");

        builder.HasOne(booking => booking.Event)
            .WithMany(eventModel => eventModel.Bookings)
            .HasForeignKey(booking => booking.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}