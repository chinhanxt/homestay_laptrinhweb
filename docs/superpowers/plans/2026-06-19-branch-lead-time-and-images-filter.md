# Branch Lead Time And Images Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add branch-scoped lead time configuration with `Hours`/`Days` semantics and add branch filtering to the admin images page without weakening branch permission boundaries.

**Architecture:** Introduce a single branch lead time resolver that all booking surfaces use, instead of duplicating `BookingLeadTimeHours` checks across public web, admin, and AI services. Update the branch settings UI and image listing controller/view to use explicit branch-scoped filters while preserving current session-based access checks.

**Tech Stack:** ASP.NET Core MVC, EF Core, PostgreSQL, Razor views, jQuery, xUnit, EF InMemory

---

## File Structure

**Create:**

- `WebHomestay/Models/BranchLeadTimeUnit.cs`
  - enum-like source of truth for `Hours` and `Days`
- `WebHomestay/Models/ViewModels/BranchLeadTimeRule.cs`
  - resolved branch lead time model returned by a shared service
- `WebHomestay/Services/IBranchLeadTimeService.cs`
  - interface for resolving branch lead time rules
- `WebHomestay/Services/BranchLeadTimeService.cs`
  - shared implementation used by booking, AI, and admin services
- `WebHomestay/Migrations/<timestamp>_AddBranchLeadTimeValueAndUnit.cs`
  - schema and data migration from `BookingLeadTimeHours`
- `WebHomestay.Tests/Services/BranchLeadTimeServiceTests.cs`
  - core lead time rule tests

**Modify:**

- `WebHomestay/Models/Branch.cs`
  - replace single lead-time-hours storage with value + unit
- `WebHomestay/Data/ApplicationDbContext.cs`
  - map new columns to lowercase PostgreSQL names
- `WebHomestay/Program.cs`
  - register the shared lead time service and update any default seeding if still needed
- `WebHomestay/Controllers/AdminSettingsController.cs`
  - accept unit + value in branch config updates
- `WebHomestay/Views/AdminSettings/Index.cshtml`
  - add dropdown `Giờ` / `Ngày` and helper text
- `WebHomestay/Controllers/AdminImagesController.cs`
  - accept optional branch filter for `Index` and `Trash`
- `WebHomestay/Views/AdminImages/Index.cshtml`
  - render branch dropdown
- `WebHomestay/Views/AdminImages/Trash.cshtml`
  - optionally mirror branch dropdown if trash should stay filterable
- `WebHomestay/Services/RoomBookingViewService.cs`
  - use shared lead time service for hourly slots and daily calendar disabled dates
- `WebHomestay/Services/BookingCreationService.cs`
  - enforce lead time in booking creation for hourly and daily flows
- `WebHomestay/Services/ContextAwareBookingConductor.cs`
  - stop suggesting invalid dates/slots and return clear messages
- `WebHomestay/Services/AdminChatQuickSendService.cs`
  - use shared lead time service instead of its private hour-only cutoff
- `WebHomestay/Services/AI/AdminAIReseedService.cs`
  - generate branch lead time text with the correct unit
- `WebHomestay/Services/BulkImportService.cs`
  - default imported old-format lead time rows to `Hours`
- `WebHomestay.Tests/Services/RoomBookingViewServiceTests.cs`
  - add hourly/day filtering tests
- `WebHomestay.Tests/Services/BookingCreationServiceTests.cs`
  - add backend enforcement tests
- `WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs`
  - add AI gating tests for invalid day ranges
- `WebHomestay.Tests/Controllers/AdminChatMonitorControllerTests.cs`
  - add quick-send coverage if branch lead time affects CTA generation

## Task 1: Add Shared Branch Lead Time Model And Service

**Files:**

- Create: `WebHomestay/Models/BranchLeadTimeUnit.cs`
- Create: `WebHomestay/Models/ViewModels/BranchLeadTimeRule.cs`
- Create: `WebHomestay/Services/IBranchLeadTimeService.cs`
- Create: `WebHomestay/Services/BranchLeadTimeService.cs`
- Modify: `WebHomestay/Program.cs`
- Test: `WebHomestay.Tests/Services/BranchLeadTimeServiceTests.cs`

- [ ] **Step 1: Write the failing tests for hour and day rules**

