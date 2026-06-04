using EventManagementService.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventManagementService.API.DataAccess.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");

        builder.HasKey(eventModel => eventModel.Id);

        builder.Property(eventModel => eventModel.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(eventModel => eventModel.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(eventModel => eventModel.Description)
            .HasColumnName("description")
            .HasMaxLength(2000);

        builder.Property(eventModel => eventModel.StartAt)
            .HasColumnName("start_at")
            .IsRequired();

        builder.Property(eventModel => eventModel.EndAt)
            .HasColumnName("end_at")
            .IsRequired();

        builder.Property(eventModel => eventModel.TotalSeats)
            .HasColumnName("total_seats")
            .IsRequired();

        builder.Property(eventModel => eventModel.AvailableSeats)
            .HasColumnName("available_seats")
            .IsRequired();

    }
}