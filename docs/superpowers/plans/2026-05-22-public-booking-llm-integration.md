# Public Booking LLM Integration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Merge public AI chat into `AIBrainOrchestrator` with Booking Conductor agent; remove rigid state machine.

**Architecture:** Add `ChatMode` to `AIBrainChatRequest`; Booking Conductor (rule-based) decides `reply | show_rooms | show_slots | show_form | auto_book`; Final Synthesizer uses this decision; `AIChatController` becomes thin proxy.

**Tech Stack:** ASP.NET Core MVC net10.0, EF Core 8.0 + Npgsql, xUnit + EF InMemory

---
### Task 1: Extend AIBrainChatRequest with Mode

**Files:**
- Modify: `WebHomestay/Services/AIBrainChatRequest.cs`

- [ ] **Step 1: Add ChatMode enum and Mode field**

```csharp
namespace WebHomestay.Services
{
    public enum ChatMode
    {
        AdminAssistant,
        PublicBooking
    }

    public class AIBrainChatRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int GuestCount { get; set; } = 1;
        public ChatMode Mode { get; set; } = ChatMode.AdminAssistant;
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add WebHomestay/Services/AIBrainChatRequest.cs
git commit -m "feat(ai): add ChatMode enum and Mode field to AIBrainChatRequest"
```

---

### Task 2: Extend AIBrainChatResponse with booking fields

**Files:**
- Modify: `WebHomestay/Services/AIBrainChatResponse.cs`

- [ ] **Step 1: Add BookingAction, BookingState, UiBlocks**

```csharp
namespace WebHomestay.Services
{
    public class AIBrainChatResponse
    {
        public Guid TraceId { get; set; }
        public string Answer { get; set; } = string.Empty;
        public string PersonaSummary { get; set; } = string.Empty;
        public string GuardResult { get; set; } = string.Empty;
        public string ModelProvider { get; set; } = string.Empty;
        public bool IsMock { get; set; }
        public string FormSchema { get; set; } = "[]";

        public string BookingAction { get; set; } = "reply";
        public AIBookingSessionState? BookingState { get; set; }
        public List<AIUiBlock> UiBlocks { get; set; } = new();
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add WebHomestay/Services/AIBrainChatResponse.cs
git commit -m "feat(ai): add BookingAction, BookingState, UiBlocks to AIBrainChatResponse"
```

---

### Task 3: Add BookingDecision class

**Files:**
- Create: `WebHomestay/Services/BookingDecision.cs`

- [ ] **Step 1: Create BookingDecision class**

```csharp
namespace WebHomestay.Services
{
    public class BookingDecision
    {
        public string Action { get; set; } = "reply";
        public AIBookingSessionState State { get; set; } = new();
        public string? Reason { get; set; }
        public List<AIUiBlock> UiBlocks { get; set; } = new();
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add WebHomestay/Services/BookingDecision.cs
git commit -m "feat(ai): add BookingDecision model for Booking Conductor output"
```

---

### Task 4: Add Booking Conductor agent to AIBrainOrchestrator

**Files:**
- Modify: `WebHomestay/Services/AIBrainOrchestrator.cs`

- [ ] **Step 1: Inject IMemoryCache and IServiceScopeFactory**

Change constructor:

```csharp
public class AIBrainOrchestrator : IAIBrainOrchestrator
{
    private readonly ApplicationDbContext _context;
    private readonly IAvailabilityService _availabilityService;
    private readonly IAIModelClient _aiModelClient;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public AIBrainOrchestrator(
        ApplicationDbContext context,
        IAvailabilityService availabilityService,
        IAIModelClient aiModelClient,
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _availabilityService = availabilityService;
        _aiModelClient = aiModelClient;
        _cache = cache;
        _scopeFactory = scopeFactory;
    }
}
```

- [ ] **Step 2: Add cache key helper and state methods**

```csharp
private const string CacheKeyPrefix = "ai-booking-conductor:";

private string CacheKey(string sessionId) => $"{CacheKeyPrefix}{sessionId}";
```

- [ ] **Step 3: Modify ChatAsync to run Booking Conductor in PublicBooking mode**

