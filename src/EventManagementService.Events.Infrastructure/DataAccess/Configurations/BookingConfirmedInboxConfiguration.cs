using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventManagementService.Events.Infrastructure.DataAccess.Configurations;

internal sealed class BookingConfirmedInboxConfiguration : IEntityTypeConfiguration<BookingConfirmedInbox>
{
    public void Configure(EntityTypeBuilder<BookingConfirmedInbox> builder)
    {
        builder.ToTable("booking_confirmed_inbox");

        builder.HasKey(x => x.BookingId);

        builder.Property(x => x.BookingId)
            .HasColumnName("booking_id")
            .ValueGeneratedNever();

        builder.Property(x => x.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.Seats)
            .HasColumnName("seats")
            .IsRequired();

        builder.Property(x => x.ConfirmedAtUtc)
            .HasColumnName("confirmed_at_utc")
            .IsRequired();

        builder.Property(x => x.ProcessedAtUtc)
            .HasColumnName("processed_at_utc")
            .IsRequired();

        builder.Property(x => x.Result)
            .HasColumnName("result")
            .HasMaxLength(50)
            .IsRequired();
    }
}