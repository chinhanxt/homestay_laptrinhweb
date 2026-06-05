# Admin Chat Monitor — Design Spec

**Date:** 2026-05-22
**Status:** Draft

## Overview

Admin chat monitoring dashboard cho phép staff/admin theo dõi real-time các cuộc hội thoại AI với khách, tạm dừng AI để trả lời thủ công, và gửi form blocks (phòng, khung giờ, thông tin) như tin nhắn.

## Architecture

SignalR duy nhất cho real-time hai chiều. Tiếp cận tối thiểu: mở rộng pipeline hiện tại, thêm 2 model mới, 1 Hub.

```
User Widget (POST /ai/chat) ──→ AIController ──→ (check pause?) ──→ AIBrainOrchestrator
       ↑                            │                                      │
       │  SignalR push              │ lưu AdminChatMessage                 │ lưu AIConversationTrace
       │  (newMessage)              ▼                                      ▼
       └──────────────────── ChatHub ───────────────────── Admin Monitor Dashboard
                              │
                              ├─ sessionUpdate → admin_monitor group
                              ├─ newMessage    → user_{sessionId} group
                              └─ sessionList   → admin_monitor group (on connect)
```

## Data Models

### AdminChatSession → `admin_chat_sessions`

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` PK | |
| `SessionId` | `text` NOT NULL | Khớp với `AIConversationTrace.SessionId` |
| `CustomerName` | `text` nullable | User tự nhập, set khi gọi chat lần đầu |
| `Status` | `text` NOT NULL | `auto` (default) | `paused` |
| `PausedBy` | `text` nullable | Admin username |
| `PausedAt` | `timestamp` nullable | |
| `AutoReplyMessage` | `text` nullable | Ghi đè auto-reply cho session này |
| `CreatedAt` | `timestamp` NOT NULL | |
| `LastActivityAt` | `timestamp` NOT NULL | Update mỗi khi có message mới |

Index: `SessionId` (unique).

### AdminChatMessage → `admin_chat_messages`

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` PK | |
| `SessionId` | `text` NOT NULL | FK to AdminChatSession.SessionId |
| `Role` | `text` NOT NULL | `user` | `admin` | `system` (auto-reply) |
| `Content` | `text` NOT NULL | |
| `FormBlockJson` | `text` nullable | JSON form block |
| `FormBlockType` | `text` nullable | `roomSelector` | `slotPicker` | `infoForm` | `paymentQr` |
| `CreatedBy` | `text` nullable | Admin username nếu role=admin |
| `CreatedAt` | `timestamp` NOT NULL | |
| `IsRead` | `boolean` NOT NULL DEFAULT false | User đã đọc chưa |

Index: `SessionId` + `CreatedAt`.

### EF Core Mapping (ApplicationDbContext)

```csharp
modelBuilder.Entity<AdminChatSession>(entity =>
{
    entity.ToTable("admin_chat_sessions");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasColumnName("id");
    entity.Property(e => e.SessionId).HasColumnName("session_id");
    entity.HasIndex(e => e.SessionId).IsUnique();
    entity.Property(e => e.CustomerName).HasColumnName("customer_name");
    entity.Property(e => e.Status).HasColumnName("status");
    entity.Property(e => e.PausedBy).HasColumnName("paused_by");
    entity.Property(e => e.PausedAt).HasColumnName("paused_at");
    entity.Property(e => e.AutoReplyMessage).HasColumnName("auto_reply_message");
    entity.Property(e => e.CreatedAt).HasColumnName("created_at");
    entity.Property(e => e.LastActivityAt).HasColumnName("last_activity_at");
});

modelBuilder.Entity<AdminChatMessage>(entity =>
{
    entity.ToTable("admin_chat_messages");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasColumnName("id");
    entity.Property(e => e.SessionId).HasColumnName("session_id");
    entity.Property(e => e.Role).HasColumnName("role");
    entity.Property(e => e.Content).HasColumnName("content");
    entity.Property(e => e.FormBlockJson).HasColumnName("form_block_json");
    entity.Property(e => e.FormBlockType).HasColumnName("form_block_type");
    entity.Property(e => e.CreatedBy).HasColumnName("created_by");
    entity.Property(e => e.CreatedAt).HasColumnName("created_at");
    entity.Property(e => e.IsRead).HasColumnName("is_read");
    entity.HasIndex(e => new { e.SessionId, e.CreatedAt });
});
```

