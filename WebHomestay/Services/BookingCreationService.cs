// WebHomestay/Services/BookingCreationService.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services;

public class BookingCreationService : IBookingCreationService
{
    private readonly ApplicationDbContext _context;
    private readonly IAvailabilityService _availabilityService;

    public BookingCreationService(ApplicationDbContext context, IAvailabilityService availabilityService)
    {
        _context = context;
        _availabilityService = availabilityService;
    }

    public async Task<Booking> CreateHourlyBookingAsync(CreateBookingRequest request)
    {
        var inventory = await _context.RoomSlotInventories
            .Include(i => i.Room)
            .SingleAsync(i => i.Id == request.SlotInventoryId);
            
        if (!await _availabilityService.IsRoomAvailable(request.RoomId, inventory.StartTime, inventory.EndTime))
            throw new InvalidOperationException("Khung giờ này vừa được người khác đặt.");

        var booking = new Booking
        {
            RoomId = request.RoomId,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            CustomerEmail = request.CustomerEmail,
            GuestCount = request.GuestCount,
            CustomerNote = request.CustomerNote,
            BookingMode = BookingMode.Hourly,
            RoomSlotInventoryId = inventory.Id,
            SlotLabel = inventory.SlotLabel,
            StartTime = inventory.StartTime,
            EndTime = inventory.EndTime,
            TotalPrice = inventory.Room.PricePerHour + (Math.Max(0, request.GuestCount - inventory.Room.Capacity) * inventory.Room.ExtraGuestFee),
            Status = "AwaitingPayment",
            PaymentStatus = "Unpaid"
        };

        inventory.Status = "Booked";
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();
        return booking;
    }

    public async Task<Booking> CreateDailyBookingAsync(CreateBookingRequest request)
    {
        var room = await _context.Rooms.FindAsync(request.RoomId);
        if (room == null) throw new InvalidOperationException("Không tìm thấy phòng.");

        var interval = BookingTimeRules.BuildDailyStay(request.CheckInDate!.Value, request.CheckOutDate!.Value);
        if (!await _availabilityService.IsRoomAvailable(request.RoomId, interval.Start, interval.End))
            throw new InvalidOperationException("Khoảng ngày này không còn trống.");

        // Calculate nights
        int nights = (request.CheckOutDate!.Value.DayNumber - request.CheckInDate!.Value.DayNumber);
        if (nights < 1) nights = 1;

        var booking = new Booking
        {
            RoomId = request.RoomId,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            CustomerEmail = request.CustomerEmail,
            GuestCount = request.GuestCount,
            CustomerNote = request.CustomerNote,
            BookingMode = BookingMode.Daily,
            StartTime = interval.Start,
            EndTime = interval.End,
            TotalPrice = (room.PricePerDay * nights) + (Math.Max(0, request.GuestCount - room.Capacity) * room.ExtraGuestFee),
            Status = "AwaitingPayment",
            PaymentStatus = "Unpaid"
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();
        return booking;
    }
}
