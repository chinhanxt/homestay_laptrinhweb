// WebHomestay/Services/AvailabilityService.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;

namespace WebHomestay.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly ApplicationDbContext _context;

    public AvailabilityService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsRoomAvailable(int roomId, DateTime start, DateTime end, int? ignoreBookingId = null)
    {
        // Check for overlapping bookings
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
        var hasOverlap = await _context.Bookings
            .AnyAsync(b => b.RoomId == roomId &&
                           b.Id != ignoreBookingId &&
                           !b.IsDeleted &&
                           b.Status != "Cancelled" &&
                           !(b.Status == "AwaitingPayment" && b.CreatedAt < fiveMinutesAgo) &&
                           b.StartTime < end &&
                           b.EndTime > start);

        if (hasOverlap) return false;

        // Check for blocked inventory slots
        var hasBlockedSlot = await _context.RoomSlotInventories
            .AnyAsync(s => s.RoomId == roomId &&
                           s.Status == "Blocked" &&
                           s.StartTime < end &&
                           s.EndTime > start);

        return !hasBlockedSlot;
    }

    public async Task<List<int>> GetAvailableRoomIds(int branchId, DateTime start, DateTime end)
    {
        // Get all rooms in branch
        var roomIds = await _context.Rooms
            .Where(r => r.BranchId == branchId && r.Status == "Available")
            .Select(r => r.Id)
            .ToListAsync();

        // Filter out those with overlapping bookings
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
        var occupiedRoomIds = await _context.Bookings
            .Where(b => roomIds.Contains(b.RoomId) &&
                        !b.IsDeleted &&
                        b.Status != "Cancelled" &&
                        !(b.Status == "AwaitingPayment" && b.CreatedAt < fiveMinutesAgo) &&
                        b.StartTime < end &&
                        b.EndTime > start)
            .Select(b => b.RoomId)
            .Distinct()
            .ToListAsync();

        var availableRoomIds = roomIds.Except(occupiedRoomIds).ToList();

        // Further filter by blocked slots
        var blockedRoomIds = await _context.RoomSlotInventories
            .Where(s => availableRoomIds.Contains(s.RoomId) &&
                        s.Status == "Blocked" &&
                        s.StartTime < end &&
                        s.EndTime > start)
            .Select(s => s.RoomId)
            .Distinct()
            .ToListAsync();

        return availableRoomIds.Except(blockedRoomIds).ToList();
    }

    public async Task<List<DateOnly>> GetBlockedDatesAsync(int roomId, DateOnly visibleFrom, int visibleDays)
    {
        var visibleTo = visibleFrom.AddDays(visibleDays);
        var startOfVisible = visibleFrom.ToDateTime(TimeOnly.MinValue);
        var endOfVisible = visibleTo.ToDateTime(TimeOnly.MaxValue);

        var bookings = await _context.Bookings
            .Where(b => b.RoomId == roomId &&
                        !b.IsDeleted &&
                        b.Status != "Cancelled" &&
                        b.StartTime < endOfVisible &&
                        b.EndTime > startOfVisible)
            .ToListAsync();

        var blockedDates = new HashSet<DateOnly>();
        for (var i = 0; i < visibleDays; i++)
        {
            var date = visibleFrom.AddDays(i);
            var dayInterval = BookingTimeRules.BuildDailyStay(date, date.AddDays(1));
            
            if (bookings.Any(b => b.StartTime < dayInterval.End && b.EndTime > dayInterval.Start))
            {
                blockedDates.Add(date);
            }
        }

        return blockedDates.ToList();
    }
}
