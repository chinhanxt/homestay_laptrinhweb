# AI Booking Flow and Compact Brain Center V2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the public AI booking mini-flow and redesign AI Brain Center into a compact 4-tab control room that configures, tests, and traces the real booking assistant.

**Architecture:** Keep the ambitious multi-agent + RAG + Graph direction, but make booking truth deterministic: EF services own availability, pricing, booking creation, and payment state; the LLM writes short sales copy and synthesizes agent context only. The public chat receives pre-chat context fields, returns typed UI blocks, and uses action endpoints for room/slot/form/payment steps. Admin Brain Center becomes a compact 4-tab interface: Tổng quan, Tri thức & Graph, Trả lời & Form, Test & Trace.

**Tech Stack:** ASP.NET Core MVC `net10.0`, EF Core/Npgsql/PostgreSQL, Razor, Bootstrap, jQuery for admin, vanilla JS for public chatbot, existing services `AIBrainOrchestrator`, `AvailabilityService`, `RoomBookingViewService`, `BookingCreationService`, `PricingService`.

---

## Current State Checkpoint

Already present and building:
- `WebHomestay/Services/AIBookingFlowModels.cs` has base DTOs.
- `WebHomestay/Controllers/AIChatController.cs` exposes `POST /ai/chat`, resolves branch/date/guest count, calls `AIBrainOrchestrator`, and returns `roomCards` when branch/date is known.
- `WebHomestay/wwwroot/js/site.js` renders `roomCards`, `hourlySlots`, `dailyRooms`, `bookingForm`, `paymentQr` blocks, but action buttons are not wired.
- `WebHomestay/wwwroot/css/user-premium.css` has basic public chat booking block styles.
- `dotnet build WebHomestay/WebHomestay.csproj -o /tmp/webhomestay-build-check --no-restore` passes with existing warnings.

Key unfinished work:
- No public pre-chat context form yet.
- No deterministic `AIBookingFlowOrchestrator` yet.
- No `POST /ai/booking-action` yet.
- Select room/slot buttons do not call backend.
- Booking form is static and does not create real bookings.
- Payment/QR block is not connected to real booking/payment state.
- Brain Center still has the larger old tab layout and n8n-style workflow section.
- No AI booking flow tests yet.

---

## File Structure and Responsibilities

### Public AI booking flow
- Modify `WebHomestay/Services/AIBookingFlowModels.cs`
  - Extend DTOs for pre-chat context, booking actions, booking form config, booking summary, payment blocks, and traceable agent outputs.
- Create `WebHomestay/Services/IAIBookingFlowOrchestrator.cs`
  - Public interface for deterministic booking-flow actions.
- Create `WebHomestay/Services/AIBookingFlowOrchestrator.cs`
  - Owns public flow state transitions: initialize context, list rooms, select room, list slot/day options, return booking form, create booking, return payment block.
  - Calls existing availability/pricing/booking services; never guesses room/price/availability.
- Modify `WebHomestay/Controllers/AIChatController.cs`
  - Keep `POST /ai/chat` for free text + pre-chat context.
  - Add `POST /ai/booking-action` for typed actions from UI buttons/forms.
- Modify `WebHomestay/Program.cs`
  - Register `IAIBookingFlowOrchestrator`.
- Modify `WebHomestay/Views/Shared/_Layout.cshtml`
  - Add compact pre-chat fields: customer name, branch dropdown, booking mode, guest count.
- Modify `WebHomestay/wwwroot/js/site.js`
  - Include pre-chat context in `/ai/chat` request.
  - Maintain client state.
  - Wire `select-room`, `select-slot`, `select-daily-room`, and `submit-booking-form` actions to `/ai/booking-action`.
- Modify `WebHomestay/wwwroot/css/user-premium.css`
  - Style pre-chat context, summary, booking form, file inputs, and payment blocks.

### Admin AI Brain Center
- Modify `WebHomestay/Views/AdminAI/Index.cshtml`
  - Reduce tabs to 4: Tổng quan, Tri thức & Graph, Trả lời & Form, Test & Trace.
  - Keep Knowledge, Graph, Final Synthesizer, form designer, and trace functionality, but reorganize them.
- Modify `WebHomestay/wwwroot/js/admin-ai-brain-center.js`
  - Add compact-tab behavior if needed.
  - Load/save fixed booking form config.
  - Add public booking flow test console using the same `/ai/chat` and `/ai/booking-action` endpoints.
- Modify `WebHomestay/wwwroot/css/admin-ai-brain-center.css`
  - Replace n8n-heavy visual emphasis with compact workflow cards and test/trace panels.
- Modify `WebHomestay/Controllers/AdminAIController.cs`
  - Add `GET/POST /admin/ai/booking-form-config` for fixed booking form designer.
  - Keep existing Knowledge, Graph, Final Synthesizer, and Trace endpoints.

### Tests
- Create `WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs`
  - Tests deterministic flow state, room filtering, no booking before form submit, and booking creation after valid form submit.

---

## Task 1: Extend DTOs for a complete public booking flow

**Files:**
- Modify: `WebHomestay/Services/AIBookingFlowModels.cs`
- Build: `dotnet build WebHomestay/WebHomestay.csproj -o /tmp/webhomestay-build-check --no-restore`

- [ ] **Step 1: Replace DTO file with the complete contract**

Replace `WebHomestay/Services/AIBookingFlowModels.cs` with:

```csharp
using WebHomestay.Models;

namespace WebHomestay.Services
{
    public class AIBookingFlowResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public string CurrentStep { get; set; } = "intent";
        public string Message { get; set; } = string.Empty;
        public AIBookingSessionState State { get; set; } = new();
        public List<AIUiBlock> UiBlocks { get; set; } = new();
    }

    public class AIBookingSessionState
    {
        public string CustomerName { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public string BookingMode { get; set; } = "hourly";
        public DateOnly? HourlyDate { get; set; }
        public DateOnly? CheckInDate { get; set; }
        public DateOnly? CheckOutDate { get; set; }
        public int GuestCount { get; set; } = 1;
        public int? SelectedRoomId { get; set; }
        public string? SelectedRoomName { get; set; }
        public int? SelectedSlotId { get; set; }
        public string? SelectedSlotLabel { get; set; }
        public int? BookingId { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
    }

    public class AIUiBlock
    {
        public string Type { get; set; } = string.Empty;
        public object Data { get; set; } = new { };
    }

    public class PublicAIChatRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public string BookingMode { get; set; } = "hourly";
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int GuestCount { get; set; } = 1;
    }

    public class AIBookingActionRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public AIBookingSessionState State { get; set; } = new();
        public int? RoomId { get; set; }
        public int? SlotId { get; set; }
        public DateOnly? CheckInDate { get; set; }
        public DateOnly? CheckOutDate { get; set; }
        public AIBookingFormSubmission? Form { get; set; }
    }

    public class AIBookingFormSubmission
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public int GuestCount { get; set; } = 1;
        public string? CustomerNote { get; set; }
    }

    public class AIRoomCard
    {
        public int RoomId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal PricePerHour { get; set; }
        public decimal PricePerDay { get; set; }
        public int Capacity { get; set; }
        public int MaxGuests { get; set; }
        public decimal ExtraGuestFee { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> Amenities { get; set; } = new();
        public string DetailsUrl { get; set; } = string.Empty;
    }

    public class AISlotOption
    {
        public int SlotId { get; set; }
        public int RoomId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class AIDailyRoomOption
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public DateOnly CheckInDate { get; set; }
        public DateOnly CheckOutDate { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class AIBookingSummaryBlock
    {
        public string CustomerName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string BookingMode { get; set; } = string.Empty;
        public string Timeline { get; set; } = string.Empty;
        public int GuestCount { get; set; }
        public decimal BasePrice { get; set; }
        public decimal ExtraGuestFee { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class AIBookingFormField
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public bool Required { get; set; } = true;
        public string HelpText { get; set; } = string.Empty;
        public int Order { get; set; }
    }

    public class AIPaymentBlock
    {
        public int BookingId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "AwaitingPayment";
        public string Message { get; set; } = string.Empty;
        public string? QrImageUrl { get; set; }
        public string SuccessUrl { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 2: Remove duplicate request class from controller later**

Do not remove `PublicAIChatRequest` from `AIChatController.cs` in this task; Task 4 will do it when the controller is refactored.

- [ ] **Step 3: Build to reveal duplicate type before controller cleanup**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj -o /tmp/webhomestay-build-check --no-restore
```

