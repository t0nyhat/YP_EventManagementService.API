using EventManagementService.Bookings.Application.Abstractions.Repositories;
using EventManagementService.Bookings.Application.Abstractions.Services;
using EventManagementService.Bookings.Application.Configuration;
using EventManagementService.Bookings.Application.Services;
using EventManagementService.Bookings.Domain.Exceptions;
using EventManagementService.Bookings.Domain.Models;
using FluentAssertions;
using Moq;

namespace EventManagementService.Bookings.Tests.Services;

public class BookingServiceTests
{
    private readonly Mock<IBookingRepository> _bookingRepository = new();

    private IBookingService CreateService() => new BookingService(_bookingRepository.Object);

    [Fact]
    public async Task CreateBookingAsync_WhenIdsAreValid_CreatesPendingBookingWithoutEventLookup()
    {
        Booking? savedBooking = null;
        _bookingRepository
            .Setup(repo => repo.CountActiveByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _bookingRepository
            .Setup(repo => repo.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Callback<Booking, CancellationToken>((booking, _) => savedBooking = booking)
            .Returns(Task.CompletedTask);

        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var booking = await CreateService().CreateBookingAsync(eventId, userId, TestContext.Current.CancellationToken);

        booking.Should().BeSameAs(savedBooking);
        booking.EventId.Should().Be(eventId);
        booking.UserId.Should().Be(userId);
        booking.Status.Should().Be(BookingStatus.Pending);
        _bookingRepository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_WhenEventIdIsEmpty_ThrowsBusinessValidationException()
    {
        var action = async () => await CreateService().CreateBookingAsync(
            Guid.Empty,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<BusinessValidationException>();
        _bookingRepository.Verify(repo => repo.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_WhenUserReachedActiveLimit_ThrowsTooManyActiveBookingsException()
    {
        _bookingRepository
            .Setup(repo => repo.CountActiveByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingRules.MaxActiveBookingsPerUser);

        var action = async () => await CreateService().CreateBookingAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<TooManyActiveBookingsException>();
        _bookingRepository.Verify(repo => repo.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetBookingByIdAsync_WhenRequesterIsOwner_ReturnsBooking()
    {
        var userId = Guid.NewGuid();
        var booking = Booking.CreatePending(Guid.NewGuid(), userId);
        _bookingRepository
            .Setup(repo => repo.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var result = await CreateService().GetBookingByIdAsync(
            booking.Id,
            userId,
            UserRole.User,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(booking);
    }

    [Fact]
    public async Task GetBookingByIdAsync_WhenRequesterIsNotOwner_ThrowsForbiddenOperationException()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        _bookingRepository
            .Setup(repo => repo.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var action = async () => await CreateService().GetBookingByIdAsync(
            booking.Id,
            Guid.NewGuid(),
            UserRole.User,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ForbiddenOperationException>();
    }

    [Fact]
    public async Task CancelBookingAsync_WhenRequesterIsAdmin_CancelsAnyBooking()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        _bookingRepository
            .Setup(repo => repo.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        await CreateService().CancelBookingAsync(
            booking.Id,
            Guid.NewGuid(),
            UserRole.Admin,
            TestContext.Current.CancellationToken);

        booking.Status.Should().Be(BookingStatus.Cancelled);
        _bookingRepository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