Replace the end of `ChatAsync` — after guardResult, before model call:

```csharp
public async Task<AIBrainChatResponse> ChatAsync(AIBrainChatRequest request, CancellationToken cancellationToken = default)
{
    var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId;
    var conversationHistory = await BuildConversationHistoryAsync(sessionId, cancellationToken);
    var conversationAwareMessage = string.IsNullOrWhiteSpace(conversationHistory)
        ? request.Message
        : $"{conversationHistory}\nKhách vừa nhắn: {request.Message}";
    var enrichedRequest = await EnrichRequestFromConversationAsync(request, conversationAwareMessage, cancellationToken);
    var personaSummary = BuildPersonaSummary(conversationAwareMessage, enrichedRequest.GuestCount);
    var liveSnapshot = await BuildLiveSnapshotAsync(enrichedRequest, cancellationToken);
    var knowledge = await RetrieveKnowledgeAsync(conversationAwareMessage, cancellationToken);
    var graphReasoning = await BuildGraphReasoningAsync(conversationAwareMessage, cancellationToken);
    var guardResult = BuildGuardResult(enrichedRequest);

    // Booking Conductor (only in PublicBooking mode)
    BookingDecision? bookingDecision = null;
    if (request.Mode == ChatMode.PublicBooking)
    {
        bookingDecision = await RunBookingConductorAsync(enrichedRequest, sessionId, cancellationToken);
    }

    var finalConfig = await GetFinalSynthesizerConfigAsync(cancellationToken);
    var filteredFormSchema = FilterFormSchema(finalConfig.FormSchema, enrichedRequest);
    var systemPrompt = BuildSystemPrompt(
        personaSummary, liveSnapshot, knowledge, graphReasoning,
        guardResult, finalConfig, filteredFormSchema, conversationHistory,
        bookingDecision, request.Mode);

    var maxTokens = request.Mode == ChatMode.PublicBooking
        ? GetPublicBookingMaxTokens()
        : 900;

    var modelResponse = await _aiModelClient.CompleteAsync(new AIModelRequest
    {
        SystemPrompt = systemPrompt,
        UserMessage = request.Message,
        Temperature = 0.35m,
        MaxTokens = maxTokens
    }, cancellationToken);

    var trace = new AIConversationTrace
    {
        SessionId = sessionId,
        CustomerMessage = request.Message,
        PersonaSummary = personaSummary,
        LiveSystemSnapshot = liveSnapshot,
        RetrievedKnowledgeJson = JsonSerializer.Serialize(knowledge),
        GraphReasoningJson = JsonSerializer.Serialize(graphReasoning),
        GuardResult = guardResult,
        FinalAnswer = modelResponse.Content,
        ModelProvider = modelResponse.Provider
    };

    _context.AIConversationTraces.Add(trace);
    await _context.SaveChangesAsync(cancellationToken);

    return new AIBrainChatResponse
    {
        TraceId = trace.Id,
        Answer = modelResponse.Content,
        PersonaSummary = personaSummary,
        GuardResult = guardResult,
        ModelProvider = modelResponse.Provider,
        IsMock = modelResponse.IsMock,
        FormSchema = filteredFormSchema,
        BookingAction = bookingDecision?.Action ?? "reply",
        BookingState = bookingDecision?.State,
        UiBlocks = bookingDecision?.UiBlocks ?? new List<AIUiBlock>()
    };
}
```

- [ ] **Step 4: Add RunBookingConductorAsync method**

