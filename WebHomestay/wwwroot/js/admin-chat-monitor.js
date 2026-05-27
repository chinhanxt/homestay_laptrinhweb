let connection = null;
let currentSessionId = null;
let sessions = {};
let totalUnreadCount = 0;

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
    document.getElementById('btn-save-auto-reply')?.addEventListener('click', saveAutoReplyConfig);
    document.querySelectorAll('[data-cancellation-filter]').forEach(button => {
        button.addEventListener('click', () => loadCancellationList(button.dataset.cancellationFilter || 'Pending'));
    });
    document.addEventListener('click', (e) => {
        const openButton = e.target.closest('[data-open-cancellation]');
        if (openButton) openCancellation(openButton.dataset.openCancellation);
    });
});

function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl('/chatHub')
        .withAutomaticReconnect()
        .build();

    connection.on('sessionUpdate', function (data) {
        sessions[data.sessionId] = { ...sessions[data.sessionId], ...data };
        if (typeof data.totalUnreadCount === 'number') totalUnreadCount = data.totalUnreadCount;
        renderSessionList();
        updateBadge();
        if (currentSessionId === data.sessionId) updateChatHeader(data);
    });

    connection.on('sessionList', function (list) {
        sessions = {};
        list.forEach(s => { sessions[s.sessionId] = s; });
        totalUnreadCount = list.reduce((sum, s) => sum + (Number(s.unreadCount) || 0), 0);
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
            totalUnreadCount = list.reduce((sum, s) => sum + (Number(s.unreadCount) || 0), 0);
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
                ${Number(s.pendingCancellationCount) > 0 ? '<span class="session-cancel-badge">Hủy phòng</span>' : ''}
                ${Number(s.unreadCount) > 0 ? `<span class="session-unread">${Number(s.unreadCount)}</span>` : ''}
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
    if (badge) badge.textContent = totalUnreadCount;
}

function selectSession(sessionId) {
    currentSessionId = sessionId;
    document.getElementById('chat-placeholder')?.classList.add('d-none');
    const panel = document.getElementById('chat-active-panel');
    if (panel) panel.classList.remove('d-none');
    renderSessionList();
    loadChatMessages(sessionId);
    updateChatHeader(sessions[sessionId] || {});
    markSessionRead(sessionId);
}

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
            renderCancellations(area, data.cancellations || []);
            if (!area.children.length) {
                area.innerHTML = '<div class="empty-state">Hội thoại này chưa có tin nhắn.</div>';
            }
            area.scrollTop = area.scrollHeight;
        })
        .catch(() => {
            area.innerHTML = '<div class="empty-state text-danger">Không tải được tin nhắn.</div>';
        });
}

function renderCancellations(container, cancellations) {
    cancellations.forEach(item => container.appendChild(createCancellationCard(item)));
}

function createCancellationCard(item) {
    const card = document.createElement('div');
    card.className = 'cancellation-card';
    card.innerHTML = `
        <div class="cancellation-card-header">
            <div>
                <span class="cancellation-kicker">Yêu cầu hủy phòng</span>
                <strong>${escapeHtml(item.customerName || 'Khách')}</strong>
            </div>
            <span class="cancellation-status cancellation-status-${escapeHtml((item.status || '').toLowerCase())}">${escapeHtml(cancellationStatusLabel(item.status))}</span>
        </div>
        <div class="cancellation-card-meta">
            <span>Mã đặt: ${escapeHtml(item.submittedBookingCode || (item.bookingId ? '#' + item.bookingId : 'Chưa liên kết'))}</span>
            <span>${formatDateTime(item.createdAt)}</span>
        </div>
        ${item.staffReason ? `<div class="cancellation-card-reason">${escapeHtml(item.staffReason)}</div>` : ''}
        <button class="btn btn-sm btn-outline-primary" type="button" data-open-cancellation="${item.id}">Xem và xử lý</button>
    `;
    return card;
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
    fetch(`/admin/chat-monitor/session/${encodeURIComponent(currentSessionId)}/quick-block/${encodeURIComponent(type)}`)
        .then(r => r.ok ? r.json() : Promise.reject(new Error('Không tạo được form gửi nhanh.')))
        .then(block => {
            const formBlockJson = JSON.stringify({ uiBlocks: block.uiBlocks || [] });
            connection.invoke(
                'adminReply',
                currentSessionId,
                block.message || 'Mình gửi bạn thông tin để thao tác nhé.',
                formBlockJson,
                block.formBlockType || 'uiBlocks').catch(console.error);
        })
        .catch(error => {
            console.error(error);
            connection.invoke('adminReply', currentSessionId, 'Hiện chưa tạo được form này, bạn nhắn lại nhu cầu để mình hỗ trợ nhé.').catch(console.error);
        });
}