```csharp
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class BranchLeadTimeServiceTests
{
    [Fact]
    public async Task ResolveAsync_HoursUnit_BuildsHourlyCutoffFromNow()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(ResolveAsync_HoursUnit_BuildsHourlyCutoffFromNow))
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.Branches.Add(new Branch
        {
            Id = 1,
            Name = "Q1",
            Address = "Sai Gon",
            BookingLeadTimeValue = 2,
            BookingLeadTimeUnit = BranchLeadTimeUnit.Hours
        });
        await context.SaveChangesAsync();

        var now = new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc);
        var service = new BranchLeadTimeService(context, () => now);

        var rule = await service.ResolveAsync(1, CancellationToken.None);

        Assert.Equal(BranchLeadTimeUnit.Hours, rule.Unit);
        Assert.Equal(now.AddHours(2), rule.HourlyCutoffUtc);
        Assert.Null(rule.EarliestAllowedDailyDate);
    }

    [Fact]
    public async Task ResolveAsync_DaysUnit_UsesUserApprovedInclusiveBlockRange()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(ResolveAsync_DaysUnit_UsesUserApprovedInclusiveBlockRange))
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.Branches.Add(new Branch
        {
            Id = 7,
            Name = "Dong Nai",
            Address = "Bien Hoa",
            BookingLeadTimeValue = 7,
            BookingLeadTimeUnit = BranchLeadTimeUnit.Days
        });
        await context.SaveChangesAsync();

        var now = new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc);
        var service = new BranchLeadTimeService(context, () => now);

        var rule = await service.ResolveAsync(7, CancellationToken.None);

        Assert.Equal(BranchLeadTimeUnit.Days, rule.Unit);
        Assert.Equal(new DateOnly(2026, 6, 27), rule.EarliestAllowedDailyDate);
    }
}
```

- [ ] **Step 2: Run the service tests to verify they fail**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BranchLeadTimeServiceTests
```

Expected: FAIL because the new model/service types do not exist yet.

- [ ] **Step 3: Add the shared types and service**

`WebHomestay/Models/BranchLeadTimeUnit.cs`

```csharp
namespace WebHomestay.Models;

public static class BranchLeadTimeUnit
{
    public const string Hours = "Hours";
    public const string Days = "Days";

    public static string Normalize(string? value)
    {
        return string.Equals(value, Days, StringComparison.OrdinalIgnoreCase)
            ? Days
            : Hours;
    }
}
```

`WebHomestay/Models/ViewModels/BranchLeadTimeRule.cs`

```csharp
namespace WebHomestay.Models.ViewModels;

public class BranchLeadTimeRule
{
    public int BranchId { get; set; }
    public int Value { get; set; }
    public string Unit { get; set; } = Models.BranchLeadTimeUnit.Hours;
    public DateTime? HourlyCutoffUtc { get; set; }
    public DateOnly? EarliestAllowedDailyDate { get; set; }

    public bool AllowsHourly(DateTime slotStartUtc)
        => !HourlyCutoffUtc.HasValue || slotStartUtc >= HourlyCutoffUtc.Value;

    public bool AllowsDaily(DateOnly checkInDate)
        => !EarliestAllowedDailyDate.HasValue || checkInDate >= EarliestAllowedDailyDate.Value;
}
```

`WebHomestay/Services/IBranchLeadTimeService.cs`

```csharp
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services;

public interface IBranchLeadTimeService
{
    Task<BranchLeadTimeRule> ResolveAsync(int? branchId, CancellationToken cancellationToken = default);
}
```

`WebHomestay/Services/BranchLeadTimeService.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services;

public class BranchLeadTimeService : IBranchLeadTimeService
{
    private readonly ApplicationDbContext _context;
    private readonly Func<DateTime> _utcNow;

