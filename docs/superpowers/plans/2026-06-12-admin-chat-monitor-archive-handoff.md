# Admin Chat Monitor Archive And Handoff Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nâng cấp monitor chat để lưu và xem đầy đủ toàn bộ hội thoại, hỗ trợ xóa mềm/khôi phục/xóa vĩnh viễn từng phiên, bắt buộc takeover trước khi staff can thiệp, và biến gửi nhanh thành form điều kiện nội bộ gửi đúng block sang khách theo realtime.

**Architecture:** Giữ `AdminChatMonitorController`, `AdminChatService`, `ChatHub`, `admin-chat-monitor.js`, và `site.js` làm xương sống hiện tại nhưng tách rõ ba lớp trách nhiệm: vòng đời phiên chat, delivery staff message/ui block, và quick-send schema/composer. `AdminChatMessages` trở thành nguồn sự thật duy nhất cho timeline monitor và lịch sử chat hydrate lại ở phía khách.

**Tech Stack:** ASP.NET Core MVC, EF Core + PostgreSQL, SignalR, vanilla JavaScript, xUnit + EF InMemory

---

## File map

- Modify: `WebHomestay/Models/AdminChatSession.cs`
  - Thêm trạng thái archive và takeover metadata.
- Modify: `WebHomestay/Models/AdminChatMessage.cs`
  - Thêm metadata delivery và ui block nếu model hiện tại còn thiếu.
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`
  - Map field mới cho session/message.
- Create: `WebHomestay/Migrations/<timestamp>_AddAdminChatArchiveAndDeliveryState.cs`
  - Migration cho archive/takeover/delivery fields.
- Modify: `WebHomestay/Services/IAdminChatService.cs`
  - Mở rộng contract cho archive, restore, purge, takeover, unified delivery.
- Modify: `WebHomestay/Services/AdminChatService.cs`
  - Cài đặt archive lifecycle, timeline query, and staff delivery persistence.
- Create: `WebHomestay/Services/AdminChatQuickSendService.cs`
  - Dựng schema và payload cho quick-send theo loại block.
- Create: `WebHomestay/Services/IAdminChatQuickSendService.cs`
  - Contract cho quick-send schema/composer.
- Modify: `WebHomestay/Controllers/AdminChatMonitorController.cs`
  - Thêm routes archive/restore/purge/takeover/reply/quick-send schema-submit/history scope.
- Modify: `WebHomestay/Hubs/ChatHub.cs`
  - Bảo đảm staff delivery phát đúng event tới group session khách và admin monitor.
- Modify: `WebHomestay/Controllers/AIChatController.cs`
  - Trả lịch sử chat hydrate từ DB nếu widget reload.
- Modify: `WebHomestay/wwwroot/js/admin-chat-monitor.js`
  - Render timeline đầy đủ, modal takeover, tab đã xóa, form quick-send.
- Modify: `WebHomestay/wwwroot/js/site.js`
  - Nhận tin staff/ui blocks từ SignalR và hydrate lịch sử từ server.
- Modify: `WebHomestay/Views/AdminChatMonitor/Index.cshtml`
  - Thêm action archive, tab deleted, modal takeover, modal quick-send, confirm purge.
- Modify: `WebHomestay/Program.cs`
  - Đăng ký quick-send service mới nếu cần.
- Create: `WebHomestay.Tests/Services/AdminChatServiceTests.cs`
  - Bao phủ archive/restore/purge/takeover/unified delivery.
- Create: `WebHomestay.Tests/Controllers/AdminChatMonitorControllerTests.cs`
  - Bao phủ endpoint scope, quick-send validation, reply requires takeover.

### Task 1: Add chat archive and takeover data model coverage

**Files:**
- Modify: `WebHomestay/Models/AdminChatSession.cs`
- Modify: `WebHomestay/Models/AdminChatMessage.cs`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`
- Create: `WebHomestay.Tests/Services/AdminChatServiceTests.cs`

- [ ] **Step 1: Write failing tests for archive lifecycle fields and takeover metadata**

