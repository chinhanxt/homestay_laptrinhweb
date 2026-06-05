# Chat Monitor Home UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve the admin chat monitor UI, Vietnamese labels, and prevent the public chatbot button from overlapping important home page content.

**Architecture:** Keep the existing ASP.NET Core MVC/Razor structure and SignalR JavaScript behavior. Make focused UI changes in the AdminChatMonitor Razor view, its JavaScript renderer, its CSS file, and the existing public premium CSS that controls the home search form and chatbot.

**Tech Stack:** ASP.NET Core MVC Razor, Bootstrap 5, Font Awesome, vanilla JavaScript, SignalR client, CSS.

---

## File Structure

- Modify `WebHomestay/Views/AdminChatMonitor/Index.cshtml`: Vietnamese static labels, clearer header controls, improved semantic structure for chat monitor.
- Modify `WebHomestay/wwwroot/js/admin-chat-monitor.js`: Vietnamese dynamic labels, fixed search filter condition, safer status label mapping, session code display.
- Modify `WebHomestay/wwwroot/css/admin-chat-monitor.css`: redesign chat monitor as chat app + lightweight operations dashboard.
- Modify `WebHomestay/wwwroot/css/user-premium.css`: responsive search form, safe bottom spacing, chatbot floating position adjustment.
- Test manually in browser after running the app; build with `dotnet build WebHomestay/WebHomestay.csproj`.

## Task 1: Việt hóa và chỉnh cấu trúc Razor chat monitor

**Files:**
- Modify: `WebHomestay/Views/AdminChatMonitor/Index.cshtml`

- [ ] **Step 1: Update page title and static Vietnamese labels**

Replace the full contents of `WebHomestay/Views/AdminChatMonitor/Index.cshtml` with:

```cshtml
@{
    ViewData["Title"] = "Giám sát hội thoại";
    Layout = "_Layout";
}

@section Styles {
    <link rel="stylesheet" href="~/css/admin-chat-monitor.css" asp-append-version="true" />
}

<div class="chat-monitor-shell">
    <aside class="chat-session-sidebar" id="session-sidebar" aria-label="Danh sách hội thoại">
        <div class="sidebar-header">
            <div>
                <span class="sidebar-kicker">Trung tâm hỗ trợ</span>
                <h1><i class="fas fa-comments me-2"></i>Hội thoại</h1>
            </div>
            <span class="session-count" id="session-count-badge">0</span>
        </div>
        <div class="sidebar-search">
            <i class="fas fa-magnifying-glass"></i>
            <input type="text" class="form-control" placeholder="Tìm theo tên hoặc mã phiên..." id="session-search" />
        </div>
        <div class="session-list" id="session-list">
            <div class="empty-state">Đang tải hội thoại...</div>
        </div>
    </aside>

    <main class="chat-main-panel" aria-label="Nội dung hội thoại">
        <div class="chat-placeholder" id="chat-placeholder">
            <div class="chat-placeholder-card">
                <i class="fas fa-comment-dots"></i>
                <h2>Chọn một hội thoại</h2>
                <p>Chọn khách ở cột bên trái để xem tin nhắn và hỗ trợ khi cần.</p>
            </div>
        </div>

        <div class="chat-active-panel d-none" id="chat-active-panel">
            <div class="chat-active-header" id="chat-active-header">
                <div class="chat-customer-block">
                    <span class="chat-header-kicker">Đang theo dõi</span>
                    <div class="chat-title-row">
                        <strong id="chat-customer-name">Khách</strong>
                        <span class="chat-session-code" id="chat-session-code">Phiên --</span>
                    </div>
                    <span class="badge chat-status-badge" id="chat-status-badge">Đang tải trạng thái</span>
                </div>
                <div class="chat-action-group">
                    <button class="btn btn-sm btn-outline-warning" id="btn-pause-ai" type="button">
                        <i class="fas fa-pause me-1"></i>Tạm dừng AI
                    </button>
                    <button class="btn btn-sm btn-outline-success d-none" id="btn-resume-ai" type="button">
                        <i class="fas fa-play me-1"></i>Bật lại AI
                    </button>
                    <button class="btn btn-sm btn-outline-secondary" id="btn-config-auto-reply" type="button">
                        <i class="fas fa-message me-1"></i>Tin nhắn tự động
                    </button>
                </div>
            </div>

            <div class="chat-messages-area" id="chat-messages-area">
                <div class="empty-state">Đang tải tin nhắn...</div>
            </div>

            <div class="chat-reply-area">
                <div class="input-group chat-reply-group">
                    <button class="btn btn-outline-primary dropdown-toggle quick-send-button" type="button" data-bs-toggle="dropdown" aria-expanded="false">
                        <i class="fas fa-plus me-1"></i>Gửi nhanh
                    </button>
                    <ul class="dropdown-menu">
                        <li><a class="dropdown-item send-form-block" data-type="roomSelector" href="#">Gửi danh sách phòng</a></li>
                        <li><a class="dropdown-item send-form-block" data-type="slotPicker" href="#">Gửi khung giờ</a></li>
                        <li><a class="dropdown-item send-form-block" data-type="infoForm" href="#">Gửi form thông tin</a></li>
                        <li><a class="dropdown-item send-form-block" data-type="paymentQr" href="#">Gửi QR thanh toán</a></li>
                    </ul>
                    <input type="text" class="form-control" id="admin-reply-input" placeholder="Nhập tin nhắn cho khách..." maxlength="1000" />
                    <button class="btn btn-primary send-reply-button" id="btn-send-reply" type="button" aria-label="Gửi tin nhắn">
                        <i class="fas fa-paper-plane"></i>
                    </button>
                </div>
            </div>
        </div>
    </main>
</div>

@section Scripts {
    <script src="~/js/admin-chat-monitor.js" asp-append-version="true"></script>
}
```