function showAutoReplyConfig() {
    if (!currentSessionId) return;
    const current = sessions[currentSessionId];
    const textarea = document.getElementById('auto-reply-message');
    const modalEl = document.getElementById('auto-reply-modal');
    if (!textarea || !modalEl || typeof bootstrap === 'undefined') return;
    textarea.value = current?.autoReplyMessage || '';
    bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function saveAutoReplyConfig() {
    if (!currentSessionId) return;
    const textarea = document.getElementById('auto-reply-message');
    const modalEl = document.getElementById('auto-reply-modal');
    const msg = textarea?.value || '';
    fetch(`/admin/chat-monitor/session/${encodeURIComponent(currentSessionId)}/auto-reply`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ autoReplyMessage: msg })
    }).then(() => {
        sessions[currentSessionId] = { ...sessions[currentSessionId], autoReplyMessage: msg };
        if (modalEl && typeof bootstrap !== 'undefined') {
            bootstrap.Modal.getOrCreateInstance(modalEl).hide();
        }
    }).catch(console.error);
}

function filterSessions() {
    renderSessionList();
}

function loadCancellationList(status) {
    const area = document.getElementById('chat-messages-area');
    document.getElementById('chat-placeholder')?.classList.add('d-none');
    document.getElementById('chat-active-panel')?.classList.remove('d-none');
    document.querySelectorAll('[data-cancellation-filter]').forEach(button => {
        const active = button.dataset.cancellationFilter === status;
        button.classList.toggle('btn-primary', active);
        button.classList.toggle('btn-outline-secondary', !active);
    });
    if (!area) return;
    area.innerHTML = '<div class="empty-state">Đang tải yêu cầu hủy...</div>';
    fetch(`/admin/chat-monitor/cancellations?status=${encodeURIComponent(status)}`)
        .then(r => r.ok ? r.json() : Promise.reject(new Error('Không tải được yêu cầu hủy.')))
        .then(items => {
            area.innerHTML = `<div class="cancellation-list-title">${escapeHtml(cancellationStatusLabel(status))}</div>`;
            if (!items.length) {
                area.innerHTML += '<div class="empty-state">Không có yêu cầu hủy phù hợp.</div>';
                return;
            }
            items.forEach(item => area.appendChild(createCancellationCard(item)));
        })
        .catch(() => {
            area.innerHTML = '<div class="empty-state text-danger">Không tải được yêu cầu hủy.</div>';
        });
}

function openCancellation(id) {
    const detail = document.getElementById('cancellation-detail');
    const modalEl = document.getElementById('cancellation-modal');
    if (!detail || !modalEl) return;
    detail.innerHTML = '<div class="empty-state">Đang tải chi tiết yêu cầu hủy...</div>';
    if (typeof bootstrap !== 'undefined') bootstrap.Modal.getOrCreateInstance(modalEl).show();

    fetch(`/admin/chat-monitor/cancellations/${encodeURIComponent(id)}`)
        .then(r => r.ok ? r.json() : Promise.reject(new Error('Không tải được chi tiết.')))
        .then(data => renderCancellationDetail(data))
        .catch(() => {
            detail.innerHTML = '<div class="empty-state text-danger">Không tải được chi tiết yêu cầu hủy.</div>';
        });
}

function renderCancellationDetail(data) {
    const detail = document.getElementById('cancellation-detail');
    if (!detail) return;
    const request = data.request || {};
    const booking = data.booking;
    const suggestions = data.suggestions || [];
    const canProcess = request.status === 'Pending';
    detail.innerHTML = `
        <div class="cancellation-detail-grid">
            <section class="cancellation-section">
                <h3>Thông tin khách</h3>
                <dl>
                    <dt>Họ tên</dt><dd>${escapeHtml(request.customerName)}</dd>
                    <dt>Số điện thoại</dt><dd>${escapeHtml(request.customerPhone)}</dd>
                    <dt>Email</dt><dd>${escapeHtml(request.customerEmail)}</dd>
                    <dt>Mã khách nhập</dt><dd>${escapeHtml(request.submittedBookingCode || 'Chưa có')}</dd>
                    <dt>Trạng thái</dt><dd>${escapeHtml(cancellationStatusLabel(request.status))}</dd>
                </dl>
            </section>
            <section class="cancellation-section">
                <h3>Minh chứng bảo vệ</h3>
                <div class="protected-file-links">
                    <a class="btn btn-sm btn-outline-secondary" href="/admin/chat-monitor/cancellations/${request.id}/file/confirmation" target="_blank" rel="noopener">Ảnh email xác nhận</a>
                    ${request.refundQrImagePath ? `<a class="btn btn-sm btn-outline-secondary" href="/admin/chat-monitor/cancellations/${request.id}/file/refund-qr" target="_blank" rel="noopener">QR nhận hoàn tiền</a>` : ''}
                    ${request.refundBillProofPath ? `<a class="btn btn-sm btn-outline-secondary" href="/admin/chat-monitor/cancellations/${request.id}/file/refund-bill" target="_blank" rel="noopener">Bill hoàn tiền</a>` : ''}
                </div>
            </section>
        </div>
        ${renderBookingSummary(booking, suggestions)}
        <section class="cancellation-section">
            <h3>Xử lý yêu cầu</h3>
            <label class="form-label" for="cancellation-staff-reason">Lý do/ghi chú cho khách</label>
            <textarea class="form-control" id="cancellation-staff-reason" rows="3" ${canProcess ? '' : 'disabled'}>${escapeHtml(request.staffReason || '')}</textarea>
            <label class="form-label mt-3" for="cancellation-refund-percent">Phần trăm hoàn tiền áp dụng</label>
            <input class="form-control" id="cancellation-refund-percent" type="number" min="0" max="100" value="${request.appliedRefundPercent ?? request.refundPercentBeforeNoticeSnapshot ?? 0}" ${canProcess ? '' : 'disabled'} />
            <label class="form-label mt-3" for="cancellation-refund-bill">Bill hoàn tiền khi chấp nhận</label>
            <input class="form-control" id="cancellation-refund-bill" type="file" accept="image/*,.pdf" ${canProcess ? '' : 'disabled'} />
            <div class="cancellation-actions">
                <button class="btn btn-success" type="button" id="btn-approve-cancellation" ${canProcess ? '' : 'disabled'}>Chấp nhận hủy</button>
                <button class="btn btn-outline-danger" type="button" id="btn-reject-cancellation" ${canProcess ? '' : 'disabled'}>Từ chối</button>
            </div>
            <div class="cancellation-action-message" id="cancellation-action-message"></div>
        </section>
    `;
    document.getElementById('btn-approve-cancellation')?.addEventListener('click', () => approveCancellation(request.id));
    document.getElementById('btn-reject-cancellation')?.addEventListener('click', () => rejectCancellation(request.id));
}

