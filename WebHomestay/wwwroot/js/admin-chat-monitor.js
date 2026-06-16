let connection = null;
let currentSessionId = null;
let sessions = {};
let totalUnreadCount = 0;
let cancellationMailPreviewState = null;
let quickSendModalState = null;
let currentSessionScope = 'active';

const statusLabels = {
    paused: 'AI đã tạm dừng',
    auto: 'AI đang trả lời',
    active: 'AI đang trả lời'
};

const formBlockLabels = {
    roomSelector: 'Danh sách phòng',
    slotPicker: 'Khung giờ',
    infoForm: 'Form thông tin',
    bookingCta: 'CTA đặt phòng',
    handoffContact: 'Liên hệ chi nhánh',
    AskInfo: 'Yêu cầu thông tin',
    askInfo: 'Yêu cầu thông tin',
    reply: 'Phản hồi',
    retry: 'Thử lại',
    'start-booking': 'Bắt đầu đặt phòng',
    startBooking: 'Bắt đầu đặt phòng',
    showRooms: 'Hiển thị phòng trống',
    selectSlot: 'Chọn khung giờ',
    autoBook: 'Đặt phòng tự động',
    bookingForm: 'Biểu mẫu đặt phòng',
    paymentQr: 'Yêu cầu thanh toán',
    uiBlocks: 'Khối tương tác',
    error: 'Lỗi'
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
            openQuickSendComposer(el.dataset.type);
        });
    });
    document.querySelectorAll('[data-session-scope]').forEach(button => {
        button.addEventListener('click', () => switchSessionScope(button.dataset.sessionScope || 'active'));
    });
    document.getElementById('btn-config-auto-reply')?.addEventListener('click', showAutoReplyConfig);
    document.getElementById('btn-save-auto-reply')?.addEventListener('click', saveAutoReplyConfig);
    document.getElementById('btn-soft-delete-session')?.addEventListener('click', () => softDeleteCurrentSession());
    document.getElementById('btn-restore-session')?.addEventListener('click', () => restoreCurrentSession());
    document.getElementById('btn-permanent-delete-session')?.addEventListener('click', () => permanentlyDeleteCurrentSession());
    document.querySelectorAll('[data-cancellation-filter]').forEach(button => {
        button.addEventListener('click', () => loadCancellationList(button.dataset.cancellationFilter || 'Pending'));
    });
    document.addEventListener('click', (e) => {
        const openButton = e.target.closest('[data-open-cancellation]');
        if (openButton) {
            const card = openButton.closest('.cancellation-card');
            const chatSessionId = card ? card.dataset.chatSessionId : currentSessionId;
            openCancellation(openButton.dataset.openCancellation, chatSessionId);
        }
    });
    document.getElementById('btn-manual-cancellation')?.addEventListener('click', openManualCancellationModal);
    document.getElementById('btn-lookup-booking')?.addEventListener('click', lookupBookingForCancellation);
    document.getElementById('btn-submit-manual-cancellation')?.addEventListener('click', submitManualCancellation);
    document.getElementById('manual-zalo-image')?.addEventListener('change', checkManualFormReady);
    document.getElementById('manual-cancellation-modal')?.addEventListener('hidden.bs.modal', resetManualCancellationForm);

    // Sidebar Tabs Switcher Initialization
    const tabChats = document.getElementById('tab-chats-btn');
    const tabCancellations = document.getElementById('tab-cancellations-btn');
    const contentChats = document.getElementById('tab-content-chats');
    const contentCancellations = document.getElementById('tab-content-cancellations');

    if (tabChats && tabCancellations && contentChats && contentCancellations) {
        tabChats.addEventListener('click', () => {
            tabChats.classList.add('active');
            tabCancellations.classList.remove('active');
            contentChats.classList.remove('d-none');
            contentCancellations.classList.add('d-none');
        });

        tabCancellations.addEventListener('click', () => {
            tabCancellations.classList.add('active');
            tabChats.classList.remove('active');
            contentCancellations.classList.remove('d-none');
            contentChats.classList.add('d-none');
            
            const listContainer = document.getElementById('cancellation-list-sidebar');
            if (listContainer && !listContainer.querySelector('.cancellation-card')) {
                loadCancellationList('Pending');
            }
        });
    }

    // Mobile details toggle button listener
    document.getElementById('btn-toggle-context')?.addEventListener('click', () => {
        const panel = document.getElementById('chat-context-panel');
        if (panel) panel.classList.toggle('d-none');
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

    connection.on('newMessage', function (data) {
        if (currentSessionId === data.sessionId) {
            const area = document.getElementById('chat-messages-area');
            if (area) {
                const emptyState = area.querySelector('.empty-state');
                if (emptyState) emptyState.remove();

                const lastBubble = area.querySelector('.chat-bubble:last-child');
                if (lastBubble) {
                    const isSameRole = lastBubble.classList.contains(`chat-bubble-${data.role}`);
                    const contentEl = lastBubble.querySelector('.chat-bubble-content');
                    const isSameContent = contentEl && contentEl.textContent.trim() === (data.content || '').trim();
                    if (isSameRole && isSameContent) {
                        return;
                    }
                }

                area.appendChild(createMsgBubble(data.role, data.content, data.createdAt, data.formBlockType, data.formBlockJson));
                area.scrollTop = area.scrollHeight;
            }
        }
    });



    connection.on('sessionList', function (list) {
        sessions = {};
        list.forEach(s => { sessions[s.sessionId] = s; });
        totalUnreadCount = list.length > 0 && typeof list[0].totalUnreadCount === 'number'
            ? list[0].totalUnreadCount
            : 0;
        renderSessionList();
        updateBadge();
    });

    connection.on('sessionDeleted', function (data) {
        if (currentSessionScope === 'active') {
            delete sessions[data.sessionId];
        } else {
            loadSessionsFallback();
            return;
        }

        if (currentSessionId === data.sessionId) {
            hideActiveChatPanel();
        }
        renderSessionList();
    });

    connection.on('sessionRestored', function (data) {
        if (currentSessionScope === 'deleted') {
            delete sessions[data.sessionId];
        } else {
            sessions[data.sessionId] = { ...sessions[data.sessionId], ...data };
        }
        renderSessionList();
    });

    connection.on('sessionPurged', function (data) {
        delete sessions[data.sessionId];
        if (currentSessionId === data.sessionId) {
            hideActiveChatPanel();
        }
        renderSessionList();
    });

    connection.start().then(function () {
        connection.invoke('joinAdmin');
        loadSessionsFallback();
    }).catch(function (err) {
        console.error('Không thể kết nối SignalR:', err);
    });
}

function loadSessionsFallback() {
    fetch(`/chinhan/hethong/chat-monitor/sessions?scope=${encodeURIComponent(currentSessionScope)}`)
        .then(r => r.json())
        .then(list => {
            sessions = {};
            list.forEach(s => { sessions[s.sessionId] = { ...sessions[s.sessionId], ...s }; });
            totalUnreadCount = list.length > 0 && typeof list[0].totalUnreadCount === 'number'
                ? list[0].totalUnreadCount
                : 0;
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
        const deletedMeta = s.isDeleted && s.deletedBy
            ? `<span class="session-deleted-note">Đã xóa bởi ${escapeHtml(s.deletedBy)}</span>`
            : '';
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
                <span class="session-status ${s.isDeleted ? 'deleted' : isPaused ? 'paused' : 'auto'}">${s.isDeleted ? 'Đã xóa mềm' : isPaused ? 'Đã tạm dừng AI' : 'Tự động'}</span>
                <span class="session-code">#${shortSessionId(s.sessionId)}</span>
            </div>
            ${deletedMeta}
        `;
        div.addEventListener('click', () => selectSession(s.sessionId));
        container.appendChild(div);
    });
}

function updateBadge() {
    const badge = document.getElementById('session-count-badge');
    if (badge) badge.textContent = totalUnreadCount;
}

function selectSession(sessionId, options = {}) {
    currentSessionId = sessionId;
    document.getElementById('chat-placeholder')?.classList.add('d-none');
    const panel = document.getElementById('chat-active-panel');
    if (panel) panel.classList.remove('d-none');
    
    // Show Right Context Panel
    const contextPanel = document.getElementById('chat-context-panel');
    if (contextPanel) contextPanel.classList.remove('d-none');
    
    if (options.keepCancellationActive) {
        document.getElementById('context-cancellation-view')?.classList.remove('d-none');
        document.getElementById('context-session-view')?.classList.add('d-none');
        document.querySelector('.chat-reply-area')?.classList.add('d-none');
    } else {
        document.getElementById('context-session-view')?.classList.remove('d-none');
        document.getElementById('context-cancellation-view')?.classList.add('d-none');
        document.querySelector('.chat-reply-area')?.classList.remove('d-none');
        updateSessionInfoStatusBox(sessions[sessionId] || {});
        // Clear active status on sidebar cancellations
        activeCancellationId = null;
        document.querySelectorAll('#cancellation-list-sidebar .cancellation-card').forEach(c => {
            c.classList.remove('active');
        });
    }
    
    // Restore active state of filters back to normal when selecting a regular session
    if (!options.keepCancellationActive) {
        document.querySelectorAll('[data-cancellation-filter]').forEach(button => {
            button.classList.remove('btn-primary');
            button.classList.add('btn-outline-secondary');
        });
    }
    
    const activeHeader = document.getElementById('chat-active-header');
    if (activeHeader) activeHeader.style.display = 'flex';
    
    const actionGroup = document.querySelector('.chat-action-group');
    if (actionGroup) actionGroup.style.display = 'flex';
    
    renderSessionList();
    loadChatMessages(sessionId);
    updateChatHeader(sessions[sessionId] || {});
    if (!sessions[sessionId]?.isDeleted) {
        markSessionRead(sessionId);
    }
}

function hideActiveChatPanel() {
    currentSessionId = null;
    document.getElementById('chat-active-panel')?.classList.add('d-none');
    document.getElementById('chat-context-panel')?.classList.add('d-none');
    document.getElementById('chat-placeholder')?.classList.remove('d-none');
}

function updateSessionInfoStatusBox(session) {
    const box = document.getElementById('session-info-status-box');
    if (!box) return;
    const isPaused = session.status === 'paused';
    const isDeleted = session.isDeleted === true;
    if (isDeleted) {
        box.textContent = 'Phiên đang nằm trong thùng rác';
        box.className = 'session-info-status deleted';
    } else if (isPaused) {
        box.textContent = 'AI đã tạm dừng · Nhân viên tiếp quản';
        box.className = 'session-info-status paused';
    } else {
        box.textContent = 'AI đang trả lời tự động';
        box.className = 'session-info-status auto';
    }
}

function markSessionRead(sessionId) {
    fetch(`/chinhan/hethong/chat-monitor/session/${encodeURIComponent(sessionId)}/mark-read`, { method: 'POST' })
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
    const autoReplyBtn = document.getElementById('btn-config-auto-reply');
    const softDeleteBtn = document.getElementById('btn-soft-delete-session');
    const restoreBtn = document.getElementById('btn-restore-session');
    const permanentDeleteBtn = document.getElementById('btn-permanent-delete-session');
    const isPaused = data.status === 'paused';
    const isDeleted = data.isDeleted === true;

    if (name) name.textContent = data.customerName || 'Khách ' + shortSessionId(currentSessionId);
    if (code) code.textContent = 'Phiên #' + shortSessionId(currentSessionId);
    if (badge) {
        badge.textContent = isDeleted ? 'Phiên đang nằm trong thùng rác' : (statusLabels[data.status] || 'AI đang trả lời');
        badge.className = `badge chat-status-badge ${isDeleted ? 'deleted' : isPaused ? 'paused' : 'auto'}`;
    }
    if (pauseBtn) pauseBtn.classList.toggle('d-none', isPaused || isDeleted);
    if (resumeBtn) resumeBtn.classList.toggle('d-none', !isPaused || isDeleted);
    if (autoReplyBtn) autoReplyBtn.classList.toggle('d-none', isDeleted);
    if (softDeleteBtn) softDeleteBtn.classList.toggle('d-none', isDeleted);
    if (restoreBtn) restoreBtn.classList.toggle('d-none', !isDeleted);
    if (permanentDeleteBtn) permanentDeleteBtn.classList.toggle('d-none', !isDeleted);

    const input = document.getElementById('admin-reply-input');
    const sendBtn = document.getElementById('btn-send-reply');
    if (input) input.disabled = isDeleted;
    if (sendBtn) sendBtn.disabled = isDeleted;
}

function loadChatMessages(sessionId) {
    const area = document.getElementById('chat-messages-area');
    area.innerHTML = '<div class="empty-state">Đang tải tin nhắn...</div>';

    fetch(`/chinhan/hethong/chat-monitor/session/${encodeURIComponent(sessionId)}`)
        .then(r => r.json())
        .then(data => {
            area.innerHTML = '';
            // Task 1 FIX: Chỉ render AdminChatMessages, không render traces để tránh lặp
            // data.traces chứa customer message + AI reply từ AIConversationTraces
            // data.messages chứa tất cả messages từ AdminChatMessages (bao gồm cả user/ai/admin/system)
            // => Chỉ cần render data.messages
            data.messages?.forEach(m => {
                area.appendChild(createMsgBubble(m.role, m.content, m.createdAt, m.formBlockType, m.formBlockJson));
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

function createMsgBubble(role, content, createdAt, formBlockType, formBlockJson) {
    const div = document.createElement('div');
    div.className = `chat-bubble chat-bubble-${role}`;
    const label = role === 'user' ? 'Khách' : role === 'ai' ? 'AI' : role === 'admin' ? 'Nhân viên' : 'Hệ thống';
    const time = createdAt ? new Date(createdAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) : '';
    const formLabel = formBlockLabels[formBlockType] || formBlockType;

    let displayContent = content || '';
    const actionPrefix = 'Thực hiện hành động: ';
    if (displayContent.startsWith(actionPrefix)) {
        const actionPart = displayContent.substring(actionPrefix.length).trim();
        const actionTranslations = {
            'start-booking': 'Bắt đầu đặt phòng',
            'startBooking': 'Bắt đầu đặt phòng',
            'select-booking-mode': 'Chọn hình thức đặt phòng',
            'select-branch': 'Chọn chi nhánh',
            'select-room': 'Chọn phòng',
            'commit-room': 'Xác nhận chọn phòng',
            'confirm-dates': 'Xác nhận ngày đặt',
            'select-slot': 'Chọn khung giờ',
            'submit-booking-form': 'Gửi form đặt phòng',
            'submit-form': 'Gửi form đặt phòng',
            'submit-contact-phone': 'Gửi thông tin liên hệ',
            'reply': 'Phản hồi',
            'retry': 'Thử lại',
            'AskInfo': 'Yêu cầu thông tin',
            'askInfo': 'Yêu cầu thông tin',
            'showRooms': 'Hiển thị phòng trống',
            'selectSlot': 'Chọn khung giờ',
            'autoBook': 'Đặt phòng tự động',
            'bookingForm': 'Biểu mẫu đặt phòng',
            'paymentQr': 'Mã QR thanh toán',
            'uiBlocks': 'Khối tương tác'
        };
        const translatedAction = actionTranslations[actionPart] || actionPart;
        displayContent = `Thực hiện hành động: ${translatedAction}`;
    }

    let formHtml = '';
    if (formBlockJson) {
        try {
            const parsed = JSON.parse(formBlockJson);
            const blocks = parsed.uiBlocks || [];
            blocks.forEach(block => {
                if (block.type === 'roomCards' || block.type === 'dailyRooms') {
                    const rooms = block.data?.rooms || [];
                    if (rooms.length > 0) {
                        formHtml += '<div class="chat-bubble-rooms mt-2 d-grid gap-2">';
                        rooms.forEach(room => {
                            const amenitiesHtml = Array.isArray(room.amenities) && room.amenities.length > 0
                                ? `<div class="mt-1 d-flex flex-wrap gap-1">${room.amenities.map(a => `<span class="badge bg-secondary" style="font-size:0.75em; color: #fff;">${escapeHtml(a)}</span>`).join('')}</div>`
                                : '';
                            formHtml += `
                                <div class="chat-bubble-room-card p-3 border rounded bg-white text-dark shadow-sm" style="font-size: 0.9em; max-width: 400px; line-height: 1.4; border-left: 4px solid #0d6efd !important;">
                                    <div class="fw-bold text-primary fs-6 mb-1">${escapeHtml(room.name || 'Phòng')}</div>
                                    ${room.description ? `<div class="text-muted mb-2" style="font-size: 0.85em; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;">${escapeHtml(room.description)}</div>` : ''}
                                    <div class="mb-1" style="font-size: 0.88em;">
                                        Giờ: <span class="text-danger fw-bold">${formatMoney(room.pricePerHour)}</span>/h · 
                                        Ngày: <span class="text-danger fw-bold">${formatMoney(room.pricePerDay || room.totalPrice)}</span>/ngày
                                    </div>
                                    <div class="text-muted" style="font-size: 0.85em;">
                                        Chuẩn: ${room.capacity} khách · Tối đa: ${room.maxGuests} khách
                                        ${room.extraGuestFee > 0 ? ` · Phụ thu: ${formatMoney(room.extraGuestFee)}` : ''}
                                    </div>
                                    ${amenitiesHtml}
                                    <div class="mt-2 d-flex gap-2">
                                        ${room.detailsUrl ? `<a href="${escapeHtml(room.detailsUrl)}" target="_blank" class="btn btn-sm btn-outline-secondary py-0 px-2" style="font-size:0.8em; line-height: 1.8;">Xem chi tiết</a>` : ''}
                                        <button class="btn btn-sm btn-primary py-0 px-2" disabled style="font-size:0.8em; line-height: 1.8;">Chọn phòng này</button>
                                    </div>
                                </div>
                            `;
                        });
                        formHtml += '</div>';
                    }
                } else if (block.type === 'hourlySlots') {
                    const slots = block.data?.slots || [];
                    if (slots.length > 0) {
                        formHtml += '<div class="chat-bubble-slots mt-2 d-flex flex-wrap gap-1" style="max-width:400px;">';
                        slots.forEach(slot => {
                            formHtml += `
                                <span class="badge bg-info text-dark p-2 border" style="font-size: 0.85em;">
                                    ${escapeHtml(slot.label)} · ${formatMoney(slot.totalPrice)}
                                </span>
                            `;
                        });
                        formHtml += '</div>';
                    }
                } else if (block.type === 'bookingForm') {
                    const fields = block.data?.fields || [];
                    if (fields.length > 0) {
                        formHtml += '<div class="chat-bubble-form-fields mt-2 p-2 border rounded bg-white text-dark text-start" style="font-size:0.85em; max-width: 400px; line-height: 1.4;">';
                        formHtml += '<div class="fw-bold mb-1 border-bottom pb-1 text-secondary">Form thông tin đặt phòng:</div>';
                        fields.forEach(f => {
                            const req = f.required ? ' <span class="text-danger">*</span>' : '';
                            formHtml += `<div class="mb-1"><strong>${escapeHtml(f.label)}:</strong> <span class="text-muted">[Khách nhập]</span>${req}</div>`;
                        });
                        formHtml += '</div>';
                    }
                } else if (block.type === 'bookingSummary') {
                    const lines = block.data?.lines || [];
                    const totalPrice = block.data?.totalPrice;
                    if (lines.length > 0) {
                        formHtml += '<div class="chat-bubble-summary mt-2 p-2 border rounded bg-white text-dark text-start" style="font-size:0.85em; max-width: 400px; border-left: 4px solid #198754 !important; line-height: 1.4;">';
                        formHtml += '<div class="fw-bold mb-1 border-bottom pb-1 text-success">Tóm tắt đặt phòng:</div>';
                        lines.forEach(line => {
                            formHtml += `<div class="mb-1">${escapeHtml(line)}</div>`;
                        });
                        if (totalPrice) {
                            formHtml += `<div class="fw-bold mt-1 text-success fs-6">Tạm tính: ${formatMoney(totalPrice)}</div>`;
                        }
                        formHtml += '</div>';
                    }
                } else if (block.type === 'paymentQr') {
                    formHtml += '<div class="chat-bubble-payment mt-2 p-2 border rounded bg-white text-dark text-start" style="font-size:0.85em; max-width: 400px; border-left: 4px solid #ffc107 !important; line-height: 1.4;">';
                    formHtml += '<div class="fw-bold mb-1 text-warning text-uppercase">Yêu cầu thanh toán</div>';
                    formHtml += `<div class="text-muted">Đã tạo link/QR thanh toán cho khách hàng.</div>`;
                    formHtml += '</div>';
                }
            });
        } catch (e) {
            console.error('Error parsing UI blocks JSON:', e);
        }
    }

    div.innerHTML = `
        <div class="chat-bubble-label">${label}${time ? ' · ' + time : ''}</div>
        <div class="chat-bubble-content">${escapeHtml(displayContent)}</div>
        ${formBlockType ? `<div class="chat-bubble-form">${escapeHtml(formLabel)}</div>` : ''}
        ${formHtml}
    `;
    return div;
}

function formatMoney(amount) {
    if (amount == null) return '0đ';
    return Number(amount).toLocaleString('vi-VN') + 'đ';
}

function pauseAI() {
    if (!currentSessionId || !connection) return;
    connection.invoke('adminPause', currentSessionId).catch(console.error);
}

function resumeAI() {
    if (!currentSessionId || !connection) return;
    connection.invoke('adminResume', currentSessionId).catch(console.error);
}

async function sendReply() {
    const input = document.getElementById('admin-reply-input');
    const content = input.value.trim();
    if (!content || !currentSessionId) return;

    const takenOver = await ensureManualTakeover();
    if (!takenOver) return;

    fetch(`/chinhan/hethong/chat-monitor/session/${encodeURIComponent(currentSessionId)}/reply`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ content })
    })
        .then(r => r.ok ? r.json() : r.json().then(err => Promise.reject(err)))
        .then(() => {
            input.value = '';
        })
        .catch(err => {
            premiumToast(err?.message || 'Không gửi được tin nhắn cho khách.', 'error');
        });
}

async function openQuickSendComposer(type) {
    if (!currentSessionId) return;

    const takenOver = await ensureManualTakeover();
    if (!takenOver) return;

    fetch(`/chinhan/hethong/chat-monitor/session/${encodeURIComponent(currentSessionId)}/quick-send/${encodeURIComponent(type)}/schema`)
        .then(r => r.ok ? r.json() : r.json().then(err => Promise.reject(err)))
        .then(schema => showQuickSendModal(type, schema))
        .catch(err => {
            premiumToast(err?.message || 'Không tải được điều kiện gửi nhanh.', 'error');
        });
}

async function ensureManualTakeover() {
    if (!currentSessionId) return false;
    const current = sessions[currentSessionId];
    if (current?.status === 'paused') return true;

    const accepted = await premiumConfirm('Sẽ tạm dừng AI để nhân viên tiếp quản. Bạn có muốn tiếp tục không?', {
        title: 'Tiếp quản hội thoại',
        confirmText: 'Tiếp quản',
        cancelText: 'Hủy',
        isDanger: false,
        type: 'info'
    });
    if (!accepted) return false;

    const response = await fetch(`/chinhan/hethong/chat-monitor/session/${encodeURIComponent(currentSessionId)}/takeover`, {
        method: 'POST'
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
        premiumAlert(data?.message || 'Không thể tạm dừng AI để tiếp quản phiên chat.', {
            title: 'Lỗi tiếp quản',
            type: 'error'
        });
        return false;
    }

    sessions[currentSessionId] = { ...sessions[currentSessionId], ...data };
    renderSessionList();
    updateChatHeader(sessions[currentSessionId]);
    return true;
}

function ensureQuickSendModal() {
    let modalEl = document.getElementById('quick-send-modal');
    if (modalEl) return modalEl;

    modalEl = document.createElement('div');
    modalEl.className = 'modal fade';
    modalEl.id = 'quick-send-modal';
    modalEl.tabIndex = -1;
    modalEl.setAttribute('aria-hidden', 'true');
    modalEl.innerHTML = `
        <div class="modal-dialog modal-dialog-centered">
            <div class="modal-content">
                <div class="modal-header">
                    <div>
                        <span class="modal-kicker">Gửi nhanh</span>
                        <h2 class="modal-title" id="quick-send-modal-title">Điều kiện gửi nhanh</h2>
                    </div>
                    <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Đóng"></button>
                </div>
                <div class="modal-body">
                    <form id="quick-send-form" class="d-grid gap-3"></form>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-light" data-bs-dismiss="modal">Hủy</button>
                    <button type="button" class="btn btn-primary" id="btn-submit-quick-send">Gửi cho khách</button>
                </div>
            </div>
        </div>
    `;
    document.body.appendChild(modalEl);
    document.getElementById('btn-submit-quick-send')?.addEventListener('click', submitQuickSendModal);
    return modalEl;
}

function showQuickSendModal(type, schema) {
    quickSendModalState = { type, schema };
    const modalEl = ensureQuickSendModal();
    const title = document.getElementById('quick-send-modal-title');
    const form = document.getElementById('quick-send-form');
    if (!form || !modalEl) return;

    if (title) title.textContent = schema?.title || 'Điều kiện gửi nhanh';
    form.innerHTML = '';

    const fields = Array.isArray(schema?.fields) ? schema.fields : [];
    fields.forEach(field => {
        form.appendChild(buildQuickSendField(field));
    });

    form.oninput = syncQuickSendFieldVisibility;
    form.onchange = syncQuickSendFieldVisibility;
    syncQuickSendFieldVisibility();

    const submitButton = document.getElementById('btn-submit-quick-send');
    if (submitButton) submitButton.textContent = schema?.submitLabel || 'Gửi cho khách';

    if (typeof bootstrap !== 'undefined') {
        bootstrap.Modal.getOrCreateInstance(modalEl).show();
    }
}

function buildQuickSendField(field) {
    const wrapper = document.createElement('label');
    wrapper.className = 'd-grid gap-2';
    wrapper.dataset.quickField = field.key || '';
    if (field.showWhen) wrapper.dataset.showWhen = JSON.stringify(field.showWhen);

    const caption = document.createElement('span');
    caption.textContent = field.label || field.key || 'Trường';
    wrapper.appendChild(caption);

    let input;
    if (field.type === 'select') {
        input = document.createElement('select');
        input.className = 'form-select';
        input.innerHTML = '<option value="">Chọn</option>';
        (Array.isArray(field.options) ? field.options : []).forEach(option => {
            const optionEl = document.createElement('option');
            optionEl.value = option.value || '';
            optionEl.textContent = option.label || option.value || '';
            if (option.branchId) optionEl.dataset.branchId = option.branchId;
            if (option.maxGuests) optionEl.dataset.maxGuests = option.maxGuests;
            input.appendChild(optionEl);
        });
    } else {
        input = document.createElement('input');
        input.className = 'form-control';
        input.type = field.type || 'text';
        if (field.min != null) input.min = String(field.min);
        if (field.max != null) input.max = String(field.max);
        if (field.placeholder) input.placeholder = field.placeholder;
    }

    input.name = field.key || '';
    input.dataset.key = field.key || '';
    input.dataset.label = field.label || field.key || 'trường này';
    input.dataset.required = field.required ? 'true' : 'false';
    wrapper.appendChild(input);
    return wrapper;
}

function syncQuickSendFieldVisibility() {
    const form = document.getElementById('quick-send-form');
    if (!form) return;

    const branchId = form.querySelector('[data-key="branchId"]')?.value || '';
    const guestCount = Number(form.querySelector('[data-key="guestCount"]')?.value || '0');
    const bookingMode = form.querySelector('[data-key="bookingMode"]')?.value || '';

    form.querySelectorAll('[data-quick-field]').forEach(wrapper => {
        const showWhenRaw = wrapper.dataset.showWhen;
        let visible = true;
        if (showWhenRaw) {
            try {
                const showWhen = JSON.parse(showWhenRaw);
                if (showWhen.bookingMode) visible = bookingMode === showWhen.bookingMode;
            } catch {}
        }
        wrapper.hidden = !visible;
    });

    const roomSelect = form.querySelector('[data-key="roomId"]');
    if (roomSelect) {
        Array.from(roomSelect.options).forEach((option, index) => {
            if (index === 0) {
                option.hidden = false;
                return;
            }
            const matchesBranch = !branchId || option.dataset.branchId === branchId;
            const maxGuests = Number(option.dataset.maxGuests || '0');
            const matchesGuests = !guestCount || !maxGuests || maxGuests >= guestCount;
            option.hidden = !(matchesBranch && matchesGuests);
        });
        if (roomSelect.selectedOptions[0]?.hidden) roomSelect.value = '';
    }
}

function collectQuickSendPayload() {
    const form = document.getElementById('quick-send-form');
    if (!form) return null;

    const payload = {};
    const inputs = form.querySelectorAll('[data-key]');
    for (const input of inputs) {
        const wrapper = input.closest('[data-quick-field]');
        if (wrapper?.hidden) continue;

        const key = input.dataset.key;
        const label = input.dataset.label || key;
        const required = input.dataset.required === 'true';
        const value = (input.value || '').trim();
        if (required && !value) {
            throw new Error(`Vui lòng nhập ${label}.`);
        }
        if (value) payload[key] = input.type === 'number' ? Number(value) : value;
    }
    return payload;
}

function submitQuickSendModal() {
    if (!currentSessionId || !quickSendModalState) return;

    let payload;
    try {
        payload = collectQuickSendPayload();
    } catch (error) {
        premiumToast(error.message || 'Vui lòng điền đủ điều kiện gửi nhanh.', 'warning');
        return;
    }

    fetch(`/chinhan/hethong/chat-monitor/session/${encodeURIComponent(currentSessionId)}/quick-send/${encodeURIComponent(quickSendModalState.type)}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ payload })
    })
        .then(r => r.ok ? r.json() : r.json().then(err => Promise.reject(err)))
        .then(data => {
            const modalEl = document.getElementById('quick-send-modal');
            if (modalEl && typeof bootstrap !== 'undefined') {
                bootstrap.Modal.getOrCreateInstance(modalEl).hide();
            }
            premiumToast('Gửi khối tương tác thành công.', 'success');
        })
        .catch(err => {
            premiumToast(err?.message || 'Không gửi được block nhanh.', 'error');
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
    fetch(`/chinhan/hethong/chat-monitor/session/${encodeURIComponent(currentSessionId)}/auto-reply`, {
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

function switchSessionScope(scope) {
    currentSessionScope = scope === 'deleted' ? 'deleted' : 'active';
    currentSessionId = null;
    sessions = {};
    document.querySelectorAll('[data-session-scope]').forEach(button => {
        const active = button.dataset.sessionScope === currentSessionScope;
        button.classList.toggle('btn-primary', active);
        button.classList.toggle('btn-outline-secondary', !active);
    });
    document.getElementById('trash-retention-info')?.classList.toggle('d-none', currentSessionScope !== 'deleted');
    document.getElementById('chat-active-panel')?.classList.add('d-none');
    document.getElementById('chat-placeholder')?.classList.remove('d-none');
    loadSessionsFallback();
}

async function softDeleteCurrentSession() {
    if (!currentSessionId) return;
    const confirmed = await premiumConfirm('Xóa phiên chat này vào thùng rác?', {
        title: 'Xóa phiên chat',
        confirmText: 'Xóa',
        cancelText: 'Hủy',
        isDanger: true,
        type: 'warning'
    });
    if (!confirmed) return;

    fetch(`/chinhan/hethong/chat-monitor/session/${encodeURIComponent(currentSessionId)}/delete`, { method: 'POST' })
        .then(r => r.ok ? r.json() : r.json().then(err => Promise.reject(err)))
        .then(() => {
            delete sessions[currentSessionId];
            hideActiveChatPanel();
            renderSessionList();
            if (currentSessionScope === 'deleted') {
                loadSessionsFallback();
            }
            premiumToast('Đã chuyển phiên chat vào thùng rác.', 'success');
        })
        .catch(err => premiumToast(err?.message || 'Không xóa được phiên chat.', 'error'));
}

async function restoreCurrentSession() {
    if (!currentSessionId) return;
    fetch(`/chinhan/hethong/chat-monitor/session/${encodeURIComponent(currentSessionId)}/restore`, { method: 'POST' })
        .then(r => r.ok ? r.json() : r.json().then(err => Promise.reject(err)))
        .then(() => {
            delete sessions[currentSessionId];
            hideActiveChatPanel();
            renderSessionList();
            loadSessionsFallback();
            premiumToast('Khôi phục phiên chat thành công.', 'success');
        })
        .catch(err => premiumToast(err?.message || 'Không khôi phục được phiên chat.', 'error'));
}

async function permanentlyDeleteCurrentSession() {
    if (!currentSessionId) return;
    const confirmed = await premiumConfirm('Xóa vĩnh viễn phiên chat này và toàn bộ tin nhắn? Thao tác này không thể khôi phục.', {
        title: 'Xóa vĩnh viễn',
        confirmText: 'Xóa vĩnh viễn',
        cancelText: 'Hủy',
        isDanger: true,
        type: 'danger'
    });
    if (!confirmed) return;

    fetch(`/chinhan/hethong/chat-monitor/session/${encodeURIComponent(currentSessionId)}/delete-permanent`, { method: 'POST' })
        .then(r => r.ok ? r.json() : r.json().then(err => Promise.reject(err)))
        .then(() => {
            delete sessions[currentSessionId];
            hideActiveChatPanel();
            renderSessionList();
            premiumToast('Đã xóa vĩnh viễn phiên chat.', 'success');
        })
        .catch(err => premiumToast(err?.message || 'Không xóa vĩnh viễn được phiên chat.', 'error'));
}

function openManualCancellationModal() {
    const modalEl = document.getElementById('manual-cancellation-modal');
    if (!modalEl) return;
    resetManualCancellationForm();
    if (typeof bootstrap !== 'undefined') bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function resetManualCancellationForm() {
    document.getElementById('manual-booking-code').value = '';
    document.getElementById('manual-zalo-image').value = '';
    document.getElementById('manual-booking-preview').classList.add('d-none');
    document.getElementById('btn-submit-manual-cancellation').disabled = true;
    document.getElementById('manual-cancellation-message').textContent = '';
}

function lookupBookingForCancellation() {
    const code = document.getElementById('manual-booking-code').value.trim();
    const message = document.getElementById('manual-cancellation-message');
    const preview = document.getElementById('manual-booking-preview');
    if (!code) {
        if (message) message.textContent = 'Vui lòng nhập mã booking.';
        return;
    }
    if (message) message.textContent = 'Đang tra cứu...';
    preview.classList.add('d-none');
    document.getElementById('btn-submit-manual-cancellation').disabled = true;

    fetch('/chinhan/hethong/chat-monitor/cancellations/lookup-booking', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ bookingCode: code })
    })
        .then(r => r.ok ? r.json() : r.json().then(err => Promise.reject(err)))
        .then(data => {
            if (message) message.textContent = '';
            document.getElementById('manual-customer-name').textContent = data.customerName || '';
            document.getElementById('manual-customer-phone').textContent = data.customerPhone || '';
            document.getElementById('manual-customer-email').textContent = data.customerEmail || '';
            document.getElementById('manual-room-name').textContent = data.roomName ? 'Phòng: ' + data.roomName : '';
            const checkIn = data.checkIn ? new Date(data.checkIn).toLocaleDateString('vi-VN') : '';
            const checkOut = data.checkOut ? new Date(data.checkOut).toLocaleDateString('vi-VN') : '';
            document.getElementById('manual-checkin-dates').textContent = checkIn && checkOut ? checkIn + ' → ' + checkOut : '';
            preview.classList.remove('d-none');
            checkManualFormReady();
        })
        .catch(err => {
            if (message) message.textContent = err?.error || 'Không tìm thấy booking.';
            preview.classList.add('d-none');
        });
}

function checkManualFormReady() {
    const hasImage = document.getElementById('manual-zalo-image').files.length > 0;
    const hasPreview = !document.getElementById('manual-booking-preview').classList.contains('d-none');
    document.getElementById('btn-submit-manual-cancellation').disabled = !(hasImage && hasPreview);
}

function submitManualCancellation() {
    const code = document.getElementById('manual-booking-code').value.trim();
    const image = document.getElementById('manual-zalo-image').files[0];
    const message = document.getElementById('manual-cancellation-message');
    if (!code || !image) {
        if (message) message.textContent = 'Vui lòng nhập mã booking và chọn ảnh.';
        return;
    }
    if (message) message.textContent = 'Đang tạo đơn hủy...';

    const formData = new FormData();
    formData.append('bookingCode', code);
    formData.append('image', image);

    fetch('/chinhan/hethong/chat-monitor/cancellations/create-manual', {
        method: 'POST',
        body: formData
    })
        .then(r => r.ok ? r.json() : r.json().then(err => Promise.reject(err)))
        .then(data => {
            if (message) message.textContent = 'Đã tạo đơn hủy thành công.';
            const modalEl = document.getElementById('manual-cancellation-modal');
            if (modalEl && typeof bootstrap !== 'undefined') bootstrap.Modal.getOrCreateInstance(modalEl).hide();
            openCancellation(data.requestId);
            loadSessionsFallback();
        })
        .catch(err => {
            if (message) message.textContent = err?.error || 'Không tạo được đơn hủy.';
        });
}

let activeCancellationFilter = 'Pending';
let activeCancellationId = null;

function loadCancellationList(status) {
    activeCancellationFilter = status || 'Pending';
    const container = document.getElementById('cancellation-list-sidebar');
    if (!container) return;

    document.querySelectorAll('[data-cancellation-filter]').forEach(button => {
        const active = button.dataset.cancellationFilter === activeCancellationFilter;
        button.classList.toggle('btn-primary', active);
        button.classList.toggle('btn-outline-secondary', !active);
    });

    container.innerHTML = '<div class="empty-state">Đang tải yêu cầu...</div>';
    fetch(`/chinhan/hethong/chat-monitor/cancellations?status=${encodeURIComponent(activeCancellationFilter)}`)
        .then(r => r.ok ? r.json() : Promise.reject(new Error('Không tải được yêu cầu hủy.')))
        .then(items => {
            container.innerHTML = '';
            if (!items.length) {
                container.innerHTML = '<div class="empty-state">Không có yêu cầu hủy phù hợp.</div>';
                return;
            }
            items.forEach(item => {
                container.appendChild(createSidebarCancellationCard(item));
            });
        })
        .catch(() => {
            container.innerHTML = '<div class="empty-state text-danger">Lỗi tải dữ liệu.</div>';
        });
}

function createSidebarCancellationCard(item) {
    const card = document.createElement('div');
    const isActive = activeCancellationId === item.id;
    card.className = `cancellation-card ${isActive ? 'active' : ''}`;
    card.dataset.cancellationId = item.id;
    card.dataset.chatSessionId = item.chatSessionId;
    
    const submittedCode = item.submittedBookingCode || (item.bookingId ? '#' + item.bookingId : 'Chưa liên kết');
    
    card.innerHTML = `
        <div class="cancellation-card-header">
            <div>
                <span class="cancellation-kicker">Yêu cầu hủy phòng</span>
                <strong>${escapeHtml(item.customerName || 'Khách')}</strong>
            </div>
            <span class="cancellation-status cancellation-status-${escapeHtml((item.status || '').toLowerCase())}">${escapeHtml(cancellationStatusLabel(item.status))}</span>
        </div>
        <div class="cancellation-card-meta">
            <span>Mã: ${escapeHtml(submittedCode)}</span>
            <span>${timeAgo(item.createdAt)}</span>
        </div>
        ${item.staffReason ? `<div class="cancellation-card-reason text-truncate" style="max-height: 38px;">${escapeHtml(item.staffReason)}</div>` : ''}
        <button class="btn btn-sm btn-outline-primary btn-open-detail w-100 mt-2" type="button">Xem và xử lý</button>
    `;
    
    const triggerDetail = () => {
        activeCancellationId = item.id;
        document.querySelectorAll('#cancellation-list-sidebar .cancellation-card').forEach(c => {
            c.classList.toggle('active', Number(c.dataset.cancellationId) === Number(item.id));
        });
        openCancellation(item.id, item.chatSessionId);
    };
    
    card.addEventListener('click', (e) => {
        if (!e.target.closest('button')) {
            triggerDetail();
        }
    });
    card.querySelector('.btn-open-detail').addEventListener('click', triggerDetail);
    
    return card;
}

function openCancellation(id, chatSessionId) {
    const detail = document.getElementById('cancellation-detail');
    if (!detail) return;
    detail.innerHTML = '<div class="empty-state">Đang tải chi tiết...</div>';
    
    const contextPanel = document.getElementById('chat-context-panel');
    if (contextPanel) contextPanel.classList.remove('d-none');
    
    const contextCancellationView = document.getElementById('context-cancellation-view');
    if (contextCancellationView) contextCancellationView.classList.remove('d-none');
    
    const contextSessionView = document.getElementById('context-session-view');
    if (contextSessionView) contextSessionView.classList.add('d-none');

    // Hide chat reply area in cancellation mode
    document.querySelector('.chat-reply-area')?.classList.add('d-none');

    // Automatically load timeline alongside this cancellation request details
    if (chatSessionId) {
        selectSession(chatSessionId, { keepCancellationActive: true });
    }

    fetch(`/chinhan/hethong/chat-monitor/cancellations/${encodeURIComponent(id)}`)
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
    const canCancelBooking = request.status === 'Approved'
        && booking
        && booking.status !== 'Cancelled'
        && data.handlingMode !== 'Auto';
    
    detail.innerHTML = `
        <div class="d-flex flex-column gap-3">
            <section class="context-section">
                <h3>Thông tin khách</h3>
                <dl>
                    <dt>Họ tên</dt><dd>${escapeHtml(request.customerName)}</dd>
                    <dt>Điện thoại</dt><dd>${escapeHtml(request.customerPhone)}</dd>
                    <dt>Email</dt><dd>${escapeHtml(request.customerEmail)}</dd>
                    <dt>Mã nhập</dt><dd>${escapeHtml(request.submittedBookingCode || 'Chưa có')}</dd>
                    <dt>Trạng thái</dt><dd>${escapeHtml(cancellationStatusLabel(request.status))}</dd>
                </dl>
            </section>
            
            <section class="context-section">
                <h3>Minh chứng</h3>
                <div class="protected-file-links">
                    <a class="btn btn-sm" href="/chinhan/hethong/chat-monitor/cancellations/${request.id}/file/confirmation" target="_blank" rel="noopener">
                        <i class="fas fa-envelope-open-text me-1"></i>Ảnh email xác nhận
                    </a>
                    ${request.refundQrImagePath ? `
                    <a class="btn btn-sm" href="/chinhan/hethong/chat-monitor/cancellations/${request.id}/file/refundQr" target="_blank" rel="noopener">
                        <i class="fas fa-qrcode me-1"></i>QR nhận hoàn tiền
                    </a>` : ''}
                    ${request.refundBillProofPath ? `
                    <a class="btn btn-sm" href="/chinhan/hethong/chat-monitor/cancellations/${request.id}/file/refundBill" target="_blank" rel="noopener">
                        <i class="fas fa-receipt me-1"></i>Bill hoàn tiền
                    </a>` : ''}
                </div>
            </section>
            
            ${renderBookingSummary(booking, suggestions)}
            
            <section class="context-section">
                <h3>Xử lý yêu cầu</h3>
                <div class="mb-2">
                    <label class="form-label" for="cancellation-staff-reason">Lý do/ghi chú cho khách</label>
                    <textarea class="form-control" id="cancellation-staff-reason" rows="3" ${canProcess ? '' : 'disabled'}>${escapeHtml(request.staffReason || '')}</textarea>
                </div>
                <div class="mb-2">
                    <label class="form-label" for="cancellation-refund-percent">Phần trăm hoàn tiền (%)</label>
                    <input class="form-control" id="cancellation-refund-percent" type="number" min="0" max="100" value="${request.appliedRefundPercent ?? request.refundPercentBeforeNoticeSnapshot ?? 0}" ${canProcess ? '' : 'disabled'} />
                </div>
                <div class="mb-3">
                    <label class="form-label" for="cancellation-refund-bill">Bill hoàn tiền (nếu duyệt)</label>
                    <input class="form-control" id="cancellation-refund-bill" type="file" accept="image/*,.pdf" ${canProcess ? '' : 'disabled'} />
                </div>
                <div class="cancellation-actions d-grid gap-2">
                    <button class="btn btn-cancellation-approve" type="button" id="btn-approve-cancellation" ${canProcess ? '' : 'disabled'}>
                        <i class="fas fa-check-circle me-2"></i>Chấp nhận hủy
                    </button>
                    <button class="btn btn-cancellation-sync" type="button" id="btn-cancel-approved-booking" ${canCancelBooking ? '' : 'disabled'}>
                        <i class="fas fa-calendar-xmark me-2"></i>Cập nhật trạng thái Booking
                    </button>
                    <button class="btn btn-cancellation-reject" type="button" id="btn-reject-cancellation" ${canProcess ? '' : 'disabled'}>
                        <i class="fas fa-ban me-2"></i>Từ chối yêu cầu
                    </button>
                </div>
                <div class="cancellation-action-message" id="cancellation-action-message"></div>
            </section>
        </div>
    `;
    
    document.getElementById('btn-approve-cancellation')?.addEventListener('click', () => approveCancellation(request.id));
    document.getElementById('btn-cancel-approved-booking')?.addEventListener('click', () => cancelApprovedBooking(request.id));
    document.getElementById('btn-reject-cancellation')?.addEventListener('click', () => rejectCancellation(request.id));
}

function renderBookingSummary(booking, suggestions) {
    const bookingHtml = booking ? `
        <div class="booking-summary-card">
            <strong>#${booking.id} · ${escapeHtml(booking.roomName || 'Phòng')}</strong>
            <span>${escapeHtml(booking.customerName || '')} · ${escapeHtml(booking.customerPhone || '')}</span>
            <span>${formatDateTime(booking.startTime)} - ${formatDateTime(booking.endTime)}</span>
            <span>Trạng thái: ${escapeHtml(booking.status)} · Tổng: ${formatCurrency(booking.totalPrice)}</span>
        </div>` : '<div class="empty-state compact">Chưa liên kết booking.</div>';
        
    const suggestionsHtml = suggestions.length
        ? suggestions.map(b => `
            <div class="booking-summary-card muted mb-2">
                <strong>#${b.id} · ${escapeHtml(b.roomName || 'Phòng')}</strong>
                <span>${escapeHtml(b.customerName || '')} · ${formatDateTime(b.startTime)}</span>
            </div>`).join('')
        : '<div class="empty-state compact">Không có gợi ý.</div>';
        
    return `
        <section class="context-section">
            <h3>Booking liên kết</h3>
            ${bookingHtml}
        </section>
        <section class="context-section">
            <h3 class="d-flex justify-content-between align-items-center cursor-pointer" data-bs-toggle="collapse" data-bs-target="#suggested-bookings-collapse" aria-expanded="true" style="cursor: pointer; margin-bottom: 0;">
                <span>Booking gợi ý</span>
                <i class="fas fa-chevron-down collapse-icon"></i>
            </h3>
            <div class="collapse show mt-2" id="suggested-bookings-collapse">
                <div class="d-flex flex-column gap-2">
                    ${suggestionsHtml}
                </div>
            </div>
        </section>`;
}

function approveCancellation(id) {
    const message = document.getElementById('cancellation-action-message');
    const file = document.getElementById('cancellation-refund-bill')?.files?.[0];
    if (!file) {
        if (message) message.textContent = 'Vui lòng chọn file bill hoàn tiền trước.';
        return;
    }
    requestCancellationPreview(id, 'approve');
}

function rejectCancellation(id) {
    requestCancellationPreview(id, 'reject');
}

function requestCancellationPreview(id, action) {
    const message = document.getElementById('cancellation-action-message');
    const formData = new FormData();
    const isApprove = action === 'approve';
    formData.append('staffReason', document.getElementById('cancellation-staff-reason')?.value || (isApprove ? 'Đã duyệt yêu cầu hủy.' : 'Yêu cầu hủy chưa đủ điều kiện xử lý.'));
    if (isApprove) {
        formData.append('appliedRefundPercent', document.getElementById('cancellation-refund-percent')?.value || '0');
        const file = document.getElementById('cancellation-refund-bill')?.files?.[0];
        if (file) formData.append('refundBillProof', file);
    }
    if (message) message.textContent = 'Đang tạo preview email...';
    fetch(`/chinhan/hethong/chat-monitor/cancellations/${encodeURIComponent(id)}/${isApprove ? 'approval-preview' : 'rejection-preview'}`, { method: 'POST', body: formData })
        .then(r => r.ok ? r.json() : r.json().then(err => Promise.reject(err)))
        .then(preview => {
            if (message) message.textContent = '';
            showCancellationMailPreview(id, action, preview);
        })
        .catch(err => {
            if (message) message.textContent = err?.message || 'Không tạo được preview email.';
        });
}

function showCancellationMailPreview(id, action, preview) {
    const isApprove = action === 'approve';
    const billFile = isApprove ? document.getElementById('cancellation-refund-bill')?.files?.[0] || null : null;
    cancellationMailPreviewState = {
        id,
        action,
        preview,
        billFile,
        originalReason: preview?.editableReason || '',
        currentReason: preview?.editableReason || ''
    };

    let modalEl = document.getElementById('cancellation-mail-preview-modal');
    if (!modalEl) {
        modalEl = document.createElement('div');
        modalEl.className = 'modal fade';
        modalEl.id = 'cancellation-mail-preview-modal';
        modalEl.tabIndex = -1;
        modalEl.setAttribute('aria-hidden', 'true');
        document.body.appendChild(modalEl);
    }

    modalEl.innerHTML = `
        <div class="modal-dialog modal-lg modal-dialog-scrollable">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title">Xem trước email ${isApprove ? 'chấp nhận hủy' : 'từ chối hủy'}</h5>
                    <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Đóng"></button>
                </div>
                <div class="modal-body">
                    <div class="mb-3">
                        <label class="form-label">Người nhận</label>
                        <div class="form-control-plaintext">${escapeHtml(preview?.recipientEmail || '')}</div>
                    </div>
                    <div class="mb-3">
                        <label class="form-label" for="cancellation-preview-subject">Tiêu đề</label>
                        <input class="form-control" id="cancellation-preview-subject" type="text" value="${escapeHtml(preview?.subject || '')}" />
                    </div>
                    <div class="mb-3">
                        <label class="form-label" for="cancellation-preview-reason">Lý do gửi khách</label>
                        <textarea class="form-control" id="cancellation-preview-reason" rows="3">${escapeHtml(cancellationMailPreviewState.currentReason)}</textarea>
                    </div>
                    <div class="mb-3">
                        <label class="form-label">Nội dung email</label>
                        <div class="border rounded p-3 bg-light text-dark" id="cancellation-preview-body">${preview?.body || ''}</div>
                    </div>
                    ${isApprove ? '<div class="mb-3" id="cancellation-preview-attachment"></div>' : ''}
                    <div class="alert alert-warning mb-0">Vui lòng kiểm tra kỹ nội dung trước khi gửi email.</div>
                    <div class="cancellation-action-message mt-2" id="cancellation-preview-message"></div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Quay lại</button>
                    <button type="button" class="btn ${isApprove ? 'btn-success' : 'btn-outline-danger'}" id="btn-finalize-cancellation-preview">${isApprove ? 'Gửi email và chấp nhận hủy' : 'Gửi email và từ chối hủy'}</button>
                </div>
            </div>
        </div>
    `;

    document.getElementById('cancellation-preview-reason')?.addEventListener('input', updatePreviewBodyReason);
    document.getElementById('btn-finalize-cancellation-preview')?.addEventListener('click', finalizeCancellationFromPreview);
    renderPreviewAttachment();
    updatePreviewSendState();
    if (typeof bootstrap !== 'undefined') bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function renderPreviewAttachment() {
    const container = document.getElementById('cancellation-preview-attachment');
    if (!container || !cancellationMailPreviewState || cancellationMailPreviewState.action !== 'approve') return;
    const file = cancellationMailPreviewState.billFile;
    container.innerHTML = `
        <label class="form-label">File đính kèm</label>
        <div class="d-flex flex-wrap align-items-center gap-2">
            <span class="badge bg-secondary">${escapeHtml(file?.name || cancellationMailPreviewState.preview?.attachmentName || 'Chưa chọn')}</span>
            <button class="btn btn-sm btn-outline-secondary" type="button" id="btn-replace-preview-bill">Đổi file</button>
            <button class="btn btn-sm btn-outline-danger" type="button" id="btn-remove-preview-bill">Gỡ file</button>
            <input class="d-none" id="cancellation-preview-bill-input" type="file" accept="image/*,.pdf" />
        </div>
    `;
    document.getElementById('btn-remove-preview-bill')?.addEventListener('click', removePreviewBill);
    document.getElementById('btn-replace-preview-bill')?.addEventListener('click', replacePreviewBill);
    document.getElementById('cancellation-preview-bill-input')?.addEventListener('change', (e) => {
        cancellationMailPreviewState.billFile = e.target.files?.[0] || null;
        renderPreviewAttachment();
        updatePreviewSendState();
    });
}

function removePreviewBill() {
    if (!cancellationMailPreviewState) return;
    cancellationMailPreviewState.billFile = null;
    renderPreviewAttachment();
    updatePreviewSendState();
}

function replacePreviewBill() {
    document.getElementById('cancellation-preview-bill-input')?.click();
}

function updatePreviewSendState() {
    const button = document.getElementById('btn-finalize-cancellation-preview');
    const message = document.getElementById('cancellation-preview-message');
    if (!button || !cancellationMailPreviewState) return;
    const needsAttachment = cancellationMailPreviewState.action === 'approve' && cancellationMailPreviewState.preview?.requiresAttachment;
    const missingAttachment = needsAttachment && !cancellationMailPreviewState.billFile;
    button.disabled = missingAttachment;
    if (message) message.textContent = missingAttachment ? 'Cần đính kèm bill hoàn tiền trước khi duyệt.' : '';
}

function updatePreviewBodyReason() {
    if (!cancellationMailPreviewState) return;
    const textarea = document.getElementById('cancellation-preview-reason');
    const body = document.getElementById('cancellation-preview-body');
    if (!textarea || !body) return;
    cancellationMailPreviewState.currentReason = textarea.value;
    const original = cancellationMailPreviewState.originalReason;
    const current = escapeHtml(textarea.value);
    body.innerHTML = original
        ? (cancellationMailPreviewState.preview?.body || '').replace(escapeHtml(original), current)
        : (cancellationMailPreviewState.preview?.body || '');
}

function finalizeCancellationFromPreview() {
    if (!cancellationMailPreviewState) return;
    const state = cancellationMailPreviewState;
    const formData = new FormData();
    formData.append('staffReason', document.getElementById('cancellation-preview-reason')?.value || state.currentReason || '');
    formData.append('notificationEmailSubject', document.getElementById('cancellation-preview-subject')?.value || state.preview?.subject || '');
    formData.append('notificationEmailBody', document.getElementById('cancellation-preview-body')?.innerHTML || state.preview?.body || '');
    if (state.action === 'approve') {
        formData.append('appliedRefundPercent', document.getElementById('cancellation-refund-percent')?.value || '0');
        if (state.billFile) formData.append('refundBillProof', state.billFile);
    }
    const modalEl = document.getElementById('cancellation-mail-preview-modal');
    if (modalEl && typeof bootstrap !== 'undefined') bootstrap.Modal.getOrCreateInstance(modalEl).hide();
    postCancellationAction(`/chinhan/hethong/chat-monitor/cancellations/${encodeURIComponent(state.id)}/${state.action === 'approve' ? 'approve' : 'reject'}`, formData, state.id);
}

function cancelApprovedBooking(id) {
    const formData = new FormData();
    postCancellationAction(`/chinhan/hethong/chat-monitor/cancellations/${encodeURIComponent(id)}/cancel-booking`, formData, id);
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
            loadCancellationList(activeCancellationFilter);
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
