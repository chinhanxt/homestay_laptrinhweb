# AI Booking Flow and Brain Center Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a reliable AI-powered mini booking flow in the public chatbot first, then redesign AI Brain Center around the same operational 6-agent workflow.

**Architecture:** The LLM should interpret intent and produce short conversational copy, while deterministic services own branch/date/room/slot availability, pricing, booking creation, and payment state. The public chat endpoint returns typed UI blocks (`roomCards`, `hourlySlots`, `dailyAvailability`, `bookingForm`, `paymentQr`) that the frontend renders as a mini booking app, not as plain text only. Admin AI Brain Center is redesigned after the public flow works, so admin tooling configures/tests the real flow instead of a separate demo.

**Tech Stack:** ASP.NET Core MVC `net10.0`, EF Core/Npgsql/PostgreSQL, Razor views, Bootstrap, jQuery in admin, vanilla JS in public `site.js`, existing booking services (`AvailabilityService`, `RoomBookingViewService`, `BookingCreationService`).

---

## File Structure and Responsibilities

### Public AI booking flow
- Modify `WebHomestay/Controllers/AIChatController.cs`
  - Keep `/ai/chat` for text intake.
  - Add structured responses with `currentStep`, `state`, and `uiBlocks`.
  - Add booking-flow action endpoints if needed for room/slot/form/payment actions.
- Create `WebHomestay/Services/AIBookingFlowModels.cs`
  - DTOs for `AIBookingFlowResponse`, `AIBookingSessionState`, `AIUiBlock`, `AIRoomCard`, `AISlotOption`, `AIDailyRoomOption`, `AIBookingFormField`, `AIPaymentBlock`.
- Create `WebHomestay/Services/IAIBookingFlowOrchestrator.cs`
  - Interface for deterministic booking flow handling.
- Create `WebHomestay/Services/AIBookingFlowOrchestrator.cs`
  - Six-agent operational flow: Intent, Location & Date, Availability, Room Ranking, Booking Form, Payment.
  - Uses existing services for truth.
- Modify `WebHomestay/Program.cs`
  - Register `IAIBookingFlowOrchestrator` and any cache/session dependencies needed.
- Modify `WebHomestay/wwwroot/js/site.js`
  - Render typed UI blocks inside the chat panel.
  - Send user actions (`selectRoom`, `selectSlot`, `submitBookingForm`) to the backend.
- Modify `WebHomestay/wwwroot/css/user-premium.css`
  - Enlarge chat panel and style room cards, slot buttons, daily options, booking form, payment block.
- Modify `WebHomestay/Views/Shared/_Layout.cshtml` only if markup hooks are missing.

### Admin AI Brain Center
- Modify `WebHomestay/Views/AdminAI/Index.cshtml`
  - Remove n8n-style diagram.
  - Add 6-agent workflow dashboard and Public Booking Flow Test tab.
  - Convert Form Designer into fixed Booking Form Designer.
- Modify `WebHomestay/wwwroot/js/admin-ai-brain-center.js`
  - Load/save fixed form designer schema.
  - Add agent workflow test actions and public flow preview.
- Modify `WebHomestay/wwwroot/css/admin-ai-brain-center.css`
  - Replace old workflow/n8n visual emphasis with workflow cards/test console layout.
- Modify `WebHomestay/Controllers/AdminAIController.cs`
  - Add endpoints for fixed booking form config and agent workflow testing.
  - Keep Knowledge, Graph, Trace endpoints.

### Tests
- Add tests under `WebHomestay.Tests/Services/` for flow parsing and availability output.
- Add tests under `WebHomestay.Tests/Admin/` only if admin view checks already support AI Brain Center markup.

---

## Phase 1 — Public AI Booking Flow

### Task 1: Define booking-flow DTOs and response contract

**Files:**
- Create: `WebHomestay/Services/AIBookingFlowModels.cs`
- Modify: `WebHomestay/Controllers/AIChatController.cs`
- Build check: `dotnet build WebHomestay/WebHomestay.csproj -o /tmp/webhomestay-build-check --no-restore`