- [ ] **Step 2: Verify Razor compiles**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: build succeeds or only unrelated pre-existing warnings appear.

## Task 2: Việt hóa dynamic JavaScript and fix session filtering

**Files:**
- Modify: `WebHomestay/wwwroot/js/admin-chat-monitor.js`

- [ ] **Step 1: Replace JavaScript with Vietnamese rendering**

Replace the full contents of `WebHomestay/wwwroot/js/admin-chat-monitor.js` with:

```javascript
let connection = null;
let currentSessionId = null;
let sessions = {};

const statusLabels = {
    paused: 'AI đã tạm dừng',
    auto: 'AI đang trả lời',
    active: 'AI đang trả lời'
};

const formBlockLabels = {
    roomSelector: 'Danh sách phòng',
    slotPicker: 'Khung giờ',
    infoForm: 'Form thông tin',
    paymentQr: 'QR thanh toán'
};

document.addEventListener('DOMContentLoaded', function () {
    initializeSignalR();
    document.getElementById('btn-pause-ai')?.addEventListener('click', () => pauseAI());
    document.getElementById('btn-resume-ai')?.addEventListener('click', () => resumeAI());
    document.getElementById('btn-send-reply')?.addEventListener('click', () => sendReply());
    document.getElementById('admin-reply-input')?.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') sendReply();
    });
    document.getElementById('session-search')?.addEventListener('input', filterSessions);
    document.querySelectorAll('.send-form-block').forEach(el => {
        el.addEventListener('click', (e) => {
            e.preventDefault();
            sendFormBlock(el.dataset.type);
        });
    });
    document.getElementById('btn-config-auto-reply')?.addEventListener('click', showAutoReplyConfig);
});

function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl('/chatHub')
        .withAutomaticReconnect()
        .build();

    connection.on('sessionUpdate', function (data) {
        sessions[data.sessionId] = { ...sessions[data.sessionId], ...data };
        renderSessionList();
        updateBadge();
        if (currentSessionId === data.sessionId) updateChatHeader(data);
    });

    connection.on('sessionList', function (list) {
        sessions = {};
        list.forEach(s => { sessions[s.sessionId] = s; });
        renderSessionList();
        updateBadge();
    });

    connection.start().then(function () {
        connection.invoke('joinAdmin');
        loadSessionsFallback();
    }).catch(function (err) {
        console.error('Không thể kết nối SignalR:', err);
    });
}

function loadSessionsFallback() {
    fetch('/admin/chat-monitor/sessions')
        .then(r => r.json())
        .then(list => {
            list.forEach(s => { sessions[s.sessionId] = { ...sessions[s.sessionId], ...s }; });
            renderSessionList();
            updateBadge();
        });
}

function renderSessionList() {
    const container = document.getElementById('session-list');
    const search = (document.getElementById('session-search')?.value || '').toLowerCase();
    const sorted = Object.values(sessions).sort((a, b) =>
        new Date(b.lastActivityAt || 0) - new Date(a.lastActivityAt || 0));

    container.innerHTML = '';
    const visibleSessions = sorted.filter(s => {
        const sessionId = (s.sessionId || '').toLowerCase();
        const customerName = (s.customerName || '').toLowerCase();
        return !search || sessionId.includes(search) || customerName.includes(search);
    });

    if (visibleSessions.length === 0) {
        container.innerHTML = '<div class="empty-state">Không tìm thấy hội thoại phù hợp.</div>';
        return;
    }

    visibleSessions.forEach(s => {
        const div = document.createElement('div');
        const isPaused = s.status === 'paused';
        div.className = `session-card ${currentSessionId === s.sessionId ? 'active' : ''}`;
        div.dataset.sessionId = s.sessionId;
        div.innerHTML = `
            <div class="session-card-top">
                <div class="session-card-name">${escapeHtml(s.customerName || 'Khách ' + shortSessionId(s.sessionId))}</div>
                <small class="session-time">${timeAgo(s.lastActivityAt)}</small>
            </div>
            <div class="session-card-preview">${escapeHtml((s.lastMessage || 'Chưa có tin nhắn gần đây').slice(0, 70))}</div>
            <div class="session-card-meta">
                <span class="session-status ${isPaused ? 'paused' : 'auto'}">${isPaused ? 'Đã tạm dừng AI' : 'Tự động'}</span>
                <span class="session-code">#${shortSessionId(s.sessionId)}</span>
            </div>
        `;
        div.addEventListener('click', () => selectSession(s.sessionId));
        container.appendChild(div);
    });
}

function updateBadge() {
    const badge = document.getElementById('session-count-badge');
    const count = Object.keys(sessions).length;
    if (badge) badge.textContent = count;
}

function selectSession(sessionId) {
    currentSessionId = sessionId;
    document.getElementById('chat-placeholder')?.classList.add('d-none');
    const panel = document.getElementById('chat-active-panel');
    if (panel) panel.classList.remove('d-none');
    renderSessionList();
    loadChatMessages(sessionId);
    updateChatHeader(sessions[sessionId] || {});
}

function updateChatHeader(data) {
    const name = document.getElementById('chat-customer-name');
    const code = document.getElementById('chat-session-code');
    const badge = document.getElementById('chat-status-badge');
    const pauseBtn = document.getElementById('btn-pause-ai');
    const resumeBtn = document.getElementById('btn-resume-ai');
    const isPaused = data.status === 'paused';

    if (name) name.textContent = data.customerName || 'Khách ' + shortSessionId(currentSessionId);
    if (code) code.textContent = 'Phiên #' + shortSessionId(currentSessionId);
    if (badge) {
        badge.textContent = statusLabels[data.status] || 'AI đang trả lời';
        badge.className = `badge chat-status-badge ${isPaused ? 'paused' : 'auto'}`;
    }
    if (pauseBtn) pauseBtn.classList.toggle('d-none', isPaused);
    if (resumeBtn) resumeBtn.classList.toggle('d-none', !isPaused);
}

function loadChatMessages(sessionId) {
    const area = document.getElementById('chat-messages-area');
    area.innerHTML = '<div class="empty-state">Đang tải tin nhắn...</div>';

    fetch(`/admin/chat-monitor/session/${encodeURIComponent(sessionId)}`)
        .then(r => r.json())
        .then(data => {
            area.innerHTML = '';
            data.traces?.forEach(t => {
                area.appendChild(createMsgBubble('user', t.content, t.createdAt));
                area.appendChild(createMsgBubble('ai', t.aiReply, t.createdAt));
            });
            data.messages?.forEach(m => {
                area.appendChild(createMsgBubble(m.role, m.content, m.createdAt, m.formBlockType));
            });
            if (!area.children.length) {
                area.innerHTML = '<div class="empty-state">Hội thoại này chưa có tin nhắn.</div>';
            }
            area.scrollTop = area.scrollHeight;
        })
        .catch(() => {
            area.innerHTML = '<div class="empty-state text-danger">Không tải được tin nhắn.</div>';
        });
}

function createMsgBubble(role, content, createdAt, formBlockType) {
    const div = document.createElement('div');
    div.className = `chat-bubble chat-bubble-${role}`;
    const label = role === 'user' ? 'Khách' : role === 'ai' ? 'AI' : role === 'admin' ? 'Nhân viên' : 'Hệ thống';
    const time = createdAt ? new Date(createdAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) : '';
    const formLabel = formBlockLabels[formBlockType] || formBlockType;
    div.innerHTML = `
        <div class="chat-bubble-label">${label}${time ? ' · ' + time : ''}</div>
        <div class="chat-bubble-content">${escapeHtml(content)}</div>
        ${formBlockType ? `<div class="chat-bubble-form">${escapeHtml(formLabel)}</div>` : ''}
    `;
    return div;
}

function pauseAI() {
    if (!currentSessionId || !connection) return;
    connection.invoke('adminPause', currentSessionId).catch(console.error);
}

function resumeAI() {
    if (!currentSessionId || !connection) return;
    connection.invoke('adminResume', currentSessionId).catch(console.error);
}

function sendReply() {
    const input = document.getElementById('admin-reply-input');
    const content = input.value.trim();
    if (!content || !currentSessionId || !connection) return;
    connection.invoke('adminReply', currentSessionId, content).catch(console.error);
    input.value = '';
}

function sendFormBlock(type) {
    if (!currentSessionId || !connection) return;
    const defaultJson = {
        roomSelector: JSON.stringify({ type: 'roomSelector', label: 'Chọn phòng' }),
        slotPicker: JSON.stringify({ type: 'slotPicker', label: 'Chọn khung giờ' }),
        infoForm: JSON.stringify({ type: 'infoForm', label: 'Nhập thông tin', fields: ['customerName', 'customerPhone', 'customerEmail'] }),
        paymentQr: JSON.stringify({ type: 'paymentQr', label: 'Thanh toán' })
    };
    const label = type === 'roomSelector' ? 'chọn phòng' : type === 'slotPicker' ? 'chọn khung giờ' : type === 'infoForm' ? 'điền thông tin' : 'thanh toán';
    connection.invoke('adminReply', currentSessionId, `Vui lòng ${label}`, defaultJson[type] || '{}', type).catch(console.error);
}

function showAutoReplyConfig() {
    if (!currentSessionId) return;
    const current = sessions[currentSessionId];
    const msg = prompt('Nhập tin nhắn tự động khi AI tạm dừng:', current?.autoReplyMessage || '');
    if (msg !== null) {
        fetch(`/admin/chat-monitor/session/${encodeURIComponent(currentSessionId)}/auto-reply`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ autoReplyMessage: msg })
        });
    }
}

function filterSessions() {
    renderSessionList();
}

function shortSessionId(sessionId) {
    return (sessionId || '').slice(0, 6) || '--';
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text || '';
    return div.innerHTML;
}

function timeAgo(dateStr) {
    if (!dateStr) return '';
    const now = new Date();
    const date = new Date(dateStr);
    const diff = Math.floor((now - date) / 60000);
    if (diff < 1) return 'Vừa xong';
    if (diff < 60) return diff + ' phút trước';
    if (diff < 1440) return Math.floor(diff / 60) + ' giờ trước';
    return Math.floor(diff / 1440) + ' ngày trước';
}
```

