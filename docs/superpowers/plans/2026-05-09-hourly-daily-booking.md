# Hourly + Daily Booking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Xây hệ thống đặt phòng hỗ trợ đồng thời `theo giờ` và `theo ngày`, với 2 mode UI riêng nhưng dùng chung một engine chống trùng lịch.

**Architecture:** Giữ `Booking` làm nguồn sự thật cho mọi khoảng chiếm phòng thực tế. `Theo ngày` tiếp tục dùng interval `14:00 -> 12:00`, còn `theo giờ` dùng `RoomSlotInventory` được sinh từ template và override riêng từng phòng. Toàn bộ validate availability đi qua một lớp rule chung để khóa chéo giữa booking ngày và slot giờ.

**Tech Stack:** ASP.NET Core MVC, Entity Framework Core + Npgsql, xUnit, EF Core InMemory, Bootstrap, vanilla JavaScript.

---

## File Map

- `WebHomestay/Models/Booking.cs`
  Thêm `BookingMode`, liên kết tới slot giờ, và metadata hiển thị booking giờ.
- `WebHomestay/Models/BookingMode.cs`
  Enum tách rõ `Daily` và `Hourly`.
- `WebHomestay/Models/RoomSlotTemplate.cs`
  Định nghĩa combo gốc và rule sinh slot.
- `WebHomestay/Models/RoomSlotTemplateAssignment.cs`
  Gán template vào phòng theo hiệu lực thời gian.
- `WebHomestay/Models/RoomSlotInventory.cs`
  Lưu slot thực tế của từng phòng theo từng ngày.
- `WebHomestay/Models/RoomSlotOverride.cs`
  Lưu ngoại lệ block, tắt combo, hoặc đổi slot riêng cho phòng.
- `WebHomestay/Models/Room.cs`
  Bổ sung navigation tới assignment, inventory, override.
- `WebHomestay/Data/ApplicationDbContext.cs`
  Đăng ký DbSet, mapping, quan hệ cho slot system.
- `WebHomestay/Data/init_db.sql`
  Cập nhật script bootstrap để khởi tạo schema mới khi dựng DB tay.
- `WebHomestay/Services/BookingTimeRules.cs`
  Chứa rule chuẩn `14:00 -> 12:00`, overlap, và helper interval.
- `WebHomestay/Services/IAvailabilityService.cs`
  Mở rộng surface API cho check chéo giữa booking ngày và slot giờ.
- `WebHomestay/Services/AvailabilityService.cs`
  Refactor sang engine overlap thống nhất.
- `WebHomestay/Services/ISlotGenerationService.cs`
  API sinh slot từ template.
- `WebHomestay/Services/SlotGenerationService.cs`
  Sinh `2h/4h/6h/ban ngày/ban đêm` với cleanup đúng rule.
- `WebHomestay/Services/IRoomBookingViewService.cs`
  Dựng dữ liệu UI cho 2 mode trên màn chi tiết phòng.
- `WebHomestay/Services/RoomBookingViewService.cs`
  Gom slot theo combo, build timeline, build daily calendar đỏ/xanh.
- `WebHomestay/Services/IBookingCreationService.cs`
  Tạo booking giờ/ngày và cập nhật inventory atomically.
- `WebHomestay/Services/BookingCreationService.cs`
  Đóng gói create flow để controller không ôm nghiệp vụ.
- `WebHomestay/Services/IRoomScheduleAdminService.cs`
  Xử lý áp template hàng loạt và override riêng phòng.
- `WebHomestay/Services/RoomScheduleAdminService.cs`
  Sinh inventory theo phòng/ngày, block slot, resync inventory.
- `WebHomestay/Models/ViewModels/RoomDetailsViewModel.cs`
  View model cho trang chi tiết phòng + booking widget 2 mode.
- `WebHomestay/Models/ViewModels/BookingCheckoutViewModel.cs`
  View model cho bước xác nhận booking giờ/ngày.
- `WebHomestay/Models/ViewModels/CreateBookingRequest.cs`
  Input model thay cho việc bind trực tiếp `Booking`.
- `WebHomestay/Models/ViewModels/AdminRoomScheduleViewModel.cs`
  View model cho admin template/apply/calendar.
- `WebHomestay/Controllers/RoomsController.cs`
  Tải màn chi tiết phòng với dữ liệu booking 2 mode.
- `WebHomestay/Controllers/BookingsController.cs`
  Checkout giờ/ngày, upload giấy tờ, tạo booking mới.
- `WebHomestay/Controllers/AdminRoomSchedulesController.cs`
  Màn admin template, apply, override, inventory calendar.
- `WebHomestay/Controllers/AdminMatrixController.cs`
  Đồng bộ matrix admin với `BookingMode` và slot-aware occupancy.
- `WebHomestay/Controllers/AdminBookingsController.cs`
  Hiển thị mode booking, slot label, thời gian thực theo mode.
- `WebHomestay/Views/Rooms/Details.cshtml`
  Thay form ngày cũ bằng không gian đặt phòng 2 mode.
- `WebHomestay/Views/Bookings/Checkout.cshtml`
  Tóm tắt theo mode, xác nhận đúng slot/check-in/out.
- `WebHomestay/Views/Bookings/Success.cshtml`
  Hiển thị kết quả theo booking giờ hoặc ngày.
- `WebHomestay/Views/AdminRoomSchedules/Templates.cshtml`
  CRUD template combo giờ.
- `WebHomestay/Views/AdminRoomSchedules/Apply.cshtml`
  Gán template cho nhiều phòng.
- `WebHomestay/Views/AdminRoomSchedules/Calendar.cshtml`
  Xem inventory thực tế, block/unblock/override slot.
- `WebHomestay/Views/AdminMatrix/Index.cshtml`
  Thêm hiển thị occupancy-aware cho booking giờ/ngày.
- `WebHomestay/Views/AdminBookings/Index.cshtml`
  Hiển thị badge mode, slot label, khoảng giờ hoặc ngày.
- `WebHomestay/Views/AdminBookings/Details.cshtml`
  Tóm tắt booking theo đúng mode.
- `WebHomestay/Views/Shared/_Layout.cshtml`
  Thêm điều hướng tới màn admin room schedules.
- `WebHomestay/wwwroot/js/room-booking.js`
  Toggle mode, load ngày, sort theo combo/timeline.
- `WebHomestay/wwwroot/css/site.css`
  Styling cho booking space và calendar đỏ/xanh.
- `WebHomestay/Program.cs`
  Đăng ký service mới.
- `WebHomestay.Tests/WebHomestay.Tests.csproj`
  Test project riêng cho domain/service/controller flow.
- `WebHomestay.Tests/Domain/BookingTimeRulesTests.cs`
  Test rule thời gian `14:00 -> 12:00` và overlap boundary.
- `WebHomestay.Tests/Services/RoomSlotSchemaTests.cs`
  Test persist schema slot.
- `WebHomestay.Tests/Services/SlotGenerationServiceTests.cs`
  Test sinh slot rolling/fixed.
- `WebHomestay.Tests/Services/AvailabilityServiceTests.cs`
  Test khóa chéo ngày/giờ.
- `WebHomestay.Tests/Services/RoomBookingViewServiceTests.cs`
  Test grouping slot và build daily calendar.
