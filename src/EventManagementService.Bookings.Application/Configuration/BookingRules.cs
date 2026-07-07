namespace EventManagementService.Bookings.Application.Configuration;

public static class BookingRules
{
    // Значение унаследовано от монолита (спринт 8), чтобы поведение API не менялось.
    public const int MaxActiveBookingsPerUser = 10;
}
