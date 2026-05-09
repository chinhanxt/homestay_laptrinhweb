// WebHomestay.Tests/Services/SlotGenerationServiceTests.cs
using WebHomestay.Models;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class SlotGenerationServiceTests
{
    [Fact]
    public void GenerateRollingSlots_CreatesThirtyMinuteCleanupGap()
    {
        var template = new RoomSlotTemplate
        {
            Name = "Combo 2h",
            Code = "2H",
            DurationMinutes = 120,
            CleanupMinutes = 30,
            SeedStartTime = new TimeOnly(0, 0)
        };
        var service = new SlotGenerationService();

        var slots = service.GenerateRollingSlots(10, template, new DateOnly(2026, 5, 10));

        Assert.Equal(new DateTime(2026, 5, 10, 0, 0, 0), slots[0].StartTime);
        Assert.Equal(new DateTime(2026, 5, 10, 2, 0, 0), slots[0].EndTime);
        Assert.Equal(new DateTime(2026, 5, 10, 2, 30, 0), slots[1].StartTime);
    }
}