- `WebHomestay.Tests/Services/BookingCreationServiceTests.cs`
  Test create booking giờ/ngày.
- `WebHomestay.Tests/Services/RoomScheduleAdminServiceTests.cs`
  Test apply template và seed inventory.
- `WebHomestay.Tests/Services/AdminBookingProjectionTests.cs`
  Test projection dữ liệu admin cho booking mode.

### Task 1: Bootstrap test harness và rule thời gian cốt lõi

**Files:**
- Create: `WebHomestay.Tests/WebHomestay.Tests.csproj`
- Create: `WebHomestay.Tests/Domain/BookingTimeRulesTests.cs`
- Create: `WebHomestay/Models/BookingMode.cs`
- Create: `WebHomestay/Services/BookingTimeRules.cs`

- [ ] **Step 1: Tạo test project và viết test fail cho rule `14:00 -> 12:00` cùng overlap**

```xml
<!-- WebHomestay.Tests/WebHomestay.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\\WebHomestay\\WebHomestay.csproj" />
  </ItemGroup>
</Project>
```

```csharp
// WebHomestay.Tests/Domain/BookingTimeRulesTests.cs
using WebHomestay.Services;

namespace WebHomestay.Tests.Domain;

public class BookingTimeRulesTests
{
    [Fact]
    public void BuildDailyStay_UsesHotelCheckInAndCheckOutTimes()
    {
        var interval = BookingTimeRules.BuildDailyStay(
            new DateOnly(2026, 5, 9),
            new DateOnly(2026, 5, 11));

        Assert.Equal(new DateTime(2026, 5, 9, 14, 0, 0), interval.Start);
        Assert.Equal(new DateTime(2026, 5, 11, 12, 0, 0), interval.End);
    }

    [Fact]
    public void Overlaps_ReturnsTrue_WhenHourlySlotFallsInsideDailyStay()
    {
        var daily = BookingTimeRules.BuildDailyStay(
            new DateOnly(2026, 5, 9),
            new DateOnly(2026, 5, 11));
        var hourlyStart = new DateTime(2026, 5, 10, 9, 30, 0);
        var hourlyEnd = new DateTime(2026, 5, 10, 11, 30, 0);

        var overlaps = BookingTimeRules.Overlaps(
            daily.Start,
            daily.End,
            hourlyStart,
            hourlyEnd);

        Assert.True(overlaps);
    }

    [Fact]
    public void Overlaps_ReturnsFalse_WhenIntervalsOnlyTouchAtBoundary()
    {
        var leftEnd = new DateTime(2026, 5, 10, 12, 0, 0);
        var rightStart = new DateTime(2026, 5, 10, 12, 0, 0);

        var overlaps = BookingTimeRules.Overlaps(
            new DateTime(2026, 5, 10, 9, 0, 0),
            leftEnd,
            rightStart,
            new DateTime(2026, 5, 10, 14, 0, 0));

        Assert.False(overlaps);
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận đang fail do thiếu code**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BookingTimeRulesTests -v minimal`

Expected: FAIL với lỗi kiểu `The type or namespace name 'BookingTimeRules' could not be found`.

- [ ] **Step 3: Viết implementation tối thiểu cho enum mode và helper rule**

```csharp
// WebHomestay/Models/BookingMode.cs
namespace WebHomestay.Models;

public enum BookingMode
{
    Daily = 1,
    Hourly = 2
}
```

```csharp
// WebHomestay/Services/BookingTimeRules.cs
namespace WebHomestay.Services;

public readonly record struct BookingInterval(DateTime Start, DateTime End);

public static class BookingTimeRules
{
    public static readonly TimeOnly DailyCheckIn = new(14, 0);
    public static readonly TimeOnly DailyCheckOut = new(12, 0);

    public static BookingInterval BuildDailyStay(DateOnly checkInDate, DateOnly checkOutDate)
    {
        var start = checkInDate.ToDateTime(DailyCheckIn);
        var end = checkOutDate.ToDateTime(DailyCheckOut);
        return new BookingInterval(start, end);
    }

    public static bool Overlaps(DateTime existingStart, DateTime existingEnd, DateTime candidateStart, DateTime candidateEnd)
    {
        return existingStart < candidateEnd && existingEnd > candidateStart;
    }
}
```

- [ ] **Step 4: Chạy lại test rule và xác nhận pass**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BookingTimeRulesTests -v minimal`

Expected: PASS với `Total tests: 3. Passed: 3. Failed: 0.`

- [ ] **Step 5: Commit**

```bash
git add WebHomestay.Tests/WebHomestay.Tests.csproj WebHomestay.Tests/Domain/BookingTimeRulesTests.cs WebHomestay/Models/BookingMode.cs WebHomestay/Services/BookingTimeRules.cs
git commit -m "test: add booking time rules coverage"
```

### Task 2: Mở rộng schema domain cho slot inventory và booking mode

**Files:**
- Create: `WebHomestay.Tests/Services/RoomSlotSchemaTests.cs`
- Create: `WebHomestay/Models/RoomSlotTemplate.cs`
- Create: `WebHomestay/Models/RoomSlotTemplateAssignment.cs`
- Create: `WebHomestay/Models/RoomSlotInventory.cs`
- Create: `WebHomestay/Models/RoomSlotOverride.cs`
- Modify: `WebHomestay/Models/Booking.cs`
- Modify: `WebHomestay/Models/Room.cs`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`
- Modify: `WebHomestay/Data/init_db.sql`
- Create: `WebHomestay/Migrations/20260509113000_AddHourlyDailyBookingModel.cs`

- [ ] **Step 1: Viết test fail cho việc persist template, inventory, và hourly booking**

```csharp
// WebHomestay.Tests/Services/RoomSlotSchemaTests.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Tests.Services;

public class RoomSlotSchemaTests
{
    [Fact]
    public async Task CanPersistTemplateInventoryAndHourlyBooking()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(CanPersistTemplateInventoryAndHourlyBooking))
            .Options;

        await using var context = new ApplicationDbContext(options);

        var room = new Room { Id = 100, Name = "THIEN YET 1", BranchId = 1 };
        var template = new RoomSlotTemplate
        {
            Name = "Combo 2h",
            Code = "2H",
            DurationMinutes = 120,
            CleanupMinutes = 30,
            SeedStartTime = new TimeOnly(0, 0)
        };
        var inventory = new RoomSlotInventory
        {
            Room = room,
            Template = template,
            SlotDate = new DateOnly(2026, 5, 10),
            SlotLabel = "09:30-11:30",
            StartTime = new DateTime(2026, 5, 10, 9, 30, 0),
            EndTime = new DateTime(2026, 5, 10, 11, 30, 0),
            Status = "Available"
        };
        var booking = new Booking
        {
            Room = room,
            CustomerName = "Nhan",
            CustomerPhone = "0900000000",
            StartTime = inventory.StartTime,
            EndTime = inventory.EndTime,
            BookingMode = BookingMode.Hourly,
            SlotLabel = inventory.SlotLabel
        };

        context.AddRange(room, template, inventory, booking);
        await context.SaveChangesAsync();

        Assert.Single(context.Set<RoomSlotTemplate>());
        Assert.Single(context.Set<RoomSlotInventory>());
        Assert.Equal(BookingMode.Hourly, context.Bookings.Single().BookingMode);
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận fail do model/dbset chưa tồn tại**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~RoomSlotSchemaTests -v minimal`