    public BranchLeadTimeService(ApplicationDbContext context, Func<DateTime>? utcNow = null)
    {
        _context = context;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public async Task<BranchLeadTimeRule> ResolveAsync(int? branchId, CancellationToken cancellationToken = default)
    {
        var branch = branchId.HasValue
            ? await _context.Branches
                .AsNoTracking()
                .Where(b => b.Id == branchId.Value)
                .Select(b => new { b.Id, b.BookingLeadTimeValue, b.BookingLeadTimeUnit })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var value = Math.Max(0, branch?.BookingLeadTimeValue ?? 2);
        var unit = BranchLeadTimeUnit.Normalize(branch?.BookingLeadTimeUnit);
        var nowUtc = _utcNow();

        return new BranchLeadTimeRule
        {
            BranchId = branch?.Id ?? 0,
            Value = value,
            Unit = unit,
            HourlyCutoffUtc = unit == BranchLeadTimeUnit.Hours ? nowUtc.AddHours(value) : null,
            EarliestAllowedDailyDate = unit == BranchLeadTimeUnit.Days
                ? DateOnly.FromDateTime(nowUtc.Date).AddDays(value + 1)
                : null
        };
    }
}
```

`WebHomestay/Program.cs`

```csharp
builder.Services.AddScoped<IBranchLeadTimeService, BranchLeadTimeService>();
```

- [ ] **Step 4: Run the service tests to verify they pass**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BranchLeadTimeServiceTests
```

Expected: PASS with both tests green.

- [ ] **Step 5: Commit**

```powershell
git add WebHomestay/Models/BranchLeadTimeUnit.cs WebHomestay/Models/ViewModels/BranchLeadTimeRule.cs WebHomestay/Services/IBranchLeadTimeService.cs WebHomestay/Services/BranchLeadTimeService.cs WebHomestay/Program.cs WebHomestay.Tests/Services/BranchLeadTimeServiceTests.cs
git commit -m "feat: add shared branch lead time resolver"
```

## Task 2: Migrate Branch Storage To Value + Unit

**Files:**

- Modify: `WebHomestay/Models/Branch.cs`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`
- Create: `WebHomestay/Migrations/<timestamp>_AddBranchLeadTimeValueAndUnit.cs`
- Modify: `WebHomestay/Migrations/ApplicationDbContextModelSnapshot.cs`
- Test: `WebHomestay.Tests/Services/BranchLeadTimeServiceTests.cs`

- [ ] **Step 1: Extend the tests to cover migrated defaults**

Add to `WebHomestay.Tests/Services/BranchLeadTimeServiceTests.cs`:

```csharp
[Fact]
public async Task ResolveAsync_WhenUnitMissing_DefaultsToHours()
{
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(nameof(ResolveAsync_WhenUnitMissing_DefaultsToHours))
        .Options;
    await using var context = new ApplicationDbContext(options);
    context.Branches.Add(new Branch
    {
        Id = 8,
        Name = "Q7",
        Address = "Riverside",
        BookingLeadTimeValue = 3,
        BookingLeadTimeUnit = null!
    });
    await context.SaveChangesAsync();

    var service = new BranchLeadTimeService(context, () => new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc));
    var rule = await service.ResolveAsync(8, CancellationToken.None);

    Assert.Equal(BranchLeadTimeUnit.Hours, rule.Unit);
    Assert.Equal(new DateTime(2026, 6, 19, 11, 0, 0, DateTimeKind.Utc), rule.HourlyCutoffUtc);
}
```

- [ ] **Step 2: Run the tests to verify the model is not ready yet**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BranchLeadTimeServiceTests
```

Expected: FAIL or compile error because `Branch` does not expose the new schema cleanly yet.

- [ ] **Step 3: Update the branch entity and EF mapping**

`WebHomestay/Models/Branch.cs`

```csharp
public string BookingLeadTimeUnit { get; set; } = BranchLeadTimeUnit.Hours;
public int BookingLeadTimeValue { get; set; } = 2;
```

Remove:

```csharp
public int BookingLeadTimeHours { get; set; } = 2;
```

In `WebHomestay/Data/ApplicationDbContext.cs` branch mapping:

```csharp
entity.Property(e => e.BookingLeadTimeValue)
    .HasColumnName("booking_lead_time_value");

entity.Property(e => e.BookingLeadTimeUnit)
    .HasColumnName("booking_lead_time_unit")
    .HasMaxLength(20);
```

Migration body:

```csharp
migrationBuilder.AddColumn<int>(
    name: "booking_lead_time_value",
    table: "branches",
    type: "integer",
    nullable: false,
    defaultValue: 2);

migrationBuilder.AddColumn<string>(
    name: "booking_lead_time_unit",
    table: "branches",
    type: "character varying(20)",
    maxLength: 20,
    nullable: false,
    defaultValue: "Hours");

migrationBuilder.Sql("""
    UPDATE branches
    SET booking_lead_time_value = COALESCE(booking_lead_time_hours, 2),
        booking_lead_time_unit = 'Hours';
""");
```

If the team wants an immediate cleanup migration, also drop `booking_lead_time_hours`; otherwise leave it one release and stop reading it in code.

- [ ] **Step 4: Run tests and build after the migration/model update**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BranchLeadTimeServiceTests
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: Tests PASS and build PASS.

- [ ] **Step 5: Commit**

```powershell
git add WebHomestay/Models/Branch.cs WebHomestay/Data/ApplicationDbContext.cs WebHomestay/Migrations WebHomestay.Tests/Services/BranchLeadTimeServiceTests.cs
git commit -m "feat: store branch lead time as value and unit"
```

