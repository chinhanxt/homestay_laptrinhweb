// WebHomestay.Tests/Services/AvailabilityServiceTests.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class AvailabilityServiceTests
{
    [Fact]
    public async Task IsRoomAvailable_ReturnsFalse_WhenDailyStayCoversExistingHourlyBooking()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(IsRoomAvailable_ReturnsFalse_WhenDailyStayCoversExistingHourlyBooking))
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.Bookings.Add(new Booking
        {
            RoomId = 5,
            CustomerName = "Old guest",
            CustomerPhone = "0900000001",
            BookingMode = BookingMode.Hourly,
            StartTime = new DateTime(2026, 5, 10, 9, 30, 0),
            EndTime = new DateTime(2026, 5, 10, 11, 30, 0),
            Status = "Confirmed"
        });
        await context.SaveChangesAsync();

        var service = new AvailabilityService(context);
        var requested = BookingTimeRules.BuildDailyStay(
            new DateOnly(2026, 5, 9),
            new DateOnly(2026, 5, 11));

        var isAvailable = await service.IsRoomAvailable(5, requested.Start, requested.End);

        Assert.False(isAvailable);
    }
}