- [ ] **Step 1: Create the DTO file**

Create `WebHomestay/Services/AIBookingFlowModels.cs` with this content:

```csharp
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
        public string Intent { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public string BookingMode { get; set; } = "unknown";
        public DateOnly? HourlyDate { get; set; }
        public DateOnly? CheckInDate { get; set; }
        public DateOnly? CheckOutDate { get; set; }
        public int GuestCount { get; set; } = 1;
        public int? SelectedRoomId { get; set; }
        public int? SelectedSlotId { get; set; }
        public int? BookingId { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
    }

    public class AIUiBlock
    {
        public string Type { get; set; } = string.Empty;
        public object Data { get; set; } = new { };
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
        public decimal PricePerHour { get; set; }
    }

    public class AIDailyRoomOption
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public DateOnly CheckInDate { get; set; }
        public DateOnly CheckOutDate { get; set; }
        public decimal PricePerDay { get; set; }
    }
}
```

- [ ] **Step 2: Extend `AIChatController` public response shape**

In `WebHomestay/Controllers/AIChatController.cs`, keep existing safe endpoint behavior, but change successful response to include the new fields while preserving `answer` for backward compatibility:

```csharp
return Ok(new
{
    answer = response.Answer,
    message = response.Answer,
    sessionId,
    currentStep = "chat",
    state = new AIBookingSessionState { GuestCount = request.GuestCount <= 0 ? 1 : request.GuestCount },
    uiBlocks = Array.Empty<AIUiBlock>(),
    formSchema = response.FormSchema
});
```

