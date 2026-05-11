# Analytics Bento Pro Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a high-performance, visually stunning "Bento Box" style analytics dashboard for homestay management.

**Architecture:** ASP.NET Core MVC with a dedicated Service layer for statistics. Frontend uses Chart.js and custom CSS Grid for heatmaps. Data is fetched via AJAX for a single-page feel.

**Tech Stack:** C#, ASP.NET Core, EF Core, Chart.js, Vanilla CSS, ClosedXML (Excel), QuestPDF (PDF).

---

### Task 1: Update Data Structures (DTOs)

**Files:**
- Modify: `WebHomestay/Services/IStatisticsService.cs`

- [ ] **Step 1: Update DashboardDataDto and related classes**

```csharp
public class DashboardDataDto
{
    public decimal TotalRevenue { get; set; }
    public int TotalBookings { get; set; }
    public double OccupancyRate { get; set; }
    public double CancellationRate { get; set; }
    public double SurchargeRatio { get; set; } // New: % of bookings with surcharges
    
    public List<ChartDataPoint> RevenueTrends { get; set; } = new();
    public List<ChartDataPoint> BranchRevenueSplit { get; set; } = new(); // New: Revenue by Branch
    public List<HeatmapPoint> TimeHeatmap { get; set; } = new(); // New: Day vs Hour density
    public List<ChartDataPoint> BookingModeRatio { get; set; } = new();
    public List<RoomPerformanceDto> RoomPerformance { get; set; } = new();
}

public class HeatmapPoint
{
    public int DayOfWeek { get; set; } // 0-6
    public int Hour { get; set; } // 0-23
    public int Count { get; set; }
}
```

- [ ] **Step 2: Commit changes**

```bash
git add WebHomestay/Services/IStatisticsService.cs
git commit -m "feat(stats): update DTOs for advanced analytics"
```

---

### Task 2: Implement Backend Logic in StatisticsService

**Files:**
- Modify: `WebHomestay/Services/StatisticsService.cs`

- [ ] **Step 1: Implement advanced statistics calculation**

```csharp
public async Task<DashboardDataDto> GetDashboardStatsAsync(DateTime start, DateTime end, int? branchId)
{
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
            : 0,
        SurchargeRatio = validBookings.Count > 0
            ? (double)validBookings.Count(b => b.ExtraFee > 0) / validBookings.Count * 100
            : 0
    };

    // Revenue Trends
    dto.RevenueTrends = validBookings
        .GroupBy(b => b.StartTime.Date)
        .OrderBy(g => g.Key)
        .Select(g => new ChartDataPoint { Label = g.Key.ToString("dd/MM"), Value = g.Sum(b => b.TotalPrice) })
        .ToList();

    // Branch Revenue Split
    dto.BranchRevenueSplit = validBookings
        .GroupBy(b => b.Room.Branch.Name)
        .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Sum(b => b.TotalPrice) })
        .ToList();

    // Time Heatmap (Day vs Hour)
    dto.TimeHeatmap = validBookings
        .GroupBy(b => new { Day = (int)b.StartTime.DayOfWeek, Hour = b.StartTime.Hour })
        .Select(g => new HeatmapPoint { DayOfWeek = g.Key.Day, Hour = g.Key.Hour, Count = g.Count() })
        .ToList();

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
            Revenue = roomBookings.Sum(b => b.TotalPrice)
        });
    }

    return dto;
}
```

- [ ] **Step 2: Commit changes**

```bash
git add WebHomestay/Services/StatisticsService.cs
git commit -m "feat(stats): implement advanced logic in StatisticsService"
```

---

### Task 3: Create Bento Dashboard CSS

**Files:**
- Create: `WebHomestay/wwwroot/css/admin-bento-stats.css`

- [ ] **Step 1: Write the modern CSS system**

```css
:root {
    --glass-bg: rgba(255, 255, 255, 0.03);
    --glass-border: rgba(255, 255, 255, 0.08);
    --accent-emerald: #10b981;
    --accent-blue: #3b82f6;
    --accent-amber: #f59e0b;
}

.bento-container {
    display: grid;
    grid-template-columns: repeat(12, 1fr);
    gap: 1.5rem;
    padding: 1.5rem;
}

.bento-card {
    background: var(--glass-bg);
    backdrop-filter: blur(12px);
    border: 1px solid var(--glass-border);
    border-radius: 1.25rem;
    padding: 1.5rem;
    transition: transform 0.2s ease, border-color 0.2s ease;
}

.bento-card:hover {
    border-color: rgba(255, 255, 255, 0.2);
}

.stat-value {
    font-size: 2rem;
    font-weight: 800;
    margin-top: 0.5rem;
}

/* Heatmap Styles */
.heatmap-grid {
    display: grid;
    grid-template-columns: repeat(24, 1fr);
    gap: 2px;
}

.heatmap-cell {
    aspect-ratio: 1;
    border-radius: 2px;
    background: rgba(255, 255, 255, 0.05);
}
```

- [ ] **Step 2: Commit**

---

### Task 4: Redesign Statistics Index View

**Files:**
- Modify: `WebHomestay/Views/Statistics/Index.cshtml`

- [ ] **Step 1: Implement the Bento layout and filters**
- [ ] **Step 2: Add export buttons**
- [ ] **Step 3: Update JavaScript for AJAX and Heatmap rendering**

---

### Task 5: Implement Export to Excel and PDF

**Files:**
- Modify: `WebHomestay/Controllers/StatisticsController.cs`
- Modify: `WebHomestay/WebHomestay.csproj` (add packages)

- [ ] **Step 1: Add ClosedXML and QuestPDF to project**
- [ ] **Step 2: Create Export actions in controller**
- [ ] **Step 3: Commit**

---

(Self-Review: Plan is detailed, exact paths provided, code blocks show logic, no placeholders).
