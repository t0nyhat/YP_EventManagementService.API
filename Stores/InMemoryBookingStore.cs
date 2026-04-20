using EventManagementService.API.Models;

namespace EventManagementService.API.Stores;

/// <summary>
/// Thread-safe in-memory booking store for the current application lifetime.
/// </summary>
public class InMemoryBookingStore : IBookingStore
{
    private readonly Dictionary<Guid, Booking> _bookings = [];
    private readonly object _lock = new object();

    /// <inheritdoc />
    public Booking Add(Booking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);

        lock (_lock)
        {
            if (_bookings.ContainsKey(booking.Id))
            {
                throw new InvalidOperationException($"Бронирование с id {booking.Id} уже существует.");
            }

            _bookings[booking.Id] = booking.Snapshot();
            return _bookings[booking.Id].Snapshot();
        }
    }

    /// <inheritdoc />
    public Booking? GetById(Guid id)
    {
        lock (_lock)
        {
            return _bookings.TryGetValue(id, out var booking)
                ? booking.Snapshot()
                : null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> GetPendingIds()
    {
        lock (_lock)
        {
            return _bookings.Values
                .Where(booking => booking.Status == BookingStatus.Pending)
                .Select(booking => booking.Id)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public bool TrySetStatus(Guid bookingId, BookingStatus status, DateTime processedAt)
    {
        lock (_lock)
        {
            if (!_bookings.TryGetValue(bookingId, out var booking) || booking.Status != BookingStatus.Pending)
            {
                return false;
            }

            switch (status)
            {
                case BookingStatus.Confirmed:
                    booking.Confirm(processedAt);
                    return true;

                case BookingStatus.Rejected:
                    booking.Reject(processedAt);
                    return true;

                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status,
                        "Можно устанавливать только финальные статусы бронирования.");
            }
        }
    }
}
