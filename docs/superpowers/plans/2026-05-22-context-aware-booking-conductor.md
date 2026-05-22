# Context-Aware Booking Conductor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the rigid Booking Conductor with a context-aware version that distinguishes message intent, splits confirmed/progress state, supports proper booking-action endpoint, and resets progress on off-topic questions.

**Architecture:** New `ContextAwareBookingConductor` class (implementing `IBookingConductor`) handles decision logic, state management, and booking actions. `AIBrainOrchestrator` delegates to it. A decision matrix maps `MessageIntent × State → Action`. Anti-spam counters prevent infinite room-show loops.

**Tech Stack:** ASP.NET Core MVC net10.0, EF Core + Npgsql, xUnit + InMemory

---

### File Structure

| File | Responsibility |
|---|---|
| `Services/IBookingConductor.cs` | Interface: `DecideAsync`, `HandleActionAsync` |
| `Services/ContextAwareBookingConductor.cs` | Intent classifier, decision engine, state manager |
| `Services/AIBookingFlowModels.cs` | Add `BookingConfirmedState`, `BookingProgressState`, `BookingSessionContainer` |
| `Services/AIBrainOrchestrator.cs` | Inject `IBookingConductor`, remove old conductor methods, add `HandleBookingActionAsync` |
| `Controllers/AIChatController.cs` | Add `POST /ai/booking-action` |
| `Controllers/AdminAIController.cs` | Add config endpoints for 6 new settings |
| `Views/AdminAI/Index.cshtml` | Expanded Public Booking Config tab |
| `wwwroot/js/admin-ai-brain-center.js` | Load/save new config fields |
| `wwwroot/js/site.js` | Handle booking-action responses client-side |
| `Tests/Services/ContextAwareBookingConductorTests.cs` | Unit tests for decision matrix, state, anti-spam |
| `AGENTS.md` | Add new settings keys |

---

### Task 1: Add container classes to AIBookingFlowModels.cs

**Files:**
- Modify: `WebHomestay/Services/AIBookingFlowModels.cs`

- [ ] **Step 1: Read existing file**

Read `WebHomestay/Services/AIBookingFlowModels.cs` to see existing classes.

- [ ] **Step 2: Add `BookingConfirmedState`, `BookingProgressState`, `BookingSessionContainer`**

Append after the last class in the file:

```csharp
public class BookingConfirmedState
{
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public DateOnly? HourlyDate { get; set; }
    public DateOnly? CheckInDate { get; set; }
    public DateOnly? CheckOutDate { get; set; }
    public int GuestCount { get; set; }
    public string BookingMode { get; set; } = "hourly";
}

public class BookingProgressState
{
    public int? SelectedRoomId { get; set; }
    public int? SelectedSlotId { get; set; }
    public string? SelectedSlotLabel { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public int? BookingId { get; set; }
}

public class BookingSessionContainer
{
    public BookingConfirmedState Confirmed { get; set; } = new();
    public BookingProgressState? Progress { get; set; }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build WebHomestay/WebHomestay.csproj 2>&1 | Select-Object -Last 5`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add WebHomestay/Services/AIBookingFlowModels.cs
git commit -m "feat(ai): add BookingConfirmedState, BookingProgressState, BookingSessionContainer"
```

---

### Task 2: Create IBookingConductor interface + MessageIntent enum

**Files:**
- Create: `WebHomestay/Services/IBookingConductor.cs`

- [ ] **Step 1: Create the interface file**

```csharp
namespace WebHomestay.Services;

public enum MessageIntent
{
    BookingIntent,
    BrowsingRooms,
    PolicyQuestion,
    OffTopic,
    PriceQuestion,
    LocationQuestion,
    Compared,
    Exit
}

public enum ConductorAction
{
    Reply,
    AskInfo,
    ShowRooms,
    ShowSlots,
    ShowForm,
    AutoBook,
    PaymentQr
}

public class ConductorResult
{
    public ConductorAction Action { get; set; }
    public BookingSessionContainer State { get; set; } = new();
    public List<object> UiBlocks { get; set; } = new();
    public string? Reason { get; set; }
}

public class BookingActionRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // "select-room" | "select-slot" | "submit-form"
    public int? RoomId { get; set; }
    public int? SlotId { get; set; }
    public Dictionary<string, string>? FormData { get; set; }
}

public class BookingActionResult
{
    public string Answer { get; set; } = string.Empty;
    public ConductorAction Action { get; set; }
    public BookingSessionContainer State { get; set; } = new();
    public List<object> UiBlocks { get; set; } = new();
}

public interface IBookingConductor
{
    Task<ConductorResult> DecideAsync(
        string sessionId,
        string message,
        AIBrainChatRequest request,
        CancellationToken cancellationToken);

