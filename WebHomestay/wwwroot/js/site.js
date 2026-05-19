document.addEventListener('DOMContentLoaded', () => {
    const widget = document.getElementById('public-ai-chatbot');
    if (!widget) return;

    const toggle = widget.querySelector('.ai-chat-toggle');
    const close = widget.querySelector('.ai-chat-close');
    const form = widget.querySelector('.ai-chat-form');
    const input = widget.querySelector('.ai-chat-input');
    const sendButton = widget.querySelector('.ai-chat-send');
    const messages = widget.querySelector('.ai-chat-messages');
    const status = widget.querySelector('.ai-chat-status');
    const nameInput = widget.querySelector('.ai-context-name');
    const branchSelect = widget.querySelector('.ai-context-branch');
    const modeSelect = widget.querySelector('.ai-context-mode');
    const guestsInput = widget.querySelector('.ai-context-guests');

    let sessionId = createSessionId();
    let bookingState = {};

    loadBranches();

    toggle.addEventListener('click', () => setOpen(!widget.classList.contains('is-open')));
    close.addEventListener('click', () => setOpen(false));

    form.addEventListener('submit', async (event) => {
        event.preventDefault();
        const message = input.value.trim();
        if (!message) return;

        appendMessage(message, 'user');
        input.value = '';
        setBusy(true);

        try {
            const response = await fetch('/ai/chat', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    sessionId,
                    message,
                    customerName: getCustomerName(),
                    branchId: getBranchId(),
                    bookingMode: getBookingMode(),
                    guestCount: getGuestCount()
                })
            });

            const data = await response.json().catch(() => ({}));
            if (data.sessionId) sessionId = data.sessionId;
            updateBookingState(data.state);

            appendMessage(data.answer || data.message || 'Mình chưa trả lời được lúc này, bạn thử lại giúp mình nhé.', 'bot');
            renderUiBlocks(data.uiBlocks || []);
        } catch {
            appendMessage('Xin lỗi, trợ lý AI đang tạm thời bận. Bạn thử lại sau ít phút nhé.', 'bot');
        } finally {
            setBusy(false);
        }
    });

    messages.addEventListener('click', async (event) => {
        const actionButton = event.target.closest('[data-ai-action]');
        if (!actionButton || !messages.contains(actionButton)) return;

        const action = actionButton.dataset.aiAction;
        if (!action) return;

        event.preventDefault();
        actionButton.disabled = true;
        setBusy(true);

        try {
            const payload = buildActionPayload(action, actionButton);
            const response = await fetch('/ai/booking-action', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            const data = await response.json().catch(() => ({}));
            handleActionResponse(data);
        } catch {
            appendMessage('Xin lỗi, thao tác đặt phòng đang tạm thời bận. Bạn thử lại sau ít phút nhé.', 'bot');
        } finally {
            setBusy(false);
            actionButton.disabled = false;
        }
    });

    messages.addEventListener('submit', async (event) => {
        const bookingForm = event.target.closest('.ai-booking-form');
        if (!bookingForm || !messages.contains(bookingForm)) return;

        event.preventDefault();
        const submit = bookingForm.querySelector('[type="submit"]');
        if (submit) submit.disabled = true;
        setBusy(true);

        try {
            const payload = buildActionPayload('submit-booking-form', bookingForm);
            payload.formSubmission = collectBookingForm(bookingForm);
            const response = await fetch('/ai/booking-action', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            const data = await response.json().catch(() => ({}));
            handleActionResponse(data);
        } catch {
            appendMessage('Xin lỗi, chưa gửi được thông tin đặt phòng. Bạn thử lại giúp mình nhé.', 'bot');
        } finally {
            setBusy(false);
            if (submit) submit.disabled = false;
        }
    });

    async function loadBranches() {
        if (!branchSelect) return;
        try {
            const response = await fetch('/ai/branches');
            const branches = await response.json().catch(() => []);
            branchSelect.innerHTML = '';
            branchSelect.appendChild(new Option('Chọn chi nhánh', ''));
            if (Array.isArray(branches)) {
                branches.forEach(branch => {
                    if (!branch || branch.id == null) return;
                    branchSelect.appendChild(new Option(branch.name || `Chi nhánh ${branch.id}`, String(branch.id)));
                });
            }
        } catch {
            branchSelect.innerHTML = '';
            branchSelect.appendChild(new Option('Không tải được chi nhánh', ''));
        }
    }

    function setOpen(open) {
        widget.classList.toggle('is-open', open);
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        if (open) setTimeout(() => input.focus(), 120);
    }

    function setBusy(busy) {
        input.disabled = busy;
        sendButton.disabled = busy;
        status.textContent = busy ? 'AI đang xử lý...' : '';
    }

    function appendMessage(text, type) {
        const bubble = document.createElement('div');
        bubble.className = `ai-message ai-message-${type}`;
        bubble.textContent = text;
        messages.appendChild(bubble);
        scrollMessages();
    }

    function handleActionResponse(data) {
        if (data.sessionId) sessionId = data.sessionId;
        updateBookingState(data.state);
        if (data.message) appendMessage(data.message, 'bot');
        renderUiBlocks(data.uiBlocks || []);
    }

    function renderUiBlocks(blocks) {
        if (!Array.isArray(blocks)) return;
        blocks.forEach(block => {
            if (!block || typeof block !== 'object') return;
            if (block.type === 'roomCards') renderRoomCards(block.data || {});
            if (block.type === 'hourlySlots') renderHourlySlots(block.data || {});
            if (block.type === 'dailyRooms') renderDailyRooms(block.data || {});
            if (block.type === 'bookingSummary') renderBookingSummary(block.data || {});
            if (block.type === 'bookingForm') renderBookingForm(block.data || {});
            if (block.type === 'paymentQr') renderPaymentQr(block.data || {});
        });
    }

    function appendBlock(className) {
        const wrapper = document.createElement('div');
        wrapper.className = `ai-booking-block ${className}`;
        messages.appendChild(wrapper);
        scrollMessages();
        return wrapper;
    }

    function renderRoomCards(data) {
        const wrapper = appendBlock('ai-room-cards');
        const rooms = Array.isArray(data.rooms) ? data.rooms : [];
        rooms.forEach(room => {
            if (!room || typeof room !== 'object') return;
            const card = document.createElement('div');
            card.className = 'ai-room-card';

            const title = document.createElement('h4');
            title.textContent = room.name || 'Phòng';
            card.appendChild(title);

            const description = document.createElement('div');
            description.className = 'ai-room-meta';
            description.textContent = room.description || '';
            card.appendChild(description);

            const prices = document.createElement('div');
            prices.className = 'ai-room-meta';
            prices.textContent = `Giờ: ${formatMoney(room.pricePerHour)}/h · Ngày: ${formatMoney(room.pricePerDay)}/ngày`;
            card.appendChild(prices);

            const capacity = document.createElement('div');
            capacity.className = 'ai-room-meta';
            capacity.textContent = `Chuẩn ${toSafeNumber(room.capacity)} khách · Tối đa ${toSafeNumber(room.maxGuests)} khách${room.extraGuestFee > 0 ? ` · Phụ thu ${formatMoney(room.extraGuestFee)}` : ''}`;
            card.appendChild(capacity);

            const actions = document.createElement('div');
            actions.className = 'ai-room-actions';

            const detailsUrl = toSafeRelativeUrl(room.detailsUrl);
            if (detailsUrl) {
                const detailsLink = document.createElement('a');
                detailsLink.className = 'ai-chip-btn';
                detailsLink.href = detailsUrl;
                detailsLink.textContent = 'Xem chi tiết';
                actions.appendChild(detailsLink);
            }

            const selectButton = document.createElement('button');
            selectButton.className = 'ai-chip-btn';
            selectButton.type = 'button';
            selectButton.dataset.aiAction = 'select-room';
            selectButton.dataset.roomId = toSafeNumber(room.roomId).toString();
            selectButton.textContent = 'Chọn phòng này';
            actions.appendChild(selectButton);

            card.appendChild(actions);
            wrapper.appendChild(card);
        });
        scrollMessages();
    }

    function renderHourlySlots(data) {
        const wrapper = appendBlock('ai-hourly-slots');
        const grid = document.createElement('div');
        grid.className = 'ai-slot-grid';
        const slots = Array.isArray(data.slots) ? data.slots : [];
        slots.forEach(slot => {
            if (!slot || typeof slot !== 'object') return;
            const button = document.createElement('button');
            button.className = 'ai-chip-btn';
            button.type = 'button';
            button.dataset.aiAction = 'select-slot';
            button.dataset.roomId = toSafeNumber(slot.roomId).toString();
            button.dataset.slotId = toSafeNumber(slot.slotId).toString();
            button.textContent = `${slot.roomName || 'Phòng'}: ${slot.label || 'Khung giờ'}`;
            grid.appendChild(button);
        });
        wrapper.appendChild(grid);
        scrollMessages();
    }

    function renderDailyRooms(data) {
        const wrapper = appendBlock('ai-daily-rooms');
        const rooms = Array.isArray(data.rooms) ? data.rooms : [];
        rooms.forEach(room => {
            if (!room || typeof room !== 'object') return;
            const item = document.createElement('button');
            item.className = 'ai-chip-btn';
            item.type = 'button';
            item.dataset.aiAction = 'select-daily-room';
            item.dataset.roomId = toSafeNumber(room.roomId).toString();
            item.textContent = `${room.roomName || 'Phòng'}: ${formatMoney(room.totalPrice || room.pricePerDay)}/ngày`;
            wrapper.appendChild(item);
        });
        scrollMessages();
    }

    function renderBookingSummary(data) {
        const wrapper = appendBlock('ai-booking-summary');
        const title = document.createElement('h4');
        title.textContent = data.title || 'Tóm tắt đặt phòng';
        wrapper.appendChild(title);

        const lines = Array.isArray(data.lines) ? data.lines : [];
        if (lines.length) {
            const list = document.createElement('ul');
            lines.forEach(line => {
                const item = document.createElement('li');
                item.textContent = line;
                list.appendChild(item);
            });
            wrapper.appendChild(list);
        }

        const total = document.createElement('div');
        total.className = 'ai-booking-total';
        total.textContent = `Tạm tính: ${formatMoney(data.totalPrice)}`;
        wrapper.appendChild(total);
        scrollMessages();
    }

    function renderBookingForm(data) {
        const wrapper = appendBlock('ai-booking-form-block');
        const formElement = document.createElement('form');
        formElement.className = 'ai-booking-form';
        formElement.dataset.aiAction = 'submit-booking-form';

        const fields = Array.isArray(data.fields) ? data.fields : [];
        const normalizedFields = fields.length ? fields : [
            { name: 'customerName', label: 'Họ tên', type: 'text', required: true, value: getCustomerName() },
            { name: 'phoneNumber', label: 'SĐT/Zalo', type: 'tel', required: true },
            { name: 'email', label: 'Email', type: 'email' },
            { name: 'notes', label: 'Ghi chú', type: 'textarea' }
        ];

        normalizedFields.forEach(field => {
            if (!field || !field.name) return;
            const label = document.createElement('label');
            const caption = document.createElement('span');
            caption.textContent = field.label || field.name;
            label.appendChild(caption);

            const control = field.type === 'textarea' ? document.createElement('textarea') : document.createElement('input');
            if (field.type !== 'textarea') control.type = toSafeInputType(field.type);
            control.name = field.name;
            control.required = !!field.required;
            control.placeholder = field.placeholder || '';
            control.value = field.value || (field.name === 'customerName' ? getCustomerName() : '');
            label.appendChild(control);
            formElement.appendChild(label);
        });

        const submit = document.createElement('button');
        submit.className = 'ai-booking-submit';
        submit.type = 'submit';
        submit.textContent = 'Gửi thông tin đặt phòng';
        formElement.appendChild(submit);

        wrapper.appendChild(formElement);
        scrollMessages();
    }

    function renderPaymentQr(data) {
        const wrapper = appendBlock('ai-payment-block');
        const message = document.createElement('div');
        message.textContent = data.message || data.instructions || 'Vui lòng thanh toán để hoàn tất giữ phòng.';
        wrapper.appendChild(message);

        if (data.amount) {
            const amount = document.createElement('strong');
            amount.textContent = `Số tiền: ${formatMoney(data.amount)}`;
            wrapper.appendChild(amount);
        }

        const paymentUrl = toSafeRelativeUrl(data.paymentUrl);
        if (paymentUrl) {
            const link = document.createElement('a');
            link.className = 'ai-chip-btn';
            link.href = paymentUrl;
            link.textContent = 'Mở trang thanh toán';
            wrapper.appendChild(link);
        }

        const successUrl = toSafeRelativeUrl(data.successUrl);
        if (successUrl) {
            const success = document.createElement('a');
            success.className = 'ai-payment-success-link';
            success.href = successUrl;
            success.textContent = 'Xem trang xác nhận sau thanh toán';
            wrapper.appendChild(success);
        }
        scrollMessages();
    }

    function buildActionPayload(action, source) {
        const state = { ...bookingState };
        state.customerName = state.customerName || getCustomerName();
        state.branchId = state.branchId || getBranchId();
        state.bookingMode = state.bookingMode && state.bookingMode !== 'unknown' ? state.bookingMode : getBookingMode();
        state.guestCount = state.guestCount || getGuestCount();

        const roomId = toSafeNumber(source.dataset.roomId);
        const slotId = toSafeNumber(source.dataset.slotId);
        if (roomId > 0) state.selectedRoomId = roomId;
        if (slotId > 0) state.selectedSlotId = slotId;

        return { sessionId, action, state };
    }

    function collectBookingForm(bookingForm) {
        const formData = new FormData(bookingForm);
        const values = {};
        formData.forEach((value, key) => { values[key] = String(value || '').trim(); });
        return {
            customerName: values.customerName || getCustomerName(),
            phoneNumber: values.phoneNumber || values.phone || values.customerPhone || '',
            email: values.email || '',
            notes: values.notes || values.note || '',
            values
        };
    }

    function updateBookingState(state) {
        if (!state || typeof state !== 'object') return;
        bookingState = state;
        if (state.customerName && nameInput && !nameInput.value) nameInput.value = state.customerName;
        if (state.branchId && branchSelect) branchSelect.value = String(state.branchId);
        if (state.bookingMode && modeSelect && (state.bookingMode === 'hourly' || state.bookingMode === 'daily')) modeSelect.value = state.bookingMode;
        if (state.guestCount && guestsInput) guestsInput.value = state.guestCount;
    }

    function getCustomerName() {
        return nameInput ? nameInput.value.trim() : '';
    }

    function getBranchId() {
        if (!branchSelect || !branchSelect.value) return null;
        const id = Number.parseInt(branchSelect.value, 10);
        return Number.isNaN(id) ? null : id;
    }

    function getBookingMode() {
        return modeSelect && modeSelect.value === 'daily' ? 'daily' : 'hourly';
    }

    function getGuestCount() {
        const count = guestsInput ? Number.parseInt(guestsInput.value, 10) : 1;
        return Number.isNaN(count) || count < 1 ? 1 : count;
    }

    function formatMoney(value) {
        return new Intl.NumberFormat('vi-VN').format(value || 0) + 'đ';
    }

    function toSafeNumber(value) {
        const number = Number.parseInt(value, 10);
        return Number.isNaN(number) ? 0 : number;
    }

    function toSafeRelativeUrl(value) {
        if (!value || typeof value !== 'string') return '';
        return value.startsWith('/') && !value.startsWith('//') ? value : '';
    }

    function toSafeInputType(value) {
        return ['text', 'tel', 'email', 'number', 'date'].includes(value) ? value : 'text';
    }

    function scrollMessages() {
        messages.scrollTop = messages.scrollHeight;
    }

    function createSessionId() {
        if (window.crypto && crypto.randomUUID) return crypto.randomUUID().replace(/-/g, '');
        return `${Date.now()}${Math.random().toString(16).slice(2)}`;
    }
});
