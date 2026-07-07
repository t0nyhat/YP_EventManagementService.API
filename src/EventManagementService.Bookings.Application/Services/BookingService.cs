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
        // Валидация идентификаторов выполняется доменной фабрикой.
        var booking = Booking.CreatePending(eventId, userId);

        await _bookingRepository.AddWithActiveLimitAsync(
            booking,
            BookingRules.MaxActiveBookingsPerUser,
            cancellationToken);

        return booking;
    }

    public async Task<Booking> GetBookingByIdAsync(
        Guid bookingId,
        Guid requesterUserId,
        UserRole requesterRole,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken)
            ?? throw new NotFoundException($"Бронирование с id {bookingId} не найдено.");

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
            ?? throw new NotFoundException($"Бронирование с id {bookingId} не найдено.");

        EnsureAccess(booking, requesterUserId, requesterRole);

        try
        {
            booking.Cancel();
            await _bookingRepository.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Фоновый обработчик успел подтвердить бронь между нашим чтением и записью.
            // Отмена из статуса Confirmed допустима, поэтому перечитываем и повторяем один раз.
            await _bookingRepository.ReloadAsync(booking, cancellationToken);
            booking.Cancel();
            await _bookingRepository.SaveChangesAsync(cancellationToken);
        }
    }

    private static void EnsureAccess(Booking booking, Guid requesterUserId, UserRole requesterRole)
    {
        if (requesterUserId == Guid.Empty)
        {
            throw new BusinessValidationException("Идентификатор пользователя должен быть указан.");
        }

        if (booking.UserId != requesterUserId && requesterRole != UserRole.Admin)
        {
            throw new ForbiddenOperationException();
        }
    }
}