Expected: build may fail with duplicate `PublicAIChatRequest` because the controller still defines one. That failure is expected until Task 4 Step 1 removes the controller-local class.

- [ ] **Step 4: Commit after Task 4, not now**

Do not commit this task alone if build fails due to the temporary duplicate type.

---

## Task 2: Add tests for booking flow service before implementation

**Files:**
- Create: `WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs`
- Test: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AIBookingFlowOrchestratorTests`

- [ ] **Step 1: Create failing tests**

Create `WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class AIBookingFlowOrchestratorTests
{
    [Fact]
    public async Task BuildRoomCardsAsync_FiltersByBranchAndGuestCount()
    {
        await using var context = CreateContext(nameof(BuildRoomCardsAsync_FiltersByBranchAndGuestCount));
        SeedBranchesAndRooms(context);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var response = await service.BuildRoomCardsAsync(new AIBookingSessionState
        {
            BranchId = 1,
            BranchName = "StayEasy Sài Gòn",
            BookingMode = "hourly",
            HourlyDate = new DateOnly(2026, 5, 20),
            GuestCount = 2
        });

        var block = Assert.Single(response.UiBlocks);
        Assert.Equal("roomCards", block.Type);
        var rooms = GetRooms(block.Data);
        Assert.Single(rooms);
        Assert.Equal("Sài Gòn Couple", rooms[0].Name);
    }

    [Fact]
    public async Task SelectRoomAsync_ReturnsSlotsButDoesNotCreateBooking()
    {
        await using var context = CreateContext(nameof(SelectRoomAsync_ReturnsSlotsButDoesNotCreateBooking));
        SeedBranchesRoomsAndSlots(context);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var response = await service.SelectRoomAsync(new AIBookingActionRequest
        {
            SessionId = "s1",
            Action = "select-room",
            RoomId = 10,
            State = new AIBookingSessionState
            {
                BranchId = 1,
                BranchName = "StayEasy Sài Gòn",
                BookingMode = "hourly",
                HourlyDate = new DateOnly(2026, 5, 20),
                GuestCount = 2
            }
        });

        Assert.Empty(context.Bookings);
        Assert.Contains(response.UiBlocks, block => block.Type == "hourlySlots");
    }

    [Fact]
    public async Task SubmitBookingFormAsync_CreatesAwaitingPaymentBookingAndReturnsPaymentBlock()
    {
        await using var context = CreateContext(nameof(SubmitBookingFormAsync_CreatesAwaitingPaymentBookingAndReturnsPaymentBlock));
        SeedBranchesRoomsAndSlots(context);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var response = await service.SubmitBookingFormAsync(new AIBookingActionRequest
        {
            SessionId = "s1",
            Action = "submit-booking-form",
            State = new AIBookingSessionState
            {
                BranchId = 1,
                BranchName = "StayEasy Sài Gòn",
                BookingMode = "hourly",
                HourlyDate = new DateOnly(2026, 5, 20),
                GuestCount = 2,
                SelectedRoomId = 10,
                SelectedRoomName = "Sài Gòn Couple",
                SelectedSlotId = 100,
                SelectedSlotLabel = "09:00-11:00"
            },
            Form = new AIBookingFormSubmission
            {
                CustomerName = "Nguyễn An",
                CustomerPhone = "0900000000",
                CustomerEmail = "an@example.com",
                GuestCount = 2,
                CustomerNote = "Đến đúng giờ"
            }
        });

        var booking = Assert.Single(context.Bookings);
        Assert.Equal("AwaitingPayment", booking.Status);
        Assert.Equal("Unpaid", booking.PaymentStatus);
        Assert.Contains(response.UiBlocks, block => block.Type == "paymentQr");
    }

    private static ApplicationDbContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static AIBookingFlowOrchestrator CreateService(ApplicationDbContext context)
    {
        var pricing = new PricingService(context);
        return new AIBookingFlowOrchestrator(
            context,
            new AvailabilityService(context),
            new BookingCreationService(context, new AvailabilityService(context), pricing),
            pricing,
            new MemoryCache(new MemoryCacheOptions()));
    }

    private static void SeedBranchesAndRooms(ApplicationDbContext context)
    {
        context.Branches.AddRange(
            new Branch { Id = 1, Name = "StayEasy Sài Gòn", Address = "Q1", Status = "Active" },
            new Branch { Id = 2, Name = "StayEasy Đà Lạt", Address = "Đà Lạt", Status = "Active" });
        context.Rooms.AddRange(
            new Room { Id = 10, BranchId = 1, Name = "Sài Gòn Couple", Status = "Available", Capacity = 2, MaxGuests = 3, PricePerHour = 120000, PricePerDay = 650000, ExtraGuestFee = 80000 },
            new Room { Id = 11, BranchId = 1, Name = "Sài Gòn Solo", Status = "Available", Capacity = 1, MaxGuests = 1, PricePerHour = 90000, PricePerDay = 500000, ExtraGuestFee = 0 },
            new Room { Id = 20, BranchId = 2, Name = "Đà Lạt View", Status = "Available", Capacity = 2, MaxGuests = 4, PricePerHour = 150000, PricePerDay = 800000, ExtraGuestFee = 100000 });
    }

    private static void SeedBranchesRoomsAndSlots(ApplicationDbContext context)
    {
        SeedBranchesAndRooms(context);
        context.RoomSlotTemplates.Add(new RoomSlotTemplate { Id = 1, Name = "Sáng", StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(11, 0), IsActive = true });
        context.RoomSlotInventories.Add(new RoomSlotInventory
        {
            Id = 100,
            RoomId = 10,
            TemplateId = 1,
            SlotDate = new DateOnly(2026, 5, 20),
            SlotLabel = "09:00-11:00",
            StartTime = new DateTime(2026, 5, 20, 9, 0, 0),
            EndTime = new DateTime(2026, 5, 20, 11, 0, 0),
            Status = "Available"
        });
    }

    private static List<AIRoomCard> GetRooms(object data)
    {
        var property = data.GetType().GetProperty("rooms") ?? data.GetType().GetProperty("Rooms");
        return Assert.IsAssignableFrom<List<AIRoomCard>>(property!.GetValue(data));
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail for missing service**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AIBookingFlowOrchestratorTests
```

Expected: FAIL because `AIBookingFlowOrchestrator` does not exist yet.

---

## Task 3: Implement deterministic booking flow orchestrator

**Files:**
- Create: `WebHomestay/Services/IAIBookingFlowOrchestrator.cs`
- Create: `WebHomestay/Services/AIBookingFlowOrchestrator.cs`
- Modify: `WebHomestay/Program.cs`
- Test: `WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs`

- [ ] **Step 1: Create the interface**

Create `WebHomestay/Services/IAIBookingFlowOrchestrator.cs`:

```csharp
namespace WebHomestay.Services
{
    public interface IAIBookingFlowOrchestrator
    {
        Task<AIBookingFlowResponse> BuildRoomCardsAsync(AIBookingSessionState state, CancellationToken cancellationToken = default);
        Task<AIBookingFlowResponse> SelectRoomAsync(AIBookingActionRequest request, CancellationToken cancellationToken = default);
        Task<AIBookingFlowResponse> SelectSlotAsync(AIBookingActionRequest request, CancellationToken cancellationToken = default);
        Task<AIBookingFlowResponse> SelectDailyRoomAsync(AIBookingActionRequest request, CancellationToken cancellationToken = default);
        Task<AIBookingFlowResponse> SubmitBookingFormAsync(AIBookingActionRequest request, CancellationToken cancellationToken = default);
        Task<AIBookingFlowResponse> HandleActionAsync(AIBookingActionRequest request, CancellationToken cancellationToken = default);
    }
}
```

- [ ] **Step 2: Create the implementation**

Create `WebHomestay/Services/AIBookingFlowOrchestrator.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services
{
    public class AIBookingFlowOrchestrator : IAIBookingFlowOrchestrator
    {
        private readonly ApplicationDbContext _context;
        private readonly IAvailabilityService _availabilityService;
        private readonly IBookingCreationService _bookingCreationService;
        private readonly PricingService _pricingService;
        private readonly IMemoryCache _cache;

        public AIBookingFlowOrchestrator(
            ApplicationDbContext context,
            IAvailabilityService availabilityService,
            IBookingCreationService bookingCreationService,
            PricingService pricingService,
            IMemoryCache cache)
        {
            _context = context;
            _availabilityService = availabilityService;
            _bookingCreationService = bookingCreationService;
            _pricingService = pricingService;
            _cache = cache;
        }

        public async Task<AIBookingFlowResponse> BuildRoomCardsAsync(AIBookingSessionState state, CancellationToken cancellationToken = default)
        {
            NormalizeState(state);
            if (!state.BranchId.HasValue)
            {
                return Response("missing-branch", "Bạn chọn giúp mình chi nhánh muốn ở nhé.", state);
            }

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == state.BranchId.Value, cancellationToken);
            if (branch != null) state.BranchName = branch.Name;

            var date = state.HourlyDate ?? state.CheckInDate ?? DateOnly.FromDateTime(DateTime.Today);
            var rooms = await _context.Rooms
                .Where(r => r.BranchId == state.BranchId.Value && r.Status == "Available" && r.MaxGuests >= Math.Max(state.GuestCount, 1))
                .Include(r => r.Amenities)
                .OrderBy(r => state.BookingMode == "daily" ? r.PricePerDay : r.PricePerHour)
                .Select(r => new AIRoomCard
                {
                    RoomId = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    PricePerHour = r.PricePerHour,
                    PricePerDay = r.PricePerDay,
                    Capacity = r.Capacity,
                    MaxGuests = r.MaxGuests,
                    ExtraGuestFee = r.ExtraGuestFee,
                    ImageUrl = r.ImageUrl,
                    Amenities = r.Amenities.Select(a => a.Name).ToList(),
                    DetailsUrl = state.BookingMode == "daily"
                        ? $"/Rooms/Details/{r.Id}?mode=daily"
                        : $"/Rooms/Details/{r.Id}?hourlyDate={date:yyyy-MM-dd}&mode=hourly"
                })
                .ToListAsync(cancellationToken);

            var message = rooms.Any()
                ? $"Mình tìm thấy {rooms.Count} phòng phù hợp ở {state.BranchName ?? "chi nhánh đã chọn"}. Bạn xem danh sách bên dưới nhé."
                : "Hiện chưa có phòng phù hợp với số khách và chi nhánh này. Bạn thử đổi ngày hoặc số khách giúp mình nhé.";
            return Response("room-options", message, state, new AIUiBlock { Type = "roomCards", Data = new { rooms } });
        }

        public async Task<AIBookingFlowResponse> SelectRoomAsync(AIBookingActionRequest request, CancellationToken cancellationToken = default)
        {
            var state = request.State;
            NormalizeState(state);
            state.SelectedRoomId = request.RoomId;

            var room = await _context.Rooms.Include(r => r.Branch).FirstOrDefaultAsync(r => r.Id == request.RoomId, cancellationToken);
            if (room == null) return Response("room-missing", "Mình không tìm thấy phòng này nữa, bạn chọn lại giúp mình nhé.", state);
            state.SelectedRoomName = room.Name;
            state.BranchId = room.BranchId;
            state.BranchName = room.Branch?.Name ?? state.BranchName;

            if (state.BookingMode == "daily")
            {
                return await BuildDailySelectionAsync(state, cancellationToken);
            }

            return await BuildHourlySlotsAsync(state, room, cancellationToken);
        }

        public async Task<AIBookingFlowResponse> SelectSlotAsync(AIBookingActionRequest request, CancellationToken cancellationToken = default)
        {
            var state = request.State;
            NormalizeState(state);
            state.SelectedSlotId = request.SlotId;

            var slot = await _context.RoomSlotInventories
                .Include(s => s.Room)
                .ThenInclude(r => r.Branch)
                .FirstOrDefaultAsync(s => s.Id == request.SlotId, cancellationToken);
            if (slot == null) return Response("slot-missing", "Khung giờ này không còn tồn tại, bạn chọn lại giúp mình nhé.", state);

            state.SelectedRoomId = slot.RoomId;
            state.SelectedRoomName = slot.Room.Name;
            state.SelectedSlotLabel = slot.SlotLabel;
            state.HourlyDate = slot.SlotDate;
            state.BranchId = slot.Room.BranchId;
            state.BranchName = slot.Room.Branch?.Name;

            var summary = await BuildHourlySummaryAsync(state, slot, cancellationToken);
            return Response("booking-form", "Mình đã tổng hợp đơn nháp. Bạn điền thông tin bên dưới để tạo đơn chờ thanh toán nhé.", state,
                new AIUiBlock { Type = "bookingSummary", Data = summary },
                new AIUiBlock { Type = "bookingForm", Data = new { fields = GetDefaultBookingFormFields(), state } });
        }

        public async Task<AIBookingFlowResponse> SelectDailyRoomAsync(AIBookingActionRequest request, CancellationToken cancellationToken = default)
        {
            var state = request.State;
            NormalizeState(state);
            state.SelectedRoomId = request.RoomId;
            state.CheckInDate = request.CheckInDate ?? state.CheckInDate;
            state.CheckOutDate = request.CheckOutDate ?? state.CheckOutDate;

            var room = await _context.Rooms.Include(r => r.Branch).FirstOrDefaultAsync(r => r.Id == state.SelectedRoomId, cancellationToken);
            if (room == null) return Response("room-missing", "Mình không tìm thấy phòng này nữa, bạn chọn lại giúp mình nhé.", state);
            if (!state.CheckInDate.HasValue || !state.CheckOutDate.HasValue) return Response("missing-date-range", "Bạn chọn giúp mình ngày nhận và ngày trả phòng nhé.", state);

            state.SelectedRoomName = room.Name;
            state.BranchId = room.BranchId;
            state.BranchName = room.Branch?.Name;
            var summary = await BuildDailySummaryAsync(state, room, cancellationToken);
            return Response("booking-form", "Mình đã tổng hợp đơn nháp. Bạn điền thông tin bên dưới để tạo đơn chờ thanh toán nhé.", state,
                new AIUiBlock { Type = "bookingSummary", Data = summary },
                new AIUiBlock { Type = "bookingForm", Data = new { fields = GetDefaultBookingFormFields(), state } });
        }

        public async Task<AIBookingFlowResponse> SubmitBookingFormAsync(AIBookingActionRequest request, CancellationToken cancellationToken = default)
        {
            var state = request.State;
            NormalizeState(state);
            if (request.Form == null) return Response("missing-form", "Bạn điền thông tin đặt phòng giúp mình nhé.", state);
            if (!state.SelectedRoomId.HasValue) return Response("missing-room", "Bạn chọn phòng trước khi gửi thông tin đặt phòng nhé.", state);

            var form = request.Form;
            var guestCount = Math.Max(form.GuestCount, state.GuestCount);
            var createRequest = new CreateBookingRequest
            {
                RoomId = state.SelectedRoomId.Value,
                BookingMode = state.BookingMode == "daily" ? BookingMode.Daily : BookingMode.Hourly,
                SlotInventoryId = state.SelectedSlotId,
                CheckInDate = state.CheckInDate,
                CheckOutDate = state.CheckOutDate,
                CustomerName = string.IsNullOrWhiteSpace(form.CustomerName) ? state.CustomerName : form.CustomerName,
                CustomerPhone = form.CustomerPhone,
                CustomerEmail = form.CustomerEmail,
                GuestCount = guestCount,
                CustomerNote = form.CustomerNote
            };

            var booking = createRequest.BookingMode == BookingMode.Hourly
                ? await _bookingCreationService.CreateHourlyBookingAsync(createRequest)
                : await _bookingCreationService.CreateDailyBookingAsync(createRequest);

            state.BookingId = booking.Id;
            state.PaymentStatus = booking.PaymentStatus;
            var payment = new AIPaymentBlock
            {
                BookingId = booking.Id,
                Amount = booking.TotalPrice,
                Status = booking.Status,
                Message = "Đơn đã được tạo ở trạng thái chờ thanh toán. Bạn vui lòng thanh toán và gửi minh chứng trên trang xác nhận.",
                SuccessUrl = $"/Bookings/Success/{booking.Id}"
            };

            CacheState(request.SessionId, state);
            return Response("payment", "Mình đã tạo đơn chờ thanh toán. Bạn kiểm tra thông tin thanh toán bên dưới nhé.", state,
                new AIUiBlock { Type = "paymentQr", Data = payment });
        }

        public Task<AIBookingFlowResponse> HandleActionAsync(AIBookingActionRequest request, CancellationToken cancellationToken = default)
        {
            return request.Action switch
            {
                "select-room" => SelectRoomAsync(request, cancellationToken),
                "select-slot" => SelectSlotAsync(request, cancellationToken),
                "select-daily-room" => SelectDailyRoomAsync(request, cancellationToken),
                "submit-booking-form" => SubmitBookingFormAsync(request, cancellationToken),
                _ => Task.FromResult(Response("unknown-action", "Mình chưa hiểu thao tác này, bạn thử lại giúp mình nhé.", request.State))
            };
        }

        private async Task<AIBookingFlowResponse> BuildHourlySlotsAsync(AIBookingSessionState state, Room room, CancellationToken cancellationToken)
        {
            var date = state.HourlyDate ?? DateOnly.FromDateTime(DateTime.Today);
            var slots = await _context.RoomSlotInventories
                .Where(slot => slot.RoomId == room.Id && slot.SlotDate == date && slot.Status == "Available")
                .OrderBy(slot => slot.StartTime)
                .ToListAsync(cancellationToken);

            var options = new List<AISlotOption>();
            foreach (var slot in slots)
            {
                if (!await _availabilityService.IsRoomAvailable(room.Id, slot.StartTime, slot.EndTime)) continue;
                options.Add(new AISlotOption
                {
                    SlotId = slot.Id,
                    RoomId = room.Id,
                    RoomName = room.Name,
                    Label = slot.SlotLabel,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    TotalPrice = await _pricingService.CalculateStayPriceAsync(room.Id, slot.StartTime, slot.EndTime, true)
                });
            }

            return Response("slot-options", "Bạn chọn giúp mình khung giờ muốn đặt nhé.", state,
                new AIUiBlock { Type = "hourlySlots", Data = new { slots = options } });
        }

        private async Task<AIBookingFlowResponse> BuildDailySelectionAsync(AIBookingSessionState state, CancellationToken cancellationToken)
        {
            if (!state.SelectedRoomId.HasValue) return Response("missing-room", "Bạn chọn phòng trước nhé.", state);
            var room = await _context.Rooms.FirstAsync(r => r.Id == state.SelectedRoomId.Value, cancellationToken);
            var checkIn = state.CheckInDate ?? DateOnly.FromDateTime(DateTime.Today);
            var checkOut = state.CheckOutDate ?? checkIn.AddDays(1);
            var interval = BookingTimeRules.BuildDailyStay(checkIn, checkOut);
            if (!await _availabilityService.IsRoomAvailable(room.Id, interval.Start, interval.End))
            {
                return Response("daily-unavailable", "Khoảng ngày này không còn trống. Bạn chọn khoảng ngày khác giúp mình nhé.", state);
            }

            var option = new AIDailyRoomOption
            {
                RoomId = room.Id,
                RoomName = room.Name,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                TotalPrice = await _pricingService.CalculateStayPriceAsync(room.Id, interval.Start, interval.End, false)
            };
            return Response("daily-options", "Khoảng ngày này đang có thể đặt. Bạn bấm chọn để mình tổng hợp đơn nháp nhé.", state,
                new AIUiBlock { Type = "dailyRooms", Data = new { rooms = new[] { option } } });
        }

        private async Task<AIBookingSummaryBlock> BuildHourlySummaryAsync(AIBookingSessionState state, RoomSlotInventory slot, CancellationToken cancellationToken)
        {
            var room = slot.Room;
            var basePrice = await _pricingService.CalculateStayPriceAsync(room.Id, slot.StartTime, slot.EndTime, true);
            var extraFee = Math.Max(0, state.GuestCount - room.Capacity) * room.ExtraGuestFee;
            return new AIBookingSummaryBlock
            {
                CustomerName = state.CustomerName,
                BranchName = state.BranchName ?? string.Empty,
                RoomName = room.Name,
                BookingMode = "hourly",
                Timeline = $"{slot.SlotLabel} ngày {slot.SlotDate:dd/MM/yyyy}",
                GuestCount = state.GuestCount,
                BasePrice = basePrice,
                ExtraGuestFee = extraFee,
                TotalPrice = basePrice + extraFee
            };
        }

        private async Task<AIBookingSummaryBlock> BuildDailySummaryAsync(AIBookingSessionState state, Room room, CancellationToken cancellationToken)
        {
            var interval = BookingTimeRules.BuildDailyStay(state.CheckInDate!.Value, state.CheckOutDate!.Value);
            var basePrice = await _pricingService.CalculateStayPriceAsync(room.Id, interval.Start, interval.End, false);
            var extraFee = Math.Max(0, state.GuestCount - room.Capacity) * room.ExtraGuestFee;
            return new AIBookingSummaryBlock
            {
                CustomerName = state.CustomerName,
                BranchName = state.BranchName ?? string.Empty,
                RoomName = room.Name,
                BookingMode = "daily",
                Timeline = $"{state.CheckInDate:dd/MM/yyyy} - {state.CheckOutDate:dd/MM/yyyy}",
                GuestCount = state.GuestCount,
                BasePrice = basePrice,
                ExtraGuestFee = extraFee,
                TotalPrice = basePrice + extraFee
            };
        }

        private List<AIBookingFormField> GetDefaultBookingFormFields()
        {
            return new List<AIBookingFormField>
            {
                new() { Key = "customerName", Label = "Họ tên", Type = "text", Required = true, Order = 1 },
                new() { Key = "customerPhone", Label = "SĐT/Zalo", Type = "tel", Required = true, Order = 2 },
                new() { Key = "customerEmail", Label = "Email", Type = "email", Required = true, Order = 3 },
                new() { Key = "guestCount", Label = "Số khách", Type = "number", Required = true, Order = 4 },
                new() { Key = "customerNote", Label = "Ghi chú", Type = "textarea", Required = false, Order = 5 }
            };
        }

        private AIBookingFlowResponse Response(string step, string message, AIBookingSessionState state, params AIUiBlock[] blocks)
        {
            return new AIBookingFlowResponse
            {
                CurrentStep = step,
                Message = message,
                State = state,
                UiBlocks = blocks.Where(block => block != null).ToList()
            };
        }

        private void NormalizeState(AIBookingSessionState state)
        {
            state.BookingMode = state.BookingMode == "daily" ? "daily" : "hourly";
            state.GuestCount = Math.Max(state.GuestCount, 1);
        }

        private void CacheState(string sessionId, AIBookingSessionState state)
        {
            if (!string.IsNullOrWhiteSpace(sessionId)) _cache.Set($"ai-booking:{sessionId}", state, TimeSpan.FromHours(2));
        }
    }
}
```

- [ ] **Step 3: Register the orchestrator**

In `WebHomestay/Program.cs`, after the existing `IAIBrainOrchestrator` registration, add:

```csharp
builder.Services.AddScoped<WebHomestay.Services.IAIBookingFlowOrchestrator, WebHomestay.Services.AIBookingFlowOrchestrator>();
```

- [ ] **Step 4: Run focused tests**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AIBookingFlowOrchestratorTests
```

Expected: PASS. If tests fail due to constructor mismatch in existing services, update only the test helper constructor calls to match current service constructors.

- [ ] **Step 5: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj -o /tmp/webhomestay-build-check --no-restore
```

Expected: build succeeds with existing warnings only.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Services/AIBookingFlowModels.cs WebHomestay/Services/IAIBookingFlowOrchestrator.cs WebHomestay/Services/AIBookingFlowOrchestrator.cs WebHomestay/Program.cs WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs
git commit -m "feat: add deterministic AI booking flow orchestrator"
```

---

## Task 4: Wire public controller to booking flow actions

**Files:**
- Modify: `WebHomestay/Controllers/AIChatController.cs`
- Test: `dotnet build WebHomestay/WebHomestay.csproj -o /tmp/webhomestay-build-check --no-restore`

- [ ] **Step 1: Replace controller dependencies and remove local request DTO**

In `AIChatController`, add field:

```csharp
private readonly IAIBookingFlowOrchestrator _bookingFlowOrchestrator;
```

Change constructor to:

```csharp
public AIChatController(IAIBrainOrchestrator aiBrainOrchestrator, ApplicationDbContext context, IAIBookingFlowOrchestrator bookingFlowOrchestrator)
{
    _aiBrainOrchestrator = aiBrainOrchestrator;
    _context = context;
    _bookingFlowOrchestrator = bookingFlowOrchestrator;
}
```

Delete the controller-local `PublicAIChatRequest` class at the bottom of the file because Task 1 moved it into `AIBookingFlowModels.cs`.

- [ ] **Step 2: Include pre-chat context in `Chat` state**

In the successful response path, replace the `state = new AIBookingSessionState { ... }` object with:

```csharp
var state = new AIBookingSessionState
{
    CustomerName = request.CustomerName?.Trim() ?? string.Empty,
    BranchId = branchId,
    BookingMode = request.BookingMode == "daily" ? "daily" : "hourly",
    HourlyDate = hasDate ? parsedDate : null,
    CheckInDate = request.BookingMode == "daily" && hasDate ? parsedDate : null,
    CheckOutDate = request.BookingMode == "daily" && hasDate ? parsedDate.AddDays(1) : null,
    GuestCount = guestCount
};
```

Then replace manual `uiBlocks` room-card query with:

```csharp
var flowResponse = branchId.HasValue && hasDate
    ? await _bookingFlowOrchestrator.BuildRoomCardsAsync(state, cancellationToken)
    : new AIBookingFlowResponse { CurrentStep = "chat", Message = response.Answer, State = state };
```

Return:

```csharp
return Ok(new
{
    answer = response.Answer,
    message = response.Answer,
    sessionId,
    currentStep = flowResponse.CurrentStep,
    state = flowResponse.State,
    uiBlocks = flowResponse.UiBlocks,
    formSchema = response.FormSchema
});
```

- [ ] **Step 3: Add booking-action endpoint**

Add this action inside `AIChatController`:

```csharp
[HttpPost("booking-action")]
public async Task<IActionResult> BookingAction([FromBody] AIBookingActionRequest request, CancellationToken cancellationToken)
{
    if (request == null || string.IsNullOrWhiteSpace(request.Action))
    {
        return BadRequest(new AIBookingFlowResponse
        {
            CurrentStep = "error",
            Message = "Thao tác không hợp lệ.",
            State = new AIBookingSessionState()
        });
    }

    try
    {
        var response = await _bookingFlowOrchestrator.HandleActionAsync(request, cancellationToken);
        response.SessionId = request.SessionId;
        return Ok(response);
    }
    catch
    {
        return StatusCode(503, new AIBookingFlowResponse
        {
            SessionId = request.SessionId,
            CurrentStep = "error",
            Message = "Xin lỗi, thao tác đặt phòng đang tạm thời bận. Bạn thử lại sau ít phút nhé.",
            State = request.State,
            UiBlocks = new List<AIUiBlock>()
        });
    }
}
```

- [ ] **Step 4: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj -o /tmp/webhomestay-build-check --no-restore
```

Expected: build succeeds with existing warnings only.

- [ ] **Step 5: Commit**

```bash
git add WebHomestay/Controllers/AIChatController.cs
git commit -m "feat: connect public AI chat to booking actions"
```

---

## Task 5: Add public pre-chat context form and action wiring

**Files:**
- Modify: `WebHomestay/Views/Shared/_Layout.cshtml`
- Modify: `WebHomestay/wwwroot/js/site.js`
- Modify: `WebHomestay/wwwroot/css/user-premium.css`
- Build: `dotnet build WebHomestay/WebHomestay.csproj -o /tmp/webhomestay-build-check --no-restore`

- [ ] **Step 1: Add pre-chat context markup**

In `_Layout.cshtml`, inside `.ai-chat-panel`, insert this block after `.ai-chat-header` and before `.ai-chat-messages`:

```html
<div class="ai-chat-context">
    <input class="ai-context-name" type="text" maxlength="80" autocomplete="name" placeholder="Tên của bạn" />
    <select class="ai-context-branch" aria-label="Chọn chi nhánh">
        <option value="">Chọn chi nhánh</option>
    </select>
    <select class="ai-context-mode" aria-label="Kiểu thuê">
        <option value="hourly">Thuê theo giờ</option>
        <option value="daily">Thuê theo ngày</option>
    </select>
    <input class="ai-context-guests" type="number" min="1" max="20" value="1" aria-label="Số người" />
</div>
```

- [ ] **Step 2: Add branches endpoint to controller**

In `AIChatController`, add:

```csharp
[HttpGet("branches")]
public async Task<IActionResult> Branches(CancellationToken cancellationToken)
{
    var branches = await _context.Branches
        .OrderBy(b => b.Id)
        .Select(b => new { b.Id, b.Name })
        .ToListAsync(cancellationToken);
    return Ok(branches);
}
```

- [ ] **Step 3: Load branches and send context in `site.js`**

Near existing DOM lookups in `site.js`, add:

```javascript
const contextName = widget.querySelector('.ai-context-name');
const contextBranch = widget.querySelector('.ai-context-branch');
const contextMode = widget.querySelector('.ai-context-mode');
const contextGuests = widget.querySelector('.ai-context-guests');
let bookingState = { bookingMode: 'hourly', guestCount: 1 };
loadBranches();
```

In the `/ai/chat` fetch body, replace the current body with:

```javascript
body: JSON.stringify({
    sessionId,
    message,
    customerName: contextName?.value?.trim() || '',
    branchId: parseOptionalInt(contextBranch?.value),
    bookingMode: contextMode?.value || 'hourly',
    guestCount: parseOptionalInt(contextGuests?.value) || 1
})
```

After parsing response data, add:

```javascript
if (data.state) bookingState = data.state;
```

Add these functions before `createSessionId()`:

```javascript
async function loadBranches() {
    if (!contextBranch) return;
    try {
        const response = await fetch('/ai/branches');
        if (!response.ok) return;
        const branches = await response.json();
        branches.forEach(branch => {
            const option = document.createElement('option');
            option.value = branch.id;
            option.textContent = branch.name;
            contextBranch.appendChild(option);
        });
    } catch { }
}

function parseOptionalInt(value) {
    const parsed = Number.parseInt(value, 10);
    return Number.isNaN(parsed) ? null : parsed;
}
```

- [ ] **Step 4: Wire action buttons**

In `site.js`, after form submit listener, add:

```javascript
messages.addEventListener('click', async event => {
    const button = event.target.closest('[data-ai-action]');
    if (!button) return;
    await postBookingAction(button.dataset.aiAction, {
        roomId: parseOptionalInt(button.dataset.roomId),
        slotId: parseOptionalInt(button.dataset.slotId)
    });
});
```

Add:

```javascript
async function postBookingAction(action, extra) {
    setBusy(true);
    try {
        const response = await fetch('/ai/booking-action', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ sessionId, action, state: bookingState, ...extra })
        });
        const data = await response.json().catch(() => ({}));
        if (data.sessionId) sessionId = data.sessionId;
        if (data.state) bookingState = data.state;
        appendMessage(data.message || 'Mình đã cập nhật lựa chọn của bạn.', 'bot');
        renderUiBlocks(data.uiBlocks || []);
    } catch {
        appendMessage('Xin lỗi, thao tác này đang tạm thời bận. Bạn thử lại sau ít phút nhé.', 'bot');
    } finally {
        setBusy(false);
    }
}
```

- [ ] **Step 5: Render booking summary and submit real form data**

In `renderUiBlocks`, add:

```javascript
if (block.type === 'bookingSummary') renderBookingSummary(block.data || {});
```

Add:

```javascript
function renderBookingSummary(data) {
    const wrapper = appendBlock('ai-booking-summary');
    wrapper.innerHTML = '';
    const items = [
        ['Khách', data.customerName || bookingState.customerName || '-'],
        ['Chi nhánh', data.branchName || '-'],
        ['Phòng', data.roomName || '-'],
        ['Lịch', data.timeline || '-'],
        ['Số khách', data.guestCount || bookingState.guestCount || 1],
        ['Tổng dự kiến', formatMoney(data.totalPrice || 0)]
    ];
    items.forEach(([label, value]) => {
        const row = document.createElement('div');
        row.className = 'ai-summary-row';
        row.innerHTML = `<span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong>`;
        wrapper.appendChild(row);
    });
}
```

Replace `renderBookingForm` with:

```javascript
function renderBookingForm(data) {
    const wrapper = appendBlock('ai-booking-form-block');
    const formElement = document.createElement('form');
    formElement.className = 'ai-booking-form';
    formElement.innerHTML = `
        <input name="customerName" placeholder="Họ tên" value="${escapeAttribute(bookingState.customerName || contextName?.value || '')}" required />
        <input name="customerPhone" placeholder="SĐT/Zalo" required />
        <input name="customerEmail" type="email" placeholder="Email" required />
        <input name="guestCount" type="number" min="1" value="${bookingState.guestCount || 1}" required />
        <textarea name="customerNote" placeholder="Ghi chú"></textarea>
        <button class="ai-chat-send" type="submit">Gửi thông tin</button>`;
    formElement.addEventListener('submit', async event => {
        event.preventDefault();
        const formData = new FormData(formElement);
        await postBookingAction('submit-booking-form', {
            form: {
                customerName: String(formData.get('customerName') || ''),
                customerPhone: String(formData.get('customerPhone') || ''),
                customerEmail: String(formData.get('customerEmail') || ''),
                guestCount: parseOptionalInt(formData.get('guestCount')) || bookingState.guestCount || 1,
                customerNote: String(formData.get('customerNote') || '')
            }
        });
    });
    wrapper.appendChild(formElement);
}

