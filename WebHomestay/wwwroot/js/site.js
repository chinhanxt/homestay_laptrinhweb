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
    const checkInInput = widget.querySelector('.ai-context-checkin');
    const checkOutInput = widget.querySelector('.ai-context-checkout');
    const timeFields = Array.from(widget.querySelectorAll('.ai-hourly-time-field'));
    const checkOutField = checkOutInput ? checkOutInput.closest('label') : null;

    let sessionId = createSessionId();
    let bookingState = {};
    let latestBookingSummary = null;

    loadBranches();
    updateContextDateTimeControls();

    toggle.addEventListener('click', () => setOpen(!widget.classList.contains('is-open')));
    close.addEventListener('click', () => setOpen(false));
    if (modeSelect) modeSelect.addEventListener('change', updateContextDateTimeControls);

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
                    startTime: getCheckInDateTime(),
                    endTime: getCheckOutDateTime(),
                    guestCount: getGuestCount()
                })
            });

            const data = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(data.message || 'AI chat request failed.');
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
            if (!response.ok) throw new Error(data.message || 'AI booking action request failed.');
            handleActionResponse(data);
        } catch {
            appendMessage('Xin lỗi, thao tác đặt phòng đang tạm thời bận. Bạn thử lại sau ít phút nhé.', 'bot');
            actionButton.disabled = false;
        } finally {
            setBusy(false);
        }
    });

    messages.addEventListener('submit', async (event) => {
        const submittedForm = event.target.closest('form');
        if (!submittedForm || !messages.contains(submittedForm)) return;
        event.preventDefault();

        const bookingForm = submittedForm.closest('.ai-booking-form');
        if (!bookingForm) return;

        const submit = bookingForm.querySelector('[type="submit"]');
        if (submit) submit.disabled = true;
        setBusy(true);

        try {
            const payload = buildActionPayload('submit-booking-form', bookingForm);
            payload.formSubmission = collectBookingForm(bookingForm);
            const idCardFiles = collectIdCardFiles(bookingForm);
            const response = await fetch('/ai/booking-action', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            const data = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(data.message || 'AI booking action request failed.');
            await uploadIdCardFiles(data.state?.bookingId, idCardFiles);
            handleActionResponse(data);
            setOpen(true);
        } catch {
            appendMessage('Xin lỗi, chưa gửi được thông tin đặt phòng. Bạn thử lại giúp mình nhé.', 'bot');
            if (submit) submit.disabled = false;
        } finally {
            setBusy(false);
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

    function groupSlotsByRoom(slots) {
        const groups = new Map();
        slots.forEach(slot => {
            if (!slot || typeof slot !== 'object') return;
            const roomId = toSafeNumber(slot.roomId);
            const roomName = slot.roomName || 'Phòng';
            const key = `${roomId}:${roomName}`;
            if (!groups.has(key)) groups.set(key, { roomId, roomName, slots: [] });
            groups.get(key).slots.push(slot);
        });
        return Array.from(groups.values());
    }

    function formatSlotChipLabel(slot) {
        const label = slot.label || 'Khung giờ';
        return `${label} · ${formatMoney(slot.totalPrice)}`;
    }

    function renderHourlySlots(data) {
        const wrapper = appendBlock('ai-hourly-slots');
        const slots = Array.isArray(data.slots) ? data.slots : [];
        if (!slots.length) {
            const empty = document.createElement('div');
            empty.className = 'ai-empty-slots';
            empty.textContent = 'Chưa có khung giờ phù hợp để hiển thị.';
            wrapper.appendChild(empty);
            scrollMessages();
            return;
        }

        groupSlotsByRoom(slots).forEach(group => {
            const roomBlock = document.createElement('div');
            roomBlock.className = 'ai-slot-room-group';

            const header = document.createElement('div');
            header.className = 'ai-slot-room-header';
            header.textContent = group.roomName;
            roomBlock.appendChild(header);

            const grid = document.createElement('div');
            grid.className = 'ai-slot-chip-grid';
            group.slots.forEach(slot => {
                const button = document.createElement('button');
                button.className = 'ai-slot-chip';
                button.type = 'button';
                button.dataset.aiAction = 'select-slot';
                button.dataset.roomId = toSafeNumber(slot.roomId).toString();
                button.dataset.slotId = toSafeNumber(slot.slotId).toString();
                button.textContent = formatSlotChipLabel(slot);
                grid.appendChild(button);
            });

            roomBlock.appendChild(grid);
            wrapper.appendChild(roomBlock);
        });
        scrollMessages();
    }

    function renderDailyRooms(data) {
        const wrapper = appendBlock('ai-daily-rooms');
        const rooms = Array.isArray(data.rooms) ? data.rooms : [];
        rooms.forEach(room => {
            if (!room || typeof room !== 'object') return;
            const item = document.createElement('div');
            item.className = 'ai-daily-room-option ai-daily-room-compact';

            const main = document.createElement('div');
            main.className = 'ai-daily-room-main';

            const title = document.createElement('strong');
            title.textContent = room.roomName || 'Phòng';
            main.appendChild(title);

            const price = document.createElement('span');
            price.textContent = formatMoney(room.totalPrice || room.pricePerDay);
            main.appendChild(price);
            item.appendChild(main);

            const actions = document.createElement('div');
            actions.className = 'ai-room-actions ai-room-actions-compact';

            const detailsUrl = toSafeRelativeUrl(room.detailsUrl);
            if (detailsUrl) {
                const detailsLink = document.createElement('a');
                detailsLink.className = 'ai-chip-btn';
                detailsLink.href = detailsUrl;
                detailsLink.target = '_blank';
                detailsLink.rel = 'noopener';
                detailsLink.textContent = 'Xem chi tiết';
                actions.appendChild(detailsLink);
            }

            const select = document.createElement('button');
            select.className = 'ai-chip-btn';
            select.type = 'button';
            select.dataset.aiAction = 'select-daily-room';
            select.dataset.roomId = toSafeNumber(room.roomId).toString();
            select.textContent = 'Chọn phòng này';
            actions.appendChild(select);

            item.appendChild(actions);
            wrapper.appendChild(item);
        });
        scrollMessages();
    }

    function renderBookingSummary(data) {
        const wrapper = appendBlock('ai-booking-summary');
        latestBookingSummary = wrapper;
        wrapper.dataset.baseLines = JSON.stringify(Array.isArray(data.lines) ? data.lines.filter(line => !String(line || '').startsWith('Số khách:') && !String(line || '').startsWith('Phụ thu khách thêm:')) : []);
        wrapper.dataset.totalPrice = toSafeNumber(data.totalPrice).toString();
        const title = document.createElement('h4');
        title.textContent = data.title || 'Tóm tắt đặt phòng';
        wrapper.appendChild(title);

        const list = document.createElement('ul');
        wrapper.appendChild(list);

        const total = document.createElement('div');
        total.className = 'ai-booking-total';
        wrapper.appendChild(total);
        updateBookingSummaryTotals(null);
        scrollMessages();
    }

    function renderBookingForm(data) {
        const wrapper = appendBlock('ai-booking-form-block');
        const formElement = document.createElement('form');
        formElement.className = 'ai-booking-form';

        const pricing = normalizeBookingPricing(data.pricing);
        const fields = Array.isArray(data.fields) ? data.fields : [];
        const normalizedFields = fields.length ? fields : [
            { name: 'customerName', label: 'Họ tên', type: 'text', required: true, value: getCustomerName() },
            { name: 'phoneNumber', label: 'SĐT/Zalo', type: 'tel', required: true },
            { name: 'email', label: 'Email', type: 'email' },
            { name: 'notes', label: 'Ghi chú', type: 'textarea' }
        ];

        normalizedFields.forEach(field => {
            if (!field || !field.name || field.name === 'paymentQr' || field.type === 'paymentQr') return;
            const label = document.createElement('label');
            const caption = document.createElement('span');
            caption.textContent = field.label || field.name;
            label.appendChild(caption);

            const normalizedType = String(field.type || 'text').toLowerCase();
            const control = normalizedType === 'textarea' ? document.createElement('textarea') : document.createElement('input');
            const inputType = toSafeInputType(normalizedType);
            if (normalizedType !== 'textarea') control.type = inputType;
            if (inputType === 'file') control.accept = 'image/*';
            if (normalizedType === 'textarea') control.maxLength = 500;
            if (['text', 'tel', 'email'].includes(inputType)) control.maxLength = 120;
            control.name = field.name;
            control.required = !!field.required;
            control.placeholder = getBookingFieldPlaceholder(field, inputType);
            if (field.name === 'guestCount') {
                control.type = 'number';
                control.min = '1';
                if (pricing.maxGuests > 0) control.max = pricing.maxGuests.toString();
                control.value = normalizeGuestCount(field.value || bookingState.guestCount || getGuestCount(), pricing).toString();
            } else if (inputType !== 'file') {
                control.value = field.value || (field.name === 'customerName' ? getCustomerName() : '');
            }
            label.appendChild(control);
            if (field.placeholder) {
                const help = document.createElement('small');
                help.className = 'ai-booking-field-help';
                help.textContent = field.placeholder;
                label.appendChild(help);
            }
            formElement.appendChild(label);
        });

        const guestCountInput = formElement.querySelector('[name="guestCount"]');
        if (guestCountInput) {
            formElement.dataset.bookingPricing = JSON.stringify(pricing);
            guestCountInput.addEventListener('input', () => syncBookingFormGuestCount(guestCountInput, pricing));
            guestCountInput.addEventListener('change', () => syncBookingFormGuestCount(guestCountInput, pricing));
            syncBookingFormGuestCount(guestCountInput, pricing);
        }

        const submit = document.createElement('button');
        submit.className = 'ai-booking-submit';
        submit.type = 'submit';
        submit.textContent = 'Gửi thông tin đặt phòng';
        formElement.appendChild(submit);

        wrapper.appendChild(formElement);
        scrollMessages();
    }

    function normalizeBookingPricing(pricing) {
        const safePricing = pricing && typeof pricing === 'object' ? pricing : {};
        return {
            capacity: toSafeNumber(safePricing.capacity),
            maxGuests: toSafeNumber(safePricing.maxGuests),
            extraGuestFee: toSafeNumber(safePricing.extraGuestFee),
            baseTotalPrice: toSafeNumber(safePricing.baseTotalPrice)
        };
    }

    function normalizeGuestCount(value, pricing) {
        const parsed = Number.parseInt(value, 10);
        const maxGuests = toSafeNumber(pricing?.maxGuests) || 20;
        if (Number.isNaN(parsed)) return 1;
        return Math.min(Math.max(parsed, 1), maxGuests);
    }

    function syncBookingFormGuestCount(input, pricing) {
        const guestCount = normalizeGuestCount(input.value, pricing);
        input.value = guestCount.toString();
        bookingState.guestCount = guestCount;
        if (guestsInput) guestsInput.value = guestCount;
        updateBookingSummaryTotals({ ...pricing, guestCount });
    }

    function updateBookingSummaryTotals(pricing) {
        if (!latestBookingSummary) return;
        const baseLines = JSON.parse(latestBookingSummary.dataset.baseLines || '[]');
        const totalFromServer = toSafeNumber(latestBookingSummary.dataset.totalPrice);
        const guestCount = normalizeGuestCount(pricing?.guestCount || bookingState.guestCount || getGuestCount(), pricing);
        const capacity = toSafeNumber(pricing?.capacity);
        const extraGuestFee = toSafeNumber(pricing?.extraGuestFee);
        const baseTotalPrice = toSafeNumber(pricing?.baseTotalPrice) || totalFromServer;
        const extraGuests = Math.max(0, guestCount - capacity);
        const extraTotal = extraGuests * extraGuestFee;
        const lines = [...baseLines, `Số khách: ${guestCount}`];
        if (capacity > 0 && extraGuests > 0 && extraGuestFee > 0) {
            lines.push(`Phụ thu khách thêm: ${extraGuests} khách × ${formatMoney(extraGuestFee)} = ${formatMoney(extraTotal)}`);
        }

        const list = latestBookingSummary.querySelector('ul');
        if (list) {
            list.innerHTML = '';
            lines.forEach(line => {
                const item = document.createElement('li');
                item.textContent = line;
                list.appendChild(item);
            });
        }

        const total = latestBookingSummary.querySelector('.ai-booking-total');
        if (total) total.textContent = `Tạm tính: ${formatMoney(baseTotalPrice + extraTotal)}`;
    }

    function renderPaymentQr(data) {
        const wrapper = appendBlock('ai-payment-block');
        const paymentUrl = toSafeRelativeUrl(data.paymentUrl || data.successUrl);
        const message = document.createElement('div');
        message.className = 'ai-payment-message';
        message.textContent = paymentUrl
            ? 'Thông tin đặt phòng đã được gửi. Bạn bấm nút bên dưới để qua trang thanh toán.'
            : 'Thông tin đặt phòng đã được gửi. Không tạo được link thanh toán, bạn liên hệ homestay để được hỗ trợ.';
        wrapper.appendChild(message);

        if (paymentUrl) {
            const link = document.createElement('a');
            link.className = 'ai-chip-btn';
            link.href = paymentUrl;
            link.textContent = 'Đi tới trang thanh toán';
            wrapper.appendChild(link);
        }
        scrollMessages();
    }

    function buildActionPayload(action, source) {
        const state = { ...bookingState };
        state.customerName = state.customerName || getCustomerName();
        state.branchId = state.branchId || getBranchId();
        state.bookingMode = state.bookingMode && state.bookingMode !== 'unknown' ? state.bookingMode : getBookingMode();
        state.guestCount = state.guestCount || getGuestCount();
        if (state.bookingMode === 'daily') {
            state.checkInDate = state.checkInDate || getDateOnly(checkInInput);
            state.checkOutDate = state.checkOutDate || getDateOnly(checkOutInput);
        } else {
            state.hourlyDate = state.hourlyDate || getDateOnly(checkInInput);
        }

        const roomId = toSafeNumber(source.dataset.roomId);
        const slotId = toSafeNumber(source.dataset.slotId);
        if (roomId > 0) state.selectedRoomId = roomId;
        if (slotId > 0) state.selectedSlotId = slotId;

        return { sessionId, action, state };
    }

    function collectBookingForm(bookingForm) {
        const formData = new FormData(bookingForm);
        const values = {};
        formData.forEach((value, key) => {
            if (value instanceof File) {
                values[key] = value.name || '';
                return;
            }
            values[key] = String(value || '').trim();
        });
        return {
            customerName: values.customerName || getCustomerName(),
            phoneNumber: values.customerPhone || values.phoneNumber || values.phone || '',
            email: values.customerEmail || values.email || '',
            notes: values.customerNote || values.notes || values.note || '',
            values
        };
    }

    function collectIdCardFiles(bookingForm) {
        return {
            front: bookingForm.querySelector('[name="idCardFront"]')?.files?.[0] || null,
            back: bookingForm.querySelector('[name="idCardBack"]')?.files?.[0] || null
        };
    }

    async function uploadIdCardFiles(bookingId, files) {
        if (!bookingId || (!files.front && !files.back)) return;
        const formData = new FormData();
        formData.append('bookingId', bookingId);
        if (files.front) formData.append('idCardFront', files.front);
        if (files.back) formData.append('idCardBack', files.back);
        const response = await fetch('/ai/booking-id-card', {
            method: 'POST',
            body: formData
        });
        if (!response.ok) throw new Error('Không upload được ảnh CCCD cho đơn đặt phòng.');
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
        if (Number.isNaN(count)) return 1;
        return Math.min(Math.max(count, 1), 20);
    }

    function getDateOnly(inputElement) {
        return inputElement && inputElement.value ? inputElement.value : null;
    }

    function getCheckInDateTime() {
        const value = getDateOnly(checkInInput);
        return value ? `${value}T00:00:00` : null;
    }

    function getCheckOutDateTime() {
        if (getBookingMode() === 'hourly') return null;
        const value = getDateOnly(checkOutInput);
        return value ? `${value}T00:00:00` : null;
    }

    function updateContextDateTimeControls() {
        if (!checkInInput || !checkOutInput) return;
        const hourly = getBookingMode() === 'hourly';
        checkInInput.type = 'date';
        checkOutInput.type = 'date';
        checkInInput.previousElementSibling.textContent = hourly ? 'Ngày đặt' : 'Ngày nhận';
        checkOutInput.previousElementSibling.textContent = 'Ngày trả';
        if (checkOutField) checkOutField.hidden = hourly;
        timeFields.forEach(field => { field.hidden = true; });
        if (checkInInput.value && checkInInput.value.length > 10) checkInInput.value = checkInInput.value.slice(0, 10);
        if (checkOutInput.value && checkOutInput.value.length > 10) checkOutInput.value = checkOutInput.value.slice(0, 10);
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
        const url = value.trim();
        if (url.startsWith('/') && !url.startsWith('//')) return url;
        try {
            const parsed = new URL(url, window.location.origin);
            return parsed.origin === window.location.origin ? `${parsed.pathname}${parsed.search}${parsed.hash}` : '';
        } catch {
            return '';
        }
    }

    function toSafeInputType(value) {
        return ['text', 'tel', 'email', 'number', 'date', 'file'].includes(value) ? value : 'text';
    }

    function getBookingFieldPlaceholder(field, inputType) {
        if (inputType === 'file') return '';
        if (field.name === 'customerName') return 'Nhập họ và tên';
        if (field.name === 'customerPhone') return 'Nhập số điện thoại';
        if (field.name === 'customerEmail') return 'email@example.com';
        if (field.name === 'guestCount') return '2';
        return '';
    }

    function scrollMessages() {
        messages.scrollTop = messages.scrollHeight;
    }

    function createSessionId() {
        if (window.crypto && crypto.randomUUID) return crypto.randomUUID().replace(/-/g, '');
        return `${Date.now()}${Math.random().toString(16).slice(2)}`;
    }
});