## SignalR Hub (`/chatHub`)

### Project setup
- Add package: `Microsoft.AspNetCore.SignalR` (included in ASP.NET Core)
- Register in `Program.cs`: `builder.Services.AddSignalR();`
- Map hub: `app.MapHub<ChatHub>("/chatHub");`

### Groups
- `user_{sessionId}` — mỗi public user
- `admin_monitor` — tất cả admin

### Server → Client events

| Event | Target Group | Payload | Trigger |
|-------|-------------|---------|---------|
| `newMessage` | `user_{sessionId}` | `{ role, content, formBlockJson?, formBlockType?, createdAt }` | Admin reply hoặc system auto-reply |
| `sessionUpdate` | `admin_monitor` | `{ sessionId, status, lastMessage, lastActivityAt, unreadCount, pausedBy? }` | User gửi tin, admin pause/resume, admin reply |
| `sessionList` | `admin_monitor` (single admin) | `[{ sessionId, status, lastMessage, lastActivityAt, unreadCount }, ...]` | Admin vừa connect — snapshot |

### Client → Server events

| Event | Payload | Action |
|-------|---------|--------|
| `joinSession` | `{ sessionId, role: "user" | "admin" }` | Join group tương ứng |
| `joinAdmin` | `{}` | Join `admin_monitor` group, trả về `sessionList` |
| `adminPause` | `{ sessionId }` | Set `AdminChatSession.Status = paused`, push `sessionUpdate` |
| `adminResume` | `{ sessionId }` | Set `AdminChatSession.Status = auto`, push `sessionUpdate` |
| `adminReply` | `{ sessionId, content, formBlockJson?, formBlockType? }` | Lưu `AdminChatMessage`, push `newMessage` + `sessionUpdate` |

### Hub class structure

```csharp
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _db;

    public async Task JoinSession(string sessionId, string role)
    {
        var groupName = role == "user" ? $"user_{sessionId}" : "admin_monitor";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        if (role == "admin") await SendSessionList();
    }

    public async Task JoinAdmin()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "admin_monitor");
        await SendSessionList();
    }

    public async Task AdminPause(string sessionId) { /* ... */ }
    public async Task AdminResume(string sessionId) { /* ... */ }
    public async Task AdminReply(string sessionId, string content,
        string? formBlockJson = null, string? formBlockType = null) { /* ... */ }
}
```

## AI Pipeline Modification

Sửa `AIChatController.Chat()`:

```
after receiving PublicAIChatRequest:
  1. Upsert AdminChatSession (tạo mới nếu chưa tồn tại, set CustomerName từ request.CustomerName)
  2. Cập nhật LastActivityAt
  3. Kiểm tra AdminChatSession.Status:
     - Nếu "paused":
       a. Tạo AdminChatMessage(role=system, content=AutoReplyMessage)
       b. Push newMessage → user_{sessionId}
       c. Push sessionUpdate → admin_monitor
       d. Trả về { answer: autoReply, isPaused: true }
     - Nếu "auto":
       a. Gọi AIBrainOrchestrator như bình thường
       b. Lưu AIConversationTrace
       c. Push sessionUpdate → admin_monitor
       d. Trả về { answer, uiBlocks, ... }
```

Upsert logic: tìm `AdminChatSession` theo `SessionId`. Nếu chưa có → tạo mới với Status=auto.

## Admin Controllers & Views

### `AdminChatMonitorController`

| Route | Method | Permission | Action |
|-------|--------|------------|--------|
| `/admin/chat-monitor` | GET | `chats.view` | Trả về View monitor |
| `/admin/chat-monitor/sessions` | GET | `chats.view` | JSON danh sách session active (dùng khi mới load trang, fallback cho snapshot) |
| `/admin/chat-monitor/session/{sessionId}` | GET | `chats.view` | JSON chi tiết session + messages |

### Floating notification (`_AdminLayout.cshtml`)

- Icon chat bubble fixed bottom-right
- Badge với số session active (lấy từ Hub `sessionUpdate`)
- Click → redirect `/admin/chat-monitor`
- JS: kết nối Hub khi trang load, lắng nghe `sessionUpdate` để cập nhật badge

### Monitor view (`/Views/AdminChatMonitor/Index.cshtml`)