- [ ] **Step 3: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj -o /tmp/webhomestay-build-check --no-restore
```

Expected: build succeeds. Existing warnings are acceptable if unrelated.

### Task 2: Enlarge public chat panel and add UI block containers

**Files:**
- Modify: `WebHomestay/wwwroot/css/user-premium.css`
- Modify: `WebHomestay/Views/Shared/_Layout.cshtml` if the existing `.ai-chat-messages` container is insufficient

- [ ] **Step 1: Update chat panel dimensions**

In `WebHomestay/wwwroot/css/user-premium.css`, update `.ai-chat-panel` to desktop mini-app sizing:

```css
.ai-chat-panel {
    width: min(640px, calc(100vw - 32px));
    height: min(760px, calc(100vh - 110px));
}
```

For mobile, update existing media rule:

```css
@media (max-width: 575.98px) {
    .ai-chat-panel {
        position: fixed;
        inset: 0;
        width: 100vw;
        height: 100vh;
        max-width: none;
        max-height: none;
        border-radius: 0;
    }
}
```

- [ ] **Step 2: Add styles for booking blocks**

Append:

```css
.ai-booking-block { margin: 12px 0; }
.ai-room-card { border: 1px solid var(--luxury-border); background: #fff; padding: 14px; margin-bottom: 10px; }
.ai-room-card h4 { font-size: 1rem; margin-bottom: 6px; }
.ai-room-meta { color: var(--luxury-text-muted); font-size: .86rem; }
.ai-room-actions { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 10px; }
.ai-chip-btn { border: 1px solid var(--luxury-border); background: #fff; padding: 8px 12px; font-weight: 700; }
.ai-chip-btn:hover { border-color: var(--luxury-accent); color: var(--luxury-accent); }
.ai-slot-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: 8px; }
.ai-booking-form { display: grid; gap: 10px; }
.ai-booking-form input, .ai-booking-form textarea { border: 1px solid var(--luxury-border); padding: 10px 12px; }
```

- [ ] **Step 3: Build**

Run the build command from Task 1.

### Task 3: Render typed UI blocks in public `site.js`

**Files:**
- Modify: `WebHomestay/wwwroot/js/site.js`

- [ ] **Step 1: Add a block renderer after bot message append**

In the fetch success block, after appending the message, call:

```javascript
renderUiBlocks(data.uiBlocks || []);
```

- [ ] **Step 2: Add renderer functions**

Add these functions before `createSessionId()`:

```javascript
function renderUiBlocks(blocks) {
    blocks.forEach(block => {
        if (block.type === 'roomCards') renderRoomCards(block.data);
        if (block.type === 'hourlySlots') renderHourlySlots(block.data);
        if (block.type === 'dailyRooms') renderDailyRooms(block.data);
        if (block.type === 'bookingForm') renderBookingForm(block.data);
        if (block.type === 'paymentQr') renderPaymentQr(block.data);
    });
}

function appendBlock(className) {
    const wrapper = document.createElement('div');
    wrapper.className = `ai-booking-block ${className}`;
    messages.appendChild(wrapper);
    messages.scrollTop = messages.scrollHeight;
    return wrapper;
}

function renderRoomCards(data) {
    const wrapper = appendBlock('ai-room-cards');
    (data.rooms || []).forEach(room => {
        const card = document.createElement('div');
        card.className = 'ai-room-card';
        card.innerHTML = `
            <h4>${escapeHtml(room.name)}</h4>
            <div class="ai-room-meta">${escapeHtml(room.description || '')}</div>
            <div class="ai-room-meta">Giờ: ${formatMoney(room.pricePerHour)}/h · Ngày: ${formatMoney(room.pricePerDay)}/ngày</div>
            <div class="ai-room-meta">Chuẩn ${room.capacity} khách · Tối đa ${room.maxGuests} khách${room.extraGuestFee > 0 ? ` · Phụ thu ${formatMoney(room.extraGuestFee)}` : ''}</div>
            <div class="ai-room-actions">
                <a class="ai-chip-btn" href="${room.detailsUrl}">Xem chi tiết</a>
                <button class="ai-chip-btn" type="button" data-ai-action="select-room" data-room-id="${room.roomId}">Chọn phòng này</button>
            </div>`;
        wrapper.appendChild(card);
    });
}

function renderHourlySlots(data) {
    const wrapper = appendBlock('ai-hourly-slots');
    const grid = document.createElement('div');
    grid.className = 'ai-slot-grid';
    (data.slots || []).forEach(slot => {
        const button = document.createElement('button');
        button.className = 'ai-chip-btn';
        button.type = 'button';
        button.dataset.aiAction = 'select-slot';
        button.dataset.roomId = slot.roomId;
        button.dataset.slotId = slot.slotId;
        button.textContent = `${slot.roomName}: ${slot.label}`;
        grid.appendChild(button);
    });
    wrapper.appendChild(grid);
}

function renderDailyRooms(data) {
    const wrapper = appendBlock('ai-daily-rooms');
    (data.rooms || []).forEach(room => {
        const item = document.createElement('button');
        item.className = 'ai-chip-btn';
        item.type = 'button';
        item.dataset.aiAction = 'select-daily-room';
        item.dataset.roomId = room.roomId;
        item.textContent = `${room.roomName}: ${formatMoney(room.pricePerDay)}/ngày`;
        wrapper.appendChild(item);
    });
}

function renderBookingForm(data) {
    const wrapper = appendBlock('ai-booking-form-block');
    wrapper.innerHTML = '<div class="ai-booking-form"><input placeholder="Họ tên" /><input placeholder="SĐT/Zalo" /><input placeholder="Email" /><textarea placeholder="Ghi chú"></textarea><button class="ai-chat-send" type="button">Gửi thông tin</button></div>';
}

function renderPaymentQr(data) {
    const wrapper = appendBlock('ai-payment-block');
    wrapper.textContent = data.message || 'Vui lòng thanh toán theo mã QR.';
}

function formatMoney(value) {
    return new Intl.NumberFormat('vi-VN').format(value || 0) + 'đ';
}

function escapeHtml(value) {
    return String(value).replace(/[&<>"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[char]));
}
```

- [ ] **Step 3: Build**

Run the build command from Task 1.

### Task 4: Return room-card UI blocks from `/ai/chat` when branch/date is known

**Files:**
- Modify: `WebHomestay/Controllers/AIChatController.cs`
- Reuse existing data from `AIBrainOrchestrator` only if available; otherwise query directly as a first slice.

- [ ] **Step 1: Inject `ApplicationDbContext` into `AIChatController`**

Add `using Microsoft.EntityFrameworkCore;` and `using WebHomestay.Data;`.

Update constructor to accept `ApplicationDbContext context`.

- [ ] **Step 2: Add a helper to parse branch/date from the request message**

Add private helper methods:

```csharp
private async Task<int?> ResolveBranchIdAsync(string text)
{
    var lowered = text.ToLowerInvariant();
    var branches = await _context.Branches.ToListAsync();
    return branches.FirstOrDefault(b => lowered.Contains(b.Name.ToLowerInvariant()))?.Id
        ?? branches.FirstOrDefault(b => b.Name.Contains("Sài Gòn") && (lowered.Contains("sài gòn") || lowered.Contains("sai gon") || lowered.Contains("sg")))?.Id
        ?? branches.FirstOrDefault(b => b.Name.Contains("Đà Lạt") && (lowered.Contains("đà lạt") || lowered.Contains("da lat") || lowered.Contains("dalat") || lowered.Contains("dl")))?.Id;
}
```

For date, mirror the existing `TryExtractDate` logic from `AIBrainOrchestrator`.

- [ ] **Step 3: Add a roomCards block when branch and date are known**

After `response` is returned from orchestrator, build blocks:

```csharp
var uiBlocks = new List<AIUiBlock>();
var branchId = request.BranchId ?? await ResolveBranchIdAsync(request.Message);
var date = TryExtractDate(request.Message, out var parsedDate) ? parsedDate : (DateOnly?)null;
if (branchId.HasValue && date.HasValue)
{
    var rooms = await _context.Rooms
        .Where(r => r.BranchId == branchId.Value && r.Status == "Available" && r.MaxGuests >= Math.Max(request.GuestCount, 1))
        .Include(r => r.Amenities)
        .OrderBy(r => r.PricePerHour)
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
            DetailsUrl = $"/Rooms/Details/{r.Id}?hourlyDate={date.Value:yyyy-MM-dd}"
        })
        .ToListAsync(cancellationToken);

    uiBlocks.Add(new AIUiBlock { Type = "roomCards", Data = new { rooms } });
}
```

- [ ] **Step 4: Return `uiBlocks`**

Include `uiBlocks` in `Ok(...)` response.

- [ ] **Step 5: Build and manually test**

Run build.

Manual test after app restart:
- Send: `sài gòn ngày 17/5 còn phòng không`
- Expected: text reply plus room cards only for Sài Gòn.

## Phase 2 — Booking form/payment and Admin Brain Center redesign

### Task 5: Implement fixed booking form designer config

**Files:**
- Modify: `WebHomestay/Views/AdminAI/Index.cshtml`
- Modify: `WebHomestay/wwwroot/js/admin-ai-brain-center.js`
- Modify: `WebHomestay/Controllers/AdminAIController.cs`

- [ ] **Step 1: Replace conditional field UI with fixed form fields**

Keep fields:
- CustomerName
- CustomerPhone
- CustomerEmail
- GuestCount
- IdCardFront
- IdCardBack
- CustomerNote

Remove condition UI from the booking form designer path. Do not remove Knowledge/Graph/Trace features.

- [ ] **Step 2: Save fixed form config in `SystemSettings`**

Use key `AIBookingFormSchema` with JSON containing label, required, order, help text.

- [ ] **Step 3: Build**

Run build.

### Task 6: Add booking form and payment blocks after room/slot selection

**Files:**
- Modify: `WebHomestay/wwwroot/js/site.js`
- Modify: `WebHomestay/Controllers/AIChatController.cs`
- Reuse: `BookingCreationService`

- [ ] **Step 1: Add action posting in `site.js`**

Add click handler for `[data-ai-action]` buttons.

- [ ] **Step 2: Add backend action endpoint**

Add `POST /ai/booking-action` that receives action type and state payload.

- [ ] **Step 3: Use `BookingCreationService` only after form submit**

Create booking only when form has customer info and room/slot or date range.

- [ ] **Step 4: Return payment block**

Return QR/payment info block with booking id and amount.

- [ ] **Step 5: Build and manually test**

Expected: select room → select slot/date → form → submit → QR block.

### Task 7: Redesign AI Brain Center around real workflow

**Files:**
- Modify: `WebHomestay/Views/AdminAI/Index.cshtml`
- Modify: `WebHomestay/wwwroot/js/admin-ai-brain-center.js`
- Modify: `WebHomestay/wwwroot/css/admin-ai-brain-center.css`
- Modify: `WebHomestay/Controllers/AdminAIController.cs`

- [ ] **Step 1: Remove n8n diagram section**

Delete the top workflow board markup that presents n8n-style canvas. Keep page shell and tabs.

- [ ] **Step 2: Add 6-agent workflow dashboard**

Add cards for:
- Intent
- Location & Date
- Availability
- Room Ranking
- Booking Form
- Payment

Each card shows purpose, input, output, and test button.

- [ ] **Step 3: Add Public Booking Flow Test tab**

Embed a test console that calls the same public AI flow endpoints and renders the same UI blocks used by `site.js`.

- [ ] **Step 4: Keep existing Knowledge, Graph, Trace panels**

Do not remove CRUD endpoints or trace viewer.

- [ ] **Step 5: Build and manually test admin page**

Expected: Admin AI page focuses on real workflow and no longer shows n8n diagram.

## Phase 3 — Testing, hardening, and acceptance

### Task 8: Add service tests for booking flow state and availability output

**Files:**
- Create/modify tests under `WebHomestay.Tests/Services/`

- [ ] **Step 1: Add test for branch/date parsing**

Test `sài gòn ngày 17/5` resolves Sài Gòn and date.

- [ ] **Step 2: Add test for room cards filtered by branch**

Seed Sài Gòn and Đà Lạt rooms in InMemory DB. Verify Sài Gòn request returns only Sài Gòn room cards.

- [ ] **Step 3: Add test for no booking before form submit**

Verify selecting room/slot returns booking form block but does not create booking yet.

- [ ] **Step 4: Run tests**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj
```

Expected: all tests pass.

### Task 9: Manual end-to-end verification checklist

**Files:**
- No code files unless bugs are found.

- [ ] **Step 1: Desktop public flow**

Run app and test:
1. `sài gòn ngày 17/5 còn phòng không`
2. Verify Sài Gòn-only room cards.
3. Click room details/select.
4. Verify slot/date UI.
5. Submit form.
6. Verify QR/payment block.

- [ ] **Step 2: Mobile public flow**

Use browser dev tools mobile viewport. Verify chat is usable as full-screen mini app.

- [ ] **Step 3: Admin workflow page**

Verify Knowledge, Graph, Trace, Final Synthesizer, Booking Form Designer, and Public Booking Flow Test all load.

---

## Self-review

- Spec coverage: The plan covers all three phases requested: public flow first, AI Brain Center redesign second, testing/hardening third.
- Placeholder scan: No TBD/TODO placeholders are used. Later implementation tasks include exact file paths and concrete expected behavior.
- Type consistency: `AIBookingFlowResponse`, `AIBookingSessionState`, `AIUiBlock`, `AIRoomCard`, `AISlotOption`, and `AIDailyRoomOption` are introduced before use.

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-17-ai-booking-flow-brain-center.md`.

Execution approach selected by user: **Subagent-Driven Development**. Implement task-by-task with review checkpoints.