Expected: FAIL với lỗi thiếu `RoomSlotTemplate`, `RoomSlotInventory`, hoặc property `BookingMode`.

- [ ] **Step 3: Bổ sung model, navigation, mapping, và schema bootstrap**

```csharp
// WebHomestay/Models/Booking.cs (đoạn thêm mới)
public BookingMode BookingMode { get; set; } = BookingMode.Daily;
public int? RoomSlotInventoryId { get; set; }
public virtual RoomSlotInventory? RoomSlotInventory { get; set; }
[StringLength(100)]
public string? SlotLabel { get; set; }
```

```csharp
// WebHomestay/Models/RoomSlotTemplate.cs
namespace WebHomestay.Models;

public class RoomSlotTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int CleanupMinutes { get; set; }
    public TimeOnly? SeedStartTime { get; set; }
    public TimeOnly? FixedStartTime { get; set; }
    public TimeOnly? FixedEndTime { get; set; }
    public bool CrossesMidnight { get; set; }
    public bool IsActive { get; set; } = true;
}
```

```csharp
// WebHomestay/Models/RoomSlotInventory.cs
namespace WebHomestay.Models;

public class RoomSlotInventory
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public virtual Room Room { get; set; } = null!;
    public int TemplateId { get; set; }
    public virtual RoomSlotTemplate Template { get; set; } = null!;
    public DateOnly SlotDate { get; set; }
    public string SlotLabel { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = "Available";
    public int? BookingId { get; set; }
}
```

```csharp
// WebHomestay/Models/RoomSlotTemplateAssignment.cs
namespace WebHomestay.Models;

public class RoomSlotTemplateAssignment
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public virtual Room Room { get; set; } = null!;
    public int TemplateId { get; set; }
    public virtual RoomSlotTemplate Template { get; set; } = null!;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}
```

```csharp
// WebHomestay/Models/RoomSlotOverride.cs
namespace WebHomestay.Models;

public class RoomSlotOverride
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public DateOnly TargetDate { get; set; }
    public int? TemplateId { get; set; }
    public int? InventoryId { get; set; }
    public string OverrideType { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
```

```csharp
// WebHomestay/Data/ApplicationDbContext.cs (đoạn thêm mới)
public DbSet<RoomSlotTemplate> RoomSlotTemplates { get; set; }
public DbSet<RoomSlotTemplateAssignment> RoomSlotTemplateAssignments { get; set; }
public DbSet<RoomSlotInventory> RoomSlotInventories { get; set; }
public DbSet<RoomSlotOverride> RoomSlotOverrides { get; set; }
```

```sql
-- WebHomestay/Data/init_db.sql (đoạn thêm mới)
ALTER TABLE bookings
    ADD COLUMN IF NOT EXISTS booking_mode INT DEFAULT 1,
    ADD COLUMN IF NOT EXISTS room_slot_inventory_id INT NULL,
    ADD COLUMN IF NOT EXISTS slot_label VARCHAR(100);

CREATE TABLE IF NOT EXISTS room_slot_templates (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    code VARCHAR(50) NOT NULL,
    duration_minutes INT NOT NULL,
    cleanup_minutes INT NOT NULL,
    seed_start_time TIME NULL,
    fixed_start_time TIME NULL,
    fixed_end_time TIME NULL,
    crosses_midnight BOOLEAN DEFAULT FALSE,
    is_active BOOLEAN DEFAULT TRUE
);
```

- [ ] **Step 4: Tạo migration và chạy lại test schema**

Run: `dotnet ef migrations add AddHourlyDailyBookingModel --project WebHomestay/WebHomestay.csproj`

Expected: SUCCESS với output chứa `Done. To undo this action, use 'ef migrations remove'`.

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~RoomSlotSchemaTests -v minimal`

Expected: PASS với `Passed: 1`.

- [ ] **Step 5: Commit**

```bash
git add WebHomestay/Models/Booking.cs WebHomestay/Models/Room.cs WebHomestay/Models/RoomSlotTemplate.cs WebHomestay/Models/RoomSlotTemplateAssignment.cs WebHomestay/Models/RoomSlotInventory.cs WebHomestay/Models/RoomSlotOverride.cs WebHomestay/Data/ApplicationDbContext.cs WebHomestay/Data/init_db.sql WebHomestay/Migrations WebHomestay.Tests/Services/RoomSlotSchemaTests.cs
git commit -m "feat: add hourly booking schema"
```

### Task 3: Refactor availability engine và sinh slot giờ

**Files:**
- Create: `WebHomestay.Tests/Services/SlotGenerationServiceTests.cs`
- Create: `WebHomestay.Tests/Services/AvailabilityServiceTests.cs`
- Create: `WebHomestay/Services/ISlotGenerationService.cs`
- Create: `WebHomestay/Services/SlotGenerationService.cs`
- Modify: `WebHomestay/Services/IAvailabilityService.cs`
- Modify: `WebHomestay/Services/AvailabilityService.cs`
- Modify: `WebHomestay/Program.cs`

- [ ] **Step 1: Viết test fail cho việc sinh slot 2h có cleanup và khóa chéo với booking ngày**

```csharp
// WebHomestay.Tests/Services/SlotGenerationServiceTests.cs
using WebHomestay.Models;
using WebHomestay.Services;

namespace WebHomestay.Tests.Services;

public class SlotGenerationServiceTests
{
    [Fact]
    public void GenerateRollingSlots_CreatesThirtyMinuteCleanupGap()
    {
        var template = new RoomSlotTemplate
        {
            Name = "Combo 2h",
            Code = "2H",
            DurationMinutes = 120,
            CleanupMinutes = 30,
            SeedStartTime = new TimeOnly(0, 0)
        };
        var service = new SlotGenerationService();

        var slots = service.GenerateRollingSlots(10, template, new DateOnly(2026, 5, 10));

        Assert.Equal(new DateTime(2026, 5, 10, 0, 0, 0), slots[0].StartTime);
        Assert.Equal(new DateTime(2026, 5, 10, 2, 0, 0), slots[0].EndTime);
        Assert.Equal(new DateTime(2026, 5, 10, 2, 30, 0), slots[1].StartTime);
    }
}
```

```csharp
// WebHomestay.Tests/Services/AvailabilityServiceTests.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;

namespace WebHomestay.Tests.Services;

public class AvailabilityServiceTests
{
    [Fact]
    public async Task IsRoomAvailable_ReturnsFalse_WhenDailyStayCoversExistingHourlyBooking()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(IsRoomAvailable_ReturnsFalse_WhenDailyStayCoversExistingHourlyBooking))
            .Options;

        await using var context = new ApplicationDbContext(options);
        context.Bookings.Add(new Booking
        {
            RoomId = 5,
            CustomerName = "Old guest",
            CustomerPhone = "0900000001",
            BookingMode = BookingMode.Hourly,
            StartTime = new DateTime(2026, 5, 10, 9, 30, 0),
            EndTime = new DateTime(2026, 5, 10, 11, 30, 0),
            Status = "Confirmed"
        });
        await context.SaveChangesAsync();

        var service = new AvailabilityService(context);
        var requested = BookingTimeRules.BuildDailyStay(
            new DateOnly(2026, 5, 9),
            new DateOnly(2026, 5, 11));

        var isAvailable = await service.IsRoomAvailable(5, requested.Start, requested.End);

        Assert.False(isAvailable);
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~SlotGenerationServiceTests -v minimal`

Expected: FAIL với lỗi thiếu `SlotGenerationService` hoặc method `GenerateRollingSlots`.

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AvailabilityServiceTests -v minimal`