- [ ] **Step 2: Check JavaScript syntax in browser console later**

No standalone test runner is configured for this file. During manual verification in Task 5, open browser devtools on `/admin/chat-monitor` and confirm no JavaScript syntax errors appear.

## Task 3: Redesign chat monitor CSS

**Files:**
- Modify: `WebHomestay/wwwroot/css/admin-chat-monitor.css`

- [ ] **Step 1: Replace chat monitor stylesheet**

Replace the full contents of `WebHomestay/wwwroot/css/admin-chat-monitor.css` with:

```css
.chat-monitor-shell {
    display: flex;
    height: calc(100vh - 96px);
    min-height: 640px;
    background: #f3f6fb;
    border: 1px solid #e5e7eb;
    overflow: hidden;
    box-shadow: 0 20px 50px rgba(15, 23, 42, 0.08);
}

.chat-session-sidebar {
    width: 340px;
    min-width: 300px;
    background: #ffffff;
    border-right: 1px solid #e5e7eb;
    display: flex;
    flex-direction: column;
}

.sidebar-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 16px;
    padding: 22px 20px 18px;
    border-bottom: 1px solid #edf2f7;
}

.sidebar-kicker,
.chat-header-kicker {
    display: block;
    color: #64748b;
    font-size: 0.72rem;
    font-weight: 800;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    margin-bottom: 4px;
}

.sidebar-header h1 {
    margin: 0;
    color: #0f172a;
    font-size: 1.35rem;
    font-weight: 800;
}

.session-count {
    min-width: 34px;
    height: 34px;
    padding: 0 10px;
    border-radius: 999px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    color: #ffffff;
    background: #2563eb;
    font-weight: 800;
}

.sidebar-search {
    position: relative;
    padding: 14px 18px;
    border-bottom: 1px solid #edf2f7;
}

.sidebar-search i {
    position: absolute;
    top: 50%;
    left: 32px;
    transform: translateY(-50%);
    color: #94a3b8;
    font-size: 0.9rem;
}

.sidebar-search .form-control {
    border: 1px solid #e2e8f0;
    border-radius: 14px;
    padding: 11px 12px 11px 38px;
    color: #0f172a;
    background: #f8fafc;
}

.sidebar-search .form-control:focus {
    border-color: #2563eb;
    box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.12);
}

.session-list {
    flex: 1;
    overflow-y: auto;
    padding: 8px;
}

.session-card {
    padding: 14px;
    border: 1px solid transparent;
    border-radius: 16px;
    cursor: pointer;
    transition: background 0.15s, border-color 0.15s, transform 0.15s;
}

.session-card:hover {
    background: #f8fbff;
    border-color: #dbeafe;
}

.session-card.active {
    background: #eff6ff;
    border-color: #93c5fd;
    box-shadow: inset 4px 0 0 #2563eb;
}

.session-card-top,
.session-card-meta,
.chat-title-row,
.chat-action-group {
    display: flex;
    align-items: center;
}

.session-card-top,
.session-card-meta {
    justify-content: space-between;
    gap: 10px;
}

.session-card-name {
    color: #0f172a;
    font-size: 0.95rem;
    font-weight: 800;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.session-time,
.session-code {
    color: #64748b;
    white-space: nowrap;
}

.session-card-preview {
    color: #64748b;
    font-size: 0.86rem;
    line-height: 1.4;
    margin: 8px 0 10px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.session-status {
    border-radius: 999px;
    padding: 4px 9px;
    font-size: 0.72rem;
    font-weight: 800;
}

.session-status.auto,
.chat-status-badge.auto {
    color: #047857;
    background: #d1fae5;
}

.session-status.paused,
.chat-status-badge.paused {
    color: #a16207;
    background: #fef3c7;
}

.chat-main-panel {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    background: #f8fafc;
}

.chat-placeholder {
    flex: 1;
    display: grid;
    place-items: center;
    padding: 32px;
}

.chat-placeholder-card {
    width: min(420px, 100%);
    padding: 36px;
    text-align: center;
    background: #ffffff;
    border: 1px solid #e5e7eb;
    border-radius: 24px;
    box-shadow: 0 16px 36px rgba(15, 23, 42, 0.06);
}

.chat-placeholder-card i {
    color: #2563eb;
    font-size: 3rem;
    margin-bottom: 16px;
}

.chat-placeholder-card h2 {
    color: #0f172a;
    font-size: 1.25rem;
    font-weight: 800;
    margin-bottom: 8px;
}

.chat-placeholder-card p {
    color: #64748b;
    margin: 0;
}

.chat-active-panel {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
}

.chat-active-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 16px;
    padding: 16px 22px;
    background: #ffffff;
    border-bottom: 1px solid #e5e7eb;
}

.chat-customer-block {
    min-width: 0;
}

.chat-title-row {
    gap: 10px;
    flex-wrap: wrap;
}

.chat-title-row strong {
    color: #0f172a;
    font-size: 1.05rem;
}

.chat-session-code {
    color: #64748b;
    font-size: 0.84rem;
    font-weight: 700;
}

.chat-status-badge {
    width: fit-content;
    margin-top: 8px;
    border-radius: 999px;
    padding: 6px 10px;
    font-size: 0.75rem;
}

.chat-action-group {
    justify-content: flex-end;
    flex-wrap: wrap;
    gap: 8px;
}

.chat-action-group .btn {
    border-radius: 999px;
    font-weight: 700;
    padding: 8px 12px;
}

.chat-messages-area {
    flex: 1;
    min-height: 0;
    overflow-y: auto;
    padding: 22px;
    background:
        radial-gradient(circle at 10% 0%, rgba(37, 99, 235, 0.07), transparent 28%),
        linear-gradient(180deg, #f8fafc 0%, #eef2f7 100%);
}

.chat-bubble {
    max-width: min(760px, 78%);
    margin-bottom: 14px;
    padding: 12px 15px;
    border-radius: 18px;
    color: #0f172a;
    font-size: 0.95rem;
    box-shadow: 0 10px 24px rgba(15, 23, 42, 0.05);
}

.chat-bubble-label {
    color: #64748b;
    font-size: 0.75rem;
    font-weight: 800;
    margin-bottom: 6px;
}

.chat-bubble-content {
    line-height: 1.6;
    white-space: pre-wrap;
}

.chat-bubble-form {
    display: inline-flex;
    margin-top: 8px;
    padding: 4px 8px;
    border-radius: 999px;
    background: #e0f2fe;
    color: #0369a1;
    font-size: 0.76rem;
    font-weight: 800;
}

.chat-bubble-user {
    background: #dbeafe;
    margin-right: auto;
    border-bottom-left-radius: 6px;
}

.chat-bubble-ai {
    background: #ffffff;
    margin-left: auto;
    border: 1px solid #e5e7eb;
    border-bottom-right-radius: 6px;
}

.chat-bubble-admin {
    background: #dcfce7;
    margin-left: auto;
    border-bottom-right-radius: 6px;
}

.chat-bubble-system {
    background: #fef3c7;
    margin-right: auto;
    border-bottom-left-radius: 6px;
}

.chat-reply-area {
    padding: 14px 22px 18px;
    background: #ffffff;
    border-top: 1px solid #e5e7eb;
}

.chat-reply-group {
    border: 1px solid #dbe3ef;
    border-radius: 18px;
    overflow: hidden;
    background: #ffffff;
}

.chat-reply-group .form-control,
.chat-reply-group .btn {
    border: 0;
    border-radius: 0;
}

.chat-reply-group .form-control {
    min-height: 52px;
    color: #0f172a;
}

.quick-send-button {
    font-weight: 800;
}

.send-reply-button {
    min-width: 58px;
}

.empty-state {
    padding: 28px 16px;
    color: #64748b;
    text-align: center;
    font-weight: 600;
}

@media (max-width: 991.98px) {
    .chat-monitor-shell {
        height: auto;
        min-height: calc(100vh - 96px);
        flex-direction: column;
    }

    .chat-session-sidebar {
        width: 100%;
        min-width: 0;
        max-height: 320px;
        border-right: 0;
        border-bottom: 1px solid #e5e7eb;
    }

    .chat-active-header {
        align-items: flex-start;
        flex-direction: column;
    }

    .chat-action-group {
        justify-content: flex-start;
    }

    .chat-bubble {
        max-width: 92%;
    }
}

@media (max-width: 575.98px) {
    .chat-monitor-shell {
        border-left: 0;
        border-right: 0;
    }

    .sidebar-header,
    .chat-active-header,
    .chat-messages-area,
    .chat-reply-area {
        padding-left: 14px;
        padding-right: 14px;
    }

    .chat-reply-group {
        display: grid;
        grid-template-columns: 1fr auto;
    }

    .quick-send-button {
        grid-column: 1 / -1;
        border-bottom: 1px solid #e5e7eb !important;
    }
}
```