```csharp
[Fact]
public async Task SoftDeleteSession_MarksSessionDeletedWithoutRemovingMessages()
{
    using var context = CreateContext();
    context.AdminChatSessions.Add(new AdminChatSession
    {
        SessionId = "arch-1",
        CustomerName = "Khách A",
        Status = "auto",
        CreatedAt = DateTime.Now,
        LastActivityAt = DateTime.Now
    });
    context.AdminChatMessages.Add(new AdminChatMessage
    {
        SessionId = "arch-1",
        Role = "user",
        Content = "xin chào",
        CreatedAt = DateTime.Now
    });
    await context.SaveChangesAsync();
    var service = new AdminChatService(context);

    await service.SoftDeleteSessionAsync("arch-1", "admin");

    var session = await context.AdminChatSessions.SingleAsync(x => x.SessionId == "arch-1");
    var messageCount = await context.AdminChatMessages.CountAsync(x => x.SessionId == "arch-1");
    Assert.True(session.IsDeleted);
    Assert.Equal("admin", session.DeletedBy);
    Assert.Equal(1, messageCount);
}

[Fact]
public async Task TakeoverSession_SetsPausedManualHandoffMetadata()
{
    using var context = CreateContext();
    context.AdminChatSessions.Add(new AdminChatSession
    {
        SessionId = "handoff-1",
        CustomerName = "Khách B",
        Status = "auto",
        CreatedAt = DateTime.Now,
        LastActivityAt = DateTime.Now
    });
    await context.SaveChangesAsync();
    var service = new AdminChatService(context);

    await service.TakeoverSessionAsync("handoff-1", "staff01");

    var session = await context.AdminChatSessions.SingleAsync(x => x.SessionId == "handoff-1");
    Assert.Equal("paused", session.Status);
    Assert.Equal("manual_handoff", session.PauseReason);
    Assert.Equal("staff01", session.TakenOverBy);
    Assert.NotNull(session.TakenOverAt);
}
```

- [ ] **Step 2: Run the new service tests to verify they fail on missing fields/methods**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminChatServiceTests`

Expected: FAIL because `IsDeleted`, `DeletedBy`, `PauseReason`, `TakenOverBy`, and lifecycle methods do not exist yet.

- [ ] **Step 3: Add the minimum archive and takeover fields to the chat models**

```csharp
public class AdminChatSession
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public string? PauseReason { get; set; }
    public DateTime? TakenOverAt { get; set; }
    public string? TakenOverBy { get; set; }
}

public class AdminChatMessage
{
    public string? UiBlocksJson { get; set; }
    public string? DeliveryStatus { get; set; } = "Delivered";
}
```

- [ ] **Step 4: Update EF mapping and create the migration**

Run: `dotnet ef migrations add AddAdminChatArchiveAndDeliveryState --project WebHomestay/WebHomestay.csproj`

Expected: A migration file is created with new nullable columns for archive/takeover metadata and message delivery metadata.

- [ ] **Step 5: Re-run the targeted tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminChatServiceTests`

Expected: Still FAIL because service methods are not implemented yet, but model/DbContext compile.

- [ ] **Step 6: Commit the data model groundwork**

```bash
git add WebHomestay/Models/AdminChatSession.cs WebHomestay/Models/AdminChatMessage.cs WebHomestay/Data/ApplicationDbContext.cs WebHomestay/Migrations
git commit -m "feat: add admin chat archive and takeover fields"
```

### Task 2: Implement archive, restore, purge, and takeover in the chat service

**Files:**
- Modify: `WebHomestay/Services/IAdminChatService.cs`
- Modify: `WebHomestay/Services/AdminChatService.cs`
- Modify: `WebHomestay.Tests/Services/AdminChatServiceTests.cs`

- [ ] **Step 1: Add failing tests for restore, purge, and active/deleted session queries**