## Task 3: Update Branch Settings UI And Controller

**Files:**

- Modify: `WebHomestay/Controllers/AdminSettingsController.cs`
- Modify: `WebHomestay/Views/AdminSettings/Index.cshtml`
- Test: manual verification on `/chinhan/hethong/settings?tab=branch`

- [ ] **Step 1: Add a controller test target by documenting the expected POST payload**

Use this form contract in the implementation:

```http
POST /chinhan/hethong/settings/update-branch
branchId=5
leadTimeValue=7
leadTimeUnit=Days
```

Expected behavior:

- value is clamped to `>= 0`
- unit normalizes to `Hours` or `Days`
- success toast names the branch

- [ ] **Step 2: Update the controller action signature**

`WebHomestay/Controllers/AdminSettingsController.cs`

```csharp
[AdminAuthorize(Permission = "settings.update")]
[HttpPost("update-branch")]
public async Task<IActionResult> UpdateBranch(int branchId, int leadTimeValue, string leadTimeUnit)
{
    var branch = await _context.Branches.FindAsync(branchId);
    if (branch != null)
    {
        branch.BookingLeadTimeValue = Math.Max(0, leadTimeValue);
        branch.BookingLeadTimeUnit = BranchLeadTimeUnit.Normalize(leadTimeUnit);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Cập nhật cấu hình cho chi nhánh {branch.Name} thành công.";
    }

    return RedirectToAction(nameof(Index), new { tab = "branch" });
}
```

- [ ] **Step 3: Update the branch settings card UI**

In `WebHomestay/Views/AdminSettings/Index.cshtml`, replace the single hours-only input block with:

```cshtml
<label class="form-label small fw-bold text-muted">Thời gian đặt trước tối thiểu</label>
<div class="d-flex gap-2 align-items-stretch">
    <div class="input-group-premium flex-grow-1">
        <i class="fas fa-clock text-muted"></i>
        <input type="number"
               name="leadTimeValue"
               min="0"
               value="@b.BookingLeadTimeValue"
               class="form-control-premium"
               placeholder="Vd: 2">
    </div>
    <select name="leadTimeUnit" class="form-select premium-input" style="max-width: 120px;">
        <option value="Hours" selected="@(b.BookingLeadTimeUnit == WebHomestay.Models.BranchLeadTimeUnit.Hours)">Giờ</option>
        <option value="Days" selected="@(b.BookingLeadTimeUnit == WebHomestay.Models.BranchLeadTimeUnit.Days)">Ngày</option>
    </select>
</div>
<div class="small text-muted mt-2">
    Nếu chọn <strong>Ngày</strong>, ví dụ hôm nay 19/06/2026 và cấu hình 7 ngày thì khách chỉ được đặt từ 27/06/2026.
</div>
```

- [ ] **Step 4: Run the web app and verify the branch settings tab manually**

Run:

```powershell
dotnet run --project WebHomestay/WebHomestay.csproj
```

Verify on `http://localhost:5000/chinhan/hethong/settings?tab=branch`:

- branch cards show value + unit
- saving with `Giờ` persists correctly
- saving with `Ngày` persists correctly
- helper text matches the approved `19 -> 27` example

- [ ] **Step 5: Commit**

```powershell
git add WebHomestay/Controllers/AdminSettingsController.cs WebHomestay/Views/AdminSettings/Index.cshtml
git commit -m "feat: support day-based branch lead time settings"
```

## Task 4: Enforce Lead Time In Public Booking View And Booking Creation

**Files:**

- Modify: `WebHomestay/Services/RoomBookingViewService.cs`
- Modify: `WebHomestay/Services/BookingCreationService.cs`
- Modify: `WebHomestay/Services/IBookingCreationService.cs` only if helper methods are added
- Test: `WebHomestay.Tests/Services/RoomBookingViewServiceTests.cs`
- Test: `WebHomestay.Tests/Services/BookingCreationServiceTests.cs`

- [ ] **Step 1: Write failing tests for daily blocking and backend enforcement**

Add to `WebHomestay.Tests/Services/BookingCreationServiceTests.cs`:

```csharp
[Fact]
public async Task CreateDailyBookingAsync_WhenCheckInIsInsideBlockedLeadTime_Throws()
{
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(nameof(CreateDailyBookingAsync_WhenCheckInIsInsideBlockedLeadTime_Throws))
        .Options;
    await using var context = new ApplicationDbContext(options);

    context.Branches.Add(new Branch
    {
        Id = 1,
        Name = "Sai Gon",
        Address = "Q1",
        BookingLeadTimeValue = 7,
        BookingLeadTimeUnit = BranchLeadTimeUnit.Days
    });
    context.Rooms.Add(new Room
    {
        Id = 10,
        BranchId = 1,
        Name = "Room 10",
        Status = "Available",
        Capacity = 2,
        MaxGuests = 2,
        PricePerDay = 500000,
        PricePerHour = 100000
    });
    await context.SaveChangesAsync();

    var service = new BookingCreationService(
        context,
        new AvailabilityService(context),
        new PricingService(context),
        new BranchLeadTimeService(context, () => new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc)));

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateDailyBookingAsync(new CreateBookingRequest
    {
        RoomId = 10,
        BookingMode = BookingMode.Daily,
        CheckInDate = new DateOnly(2026, 6, 26),
        CheckOutDate = new DateOnly(2026, 6, 28),
        CustomerName = "Nhan",
        CustomerPhone = "0900000000",
        GuestCount = 2
    }));

    Assert.Contains("27/06/2026", ex.Message);
}
```

Add to `WebHomestay.Tests/Services/RoomBookingViewServiceTests.cs`:

```csharp
[Fact]
public async Task BuildAsync_DaysUnit_MarksDatesBeforeEarliestAllowedAsBlocked()
{
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(nameof(BuildAsync_DaysUnit_MarksDatesBeforeEarliestAllowedAsBlocked))
        .Options;
    await using var context = new ApplicationDbContext(options);

    context.Branches.Add(new Branch
    {
        Id = 1,
        Name = "Dong Nai",
        Address = "Bien Hoa",
        BookingLeadTimeValue = 7,
        BookingLeadTimeUnit = BranchLeadTimeUnit.Days
    });
    context.Rooms.Add(new Room
    {
        Id = 3,
        BranchId = 1,
        Name = "Room 3",
        Status = "Available",
        PricePerDay = 500000,
        PricePerHour = 100000
    });
    await context.SaveChangesAsync();

    var room = await context.Rooms.Include(r => r.Branch).SingleAsync(r => r.Id == 3);
    var service = new RoomBookingViewService(
        context,
        new AvailabilityService(context),
        new SettingService(context, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
        new PricingService(context),
        new BranchLeadTimeService(context, () => new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc)));

    var model = await service.BuildAsync(room, new DateOnly(2026, 6, 27));

    Assert.Equal("Blocked", model.DailyCalendar.Single(d => d.Date == new DateOnly(2026, 6, 26)).Status);
    Assert.Equal("Available", model.DailyCalendar.Single(d => d.Date == new DateOnly(2026, 6, 27)).Status);
}
```

- [ ] **Step 2: Run the targeted tests to verify failure**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BookingCreationServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~RoomBookingViewServiceTests
```

Expected: FAIL because constructors and enforcement logic are still hour-only.

- [ ] **Step 3: Implement shared enforcement in the booking view and booking creation services**

Update `WebHomestay/Services/BookingCreationService.cs` constructor:

```csharp
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
```

Add daily enforcement:

```csharp
var leadTimeRule = await _branchLeadTimeService.ResolveAsync(room.BranchId);
if (!leadTimeRule.AllowsDaily(request.CheckInDate!.Value))
{
    throw new InvalidOperationException(
        $"Chi nhánh này yêu cầu đặt trước tối thiểu {leadTimeRule.Value} ngày. Ngày gần nhất có thể đặt là {leadTimeRule.EarliestAllowedDailyDate:dd/MM/yyyy}.");
}
```

Add hourly enforcement:

```csharp
var leadTimeRule = await _branchLeadTimeService.ResolveAsync(inventory.Room.BranchId);
if (!leadTimeRule.AllowsHourly(inventory.StartTime))
{
    throw new InvalidOperationException(
        $"Chi nhánh này yêu cầu đặt trước tối thiểu {leadTimeRule.Value} giờ.");
}
```

Update `WebHomestay/Services/RoomBookingViewService.cs` to inject `IBranchLeadTimeService` and use:

```csharp
var leadTimeRule = await _branchLeadTimeService.ResolveAsync(room.BranchId);
```

For hourly slots:

```csharp
if (!leadTimeRule.AllowsHourly(slot.StartTime)) continue;
```

For daily calendar:

```csharp
var dateStatus = blockedDates.Contains(date) || !leadTimeRule.AllowsDaily(date)
    ? "Blocked"
    : "Available";
```

- [ ] **Step 4: Re-run the targeted tests**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BookingCreationServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~RoomBookingViewServiceTests
```

