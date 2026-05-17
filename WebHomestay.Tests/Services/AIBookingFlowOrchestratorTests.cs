using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class AIBookingFlowOrchestratorTests
{
    [Fact]
    public async Task BuildRoomCardsAsync_FiltersByBranchAndGuestCount()
    {
        await using var context = CreateContext(nameof(BuildRoomCardsAsync_FiltersByBranchAndGuestCount));
        SeedBranchesAndRooms(context);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var response = await service.BuildRoomCardsAsync(new AIBookingSessionState
        {
            BranchId = 1,
            BranchName = "StayEasy Sài Gòn",
            BookingMode = "hourly",
            HourlyDate = new DateOnly(2026, 5, 20),
            GuestCount = 2
        });

        var block = Assert.Single(response.UiBlocks);
        Assert.Equal("roomCards", block.Type);
        var rooms = GetRooms(block.Data);
        Assert.Single(rooms);
        Assert.Equal("Sài Gòn Couple", rooms[0].Name);
        Assert.DoesNotContain(rooms, room => room.Name == "Sài Gòn Solo");
        Assert.DoesNotContain(rooms, room => room.Name == "Đà Lạt View");
    }

    [Fact]
    public async Task SelectRoomAsync_ReturnsSlotsButDoesNotCreateBooking()
    {
        await using var context = CreateContext(nameof(SelectRoomAsync_ReturnsSlotsButDoesNotCreateBooking));
        SeedBranchesRoomsAndSlots(context);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var response = await service.SelectRoomAsync(new AIBookingActionRequest
        {
            SessionId = "s1",
            Action = "select-room",
            State = new AIBookingSessionState
            {
                BranchId = 1,
                BranchName = "StayEasy Sài Gòn",
                BookingMode = "hourly",
                HourlyDate = new DateOnly(2026, 5, 20),
                GuestCount = 2,
                SelectedRoomId = 10
            }
        });

        Assert.Empty(context.Bookings);
        var slotBlock = Assert.Single(response.UiBlocks, block => block.Type == "hourlySlots");
        var slots = GetSlots(slotBlock.Data);
        var slot = Assert.Single(slots);
        Assert.Equal(100, slot.SlotId);
        Assert.Equal(10, slot.RoomId);
        Assert.Equal("Sài Gòn Couple", slot.RoomName);
        Assert.Equal("09:00-11:00", slot.Label);
        Assert.Equal(new DateTime(2026, 5, 20, 9, 0, 0), slot.StartTime);
        Assert.Equal(new DateTime(2026, 5, 20, 11, 0, 0), slot.EndTime);
        Assert.DoesNotContain(slots, slot => slot.RoomId != 10);
        Assert.DoesNotContain(slots, slot => slot.Label != "09:00-11:00");
    }

    [Fact]
    public async Task SubmitBookingFormAsync_CreatesAwaitingPaymentBookingAndReturnsPaymentBlock()
    {
        await using var context = CreateContext(nameof(SubmitBookingFormAsync_CreatesAwaitingPaymentBookingAndReturnsPaymentBlock));
        SeedBranchesRoomsAndSlots(context);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var response = await service.SubmitBookingFormAsync(new AIBookingActionRequest
        {
            SessionId = "s1",
            Action = "submit-booking-form",
            State = new AIBookingSessionState
            {
                BranchId = 1,
                BranchName = "StayEasy Sài Gòn",
                BookingMode = "hourly",
                HourlyDate = new DateOnly(2026, 5, 20),
                GuestCount = 2,
                SelectedRoomId = 10,
                SelectedRoomName = "Sài Gòn Couple",
                SelectedSlotId = 100,
                SelectedSlotLabel = "09:00-11:00"
            },
            FormSubmission = new AIBookingFormSubmission
            {
                CustomerName = "Nguyễn An",
                PhoneNumber = "0900000000",
                Email = "an@example.com",
                Notes = "Đến đúng giờ"
            }
        });

        var booking = Assert.Single(context.Bookings);
        Assert.Equal(10, booking.RoomId);
        Assert.Equal(100, booking.RoomSlotInventoryId);
        Assert.Equal("09:00-11:00", booking.SlotLabel);
        Assert.Equal("Nguyễn An", booking.CustomerName);
        Assert.Equal("0900000000", booking.CustomerPhone);
        Assert.Equal("an@example.com", booking.CustomerEmail);
        Assert.Equal("Đến đúng giờ", booking.CustomerNote);
        Assert.Equal(2, booking.GuestCount);
        Assert.Equal(BookingMode.Hourly, booking.BookingMode);
        Assert.Equal(new DateTime(2026, 5, 20, 9, 0, 0), booking.StartTime);
        Assert.Equal(new DateTime(2026, 5, 20, 11, 0, 0), booking.EndTime);
        Assert.True(booking.TotalPrice > 0);
        Assert.Equal("AwaitingPayment", booking.Status);
        Assert.Equal("Unpaid", booking.PaymentStatus);
        var inventory = await context.RoomSlotInventories.SingleAsync(slot => slot.Id == 100);
        Assert.Equal("Booked", inventory.Status);
        Assert.Contains(response.UiBlocks, block => block.Type == "paymentQr");
    }

    private static ApplicationDbContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static AIBookingFlowOrchestrator CreateService(ApplicationDbContext context)
    {
        var availability = new AvailabilityService(context);
        var pricing = new PricingService(context);
        return new AIBookingFlowOrchestrator(
            context,
            availability,
            new BookingCreationService(context, availability, pricing),
            pricing,
            new MemoryCache(new MemoryCacheOptions()));
    }

    private static void SeedBranchesAndRooms(ApplicationDbContext context)
    {
        context.Branches.AddRange(
            new Branch { Id = 1, Name = "StayEasy Sài Gòn", Address = "Q1" },
            new Branch { Id = 2, Name = "StayEasy Đà Lạt", Address = "Đà Lạt" });
        context.Rooms.AddRange(
            new Room { Id = 10, BranchId = 1, Name = "Sài Gòn Couple", Status = "Available", Capacity = 2, MaxGuests = 3, PricePerHour = 120000, PricePerDay = 650000, ExtraGuestFee = 80000 },
            new Room { Id = 11, BranchId = 1, Name = "Sài Gòn Solo", Status = "Available", Capacity = 1, MaxGuests = 1, PricePerHour = 90000, PricePerDay = 500000, ExtraGuestFee = 0 },
            new Room { Id = 20, BranchId = 2, Name = "Đà Lạt View", Status = "Available", Capacity = 2, MaxGuests = 4, PricePerHour = 150000, PricePerDay = 800000, ExtraGuestFee = 100000 });
    }

    private static void SeedBranchesRoomsAndSlots(ApplicationDbContext context)
    {
        SeedBranchesAndRooms(context);
        context.RoomSlotTemplates.Add(new RoomSlotTemplate
        {
            Id = 1,
            Name = "Sáng",
            Code = "MORNING",
            DurationMinutes = 120,
            CleanupMinutes = 0,
            FixedStartTime = new TimeOnly(9, 0),
            FixedEndTime = new TimeOnly(11, 0),
            IsActive = true
        });
        context.RoomSlotInventories.Add(new RoomSlotInventory
        {
            Id = 100,
            RoomId = 10,
            TemplateId = 1,
            SlotDate = new DateOnly(2026, 5, 20),
            SlotLabel = "09:00-11:00",
            StartTime = new DateTime(2026, 5, 20, 9, 0, 0),
            EndTime = new DateTime(2026, 5, 20, 11, 0, 0),
            Status = "Available"
        });
    }

    private static List<AIRoomCard> GetRooms(object data)
    {
        var property = data.GetType().GetProperty("rooms") ?? data.GetType().GetProperty("Rooms");
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<List<AIRoomCard>>(property.GetValue(data));
    }

    private static List<AISlotOption> GetSlots(object data)
    {
        var property = data.GetType().GetProperty("slots") ?? data.GetType().GetProperty("Slots");
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<List<AISlotOption>>(property.GetValue(data));
    }
}