Expected: FAIL nếu availability hiện tại chưa xét slot-aware overlap theo rule mới.

- [ ] **Step 3: Implement slot generator và refactor availability**

```csharp
// WebHomestay/Services/ISlotGenerationService.cs
using WebHomestay.Models;

namespace WebHomestay.Services;

public interface ISlotGenerationService
{
    List<RoomSlotInventory> GenerateRollingSlots(int roomId, RoomSlotTemplate template, DateOnly slotDate);
    RoomSlotInventory GenerateFixedSlot(int roomId, RoomSlotTemplate template, DateOnly slotDate);
}
```

```csharp
// WebHomestay/Services/SlotGenerationService.cs
using WebHomestay.Models;

namespace WebHomestay.Services;

public class SlotGenerationService : ISlotGenerationService
{
    public List<RoomSlotInventory> GenerateRollingSlots(int roomId, RoomSlotTemplate template, DateOnly slotDate)
    {
        var slots = new List<RoomSlotInventory>();
        var pointer = template.SeedStartTime ?? new TimeOnly(0, 0);
        var cutoff = new TimeOnly(23, 59);

        while (pointer.AddMinutes(template.DurationMinutes) <= cutoff)
        {
            var start = slotDate.ToDateTime(pointer);
            var end = start.AddMinutes(template.DurationMinutes);
            slots.Add(new RoomSlotInventory
            {
                RoomId = roomId,
                TemplateId = template.Id,
                SlotDate = slotDate,
                SlotLabel = $"{start:HH:mm}-{end:HH:mm}",
                StartTime = start,
                EndTime = end,
                Status = "Available"
            });

            pointer = pointer.AddMinutes(template.DurationMinutes + template.CleanupMinutes);
        }

        return slots;
    }

    public RoomSlotInventory GenerateFixedSlot(int roomId, RoomSlotTemplate template, DateOnly slotDate)
    {
        var start = slotDate.ToDateTime(template.FixedStartTime ?? new TimeOnly(0, 0));
        var endDate = template.CrossesMidnight ? slotDate.AddDays(1) : slotDate;
        var end = endDate.ToDateTime(template.FixedEndTime ?? new TimeOnly(0, 0));

        return new RoomSlotInventory
        {
            RoomId = roomId,
            TemplateId = template.Id,
            SlotDate = slotDate,
            SlotLabel = $"{start:HH:mm}-{end:HH:mm}",
            StartTime = start,
            EndTime = end,
            Status = "Available"
        };
    }
}
```

```csharp
// WebHomestay/Services/IAvailabilityService.cs
namespace WebHomestay.Services;

public interface IAvailabilityService
{
    Task<bool> IsRoomAvailable(int roomId, DateTime start, DateTime end, int? ignoreBookingId = null);
    Task<List<int>> GetAvailableRoomIds(int branchId, DateTime start, DateTime end);
    Task<List<DateOnly>> GetBlockedDatesAsync(int roomId, DateOnly visibleFrom, int visibleDays);
}
```

```csharp
// WebHomestay/Program.cs (đoạn DI)
builder.Services.AddScoped<WebHomestay.Services.IAvailabilityService, WebHomestay.Services.AvailabilityService>();
builder.Services.AddScoped<WebHomestay.Services.ISlotGenerationService, WebHomestay.Services.SlotGenerationService>();
```

- [ ] **Step 4: Chạy lại test service**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~SlotGenerationServiceTests -v minimal`

Expected: PASS.

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AvailabilityServiceTests -v minimal`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add WebHomestay/Services/IAvailabilityService.cs WebHomestay/Services/AvailabilityService.cs WebHomestay/Services/ISlotGenerationService.cs WebHomestay/Services/SlotGenerationService.cs WebHomestay/Program.cs WebHomestay.Tests/Services/SlotGenerationServiceTests.cs WebHomestay.Tests/Services/AvailabilityServiceTests.cs
git commit -m "feat: add slot generation and unified overlap engine"
```

### Task 4: Xây booking space cho khách trên trang phòng

**Files:**
- Create: `WebHomestay.Tests/Services/RoomBookingViewServiceTests.cs`
- Create: `WebHomestay/Models/ViewModels/RoomDetailsViewModel.cs`
- Create: `WebHomestay/Services/IRoomBookingViewService.cs`
- Create: `WebHomestay/Services/RoomBookingViewService.cs`
- Modify: `WebHomestay/Controllers/RoomsController.cs`
- Modify: `WebHomestay/Views/Rooms/Details.cshtml`
- Modify: `WebHomestay/wwwroot/js/room-booking.js`
- Modify: `WebHomestay/wwwroot/css/site.css`
- Modify: `WebHomestay/Program.cs`

- [ ] **Step 1: Viết test fail cho việc build hourly groups và daily calendar đỏ/xanh**

```csharp
// WebHomestay.Tests/Services/RoomBookingViewServiceTests.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;

namespace WebHomestay.Tests.Services;

public class RoomBookingViewServiceTests
{
    [Fact]
    public void BuildHourlyGroups_GroupsSlotsByTemplateName()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(BuildHourlyGroups_GroupsSlotsByTemplateName))
            .Options;
        using var context = new ApplicationDbContext(options);
        var service = new RoomBookingViewService(context, new AvailabilityService(context));
        var slots = new List<RoomSlotInventory>
        {
            new() { SlotLabel = "09:30-11:30", Status = "Available", Template = new RoomSlotTemplate { Name = "Combo 2h" } },
            new() { SlotLabel = "13:30-15:30", Status = "Booked", Template = new RoomSlotTemplate { Name = "Combo 2h" } },
            new() { SlotLabel = "10:00-19:00", Status = "Available", Template = new RoomSlotTemplate { Name = "Ban ngay" } }
        };

        var groups = service.BuildHourlyGroups(slots);

        Assert.Equal(2, groups.Count);
        Assert.Equal("Combo 2h", groups[0].Title);
        Assert.Equal(2, groups[0].Slots.Count);
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~RoomBookingViewServiceTests -v minimal`

Expected: FAIL với lỗi thiếu `RoomBookingViewService` hoặc view model group.

- [ ] **Step 3: Implement view service, controller, và UI 2 mode**

```csharp
// WebHomestay/Models/ViewModels/RoomDetailsViewModel.cs
using WebHomestay.Models;

namespace WebHomestay.Models.ViewModels;

public class RoomDetailsViewModel
{
    public Room Room { get; set; } = null!;
    public DateOnly SelectedHourlyDate { get; set; }
    public List<HourlySlotGroupViewModel> HourlyGroups { get; set; } = new();
    public List<CalendarDayViewModel> DailyCalendar { get; set; } = new();
}