Expected: PASS with the new daily/hourly enforcement.

- [ ] **Step 5: Commit**

```powershell
git add WebHomestay/Services/BookingCreationService.cs WebHomestay/Services/RoomBookingViewService.cs WebHomestay.Tests/Services/BookingCreationServiceTests.cs WebHomestay.Tests/Services/RoomBookingViewServiceTests.cs
git commit -m "feat: enforce branch lead time in booking flows"
```

## Task 5: Apply Shared Lead Time To AI And Admin Quick Actions

**Files:**

- Modify: `WebHomestay/Services/ContextAwareBookingConductor.cs`
- Modify: `WebHomestay/Services/AdminChatQuickSendService.cs`
- Modify: `WebHomestay/Services/AI/AdminAIReseedService.cs`
- Test: `WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs`
- Test: `WebHomestay.Tests/Controllers/AdminChatMonitorControllerTests.cs`
- Test: `WebHomestay.Tests/Services/AdminAIReseedServiceTests.cs`

- [ ] **Step 1: Write failing AI gating tests**

Add to `WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs`:

```csharp
[Fact]
public async Task DailyBookingIntent_WhenDateIsBeforeEarliestAllowed_ReturnsAskInfoWithExplanation()
{
    using var context = CreateContext();
    var cache = CreateCache();
    context.Branches.Add(new Branch
    {
        Id = 1,
        Name = "LumiStay Riverside Quận 7",
        Address = "Q7",
        BookingLeadTimeValue = 7,
        BookingLeadTimeUnit = BranchLeadTimeUnit.Days
    });
    SeedRoom(context, id: 91, branchId: 1, capacity: 2, maxGuests: 3, extraGuestFee: 100000m);

    var conductor = CreateConductor(context, cache, configureServices: services =>
    {
        services.AddSingleton<IBranchLeadTimeService>(new BranchLeadTimeService(context, () => new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc)));
    });

    var request = MakeRequest("đặt phòng ngày 26/6", branchId: 1, startTime: new DateTime(2026, 6, 26), guestCount: 2, bookingMode: "daily");
    var result = await conductor.DecideAsync("leadtime-ai-1", request.Message, request, CancellationToken.None);

    Assert.Equal(ConductorAction.AskInfo, result.Action);
    Assert.Contains("27/06/2026", result.Answer ?? string.Empty);
}
```

Add to `WebHomestay.Tests/Services/AdminAIReseedServiceTests.cs` an assertion like:

```csharp
Assert.Contains("7 ngày", content);
```

- [ ] **Step 2: Run AI-related tests to verify failure**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~ContextAwareBookingConductorTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIReseedServiceTests
```

Expected: FAIL because the conductor and AI reseed still assume hour-only lead time.

- [ ] **Step 3: Refactor AI/admin services to use the shared rule**

In `WebHomestay/Services/ContextAwareBookingConductor.cs`:

```csharp
private readonly IBranchLeadTimeService _branchLeadTimeService;
```

Constructor injection:

```csharp
IBranchLeadTimeService branchLeadTimeService,
...
_branchLeadTimeService = branchLeadTimeService;
```

Replace private `ResolveLeadTimeCutoffAsync` hour-only logic with:

```csharp
var leadTimeRule = await _branchLeadTimeService.ResolveAsync(state.BranchId, cancellationToken);
```

Before showing rooms or accepting date ranges:

```csharp
if (string.Equals(state.BookingMode, "daily", StringComparison.OrdinalIgnoreCase)
    && state.CheckInDate.HasValue
    && !leadTimeRule.AllowsDaily(state.CheckInDate.Value))
{
    return new ConductorResult
    {
        Action = ConductorAction.AskInfo,
        State = container,
        Answer = $"Chi nhánh này yêu cầu đặt trước tối thiểu {leadTimeRule.Value} ngày. Ngày gần nhất có thể đặt là {leadTimeRule.EarliestAllowedDailyDate:dd/MM/yyyy}.",
        UiBlocks = await BuildMissingFieldBlocksAsync(state, studioConfig, cancellationToken)
    };
}
```

For slots:

```csharp
if (!leadTimeRule.AllowsHourly(slot.StartTime)) return false;
```

In `WebHomestay/Services/AdminChatQuickSendService.cs`, replace private hour-only cutoff helper with shared service usage and branch messages using the same string format.

In `WebHomestay/Services/AI/AdminAIReseedService.cs`, format branch lead time text with:

```csharp
var unitLabel = branch.BookingLeadTimeUnit == BranchLeadTimeUnit.Days ? "ngày" : "giờ";
```

- [ ] **Step 4: Re-run the AI/admin targeted tests**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~ContextAwareBookingConductorTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIReseedServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminChatMonitorControllerTests
```