```csharp
[Fact]
public async Task GetSessionsByScope_SeparatesActiveAndDeleted()
{
    using var context = CreateContext();
    context.AdminChatSessions.AddRange(
        new AdminChatSession { SessionId = "live-1", CustomerName = "A", Status = "auto", LastActivityAt = DateTime.Now, CreatedAt = DateTime.Now },
        new AdminChatSession { SessionId = "trash-1", CustomerName = "B", Status = "paused", LastActivityAt = DateTime.Now, CreatedAt = DateTime.Now, IsDeleted = true, DeletedAt = DateTime.Now, DeletedBy = "admin" }
    );
    await context.SaveChangesAsync();
    var service = new AdminChatService(context);

    var active = await service.GetSessionsAsync(includeDeleted: false, 30);
    var deleted = await service.GetSessionsAsync(includeDeleted: true, 30);

    Assert.Contains(active, x => x.SessionId == "live-1");
    Assert.DoesNotContain(active, x => x.SessionId == "trash-1");
    Assert.Contains(deleted, x => x.SessionId == "trash-1");
}

[Fact]
public async Task PermanentlyDeleteSession_RemovesSessionAndMessages()
{
    using var context = CreateContext();
    context.AdminChatSessions.Add(new AdminChatSession { SessionId = "purge-1", CustomerName = "C", Status = "paused", LastActivityAt = DateTime.Now, CreatedAt = DateTime.Now, IsDeleted = true });
    context.AdminChatMessages.Add(new AdminChatMessage { SessionId = "purge-1", Role = "admin", Content = "test", CreatedAt = DateTime.Now });
    await context.SaveChangesAsync();
    var service = new AdminChatService(context);

    await service.PermanentlyDeleteSessionAsync("purge-1");

    Assert.False(await context.AdminChatSessions.AnyAsync(x => x.SessionId == "purge-1"));
    Assert.False(await context.AdminChatMessages.AnyAsync(x => x.SessionId == "purge-1"));
}
```

- [ ] **Step 2: Run the service test suite**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminChatServiceTests`

Expected: FAIL because scope query and purge methods do not exist yet.

- [ ] **Step 3: Extend the service contract with lifecycle and scoped query methods**

```csharp
Task<List<AdminChatSession>> GetSessionsAsync(bool includeDeleted, int timeoutMinutes = 30);
Task SoftDeleteSessionAsync(string sessionId, string deletedBy);
Task RestoreSessionAsync(string sessionId);
Task PermanentlyDeleteSessionAsync(string sessionId);
Task TakeoverSessionAsync(string sessionId, string takenOverBy);
```

- [ ] **Step 4: Implement minimal lifecycle behavior in `AdminChatService`**

```csharp
public async Task<List<AdminChatSession>> GetSessionsAsync(bool includeDeleted, int timeoutMinutes = 30)
{
    var cutoff = DateTime.Now.AddMinutes(-timeoutMinutes);
    var query = _db.AdminChatSessions.Where(s => s.LastActivityAt >= cutoff);
    query = includeDeleted ? query.Where(s => s.IsDeleted) : query.Where(s => !s.IsDeleted);
    return await query.OrderByDescending(s => s.LastActivityAt).Take(50).ToListAsync();
}

public async Task SoftDeleteSessionAsync(string sessionId, string deletedBy)
{
    var session = await _db.AdminChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
    if (session == null) return;
    session.IsDeleted = true;
    session.DeletedAt = DateTime.Now;
    session.DeletedBy = deletedBy;
    await _db.SaveChangesAsync();
}

public async Task RestoreSessionAsync(string sessionId)
{
    var session = await _db.AdminChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
    if (session == null) return;
    session.IsDeleted = false;
    session.DeletedAt = null;
    session.DeletedBy = null;
    await _db.SaveChangesAsync();
}
```

- [ ] **Step 5: Add purge and takeover implementations**

```csharp
public async Task PermanentlyDeleteSessionAsync(string sessionId)
{
    var messages = await _db.AdminChatMessages.Where(m => m.SessionId == sessionId).ToListAsync();
    var session = await _db.AdminChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
    _db.AdminChatMessages.RemoveRange(messages);
    if (session != null) _db.AdminChatSessions.Remove(session);
    await _db.SaveChangesAsync();
}