```csharp
private async Task<BookingDecision> RunBookingConductorAsync(AIBrainChatRequest request, string sessionId, CancellationToken cancellationToken)
{
    // Get or create state from cache
    var state = GetCachedState(sessionId) ?? new AIBookingSessionState();
    state = MergeStateFromRequest(state, request);
    state.BookingMode = string.IsNullOrWhiteSpace(state.BookingMode) || state.BookingMode == "unknown"
        ? "hourly" : state.BookingMode;

    var lowered = request.Message.ToLowerInvariant();
    var triggerWords = GetPublicBookingTriggerWords();
    var hasBookingIntent = triggerWords.Any(w => lowered.Contains(w.ToLowerInvariant()));

    // Rule 1: missing mandatory fields
    if (!state.BranchId.HasValue)
    {
        CacheState(sessionId, state);
        return new BookingDecision
        {
            Action = "reply",
            State = state,
            Reason = "Thiếu chi nhánh, cần hỏi khách."
        };
    }

    if (!request.StartTime.HasValue && !state.HourlyDate.HasValue && !state.CheckInDate.HasValue)
    {
        CacheState(sessionId, state);
        return new BookingDecision
        {
            Action = "reply",
            State = state,
            Reason = "Thiếu thời gian, cần hỏi khách."
        };
    }

    if (state.GuestCount <= 0)
    {
        state.GuestCount = 1;
    }

    // Rule 2: has mandatory + booking intent
    if (hasBookingIntent)
    {
        if (!state.SelectedRoomId.HasValue)
        {
            var roomDecision = await BuildShowRoomsDecisionAsync(state, cancellationToken);
            CacheState(sessionId, roomDecision.State);
            return roomDecision;
        }

        if (string.IsNullOrWhiteSpace(state.SelectedSlotLabel) && state.BookingMode == "hourly")
        {
            var slotDecision = await BuildShowSlotsDecisionAsync(state, cancellationToken);
            CacheState(sessionId, slotDecision.State);
            return slotDecision;
        }

        // auto-book
        var autoDecision = await BuildAutoBookDecisionAsync(state, cancellationToken);
        CacheState(sessionId, autoDecision.State);
        return autoDecision;
    }

    // Rule 3: has mandatory, no clear intent → show rooms
    if (!state.SelectedRoomId.HasValue)
    {
        var roomDecision = await BuildShowRoomsDecisionAsync(state, cancellationToken);
        CacheState(sessionId, roomDecision.State);
        return roomDecision;
    }

    // Default: just reply
    CacheState(sessionId, state);
    return new BookingDecision
    {
        Action = "reply",
        State = state,
        Reason = "Chưa đủ thông tin hoặc chưa rõ intent."
    };
}
```

- [ ] **Step 5: Add helper methods for state cache, room/slot building, auto-book**