Expected: PASS, with daily AI gating and hour/day copy working correctly.

- [ ] **Step 5: Commit**

```powershell
git add WebHomestay/Services/ContextAwareBookingConductor.cs WebHomestay/Services/AdminChatQuickSendService.cs WebHomestay/Services/AI/AdminAIReseedService.cs WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs WebHomestay.Tests/Services/AdminAIReseedServiceTests.cs WebHomestay.Tests/Controllers/AdminChatMonitorControllerTests.cs
git commit -m "feat: apply branch lead time to ai and admin helpers"
```

## Task 6: Add Branch Filter To Admin Images

**Files:**

- Modify: `WebHomestay/Controllers/AdminImagesController.cs`
- Modify: `WebHomestay/Views/AdminImages/Index.cshtml`
- Modify: `WebHomestay/Views/AdminImages/Trash.cshtml`
- Test: manual verification on `/chinhan/hethong/images`

- [ ] **Step 1: Define the controller contract**

Use these request shapes:

```http
GET /chinhan/hethong/images?search=090&branchId=5
GET /chinhan/hethong/images/trash?branchId=5
```

Behavior:

- `SuperAdmin` may choose any branch or all branches
- branch users remain limited to their session branch even if they tamper with query string

- [ ] **Step 2: Update `Index` and `Trash` query logic**

`WebHomestay/Controllers/AdminImagesController.cs`

```csharp
public async Task<IActionResult> Index(string? search, int? branchId)
{
    var role = HttpContext.Session.GetString("AdminRole");
    var sessionBranchId = HttpContext.Session.GetInt32("AdminBranchId");

    var allowedBranchId = role == "SuperAdmin"
        ? branchId
        : sessionBranchId;

    var query = _context.Bookings
        .Include(b => b.Room)
        .ThenInclude(r => r.Branch)
        .Where(b => !b.IsDeleted && (b.PaymentProofUrl != null || b.IdCardFrontPath != null));

    if (allowedBranchId.HasValue)
    {
        query = query.Where(b => b.Room.BranchId == allowedBranchId.Value);
    }

    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(b => b.CustomerName.Contains(search) || b.CustomerPhone.Contains(search) || b.Id.ToString().Contains(search));
    }

    ViewBag.SelectedBranchId = allowedBranchId;
    ViewBag.BranchOptions = role == "SuperAdmin"
        ? await _context.Branches.Where(b => !b.IsDeleted).OrderBy(b => b.Name).ToListAsync()
        : await _context.Branches.Where(b => sessionBranchId.HasValue && b.Id == sessionBranchId.Value).ToListAsync();

    return View(await query.OrderByDescending(b => b.CreatedAt).ToListAsync());
}
```

Mirror the same guard pattern in `Trash`.

- [ ] **Step 3: Render the branch dropdown in the view**

In `WebHomestay/Views/AdminImages/Index.cshtml` search box section:

```cshtml
@{
    var branchOptions = ViewBag.BranchOptions as List<WebHomestay.Models.Branch> ?? new();
    var selectedBranchId = ViewBag.SelectedBranchId as int?;
}

<form method="get" id="searchForm" class="d-grid gap-2">
    <div class="input-group">
        <span class="input-group-text bg-light border-0"><i class="fas fa-search text-muted"></i></span>
        <input type="text" name="search" class="form-control bg-light border-0 shadow-none"
               placeholder="Tên, SĐT, mã đơn..." value="@Context.Request.Query["search"]">
    </div>

    @if (branchOptions.Any())
    {
        <select name="branchId" class="form-select bg-light border-0 shadow-none" onchange="this.form.submit()">
            @if (Context.Session.GetString("AdminRole") == "SuperAdmin")
            {
                <option value="">Tất cả chi nhánh</option>
            }
            @foreach (var branch in branchOptions)
            {
                <option value="@branch.Id" selected="@(selectedBranchId == branch.Id)">@branch.Name</option>
            }
        </select>
    }
</form>
```

Replicate the same filter UI in `Trash` if the trash screen should remain consistent.

- [ ] **Step 4: Run the web app and verify both permission modes manually**

Run:

```powershell
dotnet run --project WebHomestay/WebHomestay.csproj
```

Verify:

- `SuperAdmin` sees `Tất cả chi nhánh` and filtering updates the list
- branch-scoped admin cannot query another branch by editing the URL
- search + branch filter work together
- if the trash page is updated too, it preserves the same behavior