function renderBookingSummary(booking, suggestions) {
    const bookingHtml = booking ? `
        <div class="booking-summary-card">
            <strong>#${booking.id} · ${escapeHtml(booking.roomName || 'Phòng')}</strong>
            <span>${escapeHtml(booking.customerName || '')} · ${escapeHtml(booking.customerPhone || '')}</span>
            <span>${formatDateTime(booking.startTime)} - ${formatDateTime(booking.endTime)}</span>
            <span>Trạng thái: ${escapeHtml(booking.status)} · Tổng tiền: ${formatCurrency(booking.totalPrice)}</span>
        </div>` : '<div class="empty-state compact">Chưa liên kết booking.</div>';
    const suggestionsHtml = suggestions.length
        ? suggestions.map(b => `<div class="booking-summary-card muted"><strong>#${b.id} · ${escapeHtml(b.roomName || 'Phòng')}</strong><span>${escapeHtml(b.customerName || '')} · ${formatDateTime(b.startTime)}</span></div>`).join('')
        : '<div class="empty-state compact">Không có booking gợi ý.</div>';
    return `
        <section class="cancellation-section">
            <h3>Booking liên kết</h3>
            ${bookingHtml}
        </section>
        <section class="cancellation-section">
            <h3>Booking gợi ý</h3>
            ${suggestionsHtml}
        </section>`;
}

function approveCancellation(id) {
    const formData = new FormData();
    formData.append('staffReason', document.getElementById('cancellation-staff-reason')?.value || 'Đã duyệt yêu cầu hủy.');
    formData.append('appliedRefundPercent', document.getElementById('cancellation-refund-percent')?.value || '0');
    const file = document.getElementById('cancellation-refund-bill')?.files?.[0];
    if (file) formData.append('refundBillProof', file);
    postCancellationAction(`/admin/chat-monitor/cancellations/${encodeURIComponent(id)}/approve`, formData, id);
}

function rejectCancellation(id) {
    const formData = new FormData();
    formData.append('staffReason', document.getElementById('cancellation-staff-reason')?.value || 'Yêu cầu hủy chưa đủ điều kiện xử lý.');
    postCancellationAction(`/admin/chat-monitor/cancellations/${encodeURIComponent(id)}/reject`, formData, id);
}

function postCancellationAction(url, formData, id) {
    const message = document.getElementById('cancellation-action-message');
    if (message) message.textContent = 'Đang xử lý...';
    fetch(url, { method: 'POST', body: formData })
        .then(r => r.ok ? r.json() : r.json().then(err => Promise.reject(err)))
        .then(() => {
            if (message) message.textContent = 'Đã cập nhật yêu cầu hủy.';
            openCancellation(id);
            loadSessionsFallback();
            if (currentSessionId) loadChatMessages(currentSessionId);
        })
        .catch(err => {
            if (message) message.textContent = err?.message || 'Không xử lý được yêu cầu hủy.';
        });
}

function cancellationStatusLabel(status) {
    if (status === 'Approved') return 'Đã chấp nhận';
    if (status === 'Rejected') return 'Đã từ chối';
    return 'Yêu cầu hủy chờ xử lý';
}

function formatDateTime(value) {
    if (!value) return '';
    return new Date(value).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
}

function formatCurrency(value) {
    return Number(value || 0).toLocaleString('vi-VN', { style: 'currency', currency: 'VND' });
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