    Task<BookingActionResult> HandleActionAsync(
        BookingActionRequest actionRequest,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build WebHomestay/WebHomestay.csproj 2>&1 | Select-Object -Last 5`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add WebHomestay/Services/IBookingConductor.cs
git commit -m "feat(ai): add IBookingConductor interface and models"
```

---

### Task 3: Create ContextAwareBookingConductor

**Files:**
- Create: `WebHomestay/Services/ContextAwareBookingConductor.cs`

- [ ] **Step 1: Create the class with constructor + fields**

```csharp
using Microsoft.Extensions.Caching.Memory;
using WebHomestay.Data;

namespace WebHomestay.Services;

public class ContextAwareBookingConductor : IBookingConductor
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private const string CacheKeyPrefix = "ai-booking-conductor:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    public ContextAwareBookingConductor(
        ApplicationDbContext context,
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    private string CacheKey(string sessionId) => $"{CacheKeyPrefix}{sessionId}";

    private BookingSessionContainer? GetCachedState(string sessionId)
        => _cache.TryGetValue(CacheKey(sessionId), out BookingSessionContainer? state) ? state : null;

    private void CacheState(string sessionId, BookingSessionContainer state)
        => _cache.Set(CacheKey(sessionId), state, CacheTtl);
}
```

- [ ] **Step 2: Implement `DecideAsync`**

```csharp
public async Task<ConductorResult> DecideAsync(
    string sessionId,
    string message,
    AIBrainChatRequest request,
    CancellationToken cancellationToken)
{
    var container = GetCachedState(sessionId) ?? new BookingSessionContainer();
    container.Confirmed = MergeFromRequest(container.Confirmed, request);

    var intent = ClassifyIntent(message, container);
    var showCount = GetShowCount(sessionId);

    var action = ResolveAction(intent, container, showCount);
    var uiBlocks = new List<object>();

    if (action == ConductorAction.ShowRooms)
    {
        uiBlocks = await BuildRoomCardsAsync(container.Confirmed, cancellationToken);
        IncrementShowCount(sessionId);
    }

    if (action == ConductorAction.Reply && intent == MessageIntent.Exit)
    {
        container.Progress = null;
    }

    if (action == ConductorAction.Reply && intent is MessageIntent.PolicyQuestion or MessageIntent.OffTopic or MessageIntent.Compared)
    {
        container.Progress = null;
    }

    CacheState(sessionId, container);

    return new ConductorResult
    {
        Action = action,
        State = container,
        UiBlocks = uiBlocks,
        Reason = $"intent={intent}, showCount={showCount}"
    };
}
```

- [ ] **Step 3: Implement `ClassifyIntent`**

```csharp
private MessageIntent ClassifyIntent(string message, BookingSessionContainer container)
{
    var lowered = message.ToLowerInvariant().Trim();

    var exitKeywords = GetSetting("AIPublicBookingExitKeywords", "thôi,bỏ,khác,xóa,hủy,không,để sau")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (exitKeywords.Any(k => lowered.Contains(k)))
        return MessageIntent.Exit;

    if (lowered.Contains("chính sách") || lowered.Contains("hủy") || lowered.Contains("check-in")
        || lowered.Contains("check out") || lowered.Contains("trả phòng") || lowered.Contains("giờ nhận")
        || lowered.Contains("hoàn") || lowered.Contains("refund"))
        return MessageIntent.PolicyQuestion;

    if (lowered.Contains("cảm ơn") || lowered.Contains("hello") || lowered.Contains("hi")
        || lowered.Contains("chào") || lowered.Contains("thanks") || lowered.Contains("ok")
        || lowered.StartsWith("bạn tên") || lowered.StartsWith("bạn là"))
        return MessageIntent.OffTopic;

    if (lowered.Contains(" ở đâu") || lowered.Contains("gần") || lowered.Contains("địa chỉ")
        || lowered.Contains("chỗ") || lowered.Contains("vị trí"))
        return MessageIntent.LocationQuestion;

    if (lowered.Contains(" vs ") || lowered.Contains(" so với ") || lowered.Contains(" khác gì ")
        || lowered.Contains("hay") || lowered.Contains("phòng nào rộng") || lowered.Contains("phòng nào đẹp"))
        return MessageIntent.Compared;

    if (lowered.Contains("giá") || lowered.Contains("bao nhiêu") || lowered.Contains("tiền")
        || lowered.Contains("rẻ") || lowered.Contains("đắt") || lowered.Contains("chi phí"))
        return MessageIntent.PriceQuestion;

    var triggerWords = GetSetting("AIPublicBookingTriggerWords", "đặt,chốt,lấy,book,giữ phòng,giữ chỗ")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (triggerWords.Any(w => lowered.Contains(w)))
        return MessageIntent.BookingIntent;

    if (lowered.Contains("xem phòng") || lowered.Contains("còn phòng") || lowered.Contains("có phòng")
        || lowered.Contains("phòng trống") || lowered.Contains("cho xem"))
        return MessageIntent.BrowsingRooms;

    // Default: treat as browsing if we have confirmed info, else off-topic
    if (container.Confirmed.BranchId.HasValue)
        return MessageIntent.BrowsingRooms;

    return MessageIntent.OffTopic;
}
```

- [ ] **Step 4: Implement `ResolveAction`**

```csharp
private bool HasEnoughInfo(BookingConfirmedState state)
    => state.BranchId.HasValue
       && (state.HourlyDate.HasValue || state.CheckInDate.HasValue)
       && state.GuestCount > 0;

private ConductorAction ResolveAction(MessageIntent intent, BookingSessionContainer container, int showCount)
{
    var autoShow = GetSetting("AIPublicBookingAutoShowRooms", "true") == "true";
    var maxShows = int.Parse(GetSetting("AIPublicBookingMaxRoomShows", "2"));
    var cooldown = int.Parse(GetSetting("AIPublicBookingRoomCooldown", "3"));
    var mode = GetSetting("AIPublicBookingProactiveMode", "balanced");

    switch (intent)
    {
        case MessageIntent.BookingIntent:
            return HasEnoughInfo(container.Confirmed)
                ? ConductorAction.ShowRooms
                : ConductorAction.AskInfo;

        case MessageIntent.BrowsingRooms:
            if (!HasEnoughInfo(container.Confirmed))
                return ConductorAction.AskInfo;
            if (!autoShow)
                return ConductorAction.Reply;
            if (showCount >= maxShows)
                return ConductorAction.Reply;
            return ConductorAction.ShowRooms;

        case MessageIntent.PriceQuestion:
            if (!HasEnoughInfo(container.Confirmed))
                return ConductorAction.AskInfo;
            if (mode == "conservative")
                return ConductorAction.Reply;
            if (showCount >= maxShows)
                return ConductorAction.Reply;
            return ConductorAction.ShowRooms;

        case MessageIntent.PolicyQuestion:
        case MessageIntent.OffTopic:
        case MessageIntent.LocationQuestion:
        case MessageIntent.Compared:
        case MessageIntent.Exit:
            return ConductorAction.Reply;

        default:
            return ConductorAction.Reply;
    }
}
```

- [ ] **Step 5: Implement `MergeFromRequest`**

```csharp
private BookingConfirmedState MergeFromRequest(BookingConfirmedState state, AIBrainChatRequest request)
{
    if (request.BranchId.HasValue) state.BranchId = request.BranchId;
    if (request.StartTime.HasValue)
    {
        state.HourlyDate = DateOnly.FromDateTime(request.StartTime.Value);
        state.CheckInDate = DateOnly.FromDateTime(request.StartTime.Value);
    }
    if (request.EndTime.HasValue)
        state.CheckOutDate = DateOnly.FromDateTime(request.EndTime.Value);
    if (request.GuestCount > 0)
        state.GuestCount = request.GuestCount;
    return state;
}
```

- [ ] **Step 6: Implement `HandleActionAsync`**

```csharp
public async Task<BookingActionResult> HandleActionAsync(
    BookingActionRequest actionRequest,
    CancellationToken cancellationToken)
{
    var container = GetCachedState(actionRequest.SessionId) ?? new BookingSessionContainer();
    container.Progress ??= new BookingProgressState();

    switch (actionRequest.Action)
    {
        case "select-room":
            container.Progress.SelectedRoomId = actionRequest.RoomId;
            CacheState(actionRequest.SessionId, container);
            // Return slots UI
            if (container.Confirmed.HourlyDate.HasValue)
            {
                return await BuildSlotsResponse(container, cancellationToken);
            }
            return new BookingActionResult
            {
                Answer = "Bạn muốn đặt phòng theo giờ hay theo ngày?",
                Action = ConductorAction.AskInfo,
                State = container
            };

        case "select-slot":
            container.Progress.SelectedSlotId = actionRequest.SlotId;
            CacheState(actionRequest.SessionId, container);
            return new BookingActionResult
            {
                Answer = "Bạn điền thông tin để mình tiến hành đặt phòng nhé.",
                Action = ConductorAction.ShowForm,
                State = container,
                UiBlocks = new List<object> { new { type = "bookingForm" } }
            };

        case "submit-form":
            if (actionRequest.FormData != null)
            {
                container.Progress.CustomerName = actionRequest.FormData.GetValueOrDefault("customerName");
                container.Progress.CustomerPhone = actionRequest.FormData.GetValueOrDefault("customerPhone");
                container.Progress.CustomerEmail = actionRequest.FormData.GetValueOrDefault("customerEmail");
            }
            CacheState(actionRequest.SessionId, container);
            return new BookingActionResult
            {
                Answer = "Cảm ơn bạn! Mình đang tiến hành đặt phòng. Bạn vui lòng chuyển khoản theo mã QR bên dưới để giữ chỗ nhé.",
                Action = ConductorAction.PaymentQr,
                State = container,
                UiBlocks = new List<object> { new { type = "paymentQr" } }
            };

        default:
            return new BookingActionResult
            {
                Answer = "Xin lỗi, mình chưa hiểu thao tác này.",
                Action = ConductorAction.Reply,
                State = container
            };
    }
}
```

- [ ] **Step 7: Implement helper methods (BuildSlotsResponse, show count tracking, GetSetting)**

```csharp
private async Task<BookingActionResult> BuildSlotsResponse(BookingSessionContainer container, CancellationToken cancellationToken)
{
    // Query available slots from RoomSlotInventories
    var date = container.Confirmed.HourlyDate ?? container.Confirmed.CheckInDate;
    if (!date.HasValue || !container.Confirmed.BranchId.HasValue || container.Progress?.SelectedRoomId == null)
    {
        return new BookingActionResult
        {
            Answer = "Vui lòng chọn ngày và phòng trước.",
            Action = ConductorAction.AskInfo,
            State = container
        };
    }

    // Simple slot building: query slots from DB
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var slots = await db.RoomSlotInventories
        .Where(s => s.RoomId == container.Progress.SelectedRoomId
                    && s.Date == date.Value
                    && s.IsAvailable)
        .OrderBy(s => s.StartTime)
        .Select(s => new
        {
            slotId = s.Id,
            time = s.StartTime.ToString(@"hh\:mm"),
            status = s.IsAvailable ? "available" : "booked"
        })
        .ToListAsync(cancellationToken);

    return new BookingActionResult
    {
        Answer = $"Có {slots.Count} khung giờ trống. Bạn chọn giờ nào?",
        Action = ConductorAction.ShowSlots,
        State = container,
        UiBlocks = new List<object>
        {
            new
            {
                type = "hourlySlots",
                slots = slots.Select(s => new { s.slotId, s.time, s.status }).ToList()
            }
        }
    };
}

private int GetShowCount(string sessionId)
    => _cache.TryGetValue($"show-count:{sessionId}", out int count) ? count : 0;

private void IncrementShowCount(string sessionId)
    => _cache.Set($"show-count:{sessionId}", GetShowCount(sessionId) + 1, CacheTtl);

private string GetSetting(string key, string fallback)
{
    try
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var setting = db.SystemSettings
            .FirstOrDefault(s => s.GroupName == "AI" && s.SettingKey == key);
        return setting != null && !string.IsNullOrWhiteSpace(setting.SettingValue)
            ? setting.SettingValue
            : fallback;
    }
    catch
    {
        return fallback;
    }
}
```

- [ ] **Step 8: Build**

Run: `dotnet build WebHomestay/WebHomestay.csproj 2>&1 | Select-Object -Last 5`
Expected: 0 errors

- [ ] **Step 9: Commit**

```bash
git add WebHomestay/Services/ContextAwareBookingConductor.cs
git commit -m "feat(ai): add ContextAwareBookingConductor with intent classification and decision engine"
```

---

### Task 4: Modify AIBrainOrchestrator

**Files:**
- Modify: `WebHomestay/Services/AIBrainOrchestrator.cs`

- [ ] **Step 1: Add `IBookingConductor` to constructor + field**

Add after existing fields:
```csharp
private readonly IBookingConductor _bookingConductor;
```

Add to constructor param list:
```csharp
IBookingConductor bookingConductor,
```

Add to constructor body:
```csharp
_bookingConductor = bookingConductor;
```

- [ ] **Step 2: Update `ChatAsync` — replace `RunBookingConductorAsync` call**

Find the section that calls `RunBookingConductorAsync` (around line 55-60) and replace with:

```csharp
ConductorResult? conductorResult = null;
if (request.Mode == ChatMode.PublicBooking)
{
    conductorResult = await _bookingConductor.DecideAsync(
        request.SessionId, request.Message, request, cancellationToken);
}
```

Update the `BuildSystemPrompt` call to pass `conductorResult`:

```csharp
var systemPrompt = BuildSystemPrompt(persona, liveSnapshot, knowledgeResult, graphResult,
    guardResult, conductorResult, request.Mode);
```

Update the response construction to populate booking fields:

```csharp
response.BookingAction = conductorResult?.Action.ToString();
response.BookingState = conductorResult?.State;
response.UiBlocks = conductorResult?.UiBlocks;
```

- [ ] **Step 3: Update `BuildSystemPrompt` signature**

Change the method to accept `ConductorResult? conductorResult` and `ChatMode mode` parameters. Add conductor context to the system prompt:

```csharp
if (conductorResult != null)
{
    if (conductorResult.Action == ConductorAction.ShowRooms)
        AppendPromptSection(sb, "Hiện tại", "Khách đang muốn xem phòng. Hãy giới thiệu ngắn gọn các lựa chọn bên dưới. UI rooms đã kèm.");
    else if (conductorResult.Action == ConductorAction.Reply)
        AppendPromptSection(sb, "Hiện tại", "Hãy trả lời tự nhiên, KHÔNG gợi ý phòng hay đặt phòng. Chỉ tư vấn thông tin.");
    else if (conductorResult.Action == ConductorAction.AskInfo)
        AppendPromptSection(sb, "Hiện tại", "Hãy hỏi thông tin còn thiếu (chi nhánh, ngày, số khách) để tư vấn phòng phù hợp.");
}
```

- [ ] **Step 4: Remove old conductor methods**

Delete these methods from `AIBrainOrchestrator.cs`:
- `RunBookingConductorAsync`
- `GetCachedState`
- `CacheState`
- `MergeStateFromRequest`
- `GetPublicBookingTriggerWords`
- `GetPublicBookingMaxTokens`
- `GetPublicBookingPrompt`
- `BuildShowRoomsDecisionAsync`
- `BuildShowSlotsDecisionAsync`
- `BuildAutoBookDecisionAsync`

Also remove the field `private const string CacheKeyPrefix = "ai-booking-conductor:";` and method `private string CacheKey(string sessionId)`.

- [ ] **Step 5: Build**

Run: `dotnet build WebHomestay/WebHomestay.csproj 2>&1 | Select-Object -Last 5`
Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Services/AIBrainOrchestrator.cs
git commit -m "refactor(ai): inject IBookingConductor, remove old RunBookingConductorAsync methods"
```

---

### Task 5: Add POST /ai/booking-action to AIChatController

**Files:**
- Modify: `WebHomestay/Controllers/AIChatController.cs`

- [ ] **Step 1: Inject IBookingConductor**

Add to constructor:

```csharp
private readonly IBookingConductor _bookingConductor;

public AIChatController(
    IAIBrainOrchestrator orchestrator,
    IWebHostEnvironment environment,
    IImageMaskingService maskingService,
    ApplicationDbContext context,
    IBookingConductor bookingConductor)
{
    ...
    _bookingConductor = bookingConductor;
}
```

- [ ] **Step 2: Add the BookingAction endpoint**

```csharp
[HttpPost("booking-action")]
public async Task<IActionResult> BookingAction([FromBody] BookingActionRequest actionRequest, CancellationToken cancellationToken)
{
    if (actionRequest == null || string.IsNullOrWhiteSpace(actionRequest.Action) || string.IsNullOrWhiteSpace(actionRequest.SessionId))
    {
        return BadRequest(new
        {
            answer = "Thao tác không hợp lệ.",
            sessionId = actionRequest?.SessionId ?? string.Empty
        });
    }

    try
    {
        var result = await _bookingConductor.HandleActionAsync(actionRequest, cancellationToken);

        return Ok(new
        {
            answer = result.Answer,
            sessionId = actionRequest.SessionId,
            currentStep = result.Action.ToString(),
            uiBlocks = result.UiBlocks,
            state = result.State
        });
    }
    catch (Exception ex)
    {
        return StatusCode(503, new
        {
            answer = "Xin lỗi, thao tác đặt phòng đang tạm thời bận. Bạn thử lại sau nhé.",
            sessionId = actionRequest.SessionId,
            currentStep = "error"
        });
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build WebHomestay/WebHomestay.csproj 2>&1 | Select-Object -Last 5`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add WebHomestay/Controllers/AIChatController.cs
git commit -m "feat(ai): add POST /ai/booking-action endpoint"
```

---

### Task 6: Register IBookingConductor in DI

**Files:**
- Modify: `WebHomestay/Program.cs`

- [ ] **Step 1: Add DI registration**

Add after `IAIBrainOrchestrator` registration line:

```csharp
builder.Services.AddScoped<WebHomestay.Services.IBookingConductor, WebHomestay.Services.ContextAwareBookingConductor>();
```

- [ ] **Step 2: Build**

Run: `dotnet build WebHomestay/WebHomestay.csproj 2>&1 | Select-Object -Last 5`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add WebHomestay/Program.cs
git commit -m "feat(ai): register IBookingConductor in DI"
```

---

### Task 7: Update AdminAIController config endpoints

**Files:**
- Modify: `WebHomestay/Controllers/AdminAIController.cs`

- [ ] **Step 1: Update `GetPublicBookingConfig` to return new settings**

Add to the response object in `GetPublicBookingConfig`:

```csharp
proactiveMode = GetAISetting(settings, "AIPublicBookingProactiveMode", "balanced"),
autoShowRooms = GetAISetting(settings, "AIPublicBookingAutoShowRooms", "true"),
maxRoomShows = GetAISetting(settings, "AIPublicBookingMaxRoomShows", "2"),
roomCooldown = GetAISetting(settings, "AIPublicBookingRoomCooldown", "3"),
exitKeywords = GetAISetting(settings, "AIPublicBookingExitKeywords", "thôi,bỏ,khác,xóa,hủy,không,để sau"),
personality = GetAISetting(settings, "AIPublicBookingPersonality", "thân thiện, nhiệt tình, như lễ tân khách sạn")
```

- [ ] **Step 2: Update `SavePublicBookingConfig` to save new settings**

Add after the existing UpsertAISetting calls:

```csharp
await UpsertAISetting("AIPublicBookingProactiveMode", request.ProactiveMode ?? "balanced", "Chế độ chủ động của AI (balanced/proactive/conservative)");
await UpsertAISetting("AIPublicBookingAutoShowRooms", request.AutoShowRooms ?? "true", "Tự động show phòng khi có branch+date+browsing intent");
await UpsertAISetting("AIPublicBookingMaxRoomShows", request.MaxRoomShows ?? "2", "Số lần tối đa show rooms trong 1 session");
await UpsertAISetting("AIPublicBookingRoomCooldown", request.RoomCooldown ?? "3", "Số message tạm dừng sau khi show rooms");
await UpsertAISetting("AIPublicBookingExitKeywords", request.ExitKeywords ?? "thôi,bỏ,khác,xóa,hủy,không,để sau", "Từ khoá thoát booking mode (phân cách bằng dấu phẩy)");
await UpsertAISetting("AIPublicBookingPersonality", request.Personality ?? "thân thiện, nhiệt tình, như lễ tân khách sạn", "Tính cách AI dùng trong system prompt");
```

- [ ] **Step 3: Update `PublicBookingConfigRequest` class**

In `FinalSynthesizerConfigRequest.cs`, add new properties to `PublicBookingConfigRequest`:

```csharp
public class PublicBookingConfigRequest
{
    public string? Prompt { get; set; }
    public string? TriggerWords { get; set; }
    public string? MaxTokens { get; set; }
    public string? Timeout { get; set; }
    public string? ProactiveMode { get; set; }
    public string? AutoShowRooms { get; set; }
    public string? MaxRoomShows { get; set; }
    public string? RoomCooldown { get; set; }
    public string? ExitKeywords { get; set; }
    public string? Personality { get; set; }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build WebHomestay/WebHomestay.csproj 2>&1 | Select-Object -Last 5`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add WebHomestay/Controllers/AdminAIController.cs WebHomestay/Services/FinalSynthesizerConfigRequest.cs
git commit -m "feat(ai): add new PublicBooking config fields to AdminAIController"
```

---

### Task 8: Update AdminAI UI (Index.cshtml + admin-ai-brain-center.js)

**Files:**
- Modify: `WebHomestay/Views/AdminAI/Index.cshtml`
- Modify: `WebHomestay/wwwroot/js/admin-ai-brain-center.js`

- [ ] **Step 1: Add new UI fields to the Public Booking Config tab**

Replace the existing tab-pane#tab-public-booking content in `Index.cshtml` with:

```html
<div class="tab-pane fade" id="tab-public-booking">
    <div class="final-synth-grid">
        <div class="final-config-card">
            <div class="agent-test-info mb-3">
                <div class="agent-test-icon"><i class="fas fa-comments"></i></div>
                <div>
                    <h3>Public Booking Config</h3>
                    <p>Cấu hình cho AI Public Booking mode. Khi khách chat ở /ai/chat, AI sẽ dùng các thiết lập này để quyết định hành động.</p>
                </div>
            </div>

            <div class="mb-4">
                <label class="form-label fw-bold">Chế độ chủ động</label>
                <div class="d-flex gap-4">
                    <div class="form-check">
                        <input class="form-check-input" type="radio" name="proactiveMode" id="mode-balanced" value="balanced" checked>
                        <label class="form-check-label" for="mode-balanced">Cân bằng (linh hoạt)</label>
                    </div>
                    <div class="form-check">
                        <input class="form-check-input" type="radio" name="proactiveMode" id="mode-proactive" value="proactive">
                        <label class="form-check-label" for="mode-proactive">Chủ động (luôn gợi ý)</label>
                    </div>
                    <div class="form-check">
                        <input class="form-check-input" type="radio" name="proactiveMode" id="mode-conservative" value="conservative">
                        <label class="form-check-label" for="mode-conservative">Bảo thủ (chỉ khi KH yêu cầu)</label>
                    </div>
                </div>
            </div>

            <div class="row g-3 mb-4">
                <div class="col-md-4">
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" id="public-booking-auto-show" checked>
                        <label class="form-check-label fw-bold" for="public-booking-auto-show">Tự động show phòng</label>
                    </div>
                </div>
                <div class="col-md-4">
                    <label class="form-label fw-bold">Max lần show</label>
                    <input id="public-booking-max-shows" type="number" class="form-control" value="2" min="1" max="10" />
                </div>
                <div class="col-md-4">
                    <label class="form-label fw-bold">Cooldown (messages)</label>
                    <input id="public-booking-cooldown" type="number" class="form-control" value="3" min="0" max="20" />
                </div>
            </div>

            <div class="mb-4">
                <label class="form-label fw-bold">Từ khoá thoát booking</label>
                <input id="public-booking-exit-keywords" class="form-control" placeholder="thôi,bỏ,khác,xóa,hủy,không,để sau" />
                <div class="form-text">Phân cách bằng dấu phẩy. Khi KH dùng từ này, AI thoát booking mode và xoá progress.</div>
            </div>

            <div class="mb-4">
                <label class="form-label fw-bold">Tính cách AI</label>
                <input id="public-booking-personality" class="form-control" placeholder="thân thiện, nhiệt tình, như lễ tân khách sạn" />
            </div>

            <hr class="my-4" />

            <label class="form-label fw-bold">Public Booking Prompt (bổ sung vào system prompt)</label>
            <textarea id="public-booking-prompt" class="form-control final-style-text" rows="6" placeholder="Booking Conductor đã quyết định hành động: {Action}..."></textarea>
            <div class="row g-3 mt-3">
                <div class="col-md-6">
                    <label class="form-label fw-bold">Trigger Words</label>
                    <input id="public-booking-trigger-words" class="form-control" placeholder="đặt,chốt,lấy,book,giữ phòng" />
                </div>
                <div class="col-md-3">
                    <label class="form-label fw-bold">Max Tokens</label>
                    <input id="public-booking-max-tokens" type="number" class="form-control" value="300" />
                </div>
                <div class="col-md-3">
                    <label class="form-label fw-bold">Timeout (giây)</label>
                    <input id="public-booking-timeout" type="number" class="form-control" value="15" />
                </div>
            </div>
            <button class="btn btn-primary rounded-pill fw-bold mt-3" onclick="savePublicBookingConfig()"><i class="fas fa-save me-2"></i>Lưu cấu hình</button>
        </div>
    </div>
</div>
```

- [ ] **Step 2: Update JS load/save functions**

Replace `loadPublicBookingConfig` and `savePublicBookingConfig` in `admin-ai-brain-center.js`:

```javascript
async function loadPublicBookingConfig() {
    try {
        const response = await fetch('/admin/ai/public-booking-config');
        if (!response.ok) throw new Error(await response.text());
        const config = await response.json();
        $('#public-booking-prompt').val(config.prompt || '');
        $('#public-booking-trigger-words').val(config.triggerWords || '');
        $('#public-booking-max-tokens').val(config.maxTokens || '300');
        $('#public-booking-timeout').val(config.timeout || '15');
        // New fields
        $(`input[name="proactiveMode"][value="${config.proactiveMode || 'balanced'}"]`).prop('checked', true);
        $('#public-booking-auto-show').prop('checked', config.autoShowRooms !== 'false');
        $('#public-booking-max-shows').val(config.maxRoomShows || '2');
        $('#public-booking-cooldown').val(config.roomCooldown || '3');
        $('#public-booking-exit-keywords').val(config.exitKeywords || '');
        $('#public-booking-personality').val(config.personality || '');
    } catch (err) {
        alert(`Không tải được Public Booking config: ${err.message}`);
    }
}

async function savePublicBookingConfig() {
    const payload = {
        prompt: $('#public-booking-prompt').val(),
        triggerWords: $('#public-booking-trigger-words').val(),
        maxTokens: $('#public-booking-max-tokens').val(),
        timeout: $('#public-booking-timeout').val(),
        proactiveMode: $('input[name="proactiveMode"]:checked').val(),
        autoShowRooms: $('#public-booking-auto-show').is(':checked') ? 'true' : 'false',
        maxRoomShows: $('#public-booking-max-shows').val(),
        roomCooldown: $('#public-booking-cooldown').val(),
        exitKeywords: $('#public-booking-exit-keywords').val(),
        personality: $('#public-booking-personality').val()
    };
    const response = await fetch('/admin/ai/public-booking-config', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });
    if (!response.ok) return alert(`Không lưu được config: ${await response.text()}`);
    alert('Đã lưu Public Booking config.');
}
```

- [ ] **Step 3: Build**

Run: `dotnet build WebHomestay/WebHomestay.csproj 2>&1 | Select-Object -Last 5`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add WebHomestay/Views/AdminAI/Index.cshtml WebHomestay/wwwroot/js/admin-ai-brain-center.js
git commit -m "feat(ai): expand Public Booking Config UI with mode, cooldown, exit keywords"
```

---

### Task 9: Test ContextAwareBookingConductor

**Files:**
- Create: `WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs`

- [ ] **Step 1: Create test file with helpers**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebHomestay.Data;
using WebHomestay.Services;
using WebHomestay.Models;
using Xunit;

namespace WebHomestay.Tests.Services;

public class ContextAwareBookingConductorTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static IMemoryCache CreateCache()
    {
        return new MemoryCache(new MemoryCacheOptions());
    }

    private static ContextAwareBookingConductor CreateConductor(ApplicationDbContext context, IMemoryCache cache)
    {
        // Use a real scope factory for GetSetting (reads from DB)
        var services = new ServiceCollection();
        services.AddScoped(_ => context);
        services.AddSingleton(cache);
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        return new ContextAwareBookingConductor(context, cache, scopeFactory);
    }

    private static void SeedSetting(ApplicationDbContext context, string key, string value)
    {
        context.SystemSettings.Add(new SystemSetting
        {
            SettingKey = key,
            SettingValue = value,
            GroupName = "AI",
            LastUpdated = DateTime.UtcNow
        });
        context.SaveChanges();
    }

    private static AIBrainChatRequest MakeRequest(string message, int? branchId = null, DateTime? startTime = null, int guestCount = 0)
    {
        return new AIBrainChatRequest
        {
            SessionId = "test-session",
            Message = message,
            BranchId = branchId,
            StartTime = startTime,
            GuestCount = guestCount,
            Mode = ChatMode.PublicBooking
        };
    }
}
```

- [ ] **Step 2: Test BookingIntent when no branch → AskInfo**

Add test method:

```csharp
[Fact]
public async Task BookingIntent_NoBranch_ReturnsAskInfo()
{
    using var context = CreateContext();
    var cache = CreateCache();
    var conductor = CreateConductor(context, cache);
    var request = MakeRequest("đặt phòng", guestCount: 2);

    var result = await conductor.DecideAsync("s1", request.Message, request, CancellationToken.None);

    Assert.Equal(ConductorAction.AskInfo, result.Action);
}
```

- [ ] **Step 3: Test BookingIntent with full info → ShowRooms**

```csharp
[Fact]
public async Task BookingIntent_WithFullInfo_ReturnsShowRooms()
{
    using var context = CreateContext();
    var cache = CreateCache();
    SeedSetting(context, "AIPublicBookingTriggerWords", "đặt,book");
    SeedSetting(context, "AIPublicBookingAutoShowRooms", "true");
    SeedSetting(context, "AIPublicBookingMaxRoomShows", "2");
    SeedSetting(context, "AIPublicBookingRoomCooldown", "3");
    SeedSetting(context, "AIPublicBookingProactiveMode", "balanced");
    var conductor = CreateConductor(context, cache);
    var request = MakeRequest("đặt phòng", branchId: 1, startTime: DateTime.Today.AddDays(1), guestCount: 2);

    var result = await conductor.DecideAsync("s2", request.Message, request, CancellationToken.None);

    Assert.Equal(ConductorAction.ShowRooms, result.Action);
}
```

- [ ] **Step 4: Test PolicyQuestion → Reply (no UI)**

```csharp
[Fact]
public async Task PolicyQuestion_ReturnsReply()
{
    using var context = CreateContext();
    var cache = CreateCache();
    var conductor = CreateConductor(context, cache);
    var request = MakeRequest("chính sách hủy phòng thế nào?", branchId: 1, startTime: DateTime.Today.AddDays(1), guestCount: 2);

    var result = await conductor.DecideAsync("s3", request.Message, request, CancellationToken.None);

    Assert.Equal(ConductorAction.Reply, result.Action);
    Assert.Empty(result.UiBlocks);
}
```

- [ ] **Step 5: Test Exit → Reply + clears Progress**

```csharp
[Fact]
public async Task ExitIntent_ClearsProgress()
{
    using var context = CreateContext();
    var cache = CreateCache();
    var conductor = CreateConductor(context, cache);

    // Set up state with progress
    var container = new BookingSessionContainer
    {
        Confirmed = new BookingConfirmedState { BranchId = 1, GuestCount = 2, BookingMode = "hourly" },
        Progress = new BookingProgressState { SelectedRoomId = 5 }
    };
    cache.Set("ai-booking-conductor:s4", container, TimeSpan.FromMinutes(30));

    var request = MakeRequest("thôi bỏ đi", branchId: 1, startTime: DateTime.Today.AddDays(1), guestCount: 2);

    var result = await conductor.DecideAsync("s4", request.Message, request, CancellationToken.None);

    Assert.Equal(ConductorAction.Reply, result.Action);
    Assert.Null(result.State.Progress);
    Assert.NotNull(result.State.Confirmed.BranchId); // confirmed preserved
}
```

- [ ] **Step 6: Test MaxShowRooms limit**

```csharp
[Fact]
public async Task ShowRooms_ExceedsMax_ReturnsReply()
{
    using var context = CreateContext();
    var cache = CreateCache();
    SeedSetting(context, "AIPublicBookingAutoShowRooms", "true");
    SeedSetting(context, "AIPublicBookingMaxRoomShows", "1");
    SeedSetting(context, "AIPublicBookingProactiveMode", "balanced");
    var conductor = CreateConductor(context, cache);
    var request = MakeRequest("xem phòng", branchId: 1, startTime: DateTime.Today.AddDays(1), guestCount: 2);

    // First call: show rooms (count 0 -> 1)
    var result1 = await conductor.DecideAsync("s5", request.Message, request, CancellationToken.None);
    Assert.Equal(ConductorAction.ShowRooms, result1.Action);

    // Second call: show rooms would be count 1 -> refused (max=1)
    var result2 = await conductor.DecideAsync("s5", request.Message, request, CancellationToken.None);
    Assert.Equal(ConductorAction.Reply, result2.Action);
}
```

- [ ] **Step 7: Run tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~ContextAwareBookingConductorTests 2>&1 | Select-Object -Last 5`
Expected: All tests pass

- [ ] **Step 8: Commit**

```bash
git add WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs
git commit -m "test(ai): add ContextAwareBookingConductor unit tests"
```

---

### Task 10: Update AGENTS.md

**Files:**
- Modify: `AGENTS.md`

- [ ] **Step 1: Add new settings keys**

In the SystemSettings AI config section, add:
```
  - `AIPublicBookingProactiveMode`, `AIPublicBookingAutoShowRooms`, `AIPublicBookingMaxRoomShows`, `AIPublicBookingRoomCooldown`, `AIPublicBookingExitKeywords`, `AIPublicBookingPersonality`
```

- [ ] **Step 2: Update Public AI Chat section**

Update the AGENTS.md description to mention the context-aware conductor:
> "Booking Conductor trạng thái (context-aware, phân loại intent, decision matrix)..."

- [ ] **Step 3: Commit**

```bash
git add AGENTS.md
git commit -m "docs(ai): add new Booking Conductor config keys to AGENTS.md"
```

---

### Task 11: Full build + test verification

**Files:**
- Build: `WebHomestay/WebHomestay.csproj`
- Test: `WebHomestay.Tests/WebHomestay.Tests.csproj`

- [ ] **Step 1: Full build**

Run: `dotnet build WebHomestay/WebHomestay.csproj 2>&1 | Select-Object -Last 5`
Expected: 0 errors

- [ ] **Step 2: Full test run**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj 2>&1 | Select-Object -Last 5`
Expected: All tests pass

- [ ] **Step 3: Final commit**

```bash
git add -A
git commit -m "chore: finalize context-aware booking conductor"
```
