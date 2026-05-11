using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Services
{
    public class StatisticsService : IStatisticsService
    {
        private readonly ApplicationDbContext _context;

        public StatisticsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardDataDto> GetDashboardStatsAsync(DateTime start, DateTime end, int? branchId)
        {
            // Set end time to end of day
            var endDate = end.Date.AddDays(1).AddTicks(-1);

            var bookingsQuery = _context.Bookings
                .Include(b => b.Room)
                .ThenInclude(r => r.Branch)
                .Where(b => !b.IsDeleted && b.StartTime >= start && b.StartTime <= endDate);

            if (branchId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.Room.BranchId == branchId.Value);
            }

            var allBookings = await bookingsQuery.ToListAsync();
            var validBookings = allBookings.Where(b => b.Status != "Cancelled").ToList();

            var completedBookings = allBookings.Where(b => b.Status == "CheckedOut").ToList();
            var activeBookings = allBookings.Where(b => b.Status == "AwaitingApproval" || b.Status == "Confirmed").ToList();

            var dto = new DashboardDataDto
            {
                TotalRevenue = completedBookings.Sum(b => b.TotalPrice),
                ActiveRevenue = activeBookings.Sum(b => b.TotalPrice),
                TotalBookings = validBookings.Count,
                CompletedBookings = completedBookings.Count,
                ActiveBookings = activeBookings.Count,
                CancellationRate = allBookings.Count > 0 
                    ? (double)allBookings.Count(b => b.Status == "Cancelled") / allBookings.Count * 100 
                    : 0,
                SurchargeRatio = validBookings.Count > 0
                    ? (double)validBookings.Count(b => b.Room != null && b.GuestCount > b.Room.Capacity && b.Room.ExtraGuestFee > 0) / validBookings.Count * 100
                    : 0
            };

            // Revenue Trends (Daily) - Only Completed
            dto.RevenueTrends = completedBookings
                .GroupBy(b => b.StartTime.Date)
                .OrderBy(g => g.Key)
                .Select(g => new ChartDataPoint { Label = g.Key.ToString("dd/MM"), Value = g.Sum(b => b.TotalPrice) })
                .ToList();
            
            // Branch Revenue Split - Only Completed
            dto.BranchRevenueSplit = completedBookings
                .GroupBy(b => b.Room?.Branch?.Name ?? "Unknown")
                .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Sum(b => b.TotalPrice) })
                .ToList();

            // Time Heatmap (Day of Week vs Hour)
            dto.TimeHeatmap = validBookings
                .GroupBy(b => new { Day = (int)b.StartTime.DayOfWeek, Hour = b.StartTime.Hour })
                .Select(g => new HeatmapPoint { DayOfWeek = g.Key.Day, Hour = g.Key.Hour, Count = g.Count() })
                .ToList();

            // Day Distribution - accounting for duration
            string[] daysVi = { "CN", "T2", "T3", "T4", "T5", "T6", "T7" };
            var dayCounts = new int[7];
            foreach (var b in completedBookings)
            {
                var current = b.StartTime.Date;
                var checkoutDate = b.EndTime.Date;
                while (current <= checkoutDate)
                {
                    dayCounts[(int)current.DayOfWeek]++;
                    current = current.AddDays(1);
                }
            }
            dto.DayDistribution = dayCounts
                .Select((count, day) => new ChartDataPoint { Label = daysVi[day], Value = count })
                .ToList();

            // Hour Distribution (0-23) - accounting for duration
            var hourCounts = new int[24];
            foreach (var b in completedBookings)
            {
                // For each booking, mark all hours it spans
                int startH = b.StartTime.Hour;
                int endH = b.EndTime.Hour;
                
                // Simple case: same day
                if (b.EndTime.Date == b.StartTime.Date)
                {
                    for (int h = startH; h <= endH; h++) { if (h < 24) hourCounts[h]++; }
                }
                else
                {
                    // Spans multiple days - just count the full range capped at 24h for the visualization
                    // or just count the hours of the first day to keep it representative of "peak hours"
                    for (int h = startH; h < 24; h++) hourCounts[h]++;
                    for (int h = 0; h <= endH; h++) { if (h < 24) hourCounts[h]++; }
                }
            }
            
            dto.HourDistribution = hourCounts
                .Select((count, hour) => new ChartDataPoint { Label = $"{hour:D2}:00", Value = count })
                .ToList();

            // Booking Mode Ratio
            dto.BookingModeRatio = new List<ChartDataPoint>
            {
                new() { Label = "Daily", Value = validBookings.Count(b => b.BookingMode == BookingMode.Daily) },
                new() { Label = "Hourly", Value = validBookings.Count(b => b.BookingMode == BookingMode.Hourly) }
            };

            // Room Performance
            var roomsQuery = _context.Rooms.Include(r => r.Branch).AsQueryable();
            if (branchId.HasValue) roomsQuery = roomsQuery.Where(r => r.BranchId == branchId.Value);
            var rooms = await roomsQuery.ToListAsync();

            foreach (var room in rooms)
            {
                var roomBookings = completedBookings.Where(b => b.RoomId == room.Id).ToList();
                dto.RoomPerformance.Add(new RoomPerformanceDto
                {
                    RoomName = room.Name,
                    BranchName = room.Branch?.Name ?? "Unknown",
                    TotalOrders = roomBookings.Count,
                    Revenue = roomBookings.Sum(b => b.TotalPrice),
                    OccupancyPercentage = 0
                });
            }

            return dto;
        }

        public async Task<List<ChartDataPoint>> GetBranchesAsync()
        {
            return await _context.Branches
                .OrderBy(b => b.Name)
                .Select(b => new ChartDataPoint { Label = b.Name, Value = b.Id })
                .ToListAsync();
        }
    }
}
