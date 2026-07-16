using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventManagementService.Bookings.Infrastructure.DataAccess.Configurations;

internal sealed class BookingOutboxMessageConfiguration : IEntityTypeConfiguration<BookingOutboxMessage>
{
    public void Configure(EntityTypeBuilder<BookingOutboxMessage> builder)
    {
        builder.ToTable("booking_outbox");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(message => message.BookingId)
            .HasColumnName("booking_id")
            .IsRequired();

        builder.HasIndex(message => message.BookingId)
            .IsUnique();

        builder.Property(message => message.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(message => message.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(message => message.Seats)
            .HasColumnName("seats")
            .IsRequired();

        builder.Property(message => message.ConfirmedAtUtc)
            .HasColumnName("confirmed_at_utc")
            .IsRequired();

        builder.Property(message => message.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(message => message.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(message => message.PublishedAtUtc)
            .HasColumnName("published_at_utc");

        builder.Property(message => message.PublishAttempts)
            .HasColumnName("publish_attempts")
            .IsRequired();

        builder.Property(message => message.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(2000);

        builder.HasIndex(message => message.PublishedAtUtc);
    }
}