- [ ] **Step 2: Build after CSS/Razor/JS updates**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: build succeeds or only unrelated pre-existing warnings appear.

## Task 4: Fix home search and chatbot overlap

**Files:**
- Modify: `WebHomestay/wwwroot/css/user-premium.css`

- [ ] **Step 1: Add safe spacing and responsive search CSS**

Append this CSS after the existing mobile chatbot media query near the end of `WebHomestay/wwwroot/css/user-premium.css`:

```css
/* Home search + chatbot spacing */
body:has(.ai-widget) .footer {
    padding-bottom: 92px !important;
}

.editorial-search {
    gap: 0;
}

.btn-search-luxury {
    min-height: 94px;
    white-space: nowrap;
}

.ai-widget {
    right: max(28px, env(safe-area-inset-right));
    bottom: max(28px, env(safe-area-inset-bottom));
}

@media (max-width: 1199.98px) {
    .editorial-search {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
    }

    .editorial-search .search-field {
        border-bottom: 1px solid var(--luxury-border);
    }

    .editorial-search .search-field:nth-child(2n) {
        border-right: none;
    }

    .btn-search-luxury {
        grid-column: 1 / -1;
        min-height: 64px;
        width: 100%;
    }
}

@media (max-width: 767.98px) {
    .editorial-search {
        grid-template-columns: 1fr;
        margin-top: 0;
    }

    .search-field {
        min-width: 0;
        border-right: none;
        border-bottom: 1px solid var(--luxury-border);
    }

    .btn-search-luxury {
        min-height: 58px;
        padding: 16px 24px;
    }

    .ai-widget {
        right: 18px;
        bottom: 18px;
    }

    body:has(.ai-widget) .footer {
        padding-bottom: 84px !important;
    }
}
```

