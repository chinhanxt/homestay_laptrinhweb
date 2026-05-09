// WebHomestay/Services/RoomBookingViewService.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services;

public class RoomBookingViewService : IRoomBookingViewService
{
    private readonly ApplicationDbContext _context;
    private readonly IAvailabilityService _availabilityService;
    private readonly ISettingService _settingService;

    public RoomBookingViewService(ApplicationDbContext context, IAvailabilityService availabilityService, ISettingService settingService)
    {
        _context = context;
        _availabilityService = availabilityService;
        _settingService = settingService;
    }

    public async Task<RoomDetailsViewModel> BuildAsync(Room room, DateOnly selectedHourlyDate)
    {
        var allSlots = await _context.RoomSlotInventories
            .Include(slot => slot.Template)
            .Where(slot => slot.RoomId == room.Id && slot.SlotDate == selectedHourlyDate)
            .OrderBy(slot => slot.StartTime)
            .ToListAsync();

        // Filter:
        // - If slot.Status == "Booked" -> Keep (Show as red/booked)
        // - If slot.Status == "Available" AND IsRoomAvailable -> Keep (Show as available)
        // - Otherwise -> Hide (it overlaps with something else)
        var hourlySlots = new List<RoomSlotInventory>();
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);

        int globalLeadTimeHours = await _settingService.GetIntAsync("BookingLeadTimeHours", 2);
        int branchLeadTimeHours = room.Branch?.BookingLeadTimeHours ?? 0;
        
        // Use branch lead time if it's set (not 0), otherwise fallback to global
        int leadTimeHours = branchLeadTimeHours > 0 ? branchLeadTimeHours : globalLeadTimeHours;
        
        DateTime cutoffTime = DateTime.UtcNow.AddHours(leadTimeHours);

        foreach (var slot in allSlots)
        {
            // Filter out slots that are in the past or within the lead time buffer
            if (slot.StartTime < cutoffTime) continue;

            if (slot.Status == "Booked")
            {
                // Verify if the booking is still valid
                var hasActiveBooking = await _context.Bookings
                    .AnyAsync(b => b.RoomSlotInventoryId == slot.Id && 
                                   !b.IsDeleted && 
                                   b.Status != "Cancelled" &&
                                   !(b.Status == "AwaitingPayment" && b.CreatedAt < fiveMinutesAgo));

                if (hasActiveBooking)
                {
                    hourlySlots.Add(slot);
                }
                else
                {
                    // Fallback: If no active booking, treat as available
                    slot.Status = "Available";
                    hourlySlots.Add(slot);
                }
            }
            else if (slot.Status == "Available" && await _availabilityService.IsRoomAvailable(room.Id, slot.StartTime, slot.EndTime))
            {
                hourlySlots.Add(slot);
            }
        }

        var blockedDates = await _availabilityService.GetBlockedDatesAsync(room.Id, DateOnly.FromDateTime(DateTime.Today), 30);

        return new RoomDetailsViewModel
        {
            Room = room,
            SelectedHourlyDate = selectedHourlyDate,
            HourlyGroups = BuildHourlyGroupsSync(hourlySlots),
            DailyCalendar = Enumerable.Range(0, 30)
                .Select(offset =>
                {
                    var date = DateOnly.FromDateTime(DateTime.Today).AddDays(offset);
                    return new CalendarDayViewModel
                    {
                        Date = date,
                        Status = blockedDates.Contains(date) ? "Blocked" : "Available"
                    };
                })
                .ToList()
        };
    }

    private List<HourlySlotGroupViewModel> BuildHourlyGroupsSync(IEnumerable<RoomSlotInventory> slots)
    {
        return slots
            .GroupBy(slot => slot.Template?.Name ?? "Khác")
            .Select(group => new HourlySlotGroupViewModel
            {
                Title = group.Key,
                Slots = group.Select(slot => new HourlySlotCardViewModel
                {
                    SlotId = slot.Id,
                    Label = slot.SlotLabel,
                    Status = slot.Status
                }).ToList()
            })
            .OrderBy(group => group.Title)
            .ToList();
    }

    public async Task<BookingCheckoutViewModel?> BuildHourlyCheckoutAsync(int roomId, int slotId)
    {
        var slot = await _context.RoomSlotInventories
            .Include(i => i.Room)
            .SingleOrDefaultAsync(i => i.Id == slotId && i.RoomId == roomId);
        if (slot == null) return null;

        return new BookingCheckoutViewModel
        {
            Room = slot.Room,
            BookingMode = BookingMode.Hourly,
            SlotInventoryId = slot.Id,
            SlotLabel = slot.SlotLabel,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            TotalPrice = slot.Room.PricePerHour // Assuming simple pricing for now
        };
    }

    public async Task<BookingCheckoutViewModel?> BuildDailyCheckoutAsync(int roomId, DateOnly checkInDate, DateOnly checkOutDate)
    {
        var room = await _context.Rooms.SingleOrDefaultAsync(r => r.Id == roomId);
        if (room == null) return null;

        var interval = BookingTimeRules.BuildDailyStay(checkInDate, checkOutDate);
        var days = (checkOutDate.ToDateTime(TimeOnly.MinValue) - checkInDate.ToDateTime(TimeOnly.MinValue)).Days;
        
        return new BookingCheckoutViewModel
        {
            Room = room,
            BookingMode = BookingMode.Daily,
            CheckInDate = checkInDate,
            CheckOutDate = checkOutDate,
            StartTime = interval.Start,
            EndTime = interval.End,
            TotalPrice = room.PricePerDay * Math.Max(1, days)
        };
    }
}
