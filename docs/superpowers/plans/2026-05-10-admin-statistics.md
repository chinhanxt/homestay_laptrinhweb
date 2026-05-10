# Admin Statistics Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a comprehensive Statistics Dashboard for administrators to track revenue, occupancy, and booking trends.

**Architecture:** ASP.NET Core MVC with a dedicated StatisticsService for data aggregation. The frontend will use Chart.js for visualization and AJAX for dynamic filtering.

**Tech Stack:** C#, Entity Framework Core, Chart.js, HTML5, Vanilla CSS, jQuery/AJAX.

---

### Task 1: Create Statistics Service Layer

**Files:**
- Create: `WebHomestay/Services/IStatisticsService.cs`
- Create: `WebHomestay/Services/StatisticsService.cs`
- Modify: `WebHomestay/Program.cs`

- [ ] **Step 1: Define IStatisticsService interface**
Define the data structures and methods needed for the dashboard.
```csharp
namespace WebHomestay.Services
{
    public interface IStatisticsService
    {
        Task<DashboardDataDto> GetDashboardStatsAsync(DateTime start, DateTime end, int? branchId);
    }

    public class DashboardDataDto
    {
        public decimal TotalRevenue { get; set; }
        public int TotalBookings { get; set; }
        public double OccupancyRate { get; set; }
        public double CancellationRate { get; set; }
        public List<ChartDataPoint> RevenueTrends { get; set; } = new();
        public List<ChartDataPoint> HourDistribution { get; set; } = new();
        public List<ChartDataPoint> BookingModeRatio { get; set; } = new();
        public List<RoomPerformanceDto> RoomPerformance { get; set; } = new();
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
```

- [ ] **Step 2: Implement StatisticsService**
Implement the logic to query the database and calculate metrics.
```csharp
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
            var bookingsQuery = _context.Bookings
                .Include(b => b.Room)
                .ThenInclude(r => r.Branch)
                .Where(b => !b.IsDeleted && b.StartTime >= start && b.StartTime <= end);

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

            // Revenue Trends (Daily)
            dto.RevenueTrends = validBookings
                .GroupBy(b => b.StartTime.Date)
                .Select(g => new ChartDataPoint { Label = g.Key.ToString("dd/MM"), Value = g.Sum(b => b.TotalPrice) })
                .OrderBy(x => x.Label)
                .ToList();

            // Hour Distribution
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
                    OccupancyPercentage = 0 // Basic placeholder for now, complex calculation in later task
                });
            }

            return dto;
        }
    }
}
```

- [ ] **Step 3: Register service in Program.cs**
```csharp
// Around line 50
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
```

- [ ] **Step 4: Commit**
```bash
git add WebHomestay/Services/IStatisticsService.cs WebHomestay/Services/StatisticsService.cs WebHomestay/Program.cs
git commit -m "feat: add IStatisticsService and basic implementation"
```

### Task 2: Create Statistics Controller

**Files:**
- Create: `WebHomestay/Controllers/StatisticsController.cs`

- [ ] **Step 1: Implement Controller Actions**
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebHomestay.Services;