public async Task TakeoverSessionAsync(string sessionId, string takenOverBy)
{
    var session = await _db.AdminChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
    if (session == null) return;
    session.Status = "paused";
    session.PauseReason = "manual_handoff";
    session.PausedBy = takenOverBy;
    session.PausedAt = DateTime.Now;
    session.TakenOverBy = takenOverBy;
    session.TakenOverAt = DateTime.Now;
    session.LastActivityAt = DateTime.Now;
    await _db.SaveChangesAsync();
}
```

- [ ] **Step 6: Re-run the service tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminChatServiceTests`

Expected: PASS for archive, restore, purge, and takeover behavior.

- [ ] **Step 7: Commit the lifecycle service**

```bash
git add WebHomestay/Services/IAdminChatService.cs WebHomestay/Services/AdminChatService.cs WebHomestay.Tests/Services/AdminChatServiceTests.cs
git commit -m "feat: add admin chat archive lifecycle service"
```

### Task 3: Add unified staff delivery and controller endpoints

**Files:**
- Modify: `WebHomestay/Controllers/AdminChatMonitorController.cs`
- Modify: `WebHomestay/Services/IAdminChatService.cs`
- Modify: `WebHomestay/Services/AdminChatService.cs`
- Modify: `WebHomestay/Hubs/ChatHub.cs`
- Create: `WebHomestay.Tests/Controllers/AdminChatMonitorControllerTests.cs`

- [ ] **Step 1: Add failing controller tests for takeover-required reply and session scopes**

```csharp
[Fact]
public async Task Reply_WhenSessionNotTakenOver_ReturnsBadRequest()
{
    var controller = CreateController();
    var result = await controller.SendReply("sess-1", new AdminChatReplyRequest("xin chào", null, null));
    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Contains("takeover", JsonSerializer.Serialize(badRequest.Value), StringComparison.OrdinalIgnoreCase);
}

[Fact]
public async Task GetSessions_WithDeletedScope_ReturnsDeletedSessionsOnly()
{
    var controller = CreateController();
    var result = await controller.GetSessions("deleted");
    Assert.IsType<OkObjectResult>(result);
}
```

- [ ] **Step 2: Run the controller tests to verify endpoint gaps**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminChatMonitorControllerTests`

Expected: FAIL because new request models/routes do not exist yet.

- [ ] **Step 3: Add controller request models and new endpoints**

```csharp
public record AdminChatReplyRequest(string Content, string? UiBlocksJson, string? FormBlockType);

[HttpPost("session/{sessionId}/takeover")]
public async Task<IActionResult> Takeover(string sessionId)
{
    var adminUser = HttpContext.Session.GetString("AdminUser") ?? "admin";
    await _adminChatService.TakeoverSessionAsync(sessionId, adminUser);
    await _hubContext.Clients.Group("admin_monitor").SendAsync("sessionUpdate", new { sessionId, status = "paused" });
    return Ok(new { success = true, sessionId, status = "paused" });
}

[HttpPost("session/{sessionId}/reply")]
public async Task<IActionResult> SendReply(string sessionId, [FromBody] AdminChatReplyRequest request)
{
    if (!await _adminChatService.IsPausedAsync(sessionId))
        return BadRequest(new { message = "Session must be taken over before staff send." });
    var adminUser = HttpContext.Session.GetString("AdminUser") ?? "admin";
    var message = await _adminChatService.AddAdminReplyAsync(sessionId, request.Content, adminUser, request.UiBlocksJson, request.FormBlockType);
    await _hubContext.Clients.Group(sessionId).SendAsync("newMessage", new
    {
        sessionId,
        role = "admin",
        content = message.Content,
        createdAt = message.CreatedAt,
        formBlockType = message.FormBlockType,
        uiBlocks = string.IsNullOrWhiteSpace(message.UiBlocksJson) ? null : JsonSerializer.Deserialize<object>(message.UiBlocksJson)
    });
    return Ok(new { success = true });
}
```

- [ ] **Step 4: Update `GetSessions` and detail endpoints to support active/deleted scope**

```csharp
[HttpGet("sessions")]
public async Task<IActionResult> GetSessions(string scope = "active")
{
    var includeDeleted = string.Equals(scope, "deleted", StringComparison.OrdinalIgnoreCase);
    var sessions = await _adminChatService.GetSessionsAsync(includeDeleted, 30);
    // existing projection continues here
}
```

- [ ] **Step 5: Re-run the controller and service tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter "FullyQualifiedName~AdminChatMonitorControllerTests|FullyQualifiedName~AdminChatServiceTests"`