```csharp
private AIBookingSessionState? GetCachedState(string sessionId)
{
    return _cache.TryGetValue(CacheKey(sessionId), out AIBookingSessionState? state) ? state : null;
}

private void CacheState(string sessionId, AIBookingSessionState state)
{
    _cache.Set(CacheKey(sessionId), state, TimeSpan.FromMinutes(30));
}

private static AIBookingSessionState MergeStateFromRequest(AIBookingSessionState state, AIBrainChatRequest request)
{
    if (request.BranchId.HasValue) state.BranchId = request.BranchId;
    if (request.StartTime.HasValue) state.HourlyDate = DateOnly.FromDateTime(request.StartTime.Value);
    if (request.GuestCount > 0) state.GuestCount = request.GuestCount;
    return state;
}

private List<string> GetPublicBookingTriggerWords()
{
    try
    {
        var setting = _context.SystemSettings.AsNoTracking()
            .FirstOrDefault(s => s.GroupName == "AI" && s.SettingKey == "AIPublicBookingTriggerWords");
        if (setting != null && !string.IsNullOrWhiteSpace(setting.SettingValue))
        {
            return setting.SettingValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
    }
    catch { }
    return new List<string> { "đặt", "chốt", "lấy", "book", "giữ phòng" };
}

private int GetPublicBookingMaxTokens()
{
    try
    {
        var setting = _context.SystemSettings.AsNoTracking()
            .FirstOrDefault(s => s.GroupName == "AI" && s.SettingKey == "AIPublicBookingMaxTokens");
        if (setting != null && int.TryParse(setting.SettingValue, out var val)) return val;
    }
    catch { }
    return 300;
}

private async Task<BookingDecision> BuildShowRoomsDecisionAsync(AIBookingSessionState state, CancellationToken cancellationToken)
{
    var rooms = await _context.Rooms
        .AsNoTracking()
        .Include(r => r.Amenities)
        .Where(r => r.BranchId == state.BranchId && r.Status == "Available" && r.MaxGuests >= state.GuestCount)
        .OrderBy(r => state.BookingMode == "daily" ? r.PricePerDay : r.PricePerHour)
        .ThenBy(r => r.Name)
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
            DetailsUrl = $"/Rooms/Details/{r.Id}"
        })
        .ToListAsync(cancellationToken);

    return new BookingDecision
    {
        Action = "show_rooms",
        State = state,
        Reason = rooms.Any() ? "Có phòng trống, hiển thị danh sách." : "Không còn phòng trống.",
        UiBlocks = new List<AIUiBlock>
        {
            new() { Type = "roomCards", Data = new { rooms } }
        }
    };
}

private async Task<BookingDecision> BuildShowSlotsDecisionAsync(AIBookingSessionState state, CancellationToken cancellationToken)
{
    var room = await _context.Rooms.FindAsync(new object[] { state.SelectedRoomId!.Value }, cancellationToken);
    if (room == null)
    {
        return new BookingDecision { Action = "reply", State = state, Reason = "Phòng không tồn tại." };
    }

    var slotDate = state.HourlyDate ?? DateOnly.FromDateTime(DateTime.Today);
    var slots = await _context.RoomSlotInventories
        .AsNoTracking()
        .Where(s => s.RoomId == room.Id && s.SlotDate == slotDate && s.Status == "Available")
        .OrderBy(s => s.StartTime)
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
                    TotalPrice = 0m
        });
    }

    return new BookingDecision
    {
        Action = options.Any() ? "show_slots" : "reply",
        State = state,
        Reason = options.Any() ? "Có slot trống." : "Không còn slot trống.",
        UiBlocks = new List<AIUiBlock>
        {
            new() { Type = "hourlySlots", Data = new { slots = options } }
        }
    };
}

private async Task<BookingDecision> BuildAutoBookDecisionAsync(AIBookingSessionState state, CancellationToken cancellationToken)
{
    try
    {
        var room = await _context.Rooms.FindAsync(new object[] { state.SelectedRoomId!.Value }, cancellationToken);
        if (room == null)
        {
            return new BookingDecision { Action = "reply", State = state, Reason = "Phòng không tồn tại." };
        }

        using var scope = _scopeFactory.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingCreationService>();

        var createRequest = new CreateBookingRequest
        {
            RoomId = room.Id,
            BookingMode = state.BookingMode == "daily" ? BookingMode.Daily : BookingMode.Hourly,
            SlotInventoryId = state.SelectedSlotId,
            CheckInDate = state.CheckInDate,
            CheckOutDate = state.CheckOutDate,
            CustomerName = state.CustomerName ?? "Khách từ AI Chat",
            CustomerPhone = "Chưa cập nhật",
            GuestCount = state.GuestCount
        };

        var booking = state.BookingMode == "daily"
            ? await bookingService.CreateDailyBookingAsync(createRequest)
            : await bookingService.CreateHourlyBookingAsync(createRequest);

        state.BookingId = booking.Id;
        state.PaymentStatus = booking.PaymentStatus;

        return new BookingDecision
        {
            Action = "auto_book",
            State = state,
            Reason = "Booking đã được tạo thành công.",
            UiBlocks = new List<AIUiBlock>
            {
                new()
                {
                    Type = "paymentQr",
                    Data = new AIPaymentBlock
                    {
                        BookingId = booking.Id,
                        PaymentStatus = booking.PaymentStatus,
                        Amount = booking.TotalPrice,
                        PaymentUrl = $"/Bookings/Success/{booking.Id}",
                        SuccessUrl = $"/Bookings/Success/{booking.Id}",
                        Instructions = "Bạn qua trang thanh toán để hoàn tất đặt phòng."
                    }
                }
            }
        };
    }
    catch (Exception ex)
    {
        return new BookingDecision
        {
            Action = "reply",
            State = state,
            Reason = $"Lỗi tạo booking: {ex.Message}"
        };
    }
}
```

- [ ] **Step 6: Modify BuildSystemPrompt to accept optional BookingDecision + Mode**