public class HourlySlotGroupViewModel
{
    public string Title { get; set; } = string.Empty;
    public List<HourlySlotCardViewModel> Slots { get; set; } = new();
}

public class HourlySlotCardViewModel
{
    public int SlotId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class CalendarDayViewModel
{
    public DateOnly Date { get; set; }
    public string Status { get; set; } = string.Empty;
}
```

```csharp
// WebHomestay/Services/IRoomBookingViewService.cs
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services;

public interface IRoomBookingViewService
{
    Task<RoomDetailsViewModel> BuildAsync(Room room, DateOnly selectedHourlyDate);
    List<HourlySlotGroupViewModel> BuildHourlyGroups(IEnumerable<RoomSlotInventory> slots);
}
```

```csharp
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

    public RoomBookingViewService(ApplicationDbContext context, IAvailabilityService availabilityService)
    {
        _context = context;
        _availabilityService = availabilityService;
    }

    public async Task<RoomDetailsViewModel> BuildAsync(Room room, DateOnly selectedHourlyDate)
    {
        var hourlySlots = await _context.RoomSlotInventories
            .Include(slot => slot.Template)
            .Where(slot => slot.RoomId == room.Id && slot.SlotDate == selectedHourlyDate)
            .OrderBy(slot => slot.StartTime)
            .ToListAsync();

        var blockedDates = await _availabilityService.GetBlockedDatesAsync(room.Id, DateOnly.FromDateTime(DateTime.Today), 30);

        return new RoomDetailsViewModel
        {
            Room = room,
            SelectedHourlyDate = selectedHourlyDate,
            HourlyGroups = BuildHourlyGroups(hourlySlots),
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

    public List<HourlySlotGroupViewModel> BuildHourlyGroups(IEnumerable<RoomSlotInventory> slots)
    {
        return slots
            .GroupBy(slot => slot.Template.Name)
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
}
```

```csharp
// WebHomestay/Controllers/RoomsController.cs (Details)
public async Task<IActionResult> Details(int? id, DateOnly? hourlyDate)
{
    if (id == null) return NotFound();

    var room = await _context.Rooms
        .Include(r => r.Branch)
        .Include(r => r.Amenities)
        .FirstOrDefaultAsync(m => m.Id == id);

    if (room == null) return NotFound();

    var selectedDate = hourlyDate ?? DateOnly.FromDateTime(DateTime.Today);
    var model = await _roomBookingViewService.BuildAsync(room, selectedDate);
    return View(model);
}
```

```csharp
// WebHomestay/Program.cs (đăng ký view service)
builder.Services.AddScoped<WebHomestay.Services.IRoomBookingViewService, WebHomestay.Services.RoomBookingViewService>();
```

```html
<!-- WebHomestay/Views/Rooms/Details.cshtml (booking space) -->
<div class="booking-space" data-room-id="@Model.Room.Id">
    <div class="booking-mode-switch">
        <button type="button" class="mode-btn active" data-mode="hourly">Theo giờ</button>
        <button type="button" class="mode-btn" data-mode="daily">Theo ngày</button>
    </div>

    <section class="booking-mode-panel" data-panel="hourly">
        <div class="hourly-date-strip">
            @foreach (var day in Model.DailyCalendar.Take(7))
            {
                <a class="date-chip @(day.Date == Model.SelectedHourlyDate ? "active" : "")"
                   asp-route-hourlyDate="@day.Date.ToString("yyyy-MM-dd")">@day.Date.ToString("dd-MM")</a>
            }
        </div>
        @foreach (var group in Model.HourlyGroups)
        {
            <div class="combo-group">
                <h5>@group.Title</h5>
                <div class="slot-grid">
                    @foreach (var slot in group.Slots)
                    {
                        <a asp-controller="Bookings" asp-action="CheckoutHourly" asp-route-roomId="@Model.Room.Id" asp-route-slotId="@slot.SlotId" class="slot-card slot-@slot.Status.ToLowerInvariant()">@slot.Label</a>
                    }
                </div>
            </div>
        }
    </section>

    <section class="booking-mode-panel d-none" data-panel="daily">
        <div class="daily-rule">Check-in 14:00 / Check-out 12:00</div>
        <div id="daily-calendar" class="daily-calendar"></div>
    </section>
</div>
```

- [ ] **Step 4: Chạy test view service và build toàn app**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~RoomBookingViewServiceTests -v minimal`

Expected: PASS.

Run: `dotnet build WebHomestay/WebHomestay.csproj`

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add WebHomestay/Models/ViewModels/RoomDetailsViewModel.cs WebHomestay/Services/IRoomBookingViewService.cs WebHomestay/Services/RoomBookingViewService.cs WebHomestay/Controllers/RoomsController.cs WebHomestay/Views/Rooms/Details.cshtml WebHomestay/wwwroot/js/room-booking.js WebHomestay/wwwroot/css/site.css WebHomestay/Program.cs WebHomestay.Tests/Services/RoomBookingViewServiceTests.cs
git commit -m "feat: add dual-mode booking UI"
```

### Task 5: Tạo checkout flow và booking creation service cho giờ/ngày

**Files:**
- Create: `WebHomestay.Tests/Services/BookingCreationServiceTests.cs`
- Create: `WebHomestay/Models/ViewModels/BookingCheckoutViewModel.cs`
- Create: `WebHomestay/Models/ViewModels/CreateBookingRequest.cs`
- Create: `WebHomestay/Services/IBookingCreationService.cs`
- Create: `WebHomestay/Services/BookingCreationService.cs`
- Modify: `WebHomestay/Services/IRoomBookingViewService.cs`
- Modify: `WebHomestay/Services/RoomBookingViewService.cs`
- Modify: `WebHomestay/Controllers/BookingsController.cs`
- Modify: `WebHomestay/Views/Bookings/Checkout.cshtml`
- Modify: `WebHomestay/Views/Bookings/Success.cshtml`
- Modify: `WebHomestay/Program.cs`

- [ ] **Step 1: Viết test fail cho create booking giờ và chặn booking ngày bị vướng slot giờ**

```csharp
// WebHomestay.Tests/Services/BookingCreationServiceTests.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;
using WebHomestay.Services;

namespace WebHomestay.Tests.Services;

public class BookingCreationServiceTests
{
    [Fact]
    public async Task CreateHourlyBookingAsync_ReservesInventoryAndPersistsBooking()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(CreateHourlyBookingAsync_ReservesInventoryAndPersistsBooking))
            .Options;
        await using var context = new ApplicationDbContext(options);

        context.RoomSlotInventories.Add(new RoomSlotInventory
        {
            Id = 77,
            RoomId = 5,
            TemplateId = 1,
            SlotLabel = "09:30-11:30",
            StartTime = new DateTime(2026, 5, 10, 9, 30, 0),
            EndTime = new DateTime(2026, 5, 10, 11, 30, 0),
            Status = "Available"
        });
        await context.SaveChangesAsync();

        var service = new BookingCreationService(context, new AvailabilityService(context));
        var request = new CreateBookingRequest
        {
            RoomId = 5,
            SlotInventoryId = 77,
            BookingMode = BookingMode.Hourly,
            CustomerName = "Nhan",
            CustomerPhone = "0900000000"
        };

        var booking = await service.CreateHourlyBookingAsync(request);

        Assert.Equal(BookingMode.Hourly, booking.BookingMode);
        Assert.Equal("Booked", context.RoomSlotInventories.Single().Status);
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BookingCreationServiceTests -v minimal`

Expected: FAIL với lỗi thiếu `BookingCreationService` hoặc `CreateBookingRequest`.

- [ ] **Step 3: Implement service tạo booking và refactor controller checkout**

```csharp
// WebHomestay/Models/ViewModels/BookingCheckoutViewModel.cs
using WebHomestay.Models;

namespace WebHomestay.Models.ViewModels;

public class BookingCheckoutViewModel
{
    public Room Room { get; set; } = null!;
    public BookingMode BookingMode { get; set; }
    public int? SlotInventoryId { get; set; }
    public string? SlotLabel { get; set; }
    public DateOnly? CheckInDate { get; set; }
    public DateOnly? CheckOutDate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal TotalPrice { get; set; }
}
```

```csharp
// WebHomestay/Models/ViewModels/CreateBookingRequest.cs
using System.ComponentModel.DataAnnotations;
using WebHomestay.Models;

namespace WebHomestay.Models.ViewModels;

public class CreateBookingRequest
{
    public int RoomId { get; set; }
    public BookingMode BookingMode { get; set; }
    public int? SlotInventoryId { get; set; }
    public DateOnly? CheckInDate { get; set; }
    public DateOnly? CheckOutDate { get; set; }
    [Required] public string CustomerName { get; set; } = string.Empty;
    [Required] public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public int GuestCount { get; set; } = 1;
    public string? CustomerNote { get; set; }
}
```

```csharp
// WebHomestay/Services/IBookingCreationService.cs
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services;

public interface IBookingCreationService
{
    Task<Booking> CreateHourlyBookingAsync(CreateBookingRequest request);
    Task<Booking> CreateDailyBookingAsync(CreateBookingRequest request);
}
```

```csharp
// WebHomestay/Services/IRoomBookingViewService.cs (mở rộng ở Task 5)
public interface IRoomBookingViewService
{
    Task<RoomDetailsViewModel> BuildAsync(Room room, DateOnly selectedHourlyDate);
    List<HourlySlotGroupViewModel> BuildHourlyGroups(IEnumerable<RoomSlotInventory> slots);
    Task<BookingCheckoutViewModel?> BuildHourlyCheckoutAsync(int roomId, int slotId);
    Task<BookingCheckoutViewModel?> BuildDailyCheckoutAsync(int roomId, DateOnly checkInDate, DateOnly checkOutDate);
}
```

```csharp
// WebHomestay/Services/RoomBookingViewService.cs (mở rộng ở Task 5)
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
        EndTime = slot.EndTime
    };
}

public async Task<BookingCheckoutViewModel?> BuildDailyCheckoutAsync(int roomId, DateOnly checkInDate, DateOnly checkOutDate)
{
    var room = await _context.Rooms.SingleOrDefaultAsync(r => r.Id == roomId);
    if (room == null) return null;

    var interval = BookingTimeRules.BuildDailyStay(checkInDate, checkOutDate);
    return new BookingCheckoutViewModel
    {
        Room = room,
        BookingMode = BookingMode.Daily,
        CheckInDate = checkInDate,
        CheckOutDate = checkOutDate,
        StartTime = interval.Start,
        EndTime = interval.End
    };
}
```

```csharp
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
        var inventory = await _context.RoomSlotInventories.SingleAsync(i => i.Id == request.SlotInventoryId);
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
        var interval = BookingTimeRules.BuildDailyStay(request.CheckInDate!.Value, request.CheckOutDate!.Value);
        if (!await _availabilityService.IsRoomAvailable(request.RoomId, interval.Start, interval.End))
            throw new InvalidOperationException("Khoảng ngày này không còn trống.");

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
            Status = "AwaitingPayment",
            PaymentStatus = "Unpaid"
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();
        return booking;
    }
}
```

```csharp
// WebHomestay/Controllers/BookingsController.cs
[HttpGet]
public async Task<IActionResult> CheckoutHourly(int roomId, int slotId)
{
    var viewModel = await _roomBookingViewService.BuildHourlyCheckoutAsync(roomId, slotId);
    if (viewModel == null) return NotFound();
    return View("Checkout", viewModel);
}