- [ ] **Step 2: Check no Razor changes are needed for Home page**

Do not modify `WebHomestay/Views/Home/Index.cshtml` unless manual testing shows the button still overlaps. The CSS grid change should be enough because the search form markup already groups all fields and the submit button inside `.editorial-search`.

- [ ] **Step 3: Build after CSS update**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: build succeeds or only unrelated pre-existing warnings appear.

## Task 5: Manual browser verification

**Files:**
- No code changes unless verification finds an issue.

- [ ] **Step 1: Run the app**

Run:

```bash
dotnet run --project WebHomestay/WebHomestay.csproj
```

Expected: app starts and prints the local URL. Keep it running.

- [ ] **Step 2: Verify home page desktop**

Open the home page in a browser at the local URL. Confirm:

- The search form is readable.
- The `TÌM PHÒNG` button is inside the form and not covered by the chatbot.
- The chatbot button stays in the lower-right corner.
- The footer is not covered by the chatbot button.

- [ ] **Step 3: Verify home page mobile width**

Use browser responsive mode around 390px width. Confirm:

- Search fields stack vertically.
- `TÌM PHÒNG` spans the form width.
- Chatbot button does not cover the search button or footer.
- Opening the chatbot still shows a full-screen panel as before.

- [ ] **Step 4: Verify admin chat monitor**

