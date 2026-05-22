let connection = null;
let currentSessionId = null;
let sessions = {};

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
        console.error('SignalR connection failed:', err);
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
    sorted.forEach(s => {
        if (search && !s.sessionId.toLowerCase().includes(search) &&
            (s.customerName || '').toLowerCase().includes(search)) return;
        const div = document.createElement('div');
        div.className = `session-card ${currentSessionId === s.sessionId ? 'active' : ''}`;
        div.dataset.sessionId = s.sessionId;
        div.innerHTML = `
            <div class="session-card-name">${escapeHtml(s.customerName || 'Khach ' + s.sessionId.slice(0, 6))}</div>
            <div class="session-card-preview">${escapeHtml((s.lastMessage || '').slice(0, 50))}</div>
            <div class="session-card-meta">
                <span class="badge ${s.status === 'paused' ? 'bg-warning' : 'bg-success'}">${s.status}</span>
                <small class="text-muted">${timeAgo(s.lastActivityAt)}</small>
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
    const badge = document.getElementById('chat-status-badge');
    const pauseBtn = document.getElementById('btn-pause-ai');
    const resumeBtn = document.getElementById('btn-resume-ai');
    if (name) name.textContent = data.customerName || 'Khach ' + (currentSessionId || '').slice(0, 6);
    if (badge) {
        badge.textContent = data.status === 'paused' ? 'Da pause AI' : 'AI dang tra loi';
        badge.className = `badge ${data.status === 'paused' ? 'bg-warning' : 'bg-success'}`;
    }
    if (pauseBtn) pauseBtn.classList.toggle('d-none', data.status === 'paused');
    if (resumeBtn) resumeBtn.classList.toggle('d-none', data.status !== 'paused');
}

function loadChatMessages(sessionId) {
    const area = document.getElementById('chat-messages-area');
    area.innerHTML = '<div class="text-center text-muted p-3">Dang tai...</div>';

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
            area.scrollTop = area.scrollHeight;
        })
        .catch(() => {
            area.innerHTML = '<div class="text-center text-danger p-3">Loi tai tin nhan</div>';
        });
}

function createMsgBubble(role, content, createdAt, formBlockType) {
    const div = document.createElement('div');
    div.className = `chat-bubble chat-bubble-${role}`;
    const label = role === 'user' ? 'Khach' : role === 'ai' ? 'AI' : role === 'admin' ? 'Admin' : 'He thong';
    const time = createdAt ? new Date(createdAt).toLocaleTimeString('vi-VN') : '';
    div.innerHTML = `
        <div class="chat-bubble-label">${label} · ${time}</div>
        <div class="chat-bubble-content">${escapeHtml(content)}</div>
        ${formBlockType ? `<div class="chat-bubble-form badge bg-info">📋 ${formBlockType}</div>` : ''}
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
        roomSelector: JSON.stringify({ type: 'roomSelector', label: 'Chon phong' }),
        slotPicker: JSON.stringify({ type: 'slotPicker', label: 'Chon khung gio' }),
        infoForm: JSON.stringify({ type: 'infoForm', label: 'Nhap thong tin', fields: ['customerName', 'customerPhone', 'customerEmail'] }),
        paymentQr: JSON.stringify({ type: 'paymentQr', label: 'Thanh toan' })
    };
    const label = type === 'roomSelector' ? 'chon phong' : type === 'slotPicker' ? 'chon khung gio' : type === 'infoForm' ? 'dien thong tin' : 'thanh toan';
    connection.invoke('adminReply', currentSessionId, `📋 Vui long ${label}`, defaultJson[type] || '{}', type).catch(console.error);
}

function showAutoReplyConfig() {
    const current = sessions[currentSessionId];
    const msg = prompt('Nhap tin nhan tu dong khi AI bi pause:', current?.autoReplyMessage || '');
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
    if (diff < 1) return 'Vua xong';
    if (diff < 60) return diff + ' phut';
    return Math.floor(diff / 60) + ' gio';
}