- **Layout:** Split panel
  - **Left sidebar (250px):** Danh sách session cards. Mỗi card: tên user (lấy từ AIBookingSessionState), tin nhắn cuối, thời gian, status badge, unread count badge
  - **Right panel:** Chat history + reply area
- **Chat history:** Hiển thị AI trace (từ `AIConversationTrace`) + admin messages (từ `AdminChatMessage`). Real-time update qua Hub
- **Reply area:** Input text + nút Send. Dropdown (hoặc nút icon) để chọn form block:
  - "Gửi danh sách phòng" → gửi `formBlockType=roomSelector`
  - "Gửi khung giờ" → gửi `formBlockType=slotPicker`
  - "Gửi form thông tin" → gửi `formBlockType=infoForm`
  - "Gửi QR thanh toán" → gửi `formBlockType=paymentQr`
- **Pause/Resume button:** Toggle pause AI cho session hiện tại
- **Config panel:** Button mở modal cấu hình auto-reply message cho session này (ghi đè)

## Form Blocks

Khi admin gửi form block, `FormBlockJson` chứa cấu trúc:

### roomSelector
```json
{
  "type": "roomSelector",
  "label": "Chọn phòng",
  "branchId": 1,
  "date": "2026-05-25"
}
```
Widget user render danh sách phòng trống (gọi `POST /ai/booking-action` với action `select-room`).

### slotPicker
```json
{
  "type": "slotPicker",
  "label": "Chọn khung giờ",
  "roomId": 3,
  "date": "2026-05-25"
}
```
Widget render khung giờ trống cho phòng đó.

### infoForm
```json
{
  "type": "infoForm",
  "label": "Nhập thông tin",
  "fields": ["customerName", "customerPhone", "customerEmail", "note"]
}
```
Widget render form với các field tương ứng.

### paymentQr
```json
{
  "type": "paymentQr",
  "label": "Thanh toán",
  "templateMessage": "Vui lòng chuyển khoản..."
}
```
Widget render QR + hướng dẫn.

## Permissions

Thêm vào permission matrix:

```
module      │ parent key    │ child keys
────────────┼───────────────┼────────────────────
chatmonitor │ chats.view    │ reply, pause, sendform
```

Resolve order giống existing: SuperAdmin → Role template → Account override.

## SystemSettings (GroupName = "ChatMonitor")

| SettingKey | Default Value | Description |
|------------|---------------|-------------|
| `AutoReplyMessage` | `"Hiện admin đang bận, vui lòng chờ một chút. Chúng tôi sẽ trả lời bạn sớm nhất."` | Tin nhắn tự động gửi cho user khi AI bị pause |
| `SessionTimeoutMinutes` | `30` | Session không có activity trong X phút sẽ không hiện trong monitor |
| `MaxActiveSessions` | `50` | Giới hạn session hiển thị trong dashboard |
| `NewSessionSound` | `true` | Bật/tắt âm thanh khi có session mới |

## UI Flow: Public Widget Changes

Thêm SignalR client vào public chat widget (`site.js` hoặc file mới):

1. Kết nối `/chatHub` sau khi tạo sessionId
2. Join group `user_{sessionId}`
3. Lắng nghe `newMessage`:
   - Nếu `role = system`: hiển thị auto-reply message với icon "đang chờ"
   - Nếu `role = admin`: hiển thị reply từ admin, với badge "Admin"
   - Nếu có `formBlockJson`: render form block tương ứng (room, slot, info, QR)
4. Khi user gửi tin nhắn qua `POST /ai/chat`:
   - Nếu response có `isPaused = true`: hiển thị auto-reply, chặn gửi tiếp (disabled input)
   - Nếu `isPaused` không có hoặc `false`: AI trả lời bình thường

## Testing

- Unit test mới cho `AdminChatService` (nếu tách) hoặc integration test cho Hub
- Test pause/resume lifecycle
- Test auto-reply khi paused
- Test admin reply + form block delivery
- Test permission checks trên controller mới

Thư mục test: `WebHomestay.Tests/Admin/` (theo convention existing).

## Out of Scope

- User typing indicator (có thể thêm sau)
- File/image attachment trong admin reply
- Chat assignment (chỉ định admin phụ trách session nào)
- Chat transfer (chuyển session giữa các admin)
- Conversation search/history archive
- Export conversation
