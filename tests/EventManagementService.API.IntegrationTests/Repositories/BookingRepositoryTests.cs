using EventManagementService.API.IntegrationTests.Infrastructure;
using EventManagementService.Domain.Models;
using EventManagementService.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EventManagementService.API.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public class BookingRepositoryTests
{
    private readonly PostgreSqlTestcontainerFixture _fixture;

    public BookingRepositoryTests(PostgreSqlTestcontainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddAsync_WhenBookingIsValid_PersistsBooking()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var eventId = await SeedEventAsync(cancellationToken);
        var booking = Booking.CreatePending(eventId, User.SystemUserId, Utc(2026, 6, 10, 9, 0, 0));

        await using (var actContext = _fixture.CreateDbContext())
        {
            var repository = new BookingRepository(actContext);
            await repository.AddAsync(booking, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }

        await using var verifyContext = _fixture.CreateDbContext();
        var persisted = await verifyContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == booking.Id, cancellationToken);

        persisted.Should().NotBeNull();
        persisted!.EventId.Should().Be(eventId);
        persisted.Status.Should().Be(BookingStatus.Pending);
        persisted.ProcessedAt.Should().BeNull();
        persisted.CreatedAt.Should().Be(Utc(2026, 6, 10, 9, 0, 0));
    }

    [Fact]
    public async Task AddAsync_WithoutSaveChanges_DoesNotPersistBooking()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var eventId = await SeedEventAsync(cancellationToken);
        var booking = Booking.CreatePending(eventId, User.SystemUserId, Utc(2026, 6, 10, 10, 0, 0));

        await using (var actContext = _fixture.CreateDbContext())
        {
            var repository = new BookingRepository(actContext);
            await repository.AddAsync(booking, cancellationToken);
        }

        await using var verifyContext = _fixture.CreateDbContext();
        var exists = await verifyContext.Bookings
            .AsNoTracking()
            .AnyAsync(item => item.Id == booking.Id, cancellationToken);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_WhenBookingExists_ReturnsBooking()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var eventId = await SeedEventAsync(cancellationToken);
        var booking = Booking.CreatePending(eventId, User.SystemUserId, Utc(2026, 6, 11, 10, 0, 0));

        await using (var seedContext = _fixture.CreateDbContext())
        {
            seedContext.Bookings.Add(booking);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        await using var actContext = _fixture.CreateDbContext();
        var repository = new BookingRepository(actContext);

        var found = await repository.GetByIdAsync(booking.Id, cancellationToken);

        found.Should().NotBeNull();
        found!.EventId.Should().Be(eventId);
        found.Status.Should().Be(BookingStatus.Pending);
        found.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenBookingDoesNotExist_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        await using var context = _fixture.CreateDbContext();
        var repository = new BookingRepository(context);

        var found = await repository.GetByIdAsync(Guid.NewGuid(), cancellationToken);

        found.Should().BeNull();
    }

    [Fact]
    public async Task GetPendingIdsAsync_WhenMixedStatusesExist_ReturnsOnlyPendingBookingIds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var eventId = await SeedEventAsync(cancellationToken);

        var pendingBooking1 = Booking.CreatePending(eventId, User.SystemUserId, Utc(2026, 6, 12, 10, 0, 0));
        var pendingBooking2 = Booking.CreatePending(eventId, User.SystemUserId, Utc(2026, 6, 12, 10, 5, 0));

        var confirmedBooking = Booking.CreatePending(eventId, User.SystemUserId, Utc(2026, 6, 12, 10, 10, 0));
        confirmedBooking.Confirm(Utc(2026, 6, 12, 10, 20, 0));

        var rejectedBooking = Booking.CreatePending(eventId, User.SystemUserId, Utc(2026, 6, 12, 10, 15, 0));
        rejectedBooking.Reject(Utc(2026, 6, 12, 10, 25, 0));

        await using (var seedContext = _fixture.CreateDbContext())
        {
            seedContext.Bookings.AddRange(pendingBooking1, pendingBooking2, confirmedBooking, rejectedBooking);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        await using var actContext = _fixture.CreateDbContext();
        var repository = new BookingRepository(actContext);

        var pendingIds = await repository.GetPendingIdsAsync(cancellationToken);

        pendingIds.Should().BeEquivalentTo([pendingBooking1.Id, pendingBooking2.Id]);
        pendingIds.Should().NotContain(confirmedBooking.Id);
        pendingIds.Should().NotContain(rejectedBooking.Id);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenBookingStatusModified_PersistsUpdatedState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetDatabaseAsync(cancellationToken);

        var eventId = await SeedEventAsync(cancellationToken);
        var booking = Booking.CreatePending(eventId, User.SystemUserId, Utc(2026, 6, 13, 9, 0, 0));

        await using (var seedContext = _fixture.CreateDbContext())
        {
            seedContext.Bookings.Add(booking);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        await using (var actContext = _fixture.CreateDbContext())
        {
            var repository = new BookingRepository(actContext);
            var tracked = await repository.GetByIdAsync(booking.Id, cancellationToken);
            tracked.Should().NotBeNull();

            tracked!.Confirm(Utc(2026, 6, 13, 9, 30, 0));
            await repository.SaveChangesAsync(cancellationToken);
        }

        await using var verifyContext = _fixture.CreateDbContext();
        var persisted = await verifyContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == booking.Id, cancellationToken);

        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(BookingStatus.Confirmed);
        persisted.ProcessedAt.Should().Be(Utc(2026, 6, 13, 9, 30, 0));
    }

    private async Task<Guid> SeedEventAsync(CancellationToken cancellationToken)
    {
        var eventItem = Event.Create(
            title: "Событие для бронирований",
            description: "Техническое событие",
            startAt: Utc(2026, 6, 1, 10, 0, 0),
            endAt: Utc(2026, 6, 1, 12, 0, 0),
            totalSeats: 100);

        await using var context = _fixture.CreateDbContext();
        context.Events.Add(eventItem);
        await context.SaveChangesAsync(cancellationToken);

        return eventItem.Id;
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute, int second)
    {
        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
    }
}
