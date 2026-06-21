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
        var tomorrow = DateTime.UtcNow.AddDays(1);
        context.RoomSlotInventories.Add(new RoomSlotInventory
        {
            Id = 77,
            RoomId = 5,
            TemplateId = 1,
            SlotLabel = "09:30-11:30",
            StartTime = new DateTime(tomorrow.Year, tomorrow.Month, tomorrow.Day, 9, 30, 0),
            EndTime = new DateTime(tomorrow.Year, tomorrow.Month, tomorrow.Day, 11, 30, 0),
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

    [Fact]
    public async Task CreateHourlyBookingAsync_WhenSlotBelongsToDifferentRoom_ThrowsAndDoesNotBookSlot()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(CreateHourlyBookingAsync_WhenSlotBelongsToDifferentRoom_ThrowsAndDoesNotBookSlot))
            .Options;
        await using var context = new ApplicationDbContext(options);

        context.Branches.Add(new Branch { Id = 1, Name = "Test Branch", Address = "123 Test Street" });
        context.Rooms.AddRange(
            new Room { Id = 5, Name = "Slot Room", BranchId = 1, Status = "Available", Capacity = 2, MaxGuests = 2, PricePerHour = 100000, PricePerDay = 500000 },
            new Room { Id = 6, Name = "Requested Room", BranchId = 1, Status = "Available", Capacity = 2, MaxGuests = 2, PricePerHour = 120000, PricePerDay = 600000 });
        context.RoomSlotInventories.Add(new RoomSlotInventory
        {
            Id = 77,
            RoomId = 5,
            TemplateId = 1,
            SlotDate = new DateOnly(2026, 5, 10),
            SlotLabel = "09:30-11:30",
            StartTime = new DateTime(2026, 5, 10, 9, 30, 0),
            EndTime = new DateTime(2026, 5, 10, 11, 30, 0),
            Status = "Available"
        });
        await context.SaveChangesAsync();

        var service = new BookingCreationService(context, new AvailabilityService(context));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateHourlyBookingAsync(new CreateBookingRequest
        {
            RoomId = 6,
            BookingMode = BookingMode.Hourly,
            SlotInventoryId = 77,
            CustomerName = "Nhân",
            CustomerPhone = "0900000000",
            GuestCount = 2
        }));

        Assert.Contains("không hợp lệ", ex.Message, StringComparison.OrdinalIgnoreCase);
        var slot = await context.RoomSlotInventories.SingleAsync(item => item.Id == 77);
        Assert.Equal("Available", slot.Status);
        Assert.Empty(context.Bookings);
    }

    [Fact]
    public async Task CreateDailyBookingAsync_WhenCheckInIsInsideBlockedLeadTime_Throws()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(CreateDailyBookingAsync_WhenCheckInIsInsideBlockedLeadTime_Throws))
            .Options;
        await using var context = new ApplicationDbContext(options);

        context.Branches.Add(new Branch
        {
            Id = 1,
            Name = "Sai Gon",
            Address = "Q1",
            BookingLeadTimeValue = 7,
            BookingLeadTimeUnit = BranchLeadTimeUnit.Days,
            BookingLeadTimeDays = 7
        });
        context.Rooms.Add(new Room
        {
            Id = 10,
            BranchId = 1,
            Name = "Room 10",
            Status = "Available",
            Capacity = 2,
            MaxGuests = 2,
            PricePerDay = 500000,
            PricePerHour = 100000
        });
        await context.SaveChangesAsync();

        var service = new BookingCreationService(
            context,
            new AvailabilityService(context),
            new PricingService(context),
            new BranchLeadTimeService(context, () => new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDailyBookingAsync(new CreateBookingRequest
        {
            RoomId = 10,
            BookingMode = BookingMode.Daily,
            CheckInDate = new DateOnly(2026, 6, 25),
            CheckOutDate = new DateOnly(2026, 6, 27),
            CustomerName = "Nhan",
            CustomerPhone = "0900000000",
            GuestCount = 2
        }));

        Assert.Contains("26/06/2026", ex.Message);
    }
}
