using EventManagementService.Bookings.Application.Abstractions.Repositories;
using EventManagementService.Bookings.Application.Abstractions.Services;
using EventManagementService.Bookings.Application.Configuration;
using EventManagementService.Bookings.Domain.Exceptions;
using EventManagementService.Bookings.Domain.Models;

namespace EventManagementService.Bookings.Application.Services;

/// <summary>
/// Handles local booking lifecycle operations.
/// </summary>
public sealed class BookingService : IBookingService
{
    private static readonly SemaphoreSlim BookingLock = new(1, 1);
    private readonly IBookingRepository _bookingRepository;

    public BookingService(IBookingRepository bookingRepository)
    {
        _bookingRepository = bookingRepository ?? throw new ArgumentNullException(nameof(bookingRepository));
    }

    public async Task<Booking> CreateBookingAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            throw new BusinessValidationException("Event id must be specified.");
        }

        if (userId == Guid.Empty)
        {
            throw new BusinessValidationException("User id must be specified.");
        }

        await BookingLock.WaitAsync(cancellationToken);
        try
        {
            var activeBookings = await _bookingRepository.CountActiveByUserAsync(userId, cancellationToken);
            if (activeBookings >= BookingRules.MaxActiveBookingsPerUser)
            {
                throw new TooManyActiveBookingsException(BookingRules.MaxActiveBookingsPerUser);
            }

            var booking = Booking.CreatePending(eventId, userId);
            await _bookingRepository.AddAsync(booking, cancellationToken);
            await _bookingRepository.SaveChangesAsync(cancellationToken);

            return booking;
        }
        finally
        {
            BookingLock.Release();
        }
    }

    public async Task<Booking> GetBookingByIdAsync(
        Guid bookingId,
        Guid requesterUserId,
        UserRole requesterRole,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken)
            ?? throw new NotFoundException($"Booking with id {bookingId} was not found.");

        EnsureAccess(booking, requesterUserId, requesterRole);
        return booking;
    }

    public async Task CancelBookingAsync(
        Guid bookingId,
        Guid requesterUserId,
        UserRole requesterRole,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken)
            ?? throw new NotFoundException($"Booking with id {bookingId} was not found.");

        EnsureAccess(booking, requesterUserId, requesterRole);

        booking.Cancel();
        await _bookingRepository.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureAccess(Booking booking, Guid requesterUserId, UserRole requesterRole)
    {
        if (requesterUserId == Guid.Empty)
        {
            throw new BusinessValidationException("User id must be specified.");
        }

        if (booking.UserId != requesterUserId && requesterRole != UserRole.Admin)
        {
            throw new ForbiddenOperationException();
        }
    }
}
