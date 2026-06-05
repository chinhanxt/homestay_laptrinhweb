# Admin Chat Read Quick Forms Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make admin chat notifications count unread customer messages and make admin quick-send send real chatbot UI blocks to customers.

**Architecture:** Store/read unread state using existing `AdminChatMessage.IsRead`, add service/controller endpoints for unread counts and mark-read, and broadcast updated counts through SignalR. For quick-send, generate UI block JSON in the admin monitor controller using the same block shapes that `WebHomestay/wwwroot/js/site.js` already renders, then update `site.js` so admin-sent blocks call `renderUiBlocks` instead of showing a placeholder text.

**Tech Stack:** ASP.NET Core MVC, EF Core, SignalR, Razor, vanilla JavaScript, Bootstrap.

---

## File Structure

- Modify `WebHomestay/Services/IAdminChatService.cs`: add unread count and mark-read service methods.
- Modify `WebHomestay/Services/AdminChatService.cs`: implement unread message count, per-session unread count, mark-read, and include unread messages in active sessions.
- Modify `WebHomestay/Controllers/AdminChatMonitorController.cs`: add mark-read and quick-block endpoints.
- Modify `WebHomestay/Hubs/ChatHub.cs`: send unread count in session lists and session updates.
- Modify `WebHomestay/wwwroot/js/admin-chat-monitor.js`: display unread count, call mark-read on session open, request quick-block JSON before sending.
- Modify `WebHomestay/wwwroot/js/site.js`: render admin-sent `uiBlocks` using existing `renderUiBlocks`.
- Modify `WebHomestay/Views/Shared/_Layout.cshtml`: notification float uses unread totals, not session totals.

## Task 1: Add unread count service methods

**Files:**
- Modify: `WebHomestay/Services/IAdminChatService.cs`
- Modify: `WebHomestay/Services/AdminChatService.cs`

- [ ] **Step 1: Update service interface**

In `WebHomestay/Services/IAdminChatService.cs`, replace the interface body with:

```csharp
public interface IAdminChatService
{
    Task<AdminChatSession> UpsertSessionAsync(string sessionId, string? customerName);
    Task<bool> IsPausedAsync(string sessionId);
    Task PauseAsync(string sessionId, string pausedBy);
    Task ResumeAsync(string sessionId);
    Task<AdminChatMessage> AddAdminReplyAsync(string sessionId, string content, string createdBy,
        string? formBlockJson = null, string? formBlockType = null);
    Task AddSystemAutoReplyAsync(string sessionId);
    Task<List<AdminChatSession>> GetActiveSessionsAsync(int timeoutMinutes = 30);
    Task<List<AdminChatMessage>> GetSessionMessagesAsync(string sessionId);
    Task<int> GetUnreadCustomerMessageCountAsync();
    Task<Dictionary<string, int>> GetUnreadCustomerMessageCountsAsync(IEnumerable<string> sessionIds);
    Task<int> MarkCustomerMessagesReadAsync(string sessionId);
}
```

- [ ] **Step 2: Add implementation methods**

In `WebHomestay/Services/AdminChatService.cs`, add these methods before the final `}` of the class:

```csharp
public async Task<int> GetUnreadCustomerMessageCountAsync()
{
    return await _db.AdminChatMessages
        .CountAsync(m => m.Role == "user" && !m.IsRead);
}

public async Task<Dictionary<string, int>> GetUnreadCustomerMessageCountsAsync(IEnumerable<string> sessionIds)
{
    var ids = sessionIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
    if (ids.Count == 0) return new Dictionary<string, int>();

    return await _db.AdminChatMessages
        .Where(m => ids.Contains(m.SessionId) && m.Role == "user" && !m.IsRead)
        .GroupBy(m => m.SessionId)
        .Select(g => new { SessionId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.SessionId, x => x.Count);
}

public async Task<int> MarkCustomerMessagesReadAsync(string sessionId)
{
    var unread = await _db.AdminChatMessages
        .Where(m => m.SessionId == sessionId && m.Role == "user" && !m.IsRead)
        .ToListAsync();

    foreach (var message in unread)
        message.IsRead = true;

    if (unread.Count > 0)
        await _db.SaveChangesAsync();

    return unread.Count;
}
```

