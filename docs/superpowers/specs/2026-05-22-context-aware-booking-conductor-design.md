# Context-Aware Booking Conductor — AI như người thật

**Ngày:** 2026-05-22
**Dự án:** Web Homestay
**Mục tiêu:** Biến AI public chat từ state machine cứng nhắc thành nhân viên sale thông minh, biết linh hoạt giữa tư vấn và chốt đơn.

---

## 1. Vấn đề hiện tại

Sau khi hợp nhất public chat vào `AIBrainOrchestrator` (2026-05-22-public-booking-llm-integration), tồn tại 3 vấn đề lớn:

1. **Thiếu endpoint `/ai/booking-action`** — Frontend JS gọi để chọn phòng/slot nhưng server trả 404. `SelectedRoomId` không bao giờ được set → kẹt vĩnh viễn ở màn hình "show rooms".
2. **Booking Conductor hiện tại quá thô** — `RunBookingConductorAsync` hễ có branch + date là show rooms. KH hỏi "chính sách hủy?" cũng show rooms. Không phân biệt intent.
3. **Không có cách thoát booking mode** — State không bao giờ được reset. KH không thể quay lại chat tự nhiên sau khi đã thấy UI blocks.

## 2. Kiến trúc tổng thể

Tách Booking Conductor thành class riêng — không nhét tiếp vào `AIBrainOrchestrator`:

```
IBookingConductor
  └── ContextAwareBookingConductor
        ├── IntentClassifier (rule-based: keyword + pattern matching)
        ├── DecisionEngine (decision matrix thuần)
        └── StateManager (confirmed + progress state)
```

- `AIBrainOrchestrator.ChatAsync` gọi `IBookingConductor.DecideAsync(request, state)` thay vì `RunBookingConductorAsync` cũ.
- `IBookingConductor.HandleActionAsync(action, payload)` xử lý select-room, select-slot, submit-form.
- Không đụng đến các agent khác (Persona, Live Snapshot, Knowledge, Graph, Guard, Final Synthesizer).

## 3. State Management

Tách `AIBookingSessionState` hiện tại thành 2 lớp:

```csharp
class BookingConfirmedState {
    int? BranchId;
    string? BranchName;
    DateOnly? HourlyDate;
    DateOnly? CheckInDate;
    DateOnly? CheckOutDate;
    int GuestCount;
    string BookingMode;  // "hourly" | "daily"
}

class BookingProgressState {
    int? SelectedRoomId;
    int? SelectedSlotId;
    string? SelectedSlotLabel;
    string? CustomerName;
    string? CustomerPhone;
    string? CustomerEmail;
    int? BookingId;
}
```

Lưu chung trong container:

```csharp
class BookingSessionContainer {
    BookingConfirmedState Confirmed;
    BookingProgressState? Progress;
}
```

Cache key: `ai-booking-conductor:{sessionId}`, TTL 30 phút.

### Luật chuyển đổi

| Tình huống | Confirmed | Progress |
|---|---|---|
| KH cung cấp branch/date/guests | Cập nhật | Giữ nguyên |
| KH chọn phòng qua action | Giữ nguyên | Cập nhật roomId |
| KH hỏi policy, off-topic, so sánh | Giữ nguyên | **Xoá** (reset progress) |
| KH nói "thôi/bỏ/khác" | Giữ nguyên | **Xoá** |
| KH đặt phòng thành công | Giữ nguyên | Cập nhật BookingId |
| KH hỏi tiếp sau auto-book | Giữ nguyên | Giữ BookingId (xem lại được) |
| 30 phút không chat | Xoá (TTL) | Xoá (TTL) |

## 4. Intent Classification

Rule-based + pattern matching, không cần gọi LLM thêm:

```csharp
enum MessageIntent {
    BookingIntent,     // "đặt phòng", "còn phòng k", "book", "giữ chỗ", "lấy phòng"
    BrowsingRooms,     // "xem phòng", "phòng nào đẹp", "cho xem phòng", "có phòng không"
    PolicyQuestion,    // "chính sách hủy", "giờ nhận phòng", "trả phòng", "check-in"
    OffTopic,          // "xin chào", "cảm ơn", "bạn tên gì", "hello", "hi"
    PriceQuestion,     // "bao nhiêu tiền", "giá rẻ nhất", "bao nhiêu", "giá"
    LocationQuestion,  // "ở đâu", "gần chợ Bến Thành không", "địa chỉ"
    Compared,          // "phòng A vs B", "khác gì nhau", "phòng nào rộng hơn"
    Exit               // "thôi", "bỏ", "khác", "xóa", "hủy", "không", "để sau"
}
```

Keywords được config qua `AIPublicBookingExitKeywords`, không hardcode.

## 5. Decision Matrix

Dựa vào `MessageIntent` + `BookingConfirmedState` → quyết định action:

| Intent | Đã có đủ info? | Action | Mô tả |
|---|---|---|---|
> **"Đã có đủ info"** = `BranchId.HasValue && (HourlyDate.HasValue || CheckInDate.HasValue) && GuestCount > 0`

| Intent | Đã có đủ info? | Action | Mô tả |
|---|---|---|---|
| `BookingIntent` | Chưa đủ | `ask_info` | Hỏi thông tin còn thiếu |
| `BookingIntent` | Đủ | `show_rooms` | Show danh sách phòng |
| `BrowsingRooms` | Chưa đủ | `ask_info` | Hỏi thông tin trước |
| `BrowsingRooms` | Đủ | `show_rooms` | Show danh sách phòng |
| `PriceQuestion` | Đủ | `smart_reply` | Trả lời kèm gợi ý, không show UI |
| `PolicyQuestion` | Bất kỳ | `reply` | Trả lời tự nhiên, không show UI |
| `OffTopic` | Bất kỳ | `reply` | Chat tự nhiên |
| `Compared` | Đủ | `reply` | So sánh bằng text, không show UI lại |
| `Exit` | Bất kỳ | `reply` | Xoá Progress, trả lời xác nhận |

### Anti-spam UI blocks

| Cơ chế | Mô tả | Config key |
|---|---|---|
| MaxRoomShows | Chỉ show rooms tối đa N lần/session | `AIPublicBookingMaxRoomShows` (default: 2) |
| Cooldown | Sau show rooms, M message tiếp theo là `reply` trừ khi KH nói rõ | `AIPublicBookingRoomCooldown` (default: 3) |
| AutoShowRooms | Bật/tắt tự động show rooms | `AIPublicBookingAutoShowRooms` (default: true) |

## 6. Endpoint `/ai/booking-action`

### Request
```json
{
  "sessionId": "abc123",
  "action": "select-room | select-slot | submit-form",
  "payload": {
    "roomId": 5,
    "slotId": 123,
    "formData": {
      "customerName": "Nguyễn Văn A",
      "customerPhone": "0901234567",
      "customerEmail": "a@example.com"
    }
  }
}
```

### Response

```json
{
  "answer": "Phòng Studio 650k/đêm. Bạn chọn giờ nào?",
  "sessionId": "abc123",
  "currentStep": "show_slots",
  "uiBlocks": [...],
  "state": { ... }
}
```

### Luồng xử lý

```
POST /ai/booking-action
  → AIChatController.BookingAction()
    → IBookingConductor.HandleActionAsync(action, payload)
      → Nếu select-room:
          Progress.SelectedRoomId = roomId
          Đã có date? → BuildAvailableSlots → trả về hourlySlots UI
          Chưa có date? → hỏi date
      → Nếu select-slot:
          Progress.SelectedSlotId = slotId
          Đã có form? → auto-book → trả về paymentQr
          Chưa có form? → trả về bookingForm
      → Nếu submit-form:
          Kiểm tra đủ field → auto-book
    ← Response { answer, uiBlocks, action, state }
```

## 7. Cấu hình Admin UI

### 7.1 Settings keys (SystemSettings, GroupName = "AI")