Open `/admin/chat-monitor` while logged in as admin. Confirm:

- Sidebar labels are Vietnamese.
- Search works by customer name and session id.
- Selecting a session shows the chat panel.
- Header shows customer name, short session id, and Vietnamese AI status.
- Pause/resume buttons use Vietnamese labels and still update state.
- Message bubbles are readable and aligned correctly.
- Sending a text message works.
- The quick-send menu labels are Vietnamese.
- Browser console has no JavaScript syntax errors.

## Task 6: Final checks

**Files:**
- No code changes unless checks fail.

- [ ] **Step 1: Run final build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: build succeeds or only unrelated pre-existing warnings appear.

- [ ] **Step 2: Review changed files**

Run:

```bash
git diff -- WebHomestay/Views/AdminChatMonitor/Index.cshtml WebHomestay/wwwroot/js/admin-chat-monitor.js WebHomestay/wwwroot/css/admin-chat-monitor.css WebHomestay/wwwroot/css/user-premium.css docs/superpowers/specs/2026-05-26-chat-monitor-home-ui-design.md docs/superpowers/plans/2026-05-26-chat-monitor-home-ui.md
```

Expected: diff only includes the approved UI/design/plan changes.

## Self-Review

- Spec coverage: Admin chat monitor layout, Vietnamese labels, chat readability, quick actions, home search responsive behavior, and chatbot safe spacing are each covered by Tasks 1-5.
- Placeholder scan: no TBD/TODO/fill-in-later steps remain.
- Type consistency: IDs and class names used in Razor, JS, and CSS match: `chat-session-code`, `chat-status-badge`, `session-list`, `admin-reply-input`, `btn-pause-ai`, `btn-resume-ai`, `btn-config-auto-reply`, `btn-send-reply`.