Expected: PASS.

- [ ] **Step 6: Commit the unified delivery endpoints**

```bash
git add WebHomestay/Controllers/AdminChatMonitorController.cs WebHomestay/Services/IAdminChatService.cs WebHomestay/Services/AdminChatService.cs WebHomestay/Hubs/ChatHub.cs WebHomestay.Tests/Controllers/AdminChatMonitorControllerTests.cs
git commit -m "feat: add admin chat takeover and unified reply delivery"
```

### Task 4: Implement quick-send schema/composer and validation

**Files:**
- Create: `WebHomestay/Services/IAdminChatQuickSendService.cs`
- Create: `WebHomestay/Services/AdminChatQuickSendService.cs`
- Modify: `WebHomestay/Controllers/AdminChatMonitorController.cs`
- Modify: `WebHomestay/Program.cs`
- Modify: `WebHomestay.Tests/Controllers/AdminChatMonitorControllerTests.cs`

- [ ] **Step 1: Add failing tests for quick-send schema and submit validation**

```csharp
[Fact]
public async Task QuickSendSchema_RoomSelector_ReturnsRequiredFields()
{
    var controller = CreateController();
    var result = await controller.GetQuickSendSchema("sess-2", "roomSelector", CancellationToken.None);
    var ok = Assert.IsType<OkObjectResult>(result);
    Assert.Contains("branchId", JsonSerializer.Serialize(ok.Value));
    Assert.Contains("guestCount", JsonSerializer.Serialize(ok.Value));
}

[Fact]
public async Task QuickSendSubmit_RoomSelector_MissingBranch_ReturnsBadRequest()
{
    var controller = CreateControllerWithPausedSession();
    var payload = new Dictionary<string, string> { ["bookingMode"] = "hourly", ["guestCount"] = "2" };
    var result = await controller.SendQuickBlock("sess-2", "roomSelector", payload, CancellationToken.None);
    Assert.IsType<BadRequestObjectResult>(result);
}
```

