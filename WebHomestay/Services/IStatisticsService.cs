using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebHomestay.Services
{
    public interface IStatisticsService
    {
        Task<DashboardDataDto> GetDashboardStatsAsync(DateTime start, DateTime end, int? branchId);
        Task<List<ChartDataPoint>> GetBranchesAsync();
    }

    public class DashboardDataDto
    {
        public decimal TotalRevenue { get; set; }
        public decimal ActiveRevenue { get; set; }
        public int TotalBookings { get; set; }
        public int CompletedBookings { get; set; }
        public int ActiveBookings { get; set; }
        public double OccupancyRate { get; set; }
        public double CancellationRate { get; set; }
        public double SurchargeRatio { get; set; }
        public List<ChartDataPoint> RevenueTrends { get; set; } = new();
        public List<ChartDataPoint> BranchRevenueSplit { get; set; } = new();
        public List<HeatmapPoint> TimeHeatmap { get; set; } = new();
        public List<ChartDataPoint> DayDistribution { get; set; } = new();
        public List<ChartDataPoint> HourDistribution { get; set; } = new();
        public List<ChartDataPoint> BookingModeRatio { get; set; } = new();
        public List<RoomPerformanceDto> RoomPerformance { get; set; } = new();
    }

    public class HeatmapPoint
    {
        public int DayOfWeek { get; set; } // 0-6
        public int Hour { get; set; } // 0-23
        public int Count { get; set; }
    }

    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public class RoomPerformanceDto
    {
        public string RoomName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public int TotalOrders { get; set; }
        public decimal Revenue { get; set; }
        public double OccupancyPercentage { get; set; }
    }
}