```csharp
private string BuildSystemPrompt(string personaSummary, string liveSnapshot, List<object> knowledge, List<object> graphReasoning, string guardResult, FinalSynthesizerPromptConfig finalConfig, string formSchema, string conversationHistory, BookingDecision? bookingDecision = null, ChatMode mode = ChatMode.AdminAssistant)
{
    var builder = new StringBuilder();
    AppendPromptSection(builder, "Base Prompt", finalConfig.BasePrompt);
    AppendPromptSection(builder, "Language Rule", finalConfig.LanguageRule);
    AppendPromptSection(builder, "Data Truth Rule", finalConfig.DataTruthRule);
    AppendPromptSection(builder, "Missing Info Rule", finalConfig.MissingInfoRule);
    AppendPromptSection(builder, "Booking Rule", finalConfig.BookingRule);
    AppendPromptSection(builder, "Form Rule", finalConfig.FormRule);
    AppendPromptSection(builder, "Payment Rule", finalConfig.PaymentRule);
    AppendPromptSection(builder, "Memory Rule", finalConfig.MemoryRule);
    AppendPromptSection(builder, "Context Format Rule", finalConfig.ContextFormatRule);

    if (mode == ChatMode.PublicBooking && bookingDecision != null)
    {
        var publicPrompt = GetPublicBookingPrompt();
        if (!string.IsNullOrWhiteSpace(publicPrompt))
        {
            builder.AppendLine(publicPrompt
                .Replace("{BookingAction}", bookingDecision.Action)
                .Replace("{BookingState}", JsonSerializer.Serialize(bookingDecision.State)));
        }

        AppendPromptSection(builder, "Booking Action", bookingDecision.Action);
        AppendPromptSection(builder, "Booking State", JsonSerializer.Serialize(bookingDecision.State));

        if (bookingDecision.Action == "auto_book")
        {
            builder.AppendLine("Booking đã được tạo thành công. Hãy thông báo cho khách và hướng dẫn thanh toán. KHÔNG tự bịa thông tin booking.");
        }
        else if (bookingDecision.Action == "show_rooms" || bookingDecision.Action == "show_slots")
        {
            builder.AppendLine("UI blocks đã kèm theo. Hãy trả lời ngắn gọn giới thiệu các lựa chọn.");
        }
        else if (bookingDecision.Action == "show_form")
        {
            builder.AppendLine("Form đã pre-fill. Hãy hướng dẫn khách điền các field còn thiếu.");
        }
    }

    if (!string.IsNullOrWhiteSpace(conversationHistory)) builder.AppendLine(conversationHistory);
    builder.AppendLine($"Final Response Synthesizer Style: {finalConfig.Style}");
    builder.AppendLine($"Final Response Form Schema JSON: {formSchema}");
    builder.AppendLine($"Persona Agent: {personaSummary}");
    builder.AppendLine($"Safety Guard: {guardResult}");
    builder.AppendLine($"Live System Agent JSON: {liveSnapshot}");
    builder.AppendLine($"Knowledge RAG Agent JSON: {JsonSerializer.Serialize(knowledge)}");
    builder.AppendLine($"Graph Reasoning Agent JSON: {JsonSerializer.Serialize(graphReasoning)}");
    return builder.ToString();
}

private string GetPublicBookingPrompt()
{
    try
    {
        var setting = _context.SystemSettings.AsNoTracking()
            .FirstOrDefault(s => s.GroupName == "AI" && s.SettingKey == "AIPublicBookingPrompt");
        if (setting != null && !string.IsNullOrWhiteSpace(setting.SettingValue)) return setting.SettingValue;
    }
    catch { }
    return @"Booking Conductor đã quyết định hành động: {BookingAction}
Booking Session State: {BookingState}
Nếu action = ""reply"": trả lời tự nhiên, không thêm UI.
Nếu action = ""show_rooms"": giới thiệu phòng ngắn gọn. UI rooms đã kèm.
Nếu action = ""show_slots"": giới thiệu khung giờ. UI slots đã kèm.
Nếu action = ""show_form"": hướng dẫn điền form. UI form đã pre-fill.
Nếu action = ""auto_book"": thông báo thành công + hướng dẫn thanh toán.";
}
```