- [ ] **Step 3: Build check**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj -o "c:/tmp/webhomestay-build-check"
```

Expected: build succeeds, with only pre-existing warnings.

## Task 2: Return unread counts from sessions and hub updates

**Files:**
- Modify: `WebHomestay/Controllers/AdminChatMonitorController.cs`
- Modify: `WebHomestay/Hubs/ChatHub.cs`

- [ ] **Step 1: Update `GetSessions` result**

In `AdminChatMonitorController.GetSessions`, after `lastMessages` is loaded, add:

```csharp
var unreadCounts = await _adminChatService.GetUnreadCustomerMessageCountsAsync(sessionIds);
```

Then add these properties to each object in `result`:

```csharp
unreadCount = unreadCounts.GetValueOrDefault(s.SessionId),
totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync()
```

- [ ] **Step 2: Add mark-read endpoint**

Add this action to `AdminChatMonitorController` before `SetAutoReply`:

```csharp
[AdminAuthorize(Permission = "chats.view")]
[HttpPost("session/{sessionId}/mark-read")]
public async Task<IActionResult> MarkRead(string sessionId)
{
    var readCount = await _adminChatService.MarkCustomerMessagesReadAsync(sessionId);
    var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync();
    return Ok(new { sessionId, readCount, unreadCount = 0, totalUnreadCount });
}
```

- [ ] **Step 3: Update `ChatHub.SendSessionList`**

In `ChatHub.SendSessionList`, after `activeSessions` is loaded, add:

```csharp
var sessionIds = activeSessions.Select(s => s.SessionId).ToList();
var unreadCounts = await _adminChatService.GetUnreadCustomerMessageCountsAsync(sessionIds);
var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync();
```

Then include in each result object:

```csharp
unreadCount = unreadCounts.GetValueOrDefault(s.SessionId),
totalUnreadCount
```

- [ ] **Step 4: Update hub broadcasts**

In `AdminPause`, `AdminResume`, and `AdminReply` sessionUpdate payloads, include:

```csharp
totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync()
```

- [ ] **Step 5: Build check**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj -o "c:/tmp/webhomestay-build-check"
```

Expected: build succeeds.

## Task 3: Frontend unread badge behavior

**Files:**
- Modify: `WebHomestay/wwwroot/js/admin-chat-monitor.js`
- Modify: `WebHomestay/Views/Shared/_Layout.cshtml`

- [ ] **Step 1: Track total unread count in admin monitor JS**

At the top of `admin-chat-monitor.js`, add:

```javascript
let totalUnreadCount = 0;
```

In `connection.on('sessionUpdate')`, after updating `sessions[data.sessionId]`, add:

```javascript
if (typeof data.totalUnreadCount === 'number') totalUnreadCount = data.totalUnreadCount;
```

In `connection.on('sessionList')`, after building `sessions`, set:

```javascript
totalUnreadCount = list.reduce((sum, s) => sum + (Number(s.unreadCount) || 0), 0);
```

Change `updateBadge()` so it displays `totalUnreadCount`, not `Object.keys(sessions).length`.

- [ ] **Step 2: Show per-session unread count**

In `renderSessionList`, add an unread pill in each session card when `s.unreadCount > 0`:

```javascript
${Number(s.unreadCount) > 0 ? `<span class="session-unread">${Number(s.unreadCount)}</span>` : ''}
```

Place it near the session time in `.session-card-top`.

- [ ] **Step 3: Mark read when selecting session**

At the end of `selectSession(sessionId)`, call:

```javascript
markSessionRead(sessionId);
```

Add function:

```javascript
function markSessionRead(sessionId) {
    fetch(`/admin/chat-monitor/session/${encodeURIComponent(sessionId)}/mark-read`, { method: 'POST' })
        .then(r => r.ok ? r.json() : null)
        .then(data => {
            if (!data) return;
            if (sessions[sessionId]) sessions[sessionId].unreadCount = 0;
            if (typeof data.totalUnreadCount === 'number') totalUnreadCount = data.totalUnreadCount;
            updateBadge();
            renderSessionList();
        })
        .catch(console.error);
}
```

- [ ] **Step 4: Update layout notification badge**

In `_Layout.cshtml`, in the `sessionList` handler for `admin-chat-notification`, replace:

```javascript
const count = list ? list.length : 0;
```

with:

```javascript
const count = Array.isArray(list)
    ? list.reduce((sum, session) => sum + (Number(session.unreadCount) || 0), 0)
    : 0;
```

- [ ] **Step 5: Add unread CSS**

In `WebHomestay/wwwroot/css/admin-chat-monitor.css`, add:

```css
.session-unread {
    min-width: 22px;
    height: 22px;
    padding: 0 7px;
    border-radius: 999px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    background: #ef4444;
    color: #ffffff;
    font-size: 0.72rem;
    font-weight: 900;
}
```

