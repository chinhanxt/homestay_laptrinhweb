// WebHomestay.Tests/Services/BookingCreationServiceTests.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class BookingCreationServiceTests
{
    [Fact]
    public async Task CreateHourlyBookingAsync_ReservesInventoryAndPersistsBooking()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(CreateHourlyBookingAsync_ReservesInventoryAndPersistsBooking))
            .Options;
        await using var context = new ApplicationDbContext(options);

        context.Branches.Add(new Branch
        {
            Id = 1,
            Name = "Test Branch",
            Address = "123 Test Street"
        });
        context.Rooms.Add(new Room
        {
            Id = 5,
            Name = "Test Room",
            BranchId = 1,
            Status = "Available"
        });
        context.RoomSlotInventories.Add(new RoomSlotInventory
        {
            Id = 77,
            RoomId = 5,
            TemplateId = 1,
            SlotLabel = "09:30-11:30",
            StartTime = new DateTime(2026, 5, 10, 9, 30, 0),
            EndTime = new DateTime(2026, 5, 10, 11, 30, 0),
            Status = "Available"
        });
        await context.SaveChangesAsync();

        var service = new BookingCreationService(context, new AvailabilityService(context));
        var request = new CreateBookingRequest
        {
            RoomId = 5,
            SlotInventoryId = 77,
            BookingMode = BookingMode.Hourly,
            CustomerName = "Nhan",
            CustomerPhone = "0900000000"
        };

        var booking = await service.CreateHourlyBookingAsync(request);

        Assert.Equal(BookingMode.Hourly, booking.BookingMode);
        Assert.Equal("Booked", context.RoomSlotInventories.Single().Status);
    }
}