- [ ] **Step 7: Build and verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Step 8: Commit**

```bash
git add WebHomestay/Services/AIBrainOrchestrator.cs
git commit -m "feat(ai): add Booking Conductor agent with PublicBooking mode"
```

---

### Task 5: Rewrite AIChatController

**Files:**
- Modify: `WebHomestay/Controllers/AIChatController.cs`

- [ ] **Step 1: Replace IAIBookingFlowOrchestrator with IAIBrainOrchestrator**

```csharp
using Microsoft.AspNetCore.Mvc;
using WebHomestay.Services;

namespace WebHomestay.Controllers
{
    [Route("ai")]
    public class AIChatController : Controller
    {
        private readonly IAIBrainOrchestrator _orchestrator;
        private readonly IWebHostEnvironment _environment;
        private readonly IImageMaskingService _maskingService;
        private readonly ApplicationDbContext _context;

        public AIChatController(
            IAIBrainOrchestrator orchestrator,
            IWebHostEnvironment environment,
            IImageMaskingService maskingService,
            ApplicationDbContext context)
        {
            _orchestrator = orchestrator;
            _environment = environment;
            _maskingService = maskingService;
            _context = context;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] PublicAIChatRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new
                {
                    answer = "Bạn nhập giúp mình nhu cầu đặt phòng để mình tư vấn nhé.",
                    message = "Bạn nhập giúp mình nhu cầu đặt phòng để mình tư vấn nhé.",
                    sessionId = request?.SessionId ?? string.Empty,
                    currentStep = "reply",
                    uiBlocks = Array.Empty<object>(),
                    state = new AIBookingSessionState()
                });
            }

            try
            {
                var brainRequest = new AIBrainChatRequest
                {
                    SessionId = request.SessionId,
                    Message = request.Message,
                    Mode = ChatMode.PublicBooking,
                    BranchId = request.BranchId,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    GuestCount = request.GuestCount
                };

                var brainResponse = await _orchestrator.ChatAsync(brainRequest, cancellationToken);

                return Ok(new
                {
                    answer = brainResponse.Answer,
                    message = brainResponse.Answer,
                    sessionId = brainResponse.SessionId ?? request.SessionId,
                    currentStep = brainResponse.BookingAction,
                    uiBlocks = brainResponse.UiBlocks,
                    state = brainResponse.BookingState ?? new AIBookingSessionState()
                });
            }
            catch
            {
                return StatusCode(503, new
                {
                    answer = "Xin lỗi, mình chưa kiểm tra được tình trạng phòng lúc này. Bạn thử lại sau ít phút nhé.",
                    message = "Xin lỗi, mình chưa kiểm tra được tình trạng phòng lúc này. Bạn thử lại sau ít phút nhé.",
                    sessionId = request.SessionId,
                    currentStep = "error",
                    uiBlocks = Array.Empty<object>(),
                    state = new AIBookingSessionState()
                });
            }
        }

        [HttpPost("booking-id-card")]
        public async Task<IActionResult> UploadBookingIdCard(int bookingId, IFormFile? idCardFront, IFormFile? idCardBack)
        {
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null) return NotFound(new { message = "Không tìm thấy đơn đặt phòng." });

            if (idCardFront != null && idCardFront.Length > 0)
            {
                booking.IdCardFrontPath = await SaveSecureFile(idCardFront);
                booking.IdCardFrontMaskedPath = await _maskingService.MaskIdCardAsync(booking.IdCardFrontPath!, true);
            }

            if (idCardBack != null && idCardBack.Length > 0)
            {
                booking.IdCardBackPath = await SaveSecureFile(idCardBack);
                booking.IdCardBackMaskedPath = await _maskingService.MaskIdCardAsync(booking.IdCardBackPath!, false);
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpPost("payment-proof")]
        public async Task<IActionResult> UploadPaymentProof(int bookingId, IFormFile? paymentProof)
        {
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null) return NotFound(new { message = "Không tìm thấy đơn đặt phòng." });
            if (paymentProof == null || paymentProof.Length == 0) return BadRequest(new { message = "Bạn chọn ảnh bill thanh toán trước nhé." });
            if (!paymentProof.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "Bill thanh toán phải là file ảnh." });

            var uploadDir = Path.Combine(_environment.WebRootPath, "uploads", "payments");
            Directory.CreateDirectory(uploadDir);
            var extension = Path.GetExtension(paymentProof.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10) extension = ".png";
            var fileName = $"bill_{bookingId}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
            var filePath = Path.Combine(uploadDir, fileName);
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await paymentProof.CopyToAsync(stream);
            }

            booking.PaymentProofUrl = "/uploads/payments/" + fileName;
            booking.Status = "AwaitingApproval";
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpGet("branches")]
        public async Task<IActionResult> Branches(CancellationToken cancellationToken)
        {
            var branches = await _context.Branches
                .OrderBy(b => b.Id)
                .Select(b => new { b.Id, b.Name })
                .ToListAsync(cancellationToken);
            return Ok(branches);
        }

        private async Task<string?> SaveSecureFile(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;
            string secureDir = Path.Combine(_environment.ContentRootPath, "App_Data", "SecureUploads", "IDCards");
            if (!Directory.Exists(secureDir)) Directory.CreateDirectory(secureDir);
            string fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            string filePath = Path.Combine(secureDir, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return fileName;
        }
    }
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add WebHomestay/Controllers/AIChatController.cs
git commit -m "refactor(ai): rewrite AIChatController to proxy to AIBrainOrchestrator"
```