function escapeAttribute(value) {
    return String(value ?? '').replace(/[&<>"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[char]));
}
```

- [ ] **Step 6: Add CSS**

Append to `user-premium.css`:

```css
.ai-chat-context {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 8px;
    padding: 12px;
    border-bottom: 1px solid var(--luxury-border);
    background: #fff;
}
.ai-chat-context input,
.ai-chat-context select {
    border: 1px solid var(--luxury-border);
    border-radius: 12px;
    padding: 9px 10px;
    font-size: .85rem;
}
.ai-booking-summary {
    border: 1px solid var(--luxury-border);
    background: #fffaf5;
    padding: 12px;
}
.ai-summary-row {
    display: flex;
    justify-content: space-between;
    gap: 12px;
    padding: 5px 0;
    font-size: .88rem;
}
.ai-payment-block {
    border: 1px solid #f0d7b7;
    background: #fff8ed;
    padding: 12px;
    font-weight: 700;
}
@media (max-width: 575.98px) {
    .ai-chat-context { grid-template-columns: 1fr; }
}
```

- [ ] **Step 7: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj -o /tmp/webhomestay-build-check --no-restore
```

Expected: build succeeds with existing warnings only.

- [ ] **Step 8: Commit**

```bash
git add WebHomestay/Views/Shared/_Layout.cshtml WebHomestay/wwwroot/js/site.js WebHomestay/wwwroot/css/user-premium.css WebHomestay/Controllers/AIChatController.cs
git commit -m "feat: add public AI booking context and actions"
```

---

## Task 6: Compact Brain Center into 4 tabs

**Files:**
- Modify: `WebHomestay/Views/AdminAI/Index.cshtml`
- Modify: `WebHomestay/wwwroot/css/admin-ai-brain-center.css`
- Build: `dotnet build WebHomestay/WebHomestay.csproj -o /tmp/webhomestay-build-check --no-restore`

- [ ] **Step 1: Replace tab buttons with 4-tab structure**

In `AdminAI/Index.cshtml`, replace the `brainIdeaTabs` nav buttons with:

```html
<ul class="nav nav-pills brain-tabs" id="brainIdeaTabs" role="tablist">
    <li class="nav-item" role="presentation"><button class="nav-link active" data-bs-toggle="pill" data-bs-target="#tab-overview" type="button">Tổng quan</button></li>
    <li class="nav-item" role="presentation"><button class="nav-link" data-bs-toggle="pill" data-bs-target="#tab-knowledge-graph" type="button">Tri thức & Graph</button></li>
    <li class="nav-item" role="presentation"><button class="nav-link" data-bs-toggle="pill" data-bs-target="#tab-response-form" type="button" onclick="loadFinalSynthesizerConfig(); loadBookingFormConfig();">Trả lời & Form</button></li>
    <li class="nav-item" role="presentation"><button class="nav-link" data-bs-toggle="pill" data-bs-target="#tab-test-trace" type="button" onclick="loadBrainConversationTraces()">Test & Trace</button></li>
</ul>
```

- [ ] **Step 2: Remove the large top n8n workflow section**

Delete the top `<section class="workflow-board" id="brain-flow">...</section>` block. Do not delete Knowledge, Graph, Final Synthesizer, or Trace markup yet; move them into the new tabs in the following steps.

- [ ] **Step 3: Add compact Tổng quan tab**

Create `#tab-overview` content with:

```html
<div class="tab-pane fade show active" id="tab-overview">
    <div class="brain-manager-toolbar">
        <div>
            <h3>AI Booking Flow</h3>
            <p>Luồng AI bán phòng gồm 6 bước. LLM chỉ tổng hợp câu trả lời; dữ liệu phòng, giá, slot và booking lấy từ hệ thống thật.</p>
        </div>
    </div>
    <div class="compact-agent-grid">
        <article><strong>1. Input Context</strong><span>Tên, chi nhánh, kiểu thuê, số người, tin nhắn.</span></article>
        <article><strong>2. Intent & Memory</strong><span>Hiểu nhu cầu và giữ state trong session.</span></article>
        <article><strong>3. Live Availability</strong><span>Đọc phòng, giá, slot/ngày trống từ DB.</span></article>
        <article><strong>4. Knowledge RAG</strong><span>Lấy chính sách, nội quy, sales script.</span></article>
        <article><strong>5. Graph Reasoning</strong><span>Suy luận quan hệ phòng, tiện nghi, persona.</span></article>
        <article><strong>6. Final Synthesizer</strong><span>Viết câu trả lời và trả UI blocks.</span></article>
    </div>
</div>
```

- [ ] **Step 4: Move Knowledge and Graph into one tab**

Wrap existing Knowledge and Graph content in:

```html
<div class="tab-pane fade" id="tab-knowledge-graph">
    <div class="compact-two-stack">
        <section class="compact-panel" id="compact-knowledge-panel">
            <!-- existing Knowledge content goes here -->
        </section>
        <section class="compact-panel" id="compact-graph-panel">
            <!-- existing Graph content goes here -->
        </section>
    </div>
</div>
```

Preserve IDs used by JavaScript: `brain-scope-list`, `brain-knowledge-list`, `brain-graph-nodes`, `brain-graph-edges`, `brain-graph-network`, modal IDs, and save button handlers.

- [ ] **Step 5: Move Final Synthesizer into Trả lời & Form tab**

Wrap existing Final Synthesizer content in:

```html
<div class="tab-pane fade" id="tab-response-form">
    <!-- existing Final Response Synthesizer config + Form Designer + Chatbot Preview goes here -->
</div>
```

Rename visible `Form Designer` title to `Booking Form Designer` and change subtitle to:

```html
<p class="text-muted mb-0">Form này chỉ hiện sau khi khách đã chọn phòng và timeline.</p>
```

- [ ] **Step 6: Move test and trace into Test & Trace tab**

Create:

```html
<div class="tab-pane fade" id="tab-test-trace">
    <div class="brain-manager-toolbar">
        <div>
            <h3>Test public booking flow</h3>
            <p>Test như khách thật: nhập context, nhắn câu hỏi, xem answer và UI blocks.</p>
        </div>
    </div>
    <div class="compact-test-grid">
        <section class="compact-panel">
            <input id="public-ai-test-name" class="form-control mb-2" placeholder="Tên khách" />
            <select id="public-ai-test-branch" class="form-select mb-2"></select>
            <select id="public-ai-test-mode" class="form-select mb-2"><option value="hourly">Theo giờ</option><option value="daily">Theo ngày</option></select>
            <input id="public-ai-test-guests" class="form-control mb-2" type="number" min="1" value="1" />
            <textarea id="public-ai-test-message" class="form-control mb-2" rows="3" placeholder="VD: ngày mai còn phòng không?"></textarea>
            <button class="btn btn-primary rounded-pill fw-bold" onclick="runPublicAIFlowTest()">Chạy test</button>
            <pre id="public-ai-test-output" class="agent-preview mt-3"></pre>
        </section>
        <section class="compact-panel">
            <!-- existing Conversation Trace list/detail goes here -->
        </section>
    </div>
</div>
```

Preserve trace IDs: `conversation-trace-list`, `conversation-trace-detail`.

- [ ] **Step 7: Add compact Brain Center CSS**

Append to `admin-ai-brain-center.css`:

```css
.compact-agent-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
    gap: 14px;
}
.compact-agent-grid article,
.compact-panel {
    border: 1px solid #dfe7f2;
    background: #fff;
    border-radius: 20px;
    padding: 18px;
    box-shadow: 0 14px 30px rgba(15, 23, 42, .06);
}
.compact-agent-grid strong {
    display: block;
    color: #0f172a;
    margin-bottom: 6px;
}
.compact-agent-grid span {
    color: #64748b;
    font-size: .92rem;
}
.compact-two-stack,
.compact-test-grid {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: 18px;
}
@media (min-width: 992px) {
    .compact-test-grid { grid-template-columns: 420px minmax(0, 1fr); }
}
```

- [ ] **Step 8: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj -o /tmp/webhomestay-build-check --no-restore
```

Expected: build succeeds with existing warnings only.

- [ ] **Step 9: Commit**

```bash
git add WebHomestay/Views/AdminAI/Index.cshtml WebHomestay/wwwroot/css/admin-ai-brain-center.css
git commit -m "feat: compact AI Brain Center into four tabs"
```

---

## Task 7: Add Booking Form config and admin public flow test

**Files:**
- Modify: `WebHomestay/Controllers/AdminAIController.cs`
- Modify: `WebHomestay/wwwroot/js/admin-ai-brain-center.js`
- Build: `dotnet build WebHomestay/WebHomestay.csproj -o /tmp/webhomestay-build-check --no-restore`

- [ ] **Step 1: Add booking form config endpoints**

In `AdminAIController`, add:

```csharp
[HttpGet("booking-form-config")]
public async Task<IActionResult> GetBookingFormConfig()
{
    var value = await _context.SystemSettings
        .Where(s => s.GroupName == "AI" && s.SettingKey == "AIBookingFormSchema")
        .Select(s => s.SettingValue)
        .FirstOrDefaultAsync();

    return Ok(new
    {
        formSchema = string.IsNullOrWhiteSpace(value) ? DefaultBookingFormSchema : value
    });
}

[HttpPost("booking-form-config")]
public async Task<IActionResult> SaveBookingFormConfig([FromBody] BookingFormConfigRequest request)
{
    await UpsertAISetting("AIBookingFormSchema", request.FormSchema ?? DefaultBookingFormSchema, "Cấu hình form đặt phòng cuối luồng AI Booking");
    await _context.SaveChangesAsync();
    return Ok(new { success = true });
}

private const string DefaultBookingFormSchema = "[{\"key\":\"customerName\",\"label\":\"Họ tên\",\"type\":\"text\",\"required\":true,\"helpText\":\"\",\"order\":1},{\"key\":\"customerPhone\",\"label\":\"SĐT/Zalo\",\"type\":\"tel\",\"required\":true,\"helpText\":\"\",\"order\":2},{\"key\":\"customerEmail\",\"label\":\"Email\",\"type\":\"email\",\"required\":true,\"helpText\":\"\",\"order\":3},{\"key\":\"guestCount\",\"label\":\"Số khách\",\"type\":\"number\",\"required\":true,\"helpText\":\"\",\"order\":4},{\"key\":\"idCardFront\",\"label\":\"CCCD mặt trước\",\"type\":\"image\",\"required\":false,\"helpText\":\"Có thể bổ sung ở trang xác nhận.\",\"order\":5},{\"key\":\"idCardBack\",\"label\":\"CCCD mặt sau\",\"type\":\"image\",\"required\":false,\"helpText\":\"Có thể bổ sung ở trang xác nhận.\",\"order\":6},{\"key\":\"customerNote\",\"label\":\"Ghi chú\",\"type\":\"textarea\",\"required\":false,\"helpText\":\"\",\"order\":7}]";
```

Add request class near other request models or at bottom of controller file:

```csharp
public class BookingFormConfigRequest
{
    public string? FormSchema { get; set; }
}
```

- [ ] **Step 2: Add admin JS for booking form config**

In `admin-ai-brain-center.js`, add:

```javascript
let bookingFormFields = [];

async function loadBookingFormConfig() {
    try {
        const response = await fetch('/admin/ai/booking-form-config');
        if (!response.ok) throw new Error(await response.text());
        const config = await response.json();
        bookingFormFields = JSON.parse(config.formSchema || '[]');
        renderBookingFormConfigPreview();
    } catch (err) {
        alert(`Không tải được Booking Form config: ${err.message}`);
    }
}

async function saveBookingFormConfig() {
    const response = await fetch('/admin/ai/booking-form-config', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ formSchema: JSON.stringify(bookingFormFields) })
    });
    if (!response.ok) return alert(`Không lưu được Booking Form config: ${await response.text()}`);
    alert('Đã lưu Booking Form config.');
}

