using WebHomestay.Services;
using WebHomestay.Services.Slots;
using WebHomestay.Services.Room;
using WebHomestay.Services.Chat;
using WebHomestay.Services.Settings;
using WebHomestay.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;
using BookingEntity = WebHomestay.Models.Entities.Core.Booking;

namespace WebHomestay.Services;

public class BookingCreationService : IBookingCreationService
{
    private readonly ApplicationDbContext _context;
    private readonly IAvailabilityService _availabilityService;
    private readonly PricingService _pricingService;
    private readonly IBranchLeadTimeService _branchLeadTimeService;

    public BookingCreationService(
        ApplicationDbContext context,
        IAvailabilityService availabilityService,
        PricingService pricingService,
        IBranchLeadTimeService branchLeadTimeService)
    {
        _context = context;
        _availabilityService = availabilityService;
        _pricingService = pricingService;
        _branchLeadTimeService = branchLeadTimeService;
    }

    public BookingCreationService(ApplicationDbContext context, IAvailabilityService availabilityService, PricingService pricingService)
        : this(context, availabilityService, pricingService, new BranchLeadTimeService(context))
    {
    }

    public BookingCreationService(ApplicationDbContext context, IAvailabilityService availabilityService)
        : this(context, availabilityService, new PricingService(context), new BranchLeadTimeService(context))
    {
    }

    public async Task<BookingEntity> CreateHourlyBookingAsync(CreateBookingRequest request)
    {
        var inventory = await _context.RoomSlotInventories
            .Include(i => i.Room)
            .SingleAsync(i => i.Id == request.SlotInventoryId);
            
        if (!await _availabilityService.IsHourlySlotAvailableForRoomAsync(inventory.Id, request.RoomId, request.GuestCount))
            throw new InvalidOperationException("Khung giờ đặt phòng không hợp lệ hoặc vừa được người khác đặt.");

        var leadTimeRule = await _branchLeadTimeService.ResolveAsync(inventory.Room.BranchId);
        if (!leadTimeRule.AllowsHourly(inventory.StartTime))
        {
            throw new InvalidOperationException(
                $"Chi nhánh này yêu cầu đặt trước tối thiểu {leadTimeRule.HourlyLeadTimeHours} giờ.");
        }

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
            TotalPrice = await _pricingService.CalculateStayPriceAsync(request.RoomId, inventory.StartTime, inventory.EndTime, true) + (Math.Max(0, request.GuestCount - inventory.Room.Capacity) * inventory.Room.ExtraGuestFee),
            Status = "AwaitingPayment",
            PaymentStatus = "Unpaid"
        };

        inventory.Status = "Booked";
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();
        return booking;
    }

    public async Task<BookingEntity> CreateDailyBookingAsync(CreateBookingRequest request)
    {
        var room = await _context.Rooms.FindAsync(request.RoomId);
        if (room == null) throw new InvalidOperationException("Không tìm thấy phòng.");

        var interval = BookingTimeRules.BuildDailyStay(request.CheckInDate!.Value, request.CheckOutDate!.Value);
        if (!await _availabilityService.IsRoomAvailable(request.RoomId, interval.Start, interval.End))
            throw new InvalidOperationException("Khoảng ngày này không còn trống.");

        var leadTimeRule = await _branchLeadTimeService.ResolveAsync(room.BranchId);
        if (!leadTimeRule.AllowsDaily(request.CheckInDate!.Value))
        {
            throw new InvalidOperationException(
                $"Chi nhánh này yêu cầu đặt trước tối thiểu {leadTimeRule.DailyLeadTimeDays} ngày. Ngày gần nhất có thể đặt là {leadTimeRule.EarliestAllowedDailyDate:dd/MM/yyyy}.");
        }

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
            TotalPrice = await _pricingService.CalculateStayPriceAsync(request.RoomId, interval.Start, interval.End, false) + (Math.Max(0, request.GuestCount - room.Capacity) * room.ExtraGuestFee),
            Status = "AwaitingPayment",
            PaymentStatus = "Unpaid"
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();
        return booking;
    }
}
