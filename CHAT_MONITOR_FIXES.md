# Chat Monitor Fixes - Tóm tắt thay đổi

## Task 1: Loại bỏ câu hỏi của khách bị lặp 2 lần ✅

**File:** `WebHomestay/wwwroot/js/admin-chat-monitor.js`

**Vấn đề:** Tin nhắn của khách đang bị hiển thị 2 lần vì render cả `data.traces` (từ AIConversationTraces) và `data.messages` (từ AdminChatMessages).

**Giải pháp:** Chỉ render `data.messages` để tránh duplicate. Bỏ phần render `data.traces`.

```javascript
// TRƯỚC:
data.traces?.forEach(t => {
    area.appendChild(createMsgBubble('user', t.content, t.createdAt));
    area.appendChild(createMsgBubble('ai', t.aiReply, t.createdAt));
});
data.messages?.forEach(m => {
    area.appendChild(createMsgBubble(m.role, m.content, m.createdAt, m.formBlockType));
});

// SAU:
// Chỉ render AdminChatMessages (đã bao gồm tất cả tin nhắn user/ai/admin/system)
data.messages?.forEach(m => {
    area.appendChild(createMsgBubble(m.role, m.content, m.createdAt, m.formBlockType));
});
```

---

## Task 2: Hiển thị tên khách hàng thay vì mã session ✅

**Files:**
- `WebHomestay/Services/ContextAwareBookingConductor.cs`

**Vấn đề:** Sidebar đang hiển thị "Khách 7943bf" (mã session) thay vì tên thật của khách hàng.

**Giải pháp:** 
1. Thêm `IAdminChatService` vào constructor của `ContextAwareBookingConductor`
2. Khi khách submit booking form (action `submit-booking-form` hoặc `submit-form`), cập nhật `AdminChatSession.CustomerName`

```csharp
// Thêm vào constructor
public ContextAwareBookingConductor(
    ApplicationDbContext context,
    IMemoryCache cache,
    IServiceScopeFactory scopeFactory,
    IBookingCreationService bookingCreationService,
    IAdminChatService adminChatService)  // ← THÊM MỚI
{
    // ...
    _adminChatService = adminChatService;
}

// Trong case "submit-booking-form":
if (actionRequest.FormData != null)
{
    container.Progress.CustomerName = actionRequest.FormData.GetValueOrDefault("customerName");
    container.Progress.CustomerPhone = actionRequest.FormData.GetValueOrDefault("phoneNumber")
        ?? actionRequest.FormData.GetValueOrDefault("customerPhone");
    container.Progress.CustomerEmail = actionRequest.FormData.GetValueOrDefault("email")
        ?? actionRequest.FormData.GetValueOrDefault("customerEmail");
    
    // ← CẬP NHẬT SESSION VỚI TÊN KHÁCH HÀNG
    if (!string.IsNullOrWhiteSpace(container.Progress.CustomerName))
    {
        await _adminChatService.UpsertSessionAsync(actionRequest.SessionId, container.Progress.CustomerName);
    }
}
```

**Cách hoạt động:**
- Khi khách chưa điền form → hiển thị "Khách [sessionId]" (như cũ)
- Khi khách điền form → tên thật sẽ được lưu vào DB và hiển thị ngay

---

## Task 3: Realtime cho chế độ "Gửi nhanh" ✅

**File:** `WebHomestay/wwwroot/js/admin-chat-monitor.js`

**Vấn đề:** Khi admin gửi tin nhắn nhanh (quick reply) hoặc form block, phải reload trang mới thấy tin nhắn xuất hiện.

**Giải pháp:** Hiển thị tin nhắn ngay lập tức trên UI admin monitor **trước khi** gửi SignalR.

### 3.1. Sửa `sendReply()`:

```javascript
function sendReply() {
    const input = document.getElementById('admin-reply-input');
    const content = input.value.trim();
    if (!content || !currentSessionId || !connection) return;
    
    // ← HIỂN THỊ TIN NHẮN NGAY LẬP TỨC
    const area = document.getElementById('chat-messages-area');
    if (area) {
        area.appendChild(createMsgBubble('admin', content, new Date().toISOString(), null));
        area.scrollTop = area.scrollHeight;
    }
    
    connection.invoke('adminReply', currentSessionId, content).catch(console.error);
    input.value = '';
}
```

### 3.2. Sửa `sendFormBlock()`:

```javascript
function sendFormBlock(type) {
    if (!currentSessionId || !connection) return;
    fetch(`/admin/chat-monitor/session/${encodeURIComponent(currentSessionId)}/quick-block/${encodeURIComponent(type)}`)
        .then(r => r.ok ? r.json() : Promise.reject(new Error('Không tạo được form gửi nhanh.')))
        .then(block => {
            const formBlockJson = JSON.stringify({ uiBlocks: block.uiBlocks || [] });
            const messageContent = block.message || 'Mình gửi bạn thông tin để thao tác nhé.';
            const blockType = block.formBlockType || 'uiBlocks';
            
            // ← HIỂN THỊ TIN NHẮN NGAY LẬP TỨC
            const area = document.getElementById('chat-messages-area');
            if (area) {
                area.appendChild(createMsgBubble('admin', messageContent, new Date().toISOString(), type));
                area.scrollTop = area.scrollHeight;
            }
            
            connection.invoke('adminReply', currentSessionId, messageContent, formBlockJson, blockType).catch(console.error);
        })
        .catch(error => {
            console.error(error);
            const fallbackMessage = 'Hiện chưa tạo được form này, bạn nhắn lại nhu cầu để mình hỗ trợ nhé.';
            
            // ← HIỂN THỊ FALLBACK MESSAGE NGAY LẬP TỨC
            const area = document.getElementById('chat-messages-area');
            if (area) {
                area.appendChild(createMsgBubble('admin', fallbackMessage, new Date().toISOString(), null));
                area.scrollTop = area.scrollHeight;
            }
            
            connection.invoke('adminReply', currentSessionId, fallbackMessage).catch(console.error);
        });
}
```

---

## Kết quả

✅ **Task 1:** Câu hỏi khách không còn bị lặp  
✅ **Task 2:** Tên khách hàng hiển thị thay vì mã session (sau khi khách điền form)  
✅ **Task 3:** Tin nhắn admin xuất hiện realtime không cần reload

---

## Cách test

1. **Restart app** để áp dụng thay đổi backend:
   ```bash
   .\r.ps1 r
   ```

2. **Test Task 1 (Loại bỏ duplicate):**
   - Mở `/admin/chat-monitor`
   - Chọn một phiên chat có tin nhắn
   - Xác nhận tin nhắn khách chỉ xuất hiện 1 lần

3. **Test Task 2 (Tên khách):**
   - Mở botchat ở tab riêng
   - Chat với AI và điền form booking (nhập tên)
   - Quay lại `/admin/chat-monitor`
   - Xác nhận sidebar hiển thị tên khách thay vì "Khách [mã]"

4. **Test Task 3 (Realtime):**
   - Mở `/admin/chat-monitor`
   - Chọn một phiên chat
   - Gửi tin nhắn nhanh hoặc form block
   - Xác nhận tin nhắn xuất hiện ngay lập tức không cần reload

---

## Files đã sửa

1. `WebHomestay/wwwroot/js/admin-chat-monitor.js` (Task 1, 3)
2. `WebHomestay/Services/ContextAwareBookingConductor.cs` (Task 2)