- [ ] **Step 6: Build check**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj -o "c:/tmp/webhomestay-build-check"
```

Expected: build succeeds.

## Task 4: Generate quick-send UI blocks from admin controller

**Files:**
- Modify: `WebHomestay/Controllers/AdminChatMonitorController.cs`

- [ ] **Step 1: Add quick-block endpoint**

Add this endpoint to `AdminChatMonitorController`:

```csharp
[AdminAuthorize(Permission = "chats.view")]
[HttpGet("session/{sessionId}/quick-block/{type}")]
public async Task<IActionResult> GetQuickBlock(string sessionId, string type, CancellationToken cancellationToken)
{
    var block = type switch
    {
        "roomSelector" => await BuildRoomSelectorBlock(cancellationToken),
        "slotPicker" => await BuildSlotPickerBlock(cancellationToken),
        "infoForm" => BuildInfoFormBlock(),
        "paymentQr" => BuildPaymentBlock(sessionId),
        _ => null
    };

    if (block == null) return BadRequest(new { message = "Loại form gửi nhanh không hợp lệ." });
    return Ok(block);
}
```

- [ ] **Step 2: Add helper methods**

Add these private helpers inside `AdminChatMonitorController`:

```csharp
private async Task<object> BuildRoomSelectorBlock(CancellationToken cancellationToken)
{
    var rooms = await _context.Rooms
        .Where(r => r.Status == "Available")
        .OrderBy(r => r.BranchId)
        .ThenBy(r => r.Name)
        .Take(12)
        .Select(r => new
        {
            roomId = r.Id,
            name = r.Name,
            description = r.Description,
            pricePerHour = r.PricePerHour,
            pricePerDay = r.PricePerDay,
            capacity = r.Capacity,
            maxGuests = r.MaxGuests,
            extraGuestFee = r.ExtraGuestFee,
            imageUrl = r.ImageUrl,
            detailsUrl = "/Rooms/Details/" + r.Id,
            amenities = r.Amenities.Select(a => a.Name).ToList()
        })
        .ToListAsync(cancellationToken);

    return new
    {
        message = "Mình gửi bạn danh sách phòng đang có thể chọn nhé.",
        formBlockType = "uiBlocks",
        uiBlocks = new object[]
        {
            new { type = "roomCards", data = new { rooms } }
        }
    };
}

private async Task<object> BuildSlotPickerBlock(CancellationToken cancellationToken)
{
    var now = DateTime.Now;
    var slots = await _context.RoomSlotInventories
        .Include(s => s.Room)
        .Where(s => s.Status == "Available" && s.StartTime > now)
        .OrderBy(s => s.StartTime)
        .Take(24)
        .Select(s => new
        {
            slotId = s.Id,
            roomId = s.RoomId,
            roomName = s.Room != null ? s.Room.Name : "Phòng",
            label = s.SlotLabel,
            startTime = s.StartTime,
            endTime = s.EndTime,
            totalPrice = s.Room != null && s.Room.PricePerHour > 0
                ? Math.Round((decimal)(s.EndTime - s.StartTime).TotalHours * s.Room.PricePerHour, 0)
                : 0m
        })
        .ToListAsync(cancellationToken);

    return new
    {
        message = slots.Count > 0 ? "Mình gửi bạn các khung giờ còn trống nhé." : "Bạn chọn ngày để mình kiểm tra khung giờ trống nhé.",
        formBlockType = "uiBlocks",
        uiBlocks = slots.Count > 0
            ? new object[] { new { type = "hourlySlots", data = new { slots } } }
            : new object[] { new { type = "dateSelector", data = new { } } }
    };
}

private object BuildInfoFormBlock()
{
    var fields = new object[]
    {
        new { name = "customerName", label = "Họ tên", type = "text", required = true, value = (string?)null, placeholder = "Nhập họ và tên" },
        new { name = "phoneNumber", label = "SĐT/Zalo", type = "tel", required = true, value = (string?)null, placeholder = "Nhập số điện thoại" },
        new { name = "email", label = "Email", type = "email", required = false, value = (string?)null, placeholder = "email@example.com" },
        new { name = "notes", label = "Ghi chú", type = "textarea", required = false, value = (string?)null, placeholder = "Yêu cầu thêm nếu có" }
    };

    return new
    {
        message = "Bạn điền thông tin đặt phòng giúp mình nhé.",
        formBlockType = "uiBlocks",
        uiBlocks = new object[]
        {
            new { type = "bookingForm", data = new { fields } }
        }
    };
}

