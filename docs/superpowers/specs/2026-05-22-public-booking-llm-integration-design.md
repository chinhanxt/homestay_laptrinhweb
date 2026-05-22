# Public Booking LLM Integration — Design Spec

**Date:** 2026-05-22
**Status:** Draft

## Problem

`/ai/chat` hiện tại chạy `AIBookingFlowOrchestrator` — một state machine thuần (không LLM), cứng nhắc theo các bước `intent → select-room → select-slot → submit-form → payment`. Dù khách hỏi thế nào cũng trả lời theo kịch bản, không tự nhiên, không phải "AI nhân viên sale" thực thụ.

## Solution

**Hợp nhất**: Public AI Chat chạy qua `AIBrainOrchestrator` (multi-agent + LLM), thêm agent mới `BookingConductor` để quyết định khi nào chuyển từ tư vấn tự nhiên sang workflow đặt phòng. Xoá `AIBookingFlowOrchestrator` cũ.

## Architecture

### Before

```
/public /ai/chat → AIChatController → AIBookingFlowOrchestrator (state machine, no LLM)
/admin /admin/ai/brain-chat → AdminAIController → AIBrainOrchestrator (multi-agent + LLM)
```

### After

```
/public /ai/chat → AIChatController ─┐
                                     ├→ AIBrainOrchestrator (mở rộng)
/admin /admin/ai/brain-chat → AdminAIController ─┘
                                      ├── Persona Agent (giữ nguyên)
                                      ├── Live Snapshot (giữ nguyên)
                                      ├── Knowledge RAG (giữ nguyên)
                                      ├── Graph Reasoning (giữ nguyên)
                                      ├── Safety Guard (giữ nguyên)
                                      ├── Booking Conductor agent (MỚI)
                                      └── Final Synthesizer (mở rộng)
```

## Components

### 1. ChatMode enum

```csharp
public enum ChatMode
{
    AdminAssistant,  // Hành vi Brain Center cũ (giữ nguyên)
    PublicBooking    // Thêm BookingConductor, trả UI blocks
}
```

Thêm vào `AIBrainChatRequest`:

```csharp
public ChatMode Mode { get; set; } = ChatMode.AdminAssistant;
```

### 2. Booking Conductor Agent (rule-based)

Agent thứ 6 trong pipeline, chạy sau Guard, trước Final Synthesizer.

**Input:** Output các agent trước + `AIBookingSessionState` từ MemoryCache.

**Output:** `BookingDecision`

```csharp
public class BookingDecision
{
    public string Action { get; set; } = "reply";
        // "reply" | "show_rooms" | "show_slots" | "show_form" | "auto_book"
    public AIBookingSessionState State { get; set; } = new();
    public string? Reason { get; set; }
    public List<AIUiBlock> UiBlocks { get; set; } = new();
}
```

**Decision tree:**

```
1. Guard block? → reply (từ chối/thông báo lỗi)

2. Cache session state tồn tại?
   → Merge với thông tin mới từ hội thoại (branchId, date, guestCount, roomId, slotId)

3. Thiếu mandatory fields (branchId, date/time, guestCount)?
   → reply + Final Synthesizer hỏi field còn thiếu

4. Đủ mandatory + khách có booking intent (trigger words)?
   → Thiếu roomId → show_rooms
   → Đã chọn room, thiếu slot → show_slots
   → Đủ hết → auto_book (gọi BookingCreationService)

5. Đủ mandatory + chưa rõ intent?
   → show_rooms (gợi ý phòng)
```

**Trigger words** đọc từ `SystemSettings["AIPublicBookingTriggerWords"]` (default: `"đặt,chốt,lấy,book,giữ phòng"`).

### 3. Cache State

Kế thừa `AIBookingSessionState` hiện tại. Key: `ai-booking-conductor:{sessionId}`, TTL 30 phút. Mỗi request đọc cache, merge, ghi lại.

### 4. AIChatController (viết lại)

```csharp
[HttpPost("chat")]
public async Task<IActionResult> Chat([FromBody] PublicAIChatRequest request)
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

    var brainResponse = await _orchestrator.ChatAsync(brainRequest);

    return Ok(new
    {
        answer = brainResponse.Answer,
        message = brainResponse.Answer,
        sessionId = brainResponse.SessionId,
        currentStep = brainResponse.BookingAction,
        uiBlocks = brainResponse.UiBlocks,
        state = brainResponse.BookingState
    });
}
```

### 5. AIBrainChatResponse (mở rộng)

```csharp
public class AIBrainChatResponse
{
    // Giữ nguyên
    public string TraceId { get; set; }
    public string Answer { get; set; }
    public string PersonaSummary { get; set; }
    public string GuardResult { get; set; }
    public string ModelProvider { get; set; }
    public bool IsMock { get; set; }
    public string FormSchema { get; set; }

    // MỚI
    public string BookingAction { get; set; } = "reply";
    public AIBookingSessionState? BookingState { get; set; }
    public List<AIUiBlock> UiBlocks { get; set; } = new();
}
```

### 6. Final Synthesizer (mở rộng)

Khi `ChatMode.PublicBooking`, system prompt nhận thêm:

```
Booking Conductor đã quyết định hành động: {BookingAction}
Booking Session State: {BookingState JSON}

Nếu action = "reply": trả lời tự nhiên, không thêm UI.
Nếu action = "show_rooms": giới thiệu phòng ngắn gọn. UI rooms đã kèm.
Nếu action = "show_slots": giới thiệu khung giờ. UI slots đã kèm.
Nếu action = "show_form": hướng dẫn điền form. UI form đã pre-fill.
Nếu action = "auto_book": thông báo thành công + hướng dẫn thanh toán.
```

**Token:** Public mode dùng `MaxTokens = 300` (mặc định, configurable).

### 7. auto_book flow

Booking Conductor gọi `BookingCreationService.CreateHourlyBookingAsync()` hoặc `CreateDailyBookingAsync()` trực tiếp.

Nếu thành công → action = "auto_book", trả `paymentQr` block.
Nếu lỗi (hết phòng, conflict) → action = "reply", báo lỗi + gợi ý thử lại.

### 8. Endpoint giữ lại

- `POST /ai/booking-id-card` — upload CCCD
- `POST /ai/payment-proof` — upload bill
- `GET /ai/branches` — danh sách chi nhánh

### 9. Endpoint xoá

- `POST /ai/booking-action` — không còn state machine step riêng

## Files thay đổi

| File | Action |
|------|--------|
| `Services/AIBrainOrchestrator.cs` | Thêm ChatMode, BookingConductor, decision tree |
| `Services/IAIBrainOrchestrator.cs` | Thêm method nếu cần |
| `Services/AIBrainChatRequest.cs` | Thêm `Mode` field |
| `Services/AIBrainChatResponse.cs` | Thêm `BookingAction`, `BookingState`, `UiBlocks` |
| `Services/AIBookingFlowModels.cs` | **Giữ** (vẫn cần models) |
| `Services/AIBookingFlowOrchestrator.cs` | **Xoá** |
| `Services/IAIBookingFlowOrchestrator.cs` | **Xoá** |
| `Controllers/AIChatController.cs` | Viết lại |
| `Program.cs` | Đổi DI registry |
| `Filters/AdminAuthorizeAttribute.cs` | Không đụng |

## Config (SystemSettings, GroupName = "AI")

| Key | Default | UI Tab |
|-----|---------|--------|
| `AIPublicBookingPrompt` | *Booking Conductor prompt bổ sung cho Final Synthesizer* | Public Booking Config |
| `AIPublicBookingTriggerWords` | `đặt,chốt,lấy,book,giữ phòng` | Public Booking Config |
| `AIPublicBookingMaxTokens` | `300` | Public Booking Config |
| `AIPublicBookingTimeout` | `15` | Public Booking Config |

Tất cả configurable qua UI Admin AI Brain Center → tab "Public Booking Config" (MỚI).

## No hardcode

Every tunable value (prompt, trigger words, max tokens, timeout) lives in `SystemSettings` with `GroupName = "AI"`. Zero hardcoded strings in code — fallback defaults only.

## Sequence diagram (auto_book scenario)

```
User                     AIChatController        AIBrainOrchestrator     BookingCreationService
  │                             │                         │                       │
  ├─ "đặt Sài Gòn Couple ──────►│                         │                       │
  │   14h-18h, 2 người"         │                         │                       │
  │                             │─── ChatAsync ──────────►│                       │
  │                             │     (Mode=PublicBooking) │                       │
  │                             │                         ├─ Persona Agent        │
  │                             │                         ├─ Live Snapshot        │
  │                             │                         ├─ Knowledge            │
  │                             │                         ├─ Graph                │
  │                             │                         ├─ Guard                │
  │                             │                         ├─ Booking Conductor    │
  │                             │                         │  decision=auto_book   │
  │                             │                         ├─ call ───────────────►│
  │                             │                         │  CreateHourlyBooking  │
  │                             │                         │◄──── booking ────────┤
  │                             │                         ├─ Final Synthesizer   │
  │                             │◄──── response ─────────┤                       │
  │                             │  {action:auto_book,     │                       │
  │◄────────────────────────────┤   uiBlocks:[paymentQr]} │                       │
  │                             │                         │                       │
```

## Performance

- Public mode timeout: 15s (configurable)
- MaxTokens: 300 (configurable)
- Timeout enforced via `CancellationTokenSource.CreateLinkedTokenSource` với thời gian từ `AIPublicBookingTimeout`
- Nếu timeout → trả fallback reply: *"Xin lỗi, hệ thống đang xử lý chậm hơn dự kiến. Bạn thử lại sau giúp mình nhé."*
- Cache read/write mỗi request (MemoryCache, ~0ms)
- Cache cũ `ai-booking-flow:{sessionId}` tự expire sau 30 phút; không cần cleanup

## Error handling

| Tình huống | Xử lý |
|------------|-------|
| LLM call fail/timeout | Fallback reply + log trace |
| BookingCreationService fail (conflict) | action=reply, báo "phòng vừa được đặt, chọn khác" |
| Thiếu API key | Trả lỗi 503 + message cho khách |
| Invalid session/cache | Tạo session mới |
