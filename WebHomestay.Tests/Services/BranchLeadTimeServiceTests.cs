using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class BranchLeadTimeServiceTests
{
    [Fact]
    public async Task ResolveAsync_SeparateHourlyAndDaily_BuildsCorrectCutoffs()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(ResolveAsync_SeparateHourlyAndDaily_BuildsCorrectCutoffs))
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.Branches.Add(new Branch
        {
            Id = 1,
            Name = "Q1",
            Address = "Sai Gon",
            BookingLeadTimeHours = 3,
            BookingLeadTimeDays = 2
        });
        await context.SaveChangesAsync();

        var now = new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc);
        var service = new BranchLeadTimeService(context, () => now);

        var rule = await service.ResolveAsync(1, CancellationToken.None);

        Assert.Equal(3, rule.HourlyLeadTimeHours);
        Assert.Equal(2, rule.DailyLeadTimeDays);
        Assert.Equal(now.AddHours(3), rule.HourlyCutoffUtc);
        Assert.Equal(new DateOnly(2026, 6, 19), rule.EarliestAllowedHourlyDate);
        Assert.Equal(new DateOnly(2026, 6, 21), rule.EarliestAllowedDailyDate); // 19/06 + 2 days = 21/06
    }

    [Fact]
    public async Task ResolveAsync_ZeroDailyLeadTime_AllowsToday()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(ResolveAsync_ZeroDailyLeadTime_AllowsToday))
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.Branches.Add(new Branch
        {
            Id = 2,
            Name = "Q1",
            Address = "Sai Gon",
            BookingLeadTimeHours = 0,
            BookingLeadTimeDays = 0
        });
        await context.SaveChangesAsync();

        var now = new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc);
        var service = new BranchLeadTimeService(context, () => now);

        var rule = await service.ResolveAsync(2, CancellationToken.None);

        Assert.Equal(0, rule.HourlyLeadTimeHours);
        Assert.Equal(0, rule.DailyLeadTimeDays);
        Assert.Equal(now, rule.HourlyCutoffUtc);
        Assert.Equal(new DateOnly(2026, 6, 19), rule.EarliestAllowedHourlyDate);
        Assert.Equal(new DateOnly(2026, 6, 19), rule.EarliestAllowedDailyDate); // 0 days means today is allowed
    }
}