private object BuildPaymentBlock(string sessionId)
{
    return new
    {
        message = "Khi có mã đơn, mình sẽ gửi link thanh toán tại đây. Bạn hoàn tất thông tin đặt phòng trước nhé.",
        formBlockType = "uiBlocks",
        uiBlocks = new object[]
        {
            new
            {
                type = "paymentQr",
                data = new
                {
                    paymentUrl = "",
                    successUrl = "",
                    instructions = "Bạn hoàn tất thông tin đặt phòng trước để hệ thống tạo link thanh toán."
                }
            }
        }
    };
}
```

- [ ] **Step 3: Build check**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj -o "c:/tmp/webhomestay-build-check"
```

Expected: build succeeds. If `RoomSlotInventory.Room` navigation name differs, inspect `WebHomestay/Models/RoomSlotInventory.cs` and adjust include/select to the actual navigation.

## Task 5: Admin JS sends quick blocks as UI blocks

**Files:**
- Modify: `WebHomestay/wwwroot/js/admin-chat-monitor.js`

- [ ] **Step 1: Replace `sendFormBlock`**

Replace existing `sendFormBlock(type)` with:

```javascript
function sendFormBlock(type) {
    if (!currentSessionId || !connection) return;
    fetch(`/admin/chat-monitor/session/${encodeURIComponent(currentSessionId)}/quick-block/${encodeURIComponent(type)}`)
        .then(r => r.ok ? r.json() : Promise.reject(new Error('Không tạo được form gửi nhanh.')))
        .then(block => {
            const formBlockJson = JSON.stringify({ uiBlocks: block.uiBlocks || [] });
            connection.invoke('adminReply', currentSessionId, block.message || 'Mình gửi bạn thông tin để thao tác nhé.', formBlockJson, block.formBlockType || 'uiBlocks').catch(console.error);
        })
        .catch(error => {
            console.error(error);
            connection.invoke('adminReply', currentSessionId, 'Hiện chưa tạo được form này, bạn nhắn lại nhu cầu để mình hỗ trợ nhé.').catch(console.error);
        });
}
```

- [ ] **Step 2: Build check**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj -o "c:/tmp/webhomestay-build-check"
```

Expected: build succeeds.

## Task 6: Public chatbot renders admin-sent UI blocks

**Files:**
- Modify: `WebHomestay/wwwroot/js/site.js`

- [ ] **Step 1: Replace placeholder renderer**

Replace `renderAdminFormBlock(formBlockJson, formBlockType)` with:

```javascript
function renderAdminFormBlock(formBlockJson, formBlockType) {
    try {
        const block = JSON.parse(formBlockJson);
        if (formBlockType === 'uiBlocks' && Array.isArray(block.uiBlocks)) {
            renderUiBlocks(block.uiBlocks);
            return;
        }
        if (Array.isArray(block.uiBlocks)) {
            renderUiBlocks(block.uiBlocks);
            return;
        }
        appendMessage('Admin đã gửi một biểu mẫu, nhưng trình duyệt chưa đọc được nội dung.', 'bot');
    } catch (e) {
        console.error('Error parsing form block:', e);
    }
}
```

- [ ] **Step 2: Avoid duplicate placeholder messages**

In `chatHubConnection.on('newMessage')`, keep `appendMessage('👤 Admin: ' + data.content, 'bot');` before rendering form blocks so the customer sees the admin message and the real block below it.

- [ ] **Step 3: Build check**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj -o "c:/tmp/webhomestay-build-check"
```

Expected: build succeeds.

## Task 7: Manual verification

**Files:**
- No code changes unless verification finds a bug.

- [ ] **Step 1: Run app**

Run:

```bash
dotnet run --project WebHomestay/WebHomestay.csproj
```

Expected: app starts. If the running app locks build output, use the already-running app and refresh browser.

- [ ] **Step 2: Verify unread badge**

Manual steps:

1. Open home page as customer and admin chat monitor in another window.
2. Customer sends two messages.
3. Admin floating badge and chat monitor badge show `2`.
4. Admin clicks that session.
5. Badge becomes `0`.
6. Refresh admin page.
7. Badge stays `0`.

- [ ] **Step 3: Verify quick-send blocks**

Manual steps:

1. Open customer chatbot for the same session.
2. In admin monitor, choose each quick-send item: room selector, slot picker, info form, payment.
3. Customer chatbot shows real UI blocks below the admin message.
4. Customer can click room/slot buttons and submit info form using existing chatbot handlers.

## Self-Review

- Spec coverage: unread count, mark-read, admin badge reduction, quick-send real UI blocks, public render reuse are all covered.
- Placeholder scan: no TBD/TODO remains. The payment quick block intentionally returns a guidance payment block when no booking link exists because there is no reliable booking id stored in `AdminChatSession`.
- Type consistency: endpoint uses `formBlockType = "uiBlocks"`; `site.js` checks the same string and expects `{ uiBlocks: [...] }` JSON.