---

### Task 6: Update Program.cs DI and remove old orchestrator

**Files:**
- Modify: `WebHomestay/Program.cs`
- Delete: `WebHomestay/Services/AIBookingFlowOrchestrator.cs`
- Delete: `WebHomestay/Services/IAIBookingFlowOrchestrator.cs`

- [ ] **Step 1: Update Program.cs DI — remove IAIBookingFlowOrchestrator**

Remove this line (around line 44):
```csharp
builder.Services.AddScoped<WebHomestay.Services.IAIBookingFlowOrchestrator, WebHomestay.Services.AIBookingFlowOrchestrator>();
```

`IAIBrainOrchestrator` is already registered (line 43). No change needed for it.

- [ ] **Step 2: Delete old files**

```bash
git rm WebHomestay/Services/AIBookingFlowOrchestrator.cs
git rm WebHomestay/Services/IAIBookingFlowOrchestrator.cs
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add WebHomestay/Program.cs
git commit -m "refactor(ai): update DI and remove old AIBookingFlowOrchestrator"
```

---

### Task 7: Add Public Booking Config endpoints to AdminAIController

**Files:**
- Modify: `WebHomestay/Controllers/AdminAIController.cs`

- [ ] **Step 1: Add GET/POST for public-booking-config**

```csharp
[AdminAuthorize(Permission = "ai.response")]
[HttpGet("public-booking-config")]
public async Task<IActionResult> GetPublicBookingConfig()
{
    var settings = await _context.SystemSettings
        .Where(s => s.GroupName == "AI")
        .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);

    return Ok(new
    {
        prompt = GetAISetting(settings, "AIPublicBookingPrompt", string.Empty),
        triggerWords = GetAISetting(settings, "AIPublicBookingTriggerWords", "đặt,chốt,lấy,book,giữ phòng"),
        maxTokens = GetAISetting(settings, "AIPublicBookingMaxTokens", "300"),
        timeout = GetAISetting(settings, "AIPublicBookingTimeout", "15")
    });
}

[AdminAuthorize(Permission = "ai.edit")]
[HttpPost("public-booking-config")]
public async Task<IActionResult> SavePublicBookingConfig([FromBody] PublicBookingConfigRequest request)
{
    await UpsertAISetting("AIPublicBookingPrompt", request.Prompt ?? string.Empty, "System prompt bổ sung cho Public Booking mode");
    await UpsertAISetting("AIPublicBookingTriggerWords", request.TriggerWords ?? "đặt,chốt,lấy,book,giữ phòng", "Từ khoá phát hiện booking intent (phân cách bằng dấu phẩy)");
    await UpsertAISetting("AIPublicBookingMaxTokens", request.MaxTokens ?? "300", "Max tokens cho public booking mode");
    await UpsertAISetting("AIPublicBookingTimeout", request.Timeout ?? "15", "Timeout (giây) cho public booking mode");
    await _context.SaveChangesAsync();
    return Ok(new { success = true });
}
```

