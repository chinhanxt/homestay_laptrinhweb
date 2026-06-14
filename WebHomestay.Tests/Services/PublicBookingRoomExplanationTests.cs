using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class PublicBookingRoomExplanationTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task BuildRoomExplanation_WhenGuestCountExceedsCapacity_IncludesSurchargeLine()
    {
        using var context = CreateContext();
        context.Rooms.Add(new Room
        {
            Id = 21,
            BranchId = 1,
            Name = "Family 21",
            Capacity = 2,
            MaxGuests = 3,
            ExtraGuestFee = 150000m,
            PricePerDay = 1400000m,
            PriceWeekendPerDay = 1600000m,
            PriceHolidayPerDay = 1800000m,
            Status = "Available"
        });
        await context.SaveChangesAsync();

        var service = new PublicBookingRoomExplanationService(context);
        var result = await service.BuildAsync(21, new BookingConfirmedState
        {
            GuestCount = 3,
            BookingMode = "daily",
            CheckInDate = new DateOnly(2026, 6, 13),
            CheckOutDate = new DateOnly(2026, 6, 15)
        }, CancellationToken.None);

        Assert.Contains(result.Lines, line => line.Contains("phụ thu"));
        Assert.Equal("weekend", result.PricingTierLabel);
        Assert.Equal(1600000m, result.DisplayPrice);
    }
}
