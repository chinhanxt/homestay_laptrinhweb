// WebHomestay.Tests/Services/RoomBookingViewServiceTests.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class RoomBookingViewServiceTests
{
    [Fact]
    public void BuildHourlyGroups_GroupsSlotsByTemplateName()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(BuildHourlyGroups_GroupsSlotsByTemplateName))
            .Options;
        using var context = new ApplicationDbContext(options);
        var service = new RoomBookingViewService(context, new AvailabilityService(context));
        var slots = new List<RoomSlotInventory>
        {
            new() { SlotLabel = "09:30-11:30", Status = "Available", Template = new RoomSlotTemplate { Name = "Combo 2h" } },
            new() { SlotLabel = "13:30-15:30", Status = "Booked", Template = new RoomSlotTemplate { Name = "Combo 2h" } },
            new() { SlotLabel = "10:00-19:00", Status = "Available", Template = new RoomSlotTemplate { Name = "Ban ngay" } }
        };

        var groups = service.BuildHourlyGroups(slots);

        Assert.Equal(2, groups.Count);
        Assert.Equal("Ban ngay", groups[0].Title);
        Assert.Equal("Combo 2h", groups[1].Title);
        Assert.Equal(2, groups[1].Slots.Count);
    }

    [Fact]
    public async Task BuildAsync_DaysUnit_MarksDatesBeforeEarliestAllowedAsBlocked()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(BuildAsync_DaysUnit_MarksDatesBeforeEarliestAllowedAsBlocked))
            .Options;
        await using var context = new ApplicationDbContext(options);

        context.Branches.Add(new Branch
        {
            Id = 1,
            Name = "Dong Nai",
            Address = "Bien Hoa",
            BookingLeadTimeValue = 7,
            BookingLeadTimeUnit = BranchLeadTimeUnit.Days,
            BookingLeadTimeDays = 7
        });
        context.Rooms.Add(new Room
        {
            Id = 3,
            BranchId = 1,
            Name = "Room 3",
            Status = "Available",
            PricePerDay = 500000,
            PricePerHour = 100000
        });
        await context.SaveChangesAsync();

        var room = await context.Rooms.Include(r => r.Branch).SingleAsync(r => r.Id == 3);
        var service = new RoomBookingViewService(
            context,
            new AvailabilityService(context),
            new SettingService(context, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            new PricingService(context),
            new BranchLeadTimeService(context, () => new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc)));

        var model = await service.BuildAsync(room, new DateOnly(2026, 6, 27));

        Assert.Equal("Blocked", model.DailyCalendar.Single(d => d.Date == new DateOnly(2026, 6, 25)).Status);
        Assert.Equal("Available", model.DailyCalendar.Single(d => d.Date == new DateOnly(2026, 6, 26)).Status);
    }
}
