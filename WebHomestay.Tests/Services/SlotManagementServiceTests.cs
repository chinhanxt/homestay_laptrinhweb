// WebHomestay.Tests/Services/SlotManagementServiceTests.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class SlotManagementServiceTests
{
    [Fact]
    public async Task ApplyTemplateToDateAsync_GeneratesInventoryRecords()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(ApplyTemplateToDateAsync_GeneratesInventoryRecords))
            .Options;
        await using var context = new ApplicationDbContext(options);
        
        context.RoomSlotTemplates.Add(new RoomSlotTemplate 
        { 
            Id = 1, 
            Name = "Combo 2h", 
            DurationMinutes = 120, 
            CleanupMinutes = 30,
            SeedStartTime = new TimeOnly(9, 0)
        });
        await context.SaveChangesAsync();

        var service = new SlotManagementService(context, new SlotGenerationService());
        
        await service.ApplyTemplateToDateAsync(5, 1, new DateOnly(2026, 5, 10));

        var count = await context.RoomSlotInventories.CountAsync(i => i.RoomId == 5 && i.SlotDate == new DateOnly(2026, 5, 10));
        Assert.True(count > 0);
    }
}