namespace WebHomestay.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class StatisticsController : Controller
    {
        private readonly IStatisticsService _statsService;

        public StatisticsController(IStatisticsService statsService)
        {
            _statsService = statsService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetStats(DateTime? start, DateTime? end, int? branchId)
        {
            var startDate = start ?? DateTime.UtcNow.AddDays(-30);
            var endDate = end ?? DateTime.UtcNow;
            
            var stats = await _statsService.GetDashboardStatsAsync(startDate, endDate, branchId);
            return Json(stats);
        }
    }
}
```

- [ ] **Step 2: Commit**
```bash
git add WebHomestay/Controllers/StatisticsController.cs
git commit -m "feat: add StatisticsController with JSON endpoint"
```

### Task 3: Implement Dashboard View & Layout

**Files:**
- Create: `WebHomestay/Views/Statistics/Index.cshtml`
- Create: `WebHomestay/wwwroot/css/admin-stats.css`

- [ ] **Step 1: Create CSS for Dashboard**
Define the "Analytics Pro" aesthetic.
```css
.stats-dashboard { padding: 2rem; background: #0f172a; color: white; min-height: 100vh; }
.stats-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 1.5rem; margin-bottom: 2rem; }
.stat-card { background: rgba(30, 41, 59, 0.7); backdrop-filter: blur(10px); border: 1px solid rgba(255,255,255,0.1); padding: 1.5rem; border-radius: 1rem; }
.stat-value { font-size: 2rem; font-weight: bold; color: #10b981; }
.chart-container { background: rgba(30, 41, 59, 0.7); padding: 1.5rem; border-radius: 1rem; margin-bottom: 1.5rem; }
```

- [ ] **Step 2: Create Dashboard HTML Structure**
```html
@{
    ViewData["Title"] = "Analytics Dashboard";
}
<div class="stats-dashboard">
    <div class="d-flex justify-content-between align-items-center mb-4">
        <h1>Analytics Pro</h1>
        <div class="filter-bar d-flex gap-2">
            <button class="btn btn-outline-light quick-filter" data-days="0">Today</button>
            <button class="btn btn-outline-light quick-filter" data-days="7">Week</button>
            <button class="btn btn-outline-light quick-filter" data-days="30">Month</button>
            <input type="date" id="startDate" class="form-control bg-dark text-white">
            <input type="date" id="endDate" class="form-control bg-dark text-white">
            <button id="applyFilter" class="btn btn-primary">Apply</button>
        </div>
    </div>

    <div class="stats-grid">
        <div class="stat-card">
            <div class="text-muted">Total Revenue</div>
            <div id="totalRevenue" class="stat-value">$0</div>
        </div>
        <div class="stat-card">
            <div class="text-muted">Orders</div>
            <div id="totalBookings" class="stat-value">0</div>
        </div>
        <div class="stat-card">
            <div class="text-muted">Occupancy</div>
            <div id="occupancyRate" class="stat-value">0%</div>
        </div>
        <div class="stat-card">
            <div class="text-muted">Cancellation</div>
            <div id="cancelRate" class="stat-value" style="color: #ef4444;">0%</div>
        </div>
    </div>

    <div class="row">
        <div class="col-md-8">
            <div class="chart-container">
                <canvas id="revenueChart"></canvas>
            </div>
        </div>
        <div class="col-md-4">
            <div class="chart-container">
                <canvas id="modeChart"></canvas>
            </div>
        </div>
    </div>

    <div class="chart-container">
        <canvas id="hourChart"></canvas>
    </div>

    <div class="chart-container">
        <h3>Room Performance</h3>
        <table class="table table-dark table-hover" id="roomTable">
            <thead>
                <tr>
                    <th>Room</th>
                    <th>Branch</th>
                    <th>Orders</th>
                    <th>Revenue</th>
                </tr>
            </thead>
            <tbody></tbody>
        </table>
    </div>
</div>
```

- [ ] **Step 3: Commit**
```bash
git add WebHomestay/Views/Statistics/Index.cshtml WebHomestay/wwwroot/css/admin-stats.css
git commit -m "ui: create dashboard layout and summary cards"
```

### Task 4: Implement JS Integration & Charts

**Files:**
- Modify: `WebHomestay/Views/Statistics/Index.cshtml`

- [ ] **Step 1: Add Chart.js Script and Initialization Logic**
```javascript
@section Scripts {
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <script>
        let revenueChart, modeChart, hourChart;

        async function fetchStats() {
            const start = $('#startDate').val();
            const end = $('#endDate').val();
            const response = await fetch(`/Statistics/GetStats?start=${start}&end=${end}`);
            const data = await response.json();
            updateDashboard(data);
        }

        function updateDashboard(data) {
            $('#totalRevenue').text(new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(data.totalRevenue));
            $('#totalBookings').text(data.totalBookings);
            $('#cancelRate').text(data.cancellationRate.toFixed(1) + '%');

            // Update Tables
            const tbody = $('#roomTable tbody').empty();
            data.roomPerformance.forEach(rp => {
                tbody.append(`<tr><td>${rp.roomName}</td><td>${rp.branchName}</td><td>${rp.totalOrders}</td><td>${rp.revenue.toLocaleString()}</td></tr>`);
            });

            // Update Charts
            updateCharts(data);
        }

        function updateCharts(data) {
            // Revenue Chart
            if (revenueChart) revenueChart.destroy();
            revenueChart = new Chart($('#revenueChart'), {
                type: 'line',
                data: {
                    labels: data.revenueTrends.map(x => x.label),
                    datasets: [{ label: 'Revenue', data: data.revenueTrends.map(x => x.value), borderColor: '#10b981', tension: 0.4 }]
                }
            });

            // Hour Chart
            if (hourChart) hourChart.destroy();
            hourChart = new Chart($('#hourChart'), {
                type: 'bar',
                data: {
                    labels: data.hourDistribution.map(x => x.label),
                    datasets: [{ label: 'Bookings', data: data.hourDistribution.map(x => x.value), backgroundColor: '#3b82f6' }]
                }
            });
        }

        $(document).ready(() => {
            fetchStats();
            $('#applyFilter').click(fetchStats);
        });
    </script>
}
```

- [ ] **Step 2: Commit**
```bash
git add WebHomestay/Views/Statistics/Index.cshtml
git commit -m "feat: implement Chart.js visualization and AJAX updates"
```
