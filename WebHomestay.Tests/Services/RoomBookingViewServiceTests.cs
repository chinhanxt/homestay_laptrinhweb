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
}
