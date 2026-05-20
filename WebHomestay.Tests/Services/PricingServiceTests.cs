using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class PricingServiceTests
{
    [Theory]
    [InlineData(2, 300000)]
    [InlineData(4, 600000)]
    public async Task CalculateStayPriceAsync_ForHourlyStay_MultipliesPricePerHourByDuration(int hours, decimal expected)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"{nameof(CalculateStayPriceAsync_ForHourlyStay_MultipliesPricePerHourByDuration)}_{hours}")
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.Branches.Add(new Branch { Id = 1, Name = "Đà Lạt", Address = "Đà Lạt" });
        context.Rooms.Add(new Room
        {
            Id = 10,
            BranchId = 1,
            Name = "Phòng Gác Mái Rừng Thông",
            Status = "Available",
            PricePerHour = 150000,
            PricePerDay = 1200000
        });
        await context.SaveChangesAsync();

        var service = new PricingService(context);
        var start = new DateTime(2026, 5, 20, 10, 0, 0);
        var end = start.AddHours(hours);

        var total = await service.CalculateStayPriceAsync(10, start, end, true);

        Assert.Equal(expected, total);
    }
}
