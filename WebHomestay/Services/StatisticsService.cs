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

            var dto = new DashboardDataDto
            {
                TotalRevenue = validBookings.Sum(b => b.TotalPrice),
                TotalBookings = validBookings.Count,
                CancellationRate = allBookings.Count > 0 
                    ? (double)allBookings.Count(b => b.Status == "Cancelled") / allBookings.Count * 100 
                    : 0
            };

            // Revenue Trends (Daily) - Grouped by Date and then sorted by Date
            dto.RevenueTrends = validBookings
                .GroupBy(b => b.StartTime.Date)
                .OrderBy(g => g.Key)
                .Select(g => new ChartDataPoint { Label = g.Key.ToString("dd/MM"), Value = g.Sum(b => b.TotalPrice) })
                .ToList();

            // Hour Distribution (0-23)
            dto.HourDistribution = validBookings
                .GroupBy(b => b.StartTime.Hour)
                .Select(g => new ChartDataPoint { Label = $"{g.Key}h", Value = g.Count() })
                .OrderBy(x => int.Parse(x.Label.Replace("h", "")))
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
                var roomBookings = validBookings.Where(b => b.RoomId == room.Id).ToList();
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
    }
}