- [ ] **Step 5: Commit**

```powershell
git add WebHomestay/Controllers/AdminImagesController.cs WebHomestay/Views/AdminImages/Index.cshtml WebHomestay/Views/AdminImages/Trash.cshtml
git commit -m "feat: add branch filters to admin images"
```

## Task 7: Update Import/Seed Text And Run Regression Suite

**Files:**

- Modify: `WebHomestay/Services/BulkImportService.cs`
- Modify: `WebHomestay/Services/AI/AdminAIReseedService.cs`
- Test: `WebHomestay.Tests/Services/BulkImportServiceTests.cs`
- Test: `WebHomestay.Tests/Services/AdminAIReseedServiceTests.cs`

- [ ] **Step 1: Extend existing import/reseed tests**

Add assertions like:

```csharp
Assert.Equal(BranchLeadTimeUnit.Hours, importedBranch.BookingLeadTimeUnit);
Assert.Equal(2, importedBranch.BookingLeadTimeValue);
```

And for reseed text:

```csharp
Assert.Contains("2 giờ", branchSummary);
Assert.Contains("7 ngày", dayBasedBranchSummary);
```

- [ ] **Step 2: Run the targeted tests to confirm the old assumptions fail**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BulkImportServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIReseedServiceTests
```

Expected: FAIL until import defaults and AI copy are updated.

- [ ] **Step 3: Update import defaults and AI wording**

In `WebHomestay/Services/BulkImportService.cs`:

```csharp
BookingLeadTimeValue = bookingLeadTime,
BookingLeadTimeUnit = BranchLeadTimeUnit.Hours,
```

In `WebHomestay/Services/AI/AdminAIReseedService.cs`:

```csharp
var leadTimeUnitLabel = branch.BookingLeadTimeUnit == BranchLeadTimeUnit.Days ? "ngày" : "giờ";
var leadTimeText = $"{branch.BookingLeadTimeValue} {leadTimeUnitLabel}";
```

- [ ] **Step 4: Re-run the import/reseed tests**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BulkImportServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIReseedServiceTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add WebHomestay/Services/BulkImportService.cs WebHomestay/Services/AI/AdminAIReseedService.cs WebHomestay.Tests/Services/BulkImportServiceTests.cs WebHomestay.Tests/Services/AdminAIReseedServiceTests.cs
git commit -m "chore: align import and ai copy with lead time units"
```

## Task 8: Full Verification And Cleanup

**Files:**

- Modify: only files from previous tasks if regressions appear
- Test: `WebHomestay.Tests/WebHomestay.Tests.csproj`

- [ ] **Step 1: Run the focused service/controller suites**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BranchLeadTimeServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~RoomBookingViewServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BookingCreationServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~ContextAwareBookingConductorTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminChatMonitorControllerTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BulkImportServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIReseedServiceTests
```

Expected: PASS.

- [ ] **Step 2: Run the full test suite**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj
```

Expected: PASS.

- [ ] **Step 3: Run an app build**

Run:

```powershell
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: PASS.

- [ ] **Step 4: Manual end-to-end verification**

Verify in browser:

- branch settings card saves `Giờ` and `Ngày`
- `7 ngày` blocks `19/06/2026` through `26/06/2026` and first allows `27/06/2026`
- hourly booking still blocks slots inside the configured hour buffer
- AI/public booking no longer suggests invalid daily dates
- admin images page filters by branch correctly and preserves permission boundaries

- [ ] **Step 5: Final commit**

```powershell
git add WebHomestay WebHomestay.Tests
git commit -m "feat: finish branch lead time and images filtering rollout"
```

## Self-Review

Spec coverage check:

- Branch lead time value/unit storage: covered by Tasks 1-3
- Shared backend rule and full-surface enforcement: covered by Tasks 1, 4, and 5
- Admin images branch dropdown: covered by Task 6
- Import/seed/AI wording alignment: covered by Task 7
- Testing of `19 -> 27` approved date rule: covered by Tasks 1, 4, 5, and 8

Placeholder scan:

- No `TODO`, `TBD`, or "handle appropriately" placeholders remain
- Every code-changing step includes concrete code or command examples

Type consistency check:

- Shared names are consistent: `BranchLeadTimeUnit`, `BranchLeadTimeRule`, `IBranchLeadTimeService`, `BranchLeadTimeService`, `BookingLeadTimeValue`, `BookingLeadTimeUnit`
- The same approved daily threshold rule `today + (N + 1)` is used in all relevant tasks