function renderBookingFormConfigPreview() {
    const preview = $('#final-chat-form-preview');
    if (!preview.length) return;
    preview.html(`
        <div class="chat-form-card">
            ${bookingFormFields.sort((a, b) => (a.order || 0) - (b.order || 0)).map(field => `
                <label>${escapeBrainHtml(field.label || field.key)}${field.required ? ' <span class="text-danger">*</span>' : ''}
                    ${field.type === 'textarea' ? '<textarea rows="2"></textarea>' : `<input type="${escapeBrainHtml(field.type || 'text')}" />`}
                    ${field.helpText ? `<small class="text-muted">${escapeBrainHtml(field.helpText)}</small>` : ''}
                </label>
            `).join('')}
            <button class="btn btn-dark rounded-pill w-100 mt-2">Gửi thông tin</button>
        </div>
    `);
}
```

- [ ] **Step 3: Add public flow test JS**

In `admin-ai-brain-center.js`, add:

```javascript
async function runPublicAIFlowTest() {
    const output = $('#public-ai-test-output');
    output.text('Đang chạy public AI flow...');
    const payload = {
        sessionId: `admin-test-${Date.now()}`,
        customerName: $('#public-ai-test-name').val() || '',
        branchId: parseAgentOptionalInt($('#public-ai-test-branch').val()),
        bookingMode: $('#public-ai-test-mode').val() || 'hourly',
        guestCount: parseInt($('#public-ai-test-guests').val() || '1', 10),
        message: $('#public-ai-test-message').val() || ''
    };
    try {
        const response = await fetch('/ai/chat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const result = await response.json();
        output.text(JSON.stringify(result, null, 2));
    } catch (err) {
        output.text(`Public flow test failed:\n${err.message}`);
    }
}
```

- [ ] **Step 4: Populate public test branch select**

In `loadBrainKnowledge()` success path or document ready, add a call:

```javascript
loadPublicAIFlowTestBranches();
```

Add:

```javascript
async function loadPublicAIFlowTestBranches() {
    const select = $('#public-ai-test-branch');
    if (!select.length) return;
    const response = await fetch('/ai/branches');
    if (!response.ok) return;
    const branches = await response.json();
    select.html('<option value="">Tự nhận diện từ tin nhắn</option>' + branches.map(branch => `<option value="${branch.id}">${escapeBrainHtml(branch.name)}</option>`).join(''));
}
```

- [ ] **Step 5: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj -o /tmp/webhomestay-build-check --no-restore
```

Expected: build succeeds with existing warnings only.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Controllers/AdminAIController.cs WebHomestay/wwwroot/js/admin-ai-brain-center.js
git commit -m "feat: add AI booking form config and admin flow test"
```

---

## Task 8: Manual verification and full test pass

**Files:**
- No planned code changes; fix only bugs found during verification.

- [ ] **Step 1: Run full test suite**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 2: Run app**

Run:

```bash
dotnet run --project WebHomestay/WebHomestay.csproj
```

Expected: app starts and auto-migration completes. Do not proceed if startup fails.

- [ ] **Step 3: Verify public hourly AI flow**

In browser:
1. Open public home page.
2. Open AI chat widget.
3. Fill context: name `An`, branch `Sài Gòn`, mode `Thuê theo giờ`, guests `2`.
4. Send: `ngày mai còn phòng không`.
5. Expected: AI text answer + room cards filtered to selected branch.
6. Click `Chọn phòng này`.
7. Expected: slot buttons appear; no booking exists yet.
8. Click a slot.
9. Expected: booking summary + booking form appear.
10. Submit form with name, phone, email, guest count.
11. Expected: payment block appears and booking exists as `AwaitingPayment`, `Unpaid`.

- [ ] **Step 4: Verify public daily AI flow**

In browser:
1. Set mode `Thuê theo ngày`.
2. Send: `ngày mai còn phòng không`.
3. Expected: room cards appear with daily pricing emphasis.
4. Select room.
5. Expected: daily selection/summary path appears, or AI asks for date range if missing.
6. Complete form.
7. Expected: daily booking created with `AwaitingPayment`, `Unpaid`.

- [ ] **Step 5: Verify Brain Center 4 tabs**

Open `/admin/ai` as SuperAdmin:
1. `Tổng quan` shows 6 compact workflow cards.
2. `Tri thức & Graph` loads Knowledge and Graph without broken JS IDs.
3. `Trả lời & Form` loads Final Synthesizer and Booking Form preview.
4. `Test & Trace` can call public flow and show JSON result.
5. Conversation trace list/detail still loads.

- [ ] **Step 6: Stop app and commit fixes if any**

If verification required bug fixes:

```bash
git add WebHomestay WebHomestay.Tests
git commit -m "fix: stabilize AI booking flow verification"
```

If no fixes were needed, do not create an empty commit.

---

## Self-review

- Spec coverage: This plan implements the agreed public pre-chat context without the removed “Hỗ trợ đặt phòng/Tư vấn” mode, deterministic room/slot/form/payment flow, `AwaitingPayment` booking creation, compact 4-tab Brain Center, and tests.
- Placeholder scan: No `TBD`, `TODO`, or “implement later” instructions remain. Every code-changing step includes concrete code or exact replacement guidance.
- Type consistency: DTO names used in controller, JS, orchestrator, and tests match: `AIBookingSessionState`, `AIBookingActionRequest`, `AIBookingFormSubmission`, `AIBookingFlowResponse`, `AIUiBlock`, `AIRoomCard`, `AISlotOption`, `AIDailyRoomOption`, `AIBookingSummaryBlock`, `AIPaymentBlock`.
- Scope check: This is one coherent vertical slice: public booking flow first, then compact admin control room around the same endpoints. It does not introduce unrelated refactors.

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-17-ai-booking-flow-brain-center-v2.md`.

Two execution options:

1. **Subagent-Driven (recommended)** - Dispatch a fresh subagent per task, review between tasks, fast iteration.

2. **Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