- [ ] **Step 2: Run the controller tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminChatMonitorControllerTests`

Expected: FAIL because quick-send schema/submit routes do not exist.

- [ ] **Step 3: Create the quick-send service contract**

```csharp
public interface IAdminChatQuickSendService
{
    Task<object> GetSchemaAsync(string sessionId, string type, CancellationToken cancellationToken);
    Task<(string Message, string FormBlockType, string UiBlocksJson)> BuildPayloadAsync(
        string sessionId,
        string type,
        IDictionary<string, string> values,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement minimal schema for `roomSelector`, `slotPicker`, `infoForm`, `bookingCta`, and `handoffContact`**

```csharp
return type switch
{
    "roomSelector" => new
    {
        type,
        title = "Gửi danh sách phòng",
        requiredFields = new[] { "branchId", "bookingMode", "guestCount" },
        fields = new object[]
        {
            new { name = "branchId", label = "Chi nhánh", type = "branch-select", required = true },
            new { name = "bookingMode", label = "Hình thức đặt", type = "select", required = true, options = new[] { "hourly", "daily" } },
            new { name = "hourlyDate", label = "Ngày đặt theo giờ", type = "date", required = false },
            new { name = "checkInDate", label = "Ngày nhận", type = "date", required = false },
            new { name = "checkOutDate", label = "Ngày trả", type = "date", required = false },
            new { name = "guestCount", label = "Số khách", type = "number", required = true }
        }
    },
    _ => throw new InvalidOperationException("Unsupported quick send type")
};
```

- [ ] **Step 5: Add quick-send schema and submit endpoints in the controller**

```csharp
[HttpGet("session/{sessionId}/quick-send/{type}/schema")]
public async Task<IActionResult> GetQuickSendSchema(string sessionId, string type, CancellationToken cancellationToken)
{
    var schema = await _adminChatQuickSendService.GetSchemaAsync(sessionId, type, cancellationToken);
    return Ok(schema);
}

[HttpPost("session/{sessionId}/quick-send/{type}")]
public async Task<IActionResult> SendQuickBlock(string sessionId, string type, [FromBody] Dictionary<string, string> values, CancellationToken cancellationToken)
{
    if (!await _adminChatService.IsPausedAsync(sessionId))
        return BadRequest(new { message = "Session must be taken over before quick send." });
    var payload = await _adminChatQuickSendService.BuildPayloadAsync(sessionId, type, values, cancellationToken);
    var adminUser = HttpContext.Session.GetString("AdminUser") ?? "admin";
    var message = await _adminChatService.AddAdminReplyAsync(sessionId, payload.Message, adminUser, payload.UiBlocksJson, payload.FormBlockType);
    await _hubContext.Clients.Group(sessionId).SendAsync("newMessage", new { sessionId, role = "admin", content = message.Content, createdAt = message.CreatedAt, formBlockType = message.FormBlockType, uiBlocks = JsonSerializer.Deserialize<object>(payload.UiBlocksJson) });
    return Ok(new { success = true });
}
```

- [ ] **Step 6: Register the quick-send service and re-run tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminChatMonitorControllerTests`

Expected: PASS for schema and validation checks.

- [ ] **Step 7: Commit the quick-send backend**

```bash
git add WebHomestay/Services/IAdminChatQuickSendService.cs WebHomestay/Services/AdminChatQuickSendService.cs WebHomestay/Controllers/AdminChatMonitorController.cs WebHomestay/Program.cs WebHomestay.Tests/Controllers/AdminChatMonitorControllerTests.cs
git commit -m "feat: add admin chat quick send schema and composer"
```

### Task 5: Upgrade the admin monitor UI for deleted scope, takeover confirm, and quick-send forms

**Files:**
- Modify: `WebHomestay/Views/AdminChatMonitor/Index.cshtml`
- Modify: `WebHomestay/wwwroot/js/admin-chat-monitor.js`

- [ ] **Step 1: Add a lightweight DOM smoke checklist comment near the monitor bootstrap**

```javascript
// Smoke checklist:
// 1. Click a live session and send text while AI is auto -> takeover confirm appears.
// 2. Switch to deleted scope -> restore and purge actions appear.
// 3. Open quick send room selector -> required fields must be filled before submit.
```

- [ ] **Step 2: Add markup for deleted filter and modal shells**

```html
<button type="button" class="btn btn-outline-secondary" id="btn-scope-active">Đang hoạt động</button>
<button type="button" class="btn btn-outline-secondary" id="btn-scope-deleted">Đã xóa</button>

<div class="modal fade" id="takeover-confirm-modal" tabindex="-1" aria-hidden="true">
  <div class="modal-dialog modal-dialog-centered">
    <div class="modal-content">
      <div class="modal-header"><h5 class="modal-title">Tiếp quản phiên chat</h5></div>
      <div class="modal-body">Sẽ tạm dừng AI để nhân viên tiếp quản.</div>
      <div class="modal-footer">
        <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Hủy</button>
        <button type="button" class="btn btn-warning" id="btn-confirm-takeover">Đồng ý tiếp quản</button>
      </div>
    </div>
  </div>
</div>

<div class="modal fade" id="quick-send-modal" tabindex="-1" aria-hidden="true"></div>
```

- [ ] **Step 3: Replace immediate quick-send firing with schema-driven modal logic**

```javascript
async function openQuickSend(type) {
    if (!currentSessionId) return;
    await ensureManualTakeover(async () => {
        const response = await fetch(`/admin/chat-monitor/session/${encodeURIComponent(currentSessionId)}/quick-send/${encodeURIComponent(type)}/schema`);
        const schema = await response.json();
        renderQuickSendModal(schema);
    });
}

async function ensureManualTakeover(onSuccess) {
    const current = sessions[currentSessionId];
    if (current?.status === 'paused') {
        await onSuccess();
        return;
    }
    pendingAfterTakeover = onSuccess;
    bootstrap.Modal.getOrCreateInstance(document.getElementById('takeover-confirm-modal')).show();
}
```

- [ ] **Step 4: Send staff text through the new reply endpoint instead of local SignalR invoke**

```javascript
async function sendReply() {
    const input = document.getElementById('admin-reply-input');
    const content = input.value.trim();
    if (!content || !currentSessionId) return;
    await ensureManualTakeover(async () => {
        const response = await fetch(`/admin/chat-monitor/session/${encodeURIComponent(currentSessionId)}/reply`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ content, uiBlocksJson: null, formBlockType: null })
        });
        if (!response.ok) throw new Error('Không gửi được tin nhắn.');
        input.value = '';
        loadChatMessages(currentSessionId);
    });
}
```

- [ ] **Step 5: Add deleted scope loading, restore, and purge actions**

```javascript
let currentScope = 'active';