[HttpGet]
public async Task<IActionResult> CheckoutDaily(int roomId, DateOnly checkInDate, DateOnly checkOutDate)
{
    var viewModel = await _roomBookingViewService.BuildDailyCheckoutAsync(roomId, checkInDate, checkOutDate);
    if (viewModel == null) return NotFound();
    return View("Checkout", viewModel);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Checkout(CreateBookingRequest request, IFormFile idCardFront, IFormFile idCardBack)
{
    ModelState.Remove(nameof(CreateBookingRequest.CheckInDate));
    ModelState.Remove(nameof(CreateBookingRequest.CheckOutDate));

    if (!ModelState.IsValid)
    {
        var invalidViewModel = request.BookingMode == BookingMode.Hourly
            ? await _roomBookingViewService.BuildHourlyCheckoutAsync(request.RoomId, request.SlotInventoryId ?? 0)
            : await _roomBookingViewService.BuildDailyCheckoutAsync(request.RoomId, request.CheckInDate!.Value, request.CheckOutDate!.Value);
        return View("Checkout", invalidViewModel);
    }

    var booking = request.BookingMode == BookingMode.Hourly
        ? await _bookingCreationService.CreateHourlyBookingAsync(request)
        : await _bookingCreationService.CreateDailyBookingAsync(request);

    return RedirectToAction(nameof(Success), new { id = booking.Id });
}
```

```csharp
// WebHomestay/Program.cs (đăng ký booking creation service)
builder.Services.AddScoped<WebHomestay.Services.IBookingCreationService, WebHomestay.Services.BookingCreationService>();
```

- [ ] **Step 4: Chạy test booking creation và smoke test build**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BookingCreationServiceTests -v minimal`

Expected: PASS.

Run: `dotnet build WebHomestay/WebHomestay.csproj`

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add WebHomestay/Models/ViewModels/BookingCheckoutViewModel.cs WebHomestay/Models/ViewModels/CreateBookingRequest.cs WebHomestay/Services/IBookingCreationService.cs WebHomestay/Services/BookingCreationService.cs WebHomestay/Controllers/BookingsController.cs WebHomestay/Views/Bookings/Checkout.cshtml WebHomestay/Views/Bookings/Success.cshtml WebHomestay/Program.cs WebHomestay.Tests/Services/BookingCreationServiceTests.cs
git commit -m "feat: add hourly and daily checkout flows"
```

### Task 6: Xây admin template/apply/override cho lịch phòng

**Files:**
- Create: `WebHomestay.Tests/Services/RoomScheduleAdminServiceTests.cs`
- Create: `WebHomestay/Models/ViewModels/AdminRoomScheduleViewModel.cs`
- Create: `WebHomestay/Services/IRoomScheduleAdminService.cs`
- Create: `WebHomestay/Services/RoomScheduleAdminService.cs`
- Create: `WebHomestay/Controllers/AdminRoomSchedulesController.cs`
- Create: `WebHomestay/Views/AdminRoomSchedules/Templates.cshtml`
- Create: `WebHomestay/Views/AdminRoomSchedules/Apply.cshtml`
- Create: `WebHomestay/Views/AdminRoomSchedules/Calendar.cshtml`
- Modify: `WebHomestay/Views/Shared/_Layout.cshtml`
- Modify: `WebHomestay/Program.cs`

- [ ] **Step 1: Viết test fail cho việc áp template hàng loạt và sinh inventory**

```csharp
// WebHomestay.Tests/Services/RoomScheduleAdminServiceTests.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;

namespace WebHomestay.Tests.Services;

public class RoomScheduleAdminServiceTests
{
    [Fact]
    public async Task ApplyTemplateAsync_CreatesAssignmentAndInventoryForEachRoom()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(ApplyTemplateAsync_CreatesAssignmentAndInventoryForEachRoom))
            .Options;
        await using var context = new ApplicationDbContext(options);

        context.Rooms.AddRange(
            new Room { Id = 1, Name = "A", BranchId = 1 },
            new Room { Id = 2, Name = "B", BranchId = 1 });
        context.RoomSlotTemplates.Add(new RoomSlotTemplate
        {
            Id = 9,
            Name = "Combo 2h",
            Code = "2H",
            DurationMinutes = 120,
            CleanupMinutes = 30,
            SeedStartTime = new TimeOnly(0, 0)
        });
        await context.SaveChangesAsync();

        var service = new RoomScheduleAdminService(context, new SlotGenerationService());
        await service.ApplyTemplateAsync(9, new[] { 1, 2 }, new DateOnly(2026, 5, 10), 1);

        Assert.Equal(2, context.RoomSlotTemplateAssignments.Count());
        Assert.True(context.RoomSlotInventories.Any(i => i.RoomId == 1));
        Assert.True(context.RoomSlotInventories.Any(i => i.RoomId == 2));
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~RoomScheduleAdminServiceTests -v minimal`

Expected: FAIL với lỗi thiếu `RoomScheduleAdminService`.

- [ ] **Step 3: Implement admin service, controller, và 3 màn quản trị**

```csharp
// WebHomestay/Services/IRoomScheduleAdminService.cs
namespace WebHomestay.Services;

public interface IRoomScheduleAdminService
{
    Task ApplyTemplateAsync(int templateId, IEnumerable<int> roomIds, DateOnly effectiveFrom, int seedDays);
    Task BlockInventoryAsync(int inventoryId, string reason);
    Task UnblockInventoryAsync(int inventoryId);
}
```

```csharp
// WebHomestay/Models/ViewModels/AdminRoomScheduleViewModel.cs
using WebHomestay.Models;

namespace WebHomestay.Models.ViewModels;

public class AdminRoomScheduleApplyViewModel
{
    public int TemplateId { get; set; }
    public List<int> RoomIds { get; set; } = new();
    public DateOnly EffectiveFrom { get; set; }
    public int SeedDays { get; set; } = 14;
    public List<RoomSlotTemplate> Templates { get; set; } = new();
    public List<Room> Rooms { get; set; } = new();
}

public class AdminRoomInventoryCalendarViewModel
{
    public Room Room { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public List<RoomSlotInventory> Slots { get; set; } = new();
}
```

```csharp
// WebHomestay/Services/RoomScheduleAdminService.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Services;

public class RoomScheduleAdminService : IRoomScheduleAdminService
{
    private readonly ApplicationDbContext _context;
    private readonly ISlotGenerationService _slotGenerationService;

    public RoomScheduleAdminService(ApplicationDbContext context, ISlotGenerationService slotGenerationService)
    {
        _context = context;
        _slotGenerationService = slotGenerationService;
    }

    public async Task ApplyTemplateAsync(int templateId, IEnumerable<int> roomIds, DateOnly effectiveFrom, int seedDays)
    {
        var template = await _context.RoomSlotTemplates.SingleAsync(t => t.Id == templateId);
        foreach (var roomId in roomIds)
        {
            _context.RoomSlotTemplateAssignments.Add(new RoomSlotTemplateAssignment
            {
                RoomId = roomId,
                TemplateId = templateId,
                EffectiveFrom = effectiveFrom,
                IsActive = true
            });

            for (var offset = 0; offset < seedDays; offset++)
            {
                var slotDate = effectiveFrom.AddDays(offset);
                var generatedSlots = template.FixedStartTime.HasValue
                    ? new[] { _slotGenerationService.GenerateFixedSlot(roomId, template, slotDate) }
                    : _slotGenerationService.GenerateRollingSlots(roomId, template, slotDate);
                _context.RoomSlotInventories.AddRange(generatedSlots);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task BlockInventoryAsync(int inventoryId, string reason)
    {
        var inventory = await _context.RoomSlotInventories.SingleAsync(i => i.Id == inventoryId);
        inventory.Status = "Blocked";
        _context.RoomSlotOverrides.Add(new RoomSlotOverride
        {
            RoomId = inventory.RoomId,
            InventoryId = inventory.Id,
            TargetDate = inventory.SlotDate,
            OverrideType = "block",
            Reason = reason
        });
        await _context.SaveChangesAsync();
    }

    public async Task UnblockInventoryAsync(int inventoryId)
    {
        var inventory = await _context.RoomSlotInventories.SingleAsync(i => i.Id == inventoryId);
        inventory.Status = "Available";
        await _context.SaveChangesAsync();
    }
}
```

```csharp
// WebHomestay/Controllers/AdminRoomSchedulesController.cs
[AdminAuthorize]
[Route("admin/room-schedules")]
public class AdminRoomSchedulesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IRoomScheduleAdminService _roomScheduleAdminService;

    public AdminRoomSchedulesController(ApplicationDbContext context, IRoomScheduleAdminService roomScheduleAdminService)
    {
        _context = context;
        _roomScheduleAdminService = roomScheduleAdminService;
    }

    [HttpGet("templates")]
    public async Task<IActionResult> Templates()
    {
        var templates = await _context.RoomSlotTemplates.OrderBy(t => t.Name).ToListAsync();
        return View(templates);
    }

    [HttpGet("apply")]
    public async Task<IActionResult> Apply()
    {
        var model = new AdminRoomScheduleApplyViewModel
        {
            Templates = await _context.RoomSlotTemplates.Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync(),
            Rooms = await _context.Rooms.Include(r => r.Branch).OrderBy(r => r.Name).ToListAsync(),
            EffectiveFrom = DateOnly.FromDateTime(DateTime.Today)
        };
        return View(model);
    }

    [HttpPost("apply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(AdminRoomScheduleApplyViewModel input)
    {
        if (!ModelState.IsValid) return View(input);
        await _roomScheduleAdminService.ApplyTemplateAsync(input.TemplateId, input.RoomIds, input.EffectiveFrom, input.SeedDays);
        TempData["SuccessMessage"] = "Đã áp template và sinh lịch phòng.";
        return RedirectToAction(nameof(Calendar), new { roomId = input.RoomIds.First() });
    }

    [HttpGet("calendar/{roomId}")]
    public async Task<IActionResult> Calendar(int roomId, DateOnly? fromDate)
    {
        var startDate = fromDate ?? DateOnly.FromDateTime(DateTime.Today);
        var room = await _context.Rooms.Include(r => r.Branch).FirstOrDefaultAsync(r => r.Id == roomId);
        if (room == null) return NotFound();

        var inventory = await _context.RoomSlotInventories
            .Include(i => i.Template)
            .Where(i => i.RoomId == roomId && i.SlotDate >= startDate && i.SlotDate < startDate.AddDays(14))
            .OrderBy(i => i.StartTime)
            .ToListAsync();

        return View(new AdminRoomInventoryCalendarViewModel
        {
            Room = room,
            StartDate = startDate,
            Slots = inventory
        });
    }
}
```

```csharp
// WebHomestay/Program.cs (đăng ký admin schedule service)
builder.Services.AddScoped<WebHomestay.Services.IRoomScheduleAdminService, WebHomestay.Services.RoomScheduleAdminService>();
```

```html
<!-- WebHomestay/Views/Shared/_Layout.cshtml (menu item) -->
<li><a class="dropdown-item" asp-controller="AdminRoomSchedules" asp-action="Templates">Lịch phòng & combo giờ</a></li>
```

- [ ] **Step 4: Chạy test admin service và build**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~RoomScheduleAdminServiceTests -v minimal`

Expected: PASS.

Run: `dotnet build WebHomestay/WebHomestay.csproj`

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add WebHomestay/Models/ViewModels/AdminRoomScheduleViewModel.cs WebHomestay/Services/IRoomScheduleAdminService.cs WebHomestay/Services/RoomScheduleAdminService.cs WebHomestay/Controllers/AdminRoomSchedulesController.cs WebHomestay/Views/AdminRoomSchedules/Templates.cshtml WebHomestay/Views/AdminRoomSchedules/Apply.cshtml WebHomestay/Views/AdminRoomSchedules/Calendar.cshtml WebHomestay/Views/Shared/_Layout.cshtml WebHomestay/Program.cs WebHomestay.Tests/Services/RoomScheduleAdminServiceTests.cs
git commit -m "feat: add admin room schedule management"
```

### Task 7: Đồng bộ màn admin hiện có, hoàn thiện migration, và chạy regression

**Files:**
- Modify: `WebHomestay/Controllers/AdminMatrixController.cs`
- Modify: `WebHomestay/Views/AdminMatrix/Index.cshtml`
- Modify: `WebHomestay/Controllers/AdminBookingsController.cs`
- Modify: `WebHomestay/Views/AdminBookings/Index.cshtml`
- Modify: `WebHomestay/Views/AdminBookings/Details.cshtml`
- Modify: `WebHomestay/Data/init_db.sql`
- Modify: `docs/superpowers/specs/2026-05-09-hourly-daily-booking-design.md` (chỉ nếu implementation cần cập nhật note nhỏ)

- [ ] **Step 1: Viết test fail cho việc render dữ liệu admin có `BookingMode` và slot label**

```csharp
// WebHomestay.Tests/Services/AdminBookingProjectionTests.cs
using WebHomestay.Models;

namespace WebHomestay.Tests.Services;

public class AdminBookingProjectionTests
{
    [Fact]
    public void HourlyBooking_ExposesModeAndSlotLabelForAdminViews()
    {
        var booking = new Booking
        {
            BookingMode = BookingMode.Hourly,
            SlotLabel = "09:30-11:30",
            StartTime = new DateTime(2026, 5, 10, 9, 30, 0),
            EndTime = new DateTime(2026, 5, 10, 11, 30, 0)
        };

        Assert.Equal(BookingMode.Hourly, booking.BookingMode);
        Assert.Equal("09:30-11:30", booking.SlotLabel);
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận fail nếu surface admin chưa cập nhật**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminBookingProjectionTests -v minimal`

Expected: FAIL nếu model hoặc property chưa có ở workspace task này.

- [ ] **Step 3: Cập nhật admin matrix, admin bookings, và script bootstrap**

```csharp
// WebHomestay/Controllers/AdminMatrixController.cs (query)
var bookings = await _context.Bookings
    .Include(b => b.Room)
    .Where(b => b.StartTime < end && b.EndTime > start && !b.IsDeleted)
    .OrderBy(b => b.StartTime)
    .ToListAsync();
```

```html
<!-- WebHomestay/Views/AdminBookings/Index.cshtml (badge mode) -->
<td>
    <div>@item.Room?.Name</div>
    <div class="small text-muted">
        @(item.BookingMode == WebHomestay.Models.BookingMode.Hourly ? "Theo giờ" : "Theo ngày")
        @if (!string.IsNullOrWhiteSpace(item.SlotLabel))
        {
            <span>- @item.SlotLabel</span>
        }
    </div>
</td>
```

```sql
-- WebHomestay/Data/init_db.sql (đảm bảo có cả inventory + override)
CREATE TABLE IF NOT EXISTS room_slot_inventories (
    id SERIAL PRIMARY KEY,
    room_id INT NOT NULL REFERENCES rooms(id) ON DELETE CASCADE,
    template_id INT NOT NULL REFERENCES room_slot_templates(id) ON DELETE CASCADE,
    slot_date DATE NOT NULL,
    slot_label VARCHAR(100) NOT NULL,
    start_time TIMESTAMP NOT NULL,
    end_time TIMESTAMP NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Available',
    booking_id INT NULL REFERENCES bookings(id) ON DELETE SET NULL
);
```

- [ ] **Step 4: Chạy full test suite, build, và migration smoke**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj -v minimal`

Expected: `Passed!`

Run: `dotnet build WebHomestay/WebHomestay.csproj`

Expected: `Build succeeded.`

Run: `dotnet ef database update --project WebHomestay/WebHomestay.csproj`

Expected: Migration apply thành công, không lỗi duplicate column/table.

- [ ] **Step 5: Commit**

```bash
git add WebHomestay/Controllers/AdminMatrixController.cs WebHomestay/Views/AdminMatrix/Index.cshtml WebHomestay/Controllers/AdminBookingsController.cs WebHomestay/Views/AdminBookings/Index.cshtml WebHomestay/Views/AdminBookings/Details.cshtml WebHomestay/Data/init_db.sql WebHomestay.Tests/Services/AdminBookingProjectionTests.cs
git commit -m "feat: align admin views with hourly and daily bookings"
```

## Self-Review

- **Spec coverage:** Task 1-3 phủ rule thời gian, overlap, slot generation. Task 4-5 phủ UI khách và flow checkout. Task 6 phủ admin template/apply/override. Task 7 phủ các màn admin hiện có, bootstrap SQL, regression.
- **Placeholder scan:** Không dùng `TODO`, `TBD`, `implement later`, hay tham chiếu mơ hồ kiểu “similar to task above”.
- **Type consistency:** Toàn plan dùng thống nhất `BookingMode`, `RoomSlotTemplate`, `RoomSlotInventory`, `CreateBookingRequest`, `BookingTimeRules`, `SlotGenerationService`, `RoomScheduleAdminService`.

Plan complete and saved to `docs/superpowers/plans/2026-05-09-hourly-daily-booking.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