- [ ] **Step 2: Add PublicBookingConfigRequest class to FinalSynthesizerConfigRequest.cs**

```csharp
public class PublicBookingConfigRequest
{
    public string? Prompt { get; set; }
    public string? TriggerWords { get; set; }
    public string? MaxTokens { get; set; }
    public string? Timeout { get; set; }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add WebHomestay/Controllers/AdminAIController.cs WebHomestay/Services/FinalSynthesizerConfigRequest.cs
git commit -m "feat(ai): add public-booking-config GET/POST endpoints to AdminAIController"
```

---

### Task 8: Add Public Booking Config tab to AdminAI UI

**Files:**
- Modify: `WebHomestay/Views/AdminAI/Index.cshtml`
- Modify: `WebHomestay/wwwroot/js/admin-ai-brain-center.js`

- [ ] **Step 1: Add tab button to Index.cshtml**

Add a new nav-item after the "Trả lời & Form" tab:
```html
<li class="nav-item" role="presentation"><button class="nav-link" data-bs-toggle="pill" data-bs-target="#tab-public-booking" type="button" onclick="loadPublicBookingConfig()">Public Booking Config</button></li>
```

- [ ] **Step 2: Add tab-pane after tab-response-form div**

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
            <label class="form-label fw-bold">Public Booking Prompt (bổ sung vào system prompt)</label>
            <textarea id="public-booking-prompt" class="form-control final-style-text" rows="8" placeholder="Booking Conductor đã quyết định hành động: {BookingAction}..."></textarea>
            <div class="row g-3 mt-3">
                <div class="col-md-6">
                    <label class="form-label fw-bold">Trigger Words</label>
                    <input id="public-booking-trigger-words" class="form-control" placeholder="đặt,chốt,lấy,book,giữ phòng" />
                    <div class="form-text">Phân cách bằng dấu phẩy. Khi khách dùng từ này, AI hiểu là booking intent.</div>
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

- [ ] **Step 3: Add JS functions to admin-ai-brain-center.js**

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
    } catch (err) {
        alert(`Không tải được Public Booking config: ${err.message}`);
    }
}

async function savePublicBookingConfig() {
    const payload = {
        prompt: $('#public-booking-prompt').val(),
        triggerWords: $('#public-booking-trigger-words').val(),
        maxTokens: $('#public-booking-max-tokens').val(),
        timeout: $('#public-booking-timeout').val()
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

- [ ] **Step 4: Build and verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add WebHomestay/Views/AdminAI/Index.cshtml WebHomestay/wwwroot/js/admin-ai-brain-center.js
git commit -m "feat(ai): add Public Booking Config tab to Admin AI UI"
```

---

### Task 9: Update tests — remove old AIBookingFlowOrchestrator tests

**Files:**
- Delete: `WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs`

- [ ] **Step 1: Delete old test file**

```bash
git rm WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs
```

- [ ] **Step 2: Run remaining tests to ensure nothing is broken**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj`
Expected: All remaining tests pass (build may need clean since old orchestrator removed)

- [ ] **Step 3: Commit**

```bash
git add WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs
git commit -m "test(ai): remove old AIBookingFlowOrchestratorTests"
```

---

### Task 10: Final integration verification

**Files:**
- Build: `WebHomestay/WebHomestay.csproj`
- Test: `WebHomestay.Tests/WebHomestay.Tests.csproj`

- [ ] **Step 1: Full build**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds with no warnings

- [ ] **Step 2: Full test run**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj`
Expected: All tests pass

- [ ] **Step 3: Commit any remaining changes**

```bash
git add -A
git commit -m "chore: finalize public booking LLM integration"
```