function loadSessionsByScope(scope) {
    currentScope = scope;
    fetch(`/admin/chat-monitor/sessions?scope=${encodeURIComponent(scope)}`)
        .then(r => r.json())
        .then(list => {
            sessions = {};
            list.forEach(s => { sessions[s.sessionId] = s; });
            renderSessionList();
        });
}
```

- [ ] **Step 6: Run the app and perform a browser smoke pass**

Run: `dotnet run --project WebHomestay/WebHomestay.csproj`

Manual check:
- mở monitor và thử gửi text khi AI đang auto
- xác nhận takeover rồi gửi lại
- mở quick-send room selector, để trống field bắt buộc và xác nhận nút gửi bị khóa
- chuyển sang tab `Đã xóa`, thực hiện khôi phục và xóa vĩnh viễn

Expected: UI follow đúng luồng confirm takeover, quick-send dùng form, deleted scope lọc đúng.

- [ ] **Step 7: Commit the monitor UI workflow**

```bash
git add WebHomestay/Views/AdminChatMonitor/Index.cshtml WebHomestay/wwwroot/js/admin-chat-monitor.js
git commit -m "feat: add admin chat monitor takeover and archive ui"
```

### Task 6: Hydrate public widget from DB and deliver staff messages to the customer view

**Files:**
- Modify: `WebHomestay/Controllers/AIChatController.cs`
- Modify: `WebHomestay/wwwroot/js/site.js`
- Modify: `WebHomestay/Controllers/AdminChatMonitorController.cs`
- Modify: `WebHomestay.Tests/Controllers/AdminChatMonitorControllerTests.cs`

- [ ] **Step 1: Add failing tests or smoke assertions for chat history hydration**

```csharp
[Fact]
public async Task ChatHistory_ReturnsStaffMessagesAndUiBlocks()
{
    var controller = CreateAiChatControllerWithHistory();
    var result = await controller.GetChatHistory("hist-1");
    var ok = Assert.IsType<OkObjectResult>(result);
    var json = JsonSerializer.Serialize(ok.Value);
    Assert.Contains("\"role\":\"admin\"", json);
}
```

- [ ] **Step 2: Run the relevant tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter "FullyQualifiedName~ChatHistory|FullyQualifiedName~AdminChatMonitorControllerTests"`

Expected: FAIL because the history endpoint does not exist.

- [ ] **Step 3: Add a public history endpoint returning persisted timeline**

```csharp
[HttpGet("/ai/chat-history/{sessionId}")]
public async Task<IActionResult> GetChatHistory(string sessionId)
{
    var history = await _adminChatService.GetSessionMessagesAsync(sessionId);
    return Ok(history.Select(m => new
    {
        role = m.Role,
        content = m.Content,
        createdAt = m.CreatedAt,
        formBlockType = m.FormBlockType,
        uiBlocks = string.IsNullOrWhiteSpace(m.UiBlocksJson) ? null : JsonSerializer.Deserialize<object>(m.UiBlocksJson)
    }));
}
```

- [ ] **Step 4: Update `site.js` to hydrate from server first and render staff payloads**

