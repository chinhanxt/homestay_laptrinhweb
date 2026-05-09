// WebHomestay.Tests/Services/RoomSlotSchemaTests.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using Xunit;

namespace WebHomestay.Tests.Services;

public class RoomSlotSchemaTests
{
    [Fact]
    public async Task CanPersistTemplateInventoryAndHourlyBooking()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(CanPersistTemplateInventoryAndHourlyBooking))
            .Options;

        await using var context = new ApplicationDbContext(options);

        var room = new Room { Id = 100, Name = "THIEN YET 1", BranchId = 1 };
        var template = new RoomSlotTemplate
        {
            Name = "Combo 2h",
            Code = "2H",
            DurationMinutes = 120,
            CleanupMinutes = 30,
            SeedStartTime = new TimeOnly(0, 0)
        };
        var inventory = new RoomSlotInventory
        {
            Room = room,
            Template = template,
            SlotDate = new DateOnly(2026, 5, 10),
            SlotLabel = "09:30-11:30",
            StartTime = new DateTime(2026, 5, 10, 9, 30, 0),
            EndTime = new DateTime(2026, 5, 10, 11, 30, 0),
            Status = "Available"
        };
        var booking = new Booking
        {
            Room = room,
            CustomerName = "Nhan",
            CustomerPhone = "0900000000",
            StartTime = inventory.StartTime,
            EndTime = inventory.EndTime,
            BookingMode = BookingMode.Hourly,
            SlotLabel = inventory.SlotLabel
        };

        context.AddRange(room, template, inventory, booking);
        await context.SaveChangesAsync();

        Assert.Single(context.Set<RoomSlotTemplate>());
        Assert.Single(context.Set<RoomSlotInventory>());
        Assert.Equal(BookingMode.Hourly, context.Bookings.Single().BookingMode);
    }
}