| Key | Default | Mô tả |
|---|---|---|
| `AIPublicBookingProactiveMode` | `"balanced"` | `balanced` / `proactive` / `conservative` |
| `AIPublicBookingAutoShowRooms` | `"true"` | Tự động show phòng khi có intent |
| `AIPublicBookingMaxRoomShows` | `"2"` | Số lần show rooms tối đa/session |
| `AIPublicBookingRoomCooldown` | `"3"` | Cooldown messages sau show rooms |
| `AIPublicBookingExitKeywords` | `"thôi,bỏ,khác,xóa,hủy,không,để sau"` | Từ khoá thoát booking |
| `AIPublicBookingPersonality` | `"thân thiện, nhiệt tình, như lễ tân khách sạn"` | Tính cách AI |

Giữ lại các keys cũ: `AIPublicBookingPrompt`, `AIPublicBookingTriggerWords`, `AIPublicBookingMaxTokens`, `AIPublicBookingTimeout`.

### 7.2 Giao diện

Tab "Public Booking Config" mở rộng:

1. **Mode selector** — 3 radio: Cân bằng / Chủ động / Bảo thủ
2. **Checkbox** — Tự động show phòng
3. **Number inputs** — Max lần show, Cooldown messages
4. **Text input** — Từ khoá thoát
5. **Text area** — Tính cách AI
6. **Text area** — Prompt mở rộng (giữ nguyên)
7. **Text input** — Trigger words (giữ nguyên)
8. **Number inputs** — Max tokens, Timeout (giữ nguyên)

## 8. Testing

### 8.1 Unit tests (ContextAwareBookingConductorTests)

Test decision matrix — 10+ test cases covering all Intent × Confirmed combos.
Test state management — confirm Progress được xoá khi Exit/Policy.
Test anti-spam — MaxRoomShows, Cooldown.

### 8.2 Integration test

- `POST /ai/booking-action select-room` → 200 + slots UI
- `POST /ai/booking-action select-slot` → 200 + form/payment
- `POST /ai/booking-action submit-form` → 200 + payment (auto-book)
- Chat sau auto-book → vẫn trả lời được

### 8.3 Build verification

`dotnet build WebHomestay/WebHomestay.csproj` — 0 errors.

## 9. Danh sách file thay đổi

| File | Action |
|---|---|
| `Services/ContextAwareBookingConductor.cs` | **Mới** |
| `Services/IBookingConductor.cs` | **Mới** |
| `Services/AIBookingFlowModels.cs` | Sửa — thêm container classes (`BookingConfirmedState`, `BookingProgressState`, `BookingSessionContainer`) |
| `Services/AIBrainOrchestrator.cs` | Sửa — inject `IBookingConductor`, xoá `RunBookingConductorAsync` + các helpers cũ (GetCachedState, CacheState, MergeStateFromRequest, GetPublicBookingTriggerWords, GetPublicBookingMaxTokens, GetPublicBookingPrompt, BuildShowRoomsDecisionAsync, BuildShowSlotsDecisionAsync, BuildAutoBookDecisionAsync) |
| `Services/AIBrainOrchestrator.cs` | Sửa — thêm `HandleBookingActionAsync` (delegate xuống `IBookingConductor`) |
| `Controllers/AIChatController.cs` | Sửa — thêm POST /ai/booking-action |
| `Controllers/AdminAIController.cs` | Sửa — thêm config GET/POST cho setting mới |
| `Views/AdminAI/Index.cshtml` | Sửa — UI tab mở rộng |
| `wwwroot/js/admin-ai-brain-center.js` | Sửa — load/save config mới |
| `wwwroot/js/site.js` | Sửa — xử lý response booking-action |
| `Tests/Services/ContextAwareBookingConductorTests.cs` | **Mới** |
| `AGENTS.md` | Cập nhật |

## 10. Không thay đổi

- `Services/AIBrainOrchestrator.cs` — pipeline agents khác giữ nguyên
- `Services/AIModelClient.cs` — không thay đổi
- `Services/AvailabilityService.cs` — không thay đổi
- `Services/BookingCreationService.cs` — không thay đổi
- `Services/PricingService.cs` — không thay đổi
- Admin AI Brain Center agents (Persona, Live Snapshot, Knowledge, Graph, Guard, Final Synthesizer)
- Models (Room, Branch, Booking, etc.) — không thay đổi