```javascript
async function loadPersistedHistory() {
    const sid = getOrCreateSessionId();
    const response = await fetch(`/ai/chat-history/${encodeURIComponent(sid)}`);
    if (!response.ok) return;
    const items = await response.json();
    messages.innerHTML = '';
    items.forEach(item => {
        appendMessage(item.content || '', item.role === 'user' ? 'user' : 'bot');
        if (Array.isArray(item.uiBlocks)) renderUiBlocks(item.uiBlocks);
    });
}

chatHubConnection.on('newMessage', function (data) {
    if (!data || data.sessionId !== currentSessionId) return;
    if (data.role === 'admin') {
        appendMessage(data.content || '', 'bot');
        if (Array.isArray(data.uiBlocks)) renderUiBlocks(data.uiBlocks);
        return;
    }
    // existing handling continues here
});
```

- [ ] **Step 5: Re-run tests and do a live two-panel smoke check**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter "FullyQualifiedName~AdminChatMonitorControllerTests|FullyQualifiedName~ChatHistory"`

Manual check:
- mở widget khách
- từ monitor takeover rồi gửi text staff
- gửi quick-send room selector
- reload widget khách và xác nhận lịch sử vẫn còn

Expected: customer sees staff text and ui block immediately and after reload.

- [ ] **Step 6: Commit the public widget hydration work**

```bash
git add WebHomestay/Controllers/AIChatController.cs WebHomestay/wwwroot/js/site.js WebHomestay.Tests/Controllers/AdminChatMonitorControllerTests.cs
git commit -m "feat: hydrate public chat history and staff delivery"
```

### Task 7: Final regression pass and cleanup

**Files:**
- Modify: `docs/superpowers/specs/2026-06-12-admin-chat-monitor-archive-handoff-design.md` (only if implementation forces a meaningful correction)
- Modify: `WebHomestay.Tests/Services/AdminChatServiceTests.cs`
- Modify: `WebHomestay.Tests/Controllers/AdminChatMonitorControllerTests.cs`

- [ ] **Step 1: Add one end-to-end regression test for deleted sessions being blocked from delivery**

```csharp
[Fact]
public async Task SendReply_WhenSessionSoftDeleted_ReturnsBadRequest()
{
    var controller = CreateControllerWithDeletedPausedSession();
    var result = await controller.SendReply("trash-2", new AdminChatReplyRequest("test", null, null));
    Assert.IsType<BadRequestObjectResult>(result);
}
```

- [ ] **Step 2: Run the full impacted suite**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter "FullyQualifiedName~AdminChatServiceTests|FullyQualifiedName~AdminChatMonitorControllerTests"`

Expected: PASS.

- [ ] **Step 3: Run a project build**

Run: `dotnet build WebHomestay/WebHomestay.csproj`

Expected: PASS.

- [ ] **Step 4: Update the spec only if implementation changes a real behavior**

```markdown
## Implementation note

- Quick-send `roomSelector` uses `branchId`, `bookingMode`, and `guestCount` as the minimum always-required fields; date fields become required conditionally by booking mode.
```

- [ ] **Step 5: Commit the regression pass**

```bash
git add WebHomestay.Tests/Services/AdminChatServiceTests.cs WebHomestay.Tests/Controllers/AdminChatMonitorControllerTests.cs docs/superpowers/specs/2026-06-12-admin-chat-monitor-archive-handoff-design.md
git commit -m "test: cover admin chat archive and handoff regressions"
```

## Self-review

- Spec coverage:
  - Lưu và xem đầy đủ timeline khách/AI/staff/system: Task 3, Task 5, Task 6.
  - Xóa mềm, khôi phục, xóa vĩnh viễn từng phiên: Task 1, Task 2, Task 5, Task 7.
  - Bắt buộc takeover trước khi staff can thiệp: Task 2, Task 3, Task 5.
  - Quick-send thành form điều kiện nội bộ: Task 4 và Task 5.
  - Khách thấy tin staff và block realtime, reload vẫn còn lịch sử: Task 3 và Task 6.
- Placeholder scan:
  - Không còn `TBD`, `TODO`, hay bước “làm phần còn lại” chung chung.
- Type consistency:
  - Plan dùng nhất quán `IsDeleted`, `PauseReason`, `TakenOverBy`, `UiBlocksJson`, `DeliveryStatus`, `AdminChatReplyRequest`, `GetSessionsAsync`, `TakeoverSessionAsync`, `BuildPayloadAsync`.
