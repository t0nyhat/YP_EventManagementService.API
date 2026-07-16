using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EventManagementService.Events.Infrastructure.DataAccess;

/// <summary>
/// Normalizes DateTime values to UTC before writing to timestamptz columns.
/// Npgsql rejects values with Kind=Unspecified/Local, while clients send dates
/// without a timezone (query string, JSON without the Z suffix): Local is converted
/// to UTC, Unspecified is treated as UTC.
/// </summary>
internal sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            value => value.Kind == DateTimeKind.Utc
                ? value
                : value.Kind == DateTimeKind.Local
                    ? value.ToUniversalTime()
                    : DateTime.SpecifyKind(value, DateTimeKind.Utc),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}
