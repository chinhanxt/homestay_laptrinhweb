document.addEventListener('DOMContentLoaded', () => {
    const widget = document.getElementById('public-ai-chatbot');
    if (!widget) return;

    const toggle = widget.querySelector('.ai-chat-toggle');
    const close = widget.querySelector('.ai-chat-close');
    const zaloToggle = widget.querySelector('.ai-zalo-toggle');
    const zaloPopover = widget.querySelector('#ai-zalo-popover');
    const zaloClose = widget.querySelector('.ai-zalo-close');
    const zaloList = widget.querySelector('.ai-zalo-list');
    const form = widget.querySelector('.ai-chat-form');
    const input = widget.querySelector('.ai-chat-input');
    const sendButton = widget.querySelector('.ai-chat-send');
    const messages = widget.querySelector('.ai-chat-messages');
    const status = widget.querySelector('.ai-chat-status');
    const contextToggle = widget.querySelector('.ai-context-toggle');
    const contextToggleHint = widget.querySelector('.ai-context-toggle-hint');
    const contextToggleIcon = widget.querySelector('.ai-context-toggle-icon');
    const contextForm = widget.querySelector('.ai-chat-context');
    const bookingConsultAction = widget.querySelector('#ai-booking-consult-action');
    const nameInput = widget.querySelector('.ai-context-name');
    const branchSelect = widget.querySelector('.ai-context-branch');
    const modeSelect = widget.querySelector('.ai-context-mode');
    const guestsInput = widget.querySelector('.ai-context-guests');
    const checkInInput = widget.querySelector('.ai-context-checkin');
    const checkOutInput = widget.querySelector('.ai-context-checkout');
    const timeFields = Array.from(widget.querySelectorAll('.ai-hourly-time-field'));
    const checkOutField = checkOutInput ? checkOutInput.closest('label') : null;
    const cancelAction = widget.querySelector('#ai-cancel-booking-action');

    let sessionId = getSessionId();
    let bookingState = JSON.parse(localStorage.getItem('ai_booking_state') || '{}');
    let latestBookingSummary = null;
    let zaloContactsLoaded = false;
    let contextTouched = { mode: false };
    let branchMetadataById = new Map();

    function getSessionId() {
        let sid = localStorage.getItem('ai_chat_session_id');
        if (!sid) {
            sid = createSessionId();
            localStorage.setItem('ai_chat_session_id', sid);
        }
        return sid;
    }

    // Connect to SignalR hub for admin chat monitor
    let chatHubConnection = null;

    function connectChatHub(currentSessionId) {
        if (typeof signalR === 'undefined') return;
        chatHubConnection = new signalR.HubConnectionBuilder()
            .withUrl('/chatHub')
            .withAutomaticReconnect()
            .build();

        chatHubConnection.on('newMessage', function (data) {
            if (data.role === 'system') {
                appendMessage(data.content, 'bot');
                const statusEl = document.querySelector('.ai-chat-status');
                if (statusEl) statusEl.textContent = '⏳ Admin đang xem tin nhắn của bạn';
            } else if (data.role === 'admin') {
                appendMessage('👤 Admin: ' + data.content, 'bot');
                const statusEl = document.querySelector('.ai-chat-status');
                if (statusEl) statusEl.textContent = '';
                if (data.formBlockJson) {
                    renderAdminFormBlock(data.formBlockJson, data.formBlockType);
                }
            }
            const input = document.querySelector('.ai-chat-input');
            if (input) input.disabled = false;
        });

        chatHubConnection.start().then(function () {
            chatHubConnection.invoke('joinSession', currentSessionId, 'user');
        }).catch(function (err) {
            console.error('Error starting SignalR chatHub connection:', err);
        });
    }

    function rejoinChatHub(newSessionId) {
        if (!chatHubConnection || typeof signalR === 'undefined') return;
        if (chatHubConnection.state === signalR.HubConnectionState.Connected) {
            chatHubConnection.invoke('joinSession', newSessionId, 'user')
                .catch(err => console.error('Error rejoining SignalR chat session:', err));
        } else if (chatHubConnection.state === signalR.HubConnectionState.Disconnected) {
            chatHubConnection.start().then(function () {
                chatHubConnection.invoke('joinSession', newSessionId, 'user');
            }).catch(err => console.error('Error starting and rejoining SignalR chat session:', err));
        }
    }

    async function loadChatHistory(sid) {
        try {
            const response = await fetch(`/ai/history?sessionId=${sid}`);
            if (!response.ok) return;
            const history = await response.json();
            if (Array.isArray(history) && history.length > 0) {
                messages.innerHTML = '';
                history.forEach(msg => {
                    const role = msg.role;
                    if (role === 'ai' || role === 'system') {
                        appendMessage(msg.content, 'bot');
                    } else if (role === 'admin') {
                        appendMessage('👤 Admin: ' + msg.content, 'bot');
                    } else {
                        appendMessage(msg.content, 'user');
                    }

                    if (msg.formBlockJson) {
                        try {
                            const block = JSON.parse(msg.formBlockJson);
                            if (Array.isArray(block.uiBlocks)) {
                                renderUiBlocks(block.uiBlocks);
                            }
                        } catch (e) {
                            console.error('Error parsing historical form block:', e);
                        }
                    }
                });
            }
        } catch (error) {
            console.error('Error loading chat history:', error);
        }
    }

    function renderAdminFormBlock(formBlockJson, formBlockType) {
        try {
            const block = JSON.parse(formBlockJson);
            if ((formBlockType === 'uiBlocks' || Array.isArray(block.uiBlocks)) && Array.isArray(block.uiBlocks)) {
                renderUiBlocks(block.uiBlocks);
                return;
            }
            appendMessage('Admin đã gửi một biểu mẫu, nhưng trình duyệt chưa đọc được nội dung.', 'bot');
        } catch (e) {
            console.error('Error parsing form block:', e);
        }
    }

    loadBranches();
    updateContextDateTimeControls();
    setContextExpanded(false);
    connectChatHub(sessionId);

    // Restore booking state inputs
    if (Object.keys(bookingState).length > 0) {
        updateBookingState(bookingState);
    }

    // Restore chat panel open/closed state
    const isChatOpen = sessionStorage.getItem('ai_chat_is_open') === 'true';
    setOpen(isChatOpen);

    // Restore chat history from server
    loadChatHistory(sessionId);

    toggle.addEventListener('click', () => {
        setZaloOpen(false);
        setOpen(!widget.classList.contains('is-open'));
    });
    close.addEventListener('click', () => setOpen(false));
    if (zaloToggle) zaloToggle.addEventListener('click', () => setZaloOpen(zaloPopover ? zaloPopover.hidden : true));
    if (zaloClose) zaloClose.addEventListener('click', () => setZaloOpen(false));
    if (contextToggle) contextToggle.addEventListener('click', () => setContextExpanded(contextForm ? contextForm.hidden : true));
    if (bookingConsultAction) {
        bookingConsultAction.addEventListener('click', async (event) => {
            event.preventDefault();
            bookingConsultAction.disabled = true;
            setBusy(true);

            try {
                const payload = {
                    sessionId: sessionId,
                    action: 'start-booking',
                    state: bookingState
                };
                const response = await fetch('/ai/booking-action', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                const data = await response.json().catch(() => ({}));
                if (!response.ok) throw new Error(data.message || 'AI booking action request failed.');
                
                // Open the chat widget panel
                setOpen(true);
                
                handleActionResponse(data);
            } catch {
                appendMessage('Xin lỗi, thao tác đặt phòng đang tạm thời bận. Bạn thử lại sau ít phút nhé.', 'bot');
            } finally {
                bookingConsultAction.disabled = false;
                setBusy(false);
            }
        });
    }

    const quickActionsToggleBar = widget.querySelector('#ai-quick-actions-toggle-bar');
    const quickActionsContainer = widget.querySelector('#ai-quick-actions-container');
    const quickActionsToggleIcon = widget.querySelector('#ai-quick-actions-toggle-icon');

    if (quickActionsToggleBar && quickActionsContainer && quickActionsToggleIcon) {
        const isCollapsed = sessionStorage.getItem('ai_quick_actions_collapsed') === 'true';
        setQuickActionsCollapsed(isCollapsed);

        quickActionsToggleBar.addEventListener('click', () => {
            const currentCollapsed = quickActionsContainer.classList.contains('is-collapsed');
            setQuickActionsCollapsed(!currentCollapsed);
        });
    }

    function setQuickActionsCollapsed(collapsed) {
        if (!quickActionsContainer || !quickActionsToggleIcon) return;
        quickActionsContainer.classList.toggle('is-collapsed', collapsed);
        sessionStorage.setItem('ai_quick_actions_collapsed', String(collapsed));
        if (collapsed) {
            quickActionsToggleIcon.classList.remove('fa-chevron-up');
            quickActionsToggleIcon.classList.add('fa-chevron-down');
            quickActionsContainer.style.display = 'none';
        } else {
            quickActionsToggleIcon.classList.remove('fa-chevron-down');
            quickActionsToggleIcon.classList.add('fa-chevron-up');
            quickActionsContainer.style.display = '';
        }
    }

    const clearButton = widget.querySelector('#ai-chat-clear');
    if (clearButton) {
        clearButton.addEventListener('click', async () => {
            const confirmed = await premiumConfirm('Bạn có chắc chắn muốn làm mới cuộc trò chuyện và bắt đầu phiên tư vấn mới không?', {
                title: 'Làm mới cuộc trò chuyện',
                confirmText: 'Làm mới',
                cancelText: 'Hủy',
                isDanger: true,
                type: 'warning'
            });
            if (confirmed) {
                localStorage.removeItem('ai_chat_session_id');
                localStorage.removeItem('ai_booking_state');
                sessionStorage.removeItem('ai_chat_is_open');
                sessionId = getSessionId();
                bookingState = {};
                messages.innerHTML = `
                    <div class="ai-message ai-message-bot">Chào bạn! Mình là trợ lý CHINHAN. Hãy click chọn nút <strong style="color: var(--luxury-accent);">"Thông tin tư vấn" <i class="fas fa-hand-point-up finger-point-up-anim"></i></strong> bên trên để nhập nhanh nhu cầu, hoặc chat trực tiếp với mình ở đây nhé! 👇</div>
                `;
                rejoinChatHub(sessionId);
                if (nameInput) nameInput.value = '';
                if (guestsInput) guestsInput.value = '1';
                if (checkInInput) checkInInput.value = '';
                if (checkOutInput) checkOutInput.value = '';
                if (branchSelect) branchSelect.value = '';
                updateContextDateTimeControls();
            }
        });
    }
    if (cancelAction) cancelAction.addEventListener('click', () => renderCancellationPrompt());
    if (modeSelect) {
        modeSelect.addEventListener('change', () => {
            contextTouched.mode = true;
            updateContextDateTimeControls();
        });
    }
    if (branchSelect) {
        branchSelect.addEventListener('change', () => {
            updateContextDateTimeControls();
        });
    }

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
            if (data.sessionId && data.sessionId !== sessionId) {
                sessionId = data.sessionId;
                localStorage.setItem('ai_chat_session_id', sessionId);
                rejoinChatHub(sessionId);
            }
            updateBookingState(data.state);

            appendMessage(data.answer || data.message || 'Mình chưa trả lời được lúc này, bạn thử lại giúp mình nhé.', 'bot');
            renderUiBlocks(data.uiBlocks || []);
            if (isCancellationIntent(message)) renderCancellationPrompt();
        } catch {
            appendMessage('Xin lỗi, nhân viên tư vấn đang tạm thời bận. Bạn thử lại sau ít phút nhé.', 'bot');
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

        const cancellationForm = submittedForm.closest('.ai-cancellation-form');
        if (cancellationForm) {
            await submitCancellationForm(cancellationForm);
            return;
        }

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
            branchMetadataById = new Map();
            branchSelect.innerHTML = '';
            branchSelect.appendChild(new Option('Chọn chi nhánh', ''));
            if (Array.isArray(branches)) {
                branches.forEach(branch => {
                    if (!branch || branch.id == null) return;
                    branchMetadataById.set(String(branch.id), branch);
                    branchSelect.appendChild(new Option(branch.name || `Chi nhánh ${branch.id}`, String(branch.id)));
                });
            }
            updateContextDateTimeControls();
        } catch {
            branchMetadataById = new Map();
            branchSelect.innerHTML = '';
            branchSelect.appendChild(new Option('Không tải được chi nhánh', ''));
        }
    }

    function setOpen(open) {
        widget.classList.toggle('is-open', open);
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        if (open) {
            sessionStorage.setItem('ai_chat_is_open', 'true');
            setTimeout(() => input.focus(), 120);
        } else {
            sessionStorage.setItem('ai_chat_is_open', 'false');
        }
    }

    function setBusy(busy) {
        input.disabled = busy;
        sendButton.disabled = busy;
        status.textContent = busy ? 'Nhân viên đang xử lý...' : '';
    }

    function appendMessage(text, type) {
        const bubble = document.createElement('div');
        bubble.className = `ai-message ai-message-${type}`;
        bubble.textContent = text;
        messages.appendChild(bubble);
        scrollMessages();
    }

    function handleActionResponse(data) {
        if (data.sessionId && data.sessionId !== sessionId) {
            sessionId = data.sessionId;
            localStorage.setItem('ai_chat_session_id', sessionId);
            rejoinChatHub(sessionId);
        }
        updateBookingState(data.state);
        const msgText = data.answer || data.message;
        if (msgText) appendMessage(msgText, 'bot');
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
            if (block.type === 'dateSelector') renderDateSelector(block.data || {});
            if (block.type === 'bookingModeChoice') renderBookingModeChoice(block.data || {});
            if (block.type === 'branchSelector') renderBranchSelector(block.data || {});
            if (block.type === 'singleDatePicker') renderSingleDatePicker(block.data || {});
            if (block.type === 'dateRangePicker') renderDateRangePicker(block.data || {});
            if (block.type === 'handoffContact') renderHandoffContact(block.data || {});
            if (block.type === 'roomDecisionCta') renderRoomDecisionCta(block.data || {});
            if (block.type === 'bookingCta') renderBookingCta(block.data || {});
            if (block.type === 'branchContactCapture') renderBranchContactCapture(block.data || {});
        });
    }

    function appendBlock(className) {
        const wrapper = document.createElement('div');
        wrapper.className = `ai-booking-block ${className}`;
        messages.appendChild(wrapper);
        scrollMessages();
        return wrapper;
    }

    function renderBookingModeChoice(data) {
        const wrapper = appendBlock('ai-booking-mode-choice');
        const title = document.createElement('h5');
        title.style.margin = '0 0 12px 0';
        title.style.fontSize = '0.95rem';
        title.style.fontWeight = 'bold';
        title.textContent = data.label || 'Chọn hình thức đặt phòng';
        wrapper.appendChild(title);

        const flows = Array.isArray(data.flows) ? data.flows : [];
        flows.forEach(flow => {
            const button = document.createElement('button');
            button.className = 'ai-choice-card d-flex align-items-center gap-3 w-100 mb-2';
            button.type = 'button';
            button.dataset.aiAction = 'select-booking-mode';
            button.dataset.bookingMode = flow.id;

            const iconDiv = document.createElement('div');
            iconDiv.className = 'ai-choice-icon';
            const icon = document.createElement('i');
            
            const flowIdLower = String(flow.id || '').toLowerCase();
            const flowNameLower = String(flow.name || '').toLowerCase();
            if (flowIdLower.includes('hourly') || flowNameLower.includes('giờ')) {
                icon.className = 'far fa-clock';
            } else {
                icon.className = 'far fa-calendar-alt';
            }
            iconDiv.appendChild(icon);
            button.appendChild(iconDiv);

            const contentDiv = document.createElement('div');
            contentDiv.className = 'flex-grow-1';

            const name = document.createElement('strong');
            name.textContent = flow.name || flow.id;
            name.style.fontWeight = '700';
            name.style.color = 'var(--luxury-primary)';
            name.style.fontSize = '0.9rem';
            name.style.lineHeight = '1.35';
            name.style.display = 'block';
            contentDiv.appendChild(name);

            if (flow.description) {
                const desc = document.createElement('span');
                desc.style.marginTop = '2px';
                desc.style.color = 'var(--luxury-text-muted)';
                desc.style.fontSize = '0.78rem';
                desc.style.lineHeight = '1.35';
                desc.style.fontWeight = 'normal';
                desc.style.display = 'block';
                desc.textContent = flow.description;
                contentDiv.appendChild(desc);
            }
            button.appendChild(contentDiv);
            wrapper.appendChild(button);
        });
        scrollMessages();
    }

    function renderBranchSelector(data) {
        const wrapper = appendBlock('ai-branch-selector');
        const title = document.createElement('h5');
        title.style.margin = '0 0 12px 0';
        title.style.fontSize = '0.95rem';
        title.style.fontWeight = 'bold';
        title.textContent = data.label || 'Chọn chi nhánh';
        wrapper.appendChild(title);

        const branches = Array.isArray(data.branches) ? data.branches : [];
        branches.forEach(branch => {
            const button = document.createElement('button');
            button.className = 'ai-choice-card d-flex align-items-center gap-3 w-100 mb-2';
            button.type = 'button';
            button.dataset.aiAction = 'select-branch';
            button.dataset.branchId = String(branch.id);

            const iconDiv = document.createElement('div');
            iconDiv.className = 'ai-choice-icon';
            const icon = document.createElement('i');
            icon.className = 'fas fa-map-marker-alt';
            iconDiv.appendChild(icon);
            button.appendChild(iconDiv);

            const contentDiv = document.createElement('div');
            contentDiv.className = 'flex-grow-1';

            const name = document.createElement('strong');
            name.textContent = branch.name;
            name.style.fontWeight = '700';
            name.style.color = 'var(--luxury-primary)';
            name.style.fontSize = '0.9rem';
            name.style.lineHeight = '1.35';
            name.style.display = 'block';
            contentDiv.appendChild(name);

            const subtitle = document.createElement('span');
            subtitle.style.marginTop = '2px';
            subtitle.style.color = 'var(--luxury-text-muted)';
            subtitle.style.fontSize = '0.78rem';
            subtitle.style.lineHeight = '1.35';
            subtitle.style.fontWeight = 'normal';
            subtitle.style.display = 'block';
            subtitle.textContent = 'Nhấp để chọn chi nhánh này';
            contentDiv.appendChild(subtitle);

            button.appendChild(contentDiv);
            wrapper.appendChild(button);
        });
        scrollMessages();
    }

    function renderSingleDatePicker(data) {
        const wrapper = appendBlock('ai-single-date-picker');
        
        const container = document.createElement('div');
        container.className = 'ai-booking-form-card';
        
        const label = document.createElement('label');
        label.className = 'ai-input-label d-block mb-2';
        
        const caption = document.createElement('span');
        caption.textContent = data.label || 'Chọn ngày đặt phòng';
        label.appendChild(caption);
        container.appendChild(label);

        const inputWrapper = document.createElement('div');
        inputWrapper.className = 'ai-date-input-wrapper mb-3';

        const icon = document.createElement('i');
        icon.className = 'far fa-calendar-alt ai-input-icon';
        inputWrapper.appendChild(icon);

        const dateInput = document.createElement('input');
        dateInput.type = 'date';
        dateInput.name = 'checkInDate';
        dateInput.className = 'ai-date-picker-input';

        const constraints = getBookingDateConstraints('hourly');
        dateInput.min = constraints.checkInMin;
        dateInput.value = normalizeDateValue(dateInput.value, constraints.checkInMin);
        
        inputWrapper.appendChild(dateInput);
        container.appendChild(inputWrapper);

        const button = document.createElement('button');
        button.className = 'ai-booking-submit w-100';
        button.type = 'button';
        button.dataset.aiAction = 'confirm-dates';
        button.textContent = 'Xác nhận ngày';
        container.appendChild(button);

        wrapper.appendChild(container);
        scrollMessages();
    }

    function renderDateRangePicker(data) {
        const wrapper = appendBlock('ai-date-range-picker');
        
        const container = document.createElement('div');
        container.className = 'ai-booking-form-card';
        
        const grid = document.createElement('div');
        grid.className = 'row g-2 mb-3';

        const ciCol = document.createElement('div');
        ciCol.className = 'col-6';
        
        const ciLabel = document.createElement('label');
        ciLabel.className = 'ai-input-label d-block mb-1';
        ciLabel.textContent = 'Ngày nhận phòng';
        ciCol.appendChild(ciLabel);

        const ciWrapper = document.createElement('div');
        ciWrapper.className = 'ai-date-input-wrapper';
        
        const ciIcon = document.createElement('i');
        ciIcon.className = 'far fa-calendar-plus ai-input-icon';
        ciWrapper.appendChild(ciIcon);

        const ciInput = document.createElement('input');
        ciInput.type = 'date';
        ciInput.name = 'checkInDate';
        ciInput.className = 'ai-date-picker-input';
        const constraints = getBookingDateConstraints('daily');
        ciInput.min = constraints.checkInMin;
        ciInput.value = normalizeDateValue(ciInput.value, constraints.checkInMin);
        ciWrapper.appendChild(ciInput);
        ciCol.appendChild(ciWrapper);
        grid.appendChild(ciCol);

        const coCol = document.createElement('div');
        coCol.className = 'col-6';

        const coLabel = document.createElement('label');
        coLabel.className = 'ai-input-label d-block mb-1';
        coLabel.textContent = 'Ngày trả phòng';
        coCol.appendChild(coLabel);

        const coWrapper = document.createElement('div');
        coWrapper.className = 'ai-date-input-wrapper';
        
        const coIcon = document.createElement('i');
        coIcon.className = 'far fa-calendar-minus ai-input-icon';
        coWrapper.appendChild(coIcon);

        const coInput = document.createElement('input');
        coInput.type = 'date';
        coInput.name = 'checkOutDate';
        coInput.className = 'ai-date-picker-input';

        coInput.min = constraints.checkOutMin;
        coInput.value = normalizeDateValue(coInput.value, constraints.checkOutMin);
        coWrapper.appendChild(coInput);
        coCol.appendChild(coWrapper);
        grid.appendChild(coCol);

        container.appendChild(grid);

        ciInput.addEventListener('change', () => {
            if (ciInput.value) {
                coInput.min = addDaysToDateString(ciInput.value, 1);
                if (coInput.value <= ciInput.value) {
                    coInput.value = coInput.min;
                }
            }
        });

        const button = document.createElement('button');
        button.className = 'ai-booking-submit w-100';
        button.type = 'button';
        button.dataset.aiAction = 'confirm-dates';
        button.textContent = 'Xác nhận ngày';
        container.appendChild(button);

        wrapper.appendChild(container);
        scrollMessages();
    }

    function renderRoomCards(data) {
        const wrapper = appendBlock('ai-room-cards');
        const rooms = Array.isArray(data.rooms) ? data.rooms : [];
        rooms.forEach(room => {
            if (!room || typeof room !== 'object') return;
            const card = document.createElement('div');
            card.className = 'ai-room-card';

            if (room.imageUrl) {
                const imgWrapper = document.createElement('div');
                imgWrapper.className = 'ai-room-img-wrapper';
                const img = document.createElement('img');
                img.src = room.imageUrl;
                img.alt = room.name || 'Phòng';
                imgWrapper.appendChild(img);
                card.appendChild(imgWrapper);
            }

            const title = document.createElement('h4');
            title.textContent = room.name || 'Phòng';
            card.appendChild(title);

            const badgeContainer = document.createElement('div');
            badgeContainer.className = 'mb-2 d-flex flex-wrap gap-1 align-items-center';

            if (room.requestedSlotAvailable !== false) {
                const availBadge = document.createElement('span');
                availBadge.className = 'ai-availability-badge';
                availBadge.innerHTML = '<i class="fas fa-check-circle me-1"></i>Còn trống';
                badgeContainer.appendChild(availBadge);
            }

            if (room.fitsStandardOccupancy) {
                const stdBadge = document.createElement('span');
                stdBadge.className = 'ai-occupancy-badge ai-occupancy-badge-standard';
                stdBadge.innerHTML = '<i class="fas fa-user-friends me-1"></i>Chuẩn khách';
                badgeContainer.appendChild(stdBadge);
            } else {
                const extBadge = document.createElement('span');
                extBadge.className = 'ai-occupancy-badge ai-occupancy-badge-extra';
                extBadge.innerHTML = '<i class="fas fa-exclamation-triangle me-1"></i>Có phụ thu';
                badgeContainer.appendChild(extBadge);
            }
            card.appendChild(badgeContainer);

            const description = document.createElement('div');
            description.className = 'ai-room-meta mb-1';
            description.textContent = room.description || '';
            card.appendChild(description);

            const prices = document.createElement('div');
            prices.className = 'ai-room-meta mb-1';
            prices.textContent = `Giờ: ${formatMoney(room.pricePerHour)}/h · Ngày: ${formatMoney(room.pricePerDay)}/ngày`;
            card.appendChild(prices);

            const capacity = document.createElement('div');
            capacity.className = 'ai-room-meta mb-3';
            capacity.textContent = `Chuẩn ${toSafeNumber(room.capacity)} khách · Tối đa ${toSafeNumber(room.maxGuests)} khách${room.extraGuestFee > 0 ? ` · Phụ thu ${formatMoney(room.extraGuestFee)}/khách` : ''}`;
            card.appendChild(capacity);

            const actions = document.createElement('div');
            actions.className = 'ai-room-actions';

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

    function renderHandoffContact(data) {
        const wrapper = appendBlock('ai-handoff-contact-block');
        
        const container = document.createElement('div');
        container.className = 'ai-handoff-contact';

        const title = document.createElement('h5');
        title.className = 'ai-block-title';
        title.textContent = data.branchName || 'CHINHAN';
        container.appendChild(title);

        const instruction = document.createElement('p');
        instruction.className = 'ai-payment-message mb-3';
        instruction.textContent = data.contactInstruction || 'Bạn vui lòng liên hệ chi nhánh qua thông tin dưới đây để được hỗ trợ.';
        container.appendChild(instruction);

        const address = document.createElement('div');
        address.className = 'ai-room-meta mb-1';
        address.innerHTML = `<i class="fas fa-map-marker-alt me-1 text-accent"></i> ${data.address || 'Hệ thống StayEasy Homestay'}`;
        container.appendChild(address);

        const hotline = document.createElement('div');
        hotline.className = 'ai-room-meta mb-1';
        hotline.innerHTML = `<i class="fas fa-phone-alt me-1 text-accent"></i> Hotline: <a href="tel:${data.hotline || ''}">${data.hotline || 'Chưa cấu hình'}</a>`;
        container.appendChild(hotline);

        const email = document.createElement('div');
        email.className = 'ai-room-meta mb-3';
        email.innerHTML = `<i class="fas fa-envelope me-1 text-accent"></i> Email: ${data.email || 'Chưa cấu hình'}`;
        container.appendChild(email);

        const actions = document.createElement('div');
        actions.className = 'ai-handoff-actions';

        if (data.hotline) {
            const callBtn = document.createElement('a');
            callBtn.className = 'ai-handoff-btn ai-handoff-btn-call';
            callBtn.href = `tel:${data.hotline}`;
            callBtn.innerHTML = '<i class="fas fa-phone me-1"></i> Gọi Hotline';
            actions.appendChild(callBtn);
        }

        if (data.hotline) {
            const zaloBtn = document.createElement('a');
            zaloBtn.className = 'ai-handoff-btn ai-handoff-btn-zalo';
            zaloBtn.href = buildZaloLink(data.hotline);
            zaloBtn.target = '_blank';
            zaloBtn.rel = 'noopener';
            zaloBtn.innerHTML = '<i class="fas fa-comment me-1"></i> Chat Zalo';
            actions.appendChild(zaloBtn);
        }

        if (data.mapUrl) {
            const mapBtn = document.createElement('a');
            mapBtn.className = 'ai-handoff-btn ai-handoff-btn-map';
            mapBtn.href = data.mapUrl;
            mapBtn.target = '_blank';
            mapBtn.rel = 'noopener';
            mapBtn.innerHTML = '<i class="fas fa-map me-1"></i> Xem Bản đồ';
            actions.appendChild(mapBtn);
        }

        container.appendChild(actions);
        wrapper.appendChild(container);
        scrollMessages();
    }

    function renderRoomDecisionCta(data) {
        const wrapper = appendBlock('ai-room-decision-cta');
        
        const container = document.createElement('div');
        container.className = 'ai-room-decision-card';
        
        if (data.message) {
            const msg = document.createElement('p');
            msg.className = 'ai-room-meta mb-3';
            msg.textContent = data.message;
            container.appendChild(msg);
        }

        const formGroup = document.createElement('div');
        formGroup.className = 'ai-guest-update-group mb-3 d-flex align-items-center justify-content-between gap-2';
        
        const labelText = document.createElement('span');
        labelText.className = 'ai-room-meta fw-bold';
        labelText.textContent = 'Số lượng khách:';
        formGroup.appendChild(labelText);

        const inputGroup = document.createElement('div');
        inputGroup.className = 'd-flex align-items-center gap-2';

        const guestInput = document.createElement('input');
        guestInput.type = 'number';
        guestInput.className = 'ai-guest-input form-control';
        guestInput.style.width = '60px';
        guestInput.min = '1';
        if (data.maxGuests) guestInput.max = String(data.maxGuests);
        guestInput.value = String(data.guestCount || 1);
        inputGroup.appendChild(guestInput);

        const updateBtn = document.createElement('button');
        updateBtn.className = 'ai-chip-btn';
        updateBtn.type = 'button';
        updateBtn.textContent = 'Xác nhận thay đổi';
        
        updateBtn.addEventListener('click', async () => {
            const guestCount = parseInt(guestInput.value, 10) || 1;
            bookingState.guestCount = guestCount;
            if (guestsInput) guestsInput.value = String(guestCount);
            
            updateBtn.disabled = true;
            setBusy(true);
            try {
                const payload = buildActionPayload('select-room', updateBtn);
                payload.state.guestCount = guestCount;
                
                const response = await fetch('/ai/booking-action', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                const responseData = await response.json().catch(() => ({}));
                if (!response.ok) throw new Error(responseData.message || 'AI booking action failed.');
                handleActionResponse(responseData);
            } catch {
                appendMessage('Xin lỗi, không cập nhật được số khách. Bạn thử lại nhé.', 'bot');
                updateBtn.disabled = false;
            } finally {
                setBusy(false);
            }
        });
        
        inputGroup.appendChild(updateBtn);
        formGroup.appendChild(inputGroup);
        container.appendChild(formGroup);

        const actions = document.createElement('div');
        actions.className = 'ai-room-actions';

        const detailsUrl = toSafeRelativeUrl(data.detailsUrl);
        if (detailsUrl) {
            const detailsLink = document.createElement('a');
            detailsLink.className = 'ai-chip-btn';
            detailsLink.href = detailsUrl;
            detailsLink.target = '_blank';
            detailsLink.rel = 'noopener';
            detailsLink.textContent = data.detailsLabel || 'Xem chi tiết phòng này';
            actions.appendChild(detailsLink);
        }

        if (data.showCommitButton) {
            const commitBtn = document.createElement('button');
            commitBtn.className = 'ai-chip-btn ai-chip-btn-primary';
            commitBtn.type = 'button';
            commitBtn.dataset.aiAction = 'commit-room';
            commitBtn.dataset.roomId = String(data.roomId);
            commitBtn.textContent = data.commitLabel || 'Xác nhận phòng này';
            actions.appendChild(commitBtn);
        }

        container.appendChild(actions);
        wrapper.appendChild(container);
        scrollMessages();
    }

    function renderBookingSummary(data) {
        const wrapper = appendBlock('ai-booking-summary');
        latestBookingSummary = wrapper;
        wrapper.dataset.baseLines = JSON.stringify(Array.isArray(data.lines) ? data.lines.filter(line => !String(line || '').startsWith('Số khách:') && !String(line || '').startsWith('Phụ thu khách thêm:')) : []);
        wrapper.dataset.totalPrice = toSafeNumber(data.totalPrice).toString();
        wrapper.dataset.bookingPricing = JSON.stringify(normalizeBookingPricing(data.pricing));
        const title = document.createElement('h4');
        title.textContent = data.title || 'Tóm tắt đặt phòng';
        wrapper.appendChild(title);

        const list = document.createElement('ul');
        wrapper.appendChild(list);

        const total = document.createElement('div');
        total.className = 'ai-booking-total';
        wrapper.appendChild(total);
        updateBookingSummaryTotals(data.pricing || null);
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
        const storedPricing = normalizeBookingPricing(JSON.parse(latestBookingSummary.dataset.bookingPricing || '{}'));
        const resolvedPricing = {
            ...storedPricing,
            ...(pricing && typeof pricing === 'object' ? normalizeBookingPricing(pricing) : {})
        };
        const totalFromServer = toSafeNumber(latestBookingSummary.dataset.totalPrice);
        const guestCount = normalizeGuestCount(pricing?.guestCount || bookingState.guestCount || getGuestCount(), resolvedPricing);
        const capacity = toSafeNumber(resolvedPricing.capacity);
        const extraGuestFee = toSafeNumber(resolvedPricing.extraGuestFee);
        const baseTotalPrice = toSafeNumber(resolvedPricing.baseTotalPrice) || totalFromServer;
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

    async function setZaloOpen(open) {
        if (!zaloPopover) return;
        zaloPopover.hidden = !open;
        widget.classList.toggle('is-zalo-open', open);
        if (zaloToggle) zaloToggle.setAttribute('aria-expanded', String(open));
        if (open && !zaloContactsLoaded) await loadZaloContacts();
    }

    async function loadZaloContacts() {
        if (!zaloList) return;
        zaloList.textContent = 'Đang tải danh sách chi nhánh...';
        try {
            const response = await fetch('/chinhan/hethong/branches/public-contacts');
            const branches = await response.json().catch(() => []);
            if (!response.ok) throw new Error('Không tải được danh sách Zalo.');
            renderZaloContacts(Array.isArray(branches) ? branches : []);
            zaloContactsLoaded = true;
        } catch (error) {
            zaloList.textContent = error.message || 'Không tải được danh sách Zalo.';
        }
    }

    function renderZaloContacts(branches) {
        if (!zaloList) return;
        zaloList.innerHTML = '';
        if (branches.length === 0) {
            zaloList.textContent = 'Hiện chưa có chi nhánh để liên hệ Zalo.';
            return;
        }

        branches.forEach(branch => {
            const item = document.createElement('div');
            item.className = 'ai-zalo-item';

            const name = document.createElement('div');
            name.className = 'ai-zalo-branch-name';
            name.textContent = branch.name || 'Chi nhánh';
            item.appendChild(name);

            const zaloPhone = String(branch.zaloPhone || '').trim();
            if (zaloPhone) {
                const link = document.createElement('a');
                link.href = buildZaloLink(zaloPhone);
                link.target = '_blank';
                link.rel = 'noopener noreferrer';
                link.textContent = zaloPhone;
                item.appendChild(link);
            } else {
                const empty = document.createElement('span');
                empty.className = 'ai-zalo-empty';
                empty.textContent = 'Chưa cấu hình Zalo';
                item.appendChild(empty);
            }

            zaloList.appendChild(item);
        });
    }

    function setContextExpanded(expanded) {
        if (!contextForm) return;
        contextForm.hidden = !expanded;
        contextForm.classList.toggle('is-collapsed', !expanded);
        if (contextToggle) contextToggle.setAttribute('aria-expanded', String(expanded));
        if (contextToggleHint) {
            contextToggleHint.textContent = expanded
                ? 'Ẩn thông tin để quay lại khung chat rộng hơn'
                : 'Tên, chi nhánh, ngày đặt, số khách';
        }
        if (contextToggleIcon) {
            contextToggleIcon.classList.toggle('fa-chevron-down', !expanded);
            contextToggleIcon.classList.toggle('fa-chevron-up', expanded);
        }
    }

    function renderCancellationPrompt() {
        const wrapper = appendBlock('ai-cancellation-prompt');
        const text = document.createElement('div');
        text.className = 'ai-payment-message';
        text.textContent = 'Nhân viên không tự hủy phòng, nhưng bạn có thể gửi yêu cầu để nhân viên kiểm tra hoặc liên hệ trực tiếp chi nhánh đã đặt.';
        wrapper.appendChild(text);

        const requestButton = document.createElement('button');
        requestButton.type = 'button';
        requestButton.className = 'ai-action-btn ai-action-primary';
        requestButton.textContent = 'Gửi yêu cầu hủy';
        requestButton.addEventListener('click', () => renderCancellationForm());
        wrapper.appendChild(requestButton);

        const contactButton = document.createElement('button');
        contactButton.type = 'button';
        contactButton.className = 'ai-action-btn ai-action-secondary';
        contactButton.textContent = 'Liên hệ Zalo/email';
        contactButton.addEventListener('click', () => renderCancellationBranchContactSelector());
        wrapper.appendChild(contactButton);

        scrollMessages();
    }

    async function renderCancellationForm() {
        setOpen(true);
        const wrapper = appendBlock('ai-cancellation-form-block');
        wrapper.textContent = 'Đang tải chính sách hủy...';
        let policy = null;
        try {
            const response = await fetch('/ai/cancellations/policy');
            policy = await response.json().catch(() => null);
        } catch {}

        wrapper.innerHTML = '';
        const formElement = document.createElement('form');
        formElement.className = 'ai-cancellation-form ai-booking-form';

        const title = document.createElement('h4');
        title.textContent = 'Yêu cầu hủy phòng';
        formElement.appendChild(title);

        const policyBox = document.createElement('div');
        policyBox.className = 'ai-payment-message';
        policyBox.textContent = policy
            ? `${policy.policyMessage || ''} Trước ${policy.noticeHours} giờ: hoàn ${policy.refundPercentBeforeNotice}%. Sau ngưỡng: hoàn ${policy.refundPercentAfterNotice}%.`
            : 'Yêu cầu hủy sẽ được nhân viên kiểm tra và phản hồi qua email.';
        formElement.appendChild(policyBox);

        const contactOption = document.createElement('button');
        contactOption.type = 'button';
        contactOption.className = 'ai-action-btn ai-action-secondary';
        contactOption.textContent = 'Liên hệ Zalo/email chi nhánh';
        contactOption.addEventListener('click', () => renderCancellationBranchContactSelector());
        formElement.appendChild(contactOption);

        addCancellationInput(formElement, 'bookingCode', 'Mã đơn nếu có', 'text', false);
        addCancellationInput(formElement, 'customerName', 'Họ tên', 'text', true, getCustomerName());
        addCancellationInput(formElement, 'customerPhone', 'Số điện thoại', 'tel', true);
        addCancellationInput(formElement, 'customerEmail', 'Email nhận phản hồi', 'email', true);
        addCancellationInput(formElement, 'confirmationEmailProof', 'Ảnh email xác nhận đặt phòng', 'file', true);
        addCancellationInput(formElement, 'refundQrImage', 'Ảnh QR nhận hoàn tiền', 'file', false);
        addCancellationInput(formElement, 'refundBankName', 'Ngân hàng ví dụ MB, VCB', 'text', false);
        addCancellationInput(formElement, 'refundBankAccountNumber', 'Số tài khoản', 'text', false);
        addCancellationInput(formElement, 'refundBankAccountHolder', 'Tên chủ tài khoản', 'text', false);

        const submit = document.createElement('button');
        submit.className = 'ai-booking-submit';
        submit.type = 'submit';
        submit.textContent = 'Gửi yêu cầu hủy';
        formElement.appendChild(submit);
        wrapper.appendChild(formElement);
        scrollMessages();
    }

    async function renderCancellationBranchContactSelector() {
        setOpen(true);
        const wrapper = appendBlock('ai-cancellation-contact-block');
        wrapper.textContent = 'Đang tải danh sách chi nhánh...';

        try {
            const response = await fetch('/chinhan/hethong/branches/public-contacts');
            const branches = await response.json().catch(() => []);
            if (!response.ok) throw new Error('Không tải được danh sách chi nhánh.');
            if (!Array.isArray(branches) || branches.length === 0) {
                wrapper.textContent = 'Hiện chưa có thông tin chi nhánh để liên hệ.';
                scrollMessages();
                return;
            }

            wrapper.innerHTML = '';
            const message = document.createElement('div');
            message.className = 'ai-payment-message';
            message.textContent = 'Bạn chọn đúng chi nhánh đã đặt phòng để nhận Zalo/email liên hệ.';
            wrapper.appendChild(message);

            const select = document.createElement('select');
            select.className = 'ai-booking-input';
            const placeholder = document.createElement('option');
            placeholder.value = '';
            placeholder.textContent = 'Chọn chi nhánh đã đặt';
            select.appendChild(placeholder);

            branches.forEach(branch => {
                const option = document.createElement('option');
                option.value = String(branch.id || '');
                option.textContent = branch.name;
                select.appendChild(option);
            });
            wrapper.appendChild(select);

            const contactBox = document.createElement('div');
            contactBox.className = 'ai-payment-message';
            contactBox.hidden = true;
            wrapper.appendChild(contactBox);

            select.addEventListener('change', () => {
                const selected = branches.find(branch => String(branch.id || '') === select.value);
                renderSelectedBranchContact(contactBox, selected);
            });
        } catch (error) {
            wrapper.textContent = error.message || 'Không tải được thông tin chi nhánh.';
        }

        scrollMessages();
    }

    function renderSelectedBranchContact(contactBox, branch) {
        contactBox.hidden = !branch;
        contactBox.innerHTML = '';
        if (!branch) return;

        const title = document.createElement('div');
        title.className = 'fw-bold mb-2';
        title.textContent = branch.name || 'Chi nhánh';
        contactBox.appendChild(title);

        if (branch.address) {
            const address = document.createElement('div');
            address.textContent = `Địa chỉ: ${branch.address}`;
            contactBox.appendChild(address);
        }

        const zaloPhone = String(branch.zaloPhone || '').trim();
        const zaloLine = document.createElement('div');
        if (zaloPhone) {
            const zaloLink = document.createElement('a');
            zaloLink.href = buildZaloLink(zaloPhone);
            zaloLink.target = '_blank';
            zaloLink.rel = 'noopener noreferrer';
            zaloLink.textContent = zaloPhone;
            zaloLine.appendChild(document.createTextNode('Zalo: '));
            zaloLine.appendChild(zaloLink);
        } else {
            zaloLine.textContent = 'Zalo: Chi nhánh chưa cấu hình SĐT Zalo.';
        }
        contactBox.appendChild(zaloLine);

        const email = String(branch.email || '').trim();
        const emailLine = document.createElement('div');
        if (email) {
            const emailLink = document.createElement('a');
            emailLink.href = `mailto:${email}`;
            emailLink.textContent = email;
            emailLine.appendChild(document.createTextNode('Email: '));
            emailLine.appendChild(emailLink);
        } else {
            emailLine.textContent = 'Email: Chi nhánh chưa cấu hình email.';
        }
        contactBox.appendChild(emailLine);

        scrollMessages();
    }

    function addCancellationInput(formElement, name, labelText, type, required, value) {
        const label = document.createElement('label');
        const caption = document.createElement('span');
        caption.textContent = labelText;
        label.appendChild(caption);
        const inputEl = document.createElement('input');
        inputEl.name = name;
        inputEl.type = type;
        inputEl.required = required;
        if (type === 'file') inputEl.accept = 'image/*';
        if (type !== 'file' && value) inputEl.value = value;
        label.appendChild(inputEl);
        formElement.appendChild(label);
    }

    async function submitCancellationForm(cancellationForm) {
        const submit = cancellationForm.querySelector('[type="submit"]');
        if (submit) submit.disabled = true;
        setBusy(true);
        try {
            const formData = new FormData(cancellationForm);
            formData.append('sessionId', sessionId);
            const response = await fetch('/ai/cancellations/submit', { method: 'POST', body: formData });
            const data = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(data.message || 'Không gửi được yêu cầu hủy.');
            appendMessage(data.message || 'Yêu cầu hủy đã được gửi. Nhân viên sẽ phản hồi qua email.', 'bot');
            cancellationForm.remove();
        } catch (error) {
            appendMessage(error.message || 'Chưa gửi được yêu cầu hủy. Bạn thử lại giúp mình nhé.', 'bot');
            if (submit) submit.disabled = false;
        } finally {
            setBusy(false);
        }
    }

    function isCancellationIntent(message) {
        const text = String(message || '').toLowerCase();
        return (text.includes('hủy') || text.includes('huy')) && text.includes('phòng');
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

    function renderBookingCta(data) {
        const wrapper = appendBlock('ai-booking-cta-block');

        const message = document.createElement('p');
        message.className = 'ai-payment-message mb-3 text-center';
        message.style.fontSize = '0.92rem';
        message.style.fontWeight = '500';
        message.style.color = 'var(--luxury-primary)';
        message.textContent = data.message || 'Hoàn tất thông tin ở trang đặt phòng chính thức.';
        wrapper.appendChild(message);

        const checkoutLink = document.createElement('a');
        checkoutLink.className = 'ai-action-btn ai-action-primary w-100 d-block text-center py-2';
        checkoutLink.style.textDecoration = 'none';
        checkoutLink.style.fontSize = '0.9rem';
        checkoutLink.href = toSafeRelativeUrl(data.target);
        checkoutLink.innerHTML = '<i class="fas fa-check-double me-2"></i>Đi tới trang đặt phòng';
        wrapper.appendChild(checkoutLink);

        if (data.secondaryMessage) {
            const secMessage = document.createElement('p');
            secMessage.className = 'ai-payment-message mt-3 mb-0 text-center';
            secMessage.style.fontSize = '0.8rem';
            secMessage.style.fontStyle = 'italic';
            secMessage.textContent = data.secondaryMessage;
            wrapper.appendChild(secMessage);
        }
        scrollMessages();
    }

    function renderBranchContactCapture(data) {
        const wrapper = appendBlock('ai-branch-contact-capture');

        if (data.branchName) {
            const title = document.createElement('h5');
            title.className = 'ai-block-title mb-2';
            title.textContent = data.branchName;
            wrapper.appendChild(title);
        }

        if (data.message) {
            const msg = document.createElement('p');
            msg.className = 'ai-payment-message mb-3';
            msg.textContent = data.message;
            wrapper.appendChild(msg);
        }

        if (data.address) {
            const addr = document.createElement('div');
            addr.className = 'ai-room-meta mb-1';
            addr.innerHTML = `<i class="fas fa-map-marker-alt me-1 text-accent"></i> ${data.address}`;
            wrapper.appendChild(addr);
        }

        if (data.hotline) {
            const phoneVal = String(data.hotline).trim();
            const phoneDiv = document.createElement('div');
            phoneDiv.className = 'ai-room-meta mb-1';
            phoneDiv.innerHTML = `<i class="fas fa-phone-alt me-1 text-accent"></i> Hotline: <a href="tel:${phoneVal}">${phoneVal}</a>`;
            wrapper.appendChild(phoneDiv);
        }

        const formElement = document.createElement('form');
        formElement.className = 'ai-contact-capture-form mt-3';
        
        const phoneInput = document.createElement('input');
        phoneInput.type = 'tel';
        phoneInput.placeholder = 'Số điện thoại của bạn';
        phoneInput.required = true;
        phoneInput.name = 'phone';
        phoneInput.maxLength = 20;
        formElement.appendChild(phoneInput);

        const submitBtn = document.createElement('button');
        submitBtn.type = 'submit';
        submitBtn.className = 'ai-booking-submit';
        submitBtn.style.padding = '9px 16px';
        submitBtn.style.borderRadius = '8px';
        submitBtn.style.fontSize = '0.85rem';
        submitBtn.textContent = 'Gửi liên hệ';
        formElement.appendChild(submitBtn);

        wrapper.appendChild(formElement);

        formElement.addEventListener('submit', async (e) => {
            e.preventDefault();
            const phone = phoneInput.value.trim();
            if (!phone) return;

            submitBtn.disabled = true;
            phoneInput.disabled = true;
            setBusy(true);

            try {
                const payload = {
                    sessionId: sessionId,
                    action: 'submit-contact-phone',
                    state: {
                        ...bookingState,
                        customerPhone: phone
                    },
                    formSubmission: {
                        phoneNumber: phone,
                        values: { phone: phone }
                    }
                };

                const response = await fetch('/ai/booking-action', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                
                const responseData = await response.json().catch(() => ({}));
                if (!response.ok) throw new Error(responseData.message || 'Gửi liên hệ thất bại.');

                formElement.classList.add('is-submitted');
                submitBtn.textContent = 'Đã gửi';
                appendMessage(`Cảm ơn bạn! Số điện thoại ${phone} đã được gửi đến nhân viên chi nhánh ${data.branchName || ''} hỗ trợ.`, 'bot');
                
                updateBookingState(responseData.state || payload.state);
            } catch (err) {
                appendMessage('Xin lỗi, không gửi được thông tin liên hệ. Bạn vui lòng thử lại nhé.', 'bot');
                submitBtn.disabled = false;
                phoneInput.disabled = false;
            } finally {
                setBusy(false);
            }
        });

        scrollMessages();
    }

    function renderDateSelector(data) {
        const wrapper = appendBlock('ai-date-selector');
        
        const container = document.createElement('div');
        container.className = 'p-3';

        const mode = getBookingMode();

        const modeGroup = document.createElement('div');
        modeGroup.className = 'ai-date-mode-group mb-3';

        const hourlyLabel = document.createElement('label');
        hourlyLabel.className = 'ai-date-mode-option flex-grow-1 justify-content-center';
        const hourlyRadio = document.createElement('input');
        hourlyRadio.type = 'radio';
        hourlyRadio.name = 'ds-booking-mode';
        hourlyRadio.value = 'hourly';
        hourlyRadio.checked = mode === 'hourly';
        hourlyLabel.appendChild(hourlyRadio);
        hourlyLabel.appendChild(document.createTextNode(' Theo giờ'));

        const dailyLabel = document.createElement('label');
        dailyLabel.className = 'ai-date-mode-option flex-grow-1 justify-content-center';
        const dailyRadio = document.createElement('input');
        dailyRadio.type = 'radio';
        dailyRadio.name = 'ds-booking-mode';
        dailyRadio.value = 'daily';
        dailyRadio.checked = mode === 'daily';
        dailyLabel.appendChild(dailyRadio);
        dailyLabel.appendChild(document.createTextNode(' Theo ngày'));

        modeGroup.appendChild(hourlyLabel);
        modeGroup.appendChild(dailyLabel);
        container.appendChild(modeGroup);

        const dateGroup = document.createElement('div');
        dateGroup.className = 'row g-2 mb-3';

        const checkInCol = document.createElement('div');
        checkInCol.className = 'col-12';
        
        const checkInLabel = document.createElement('label');
        checkInLabel.className = 'ai-input-label d-block mb-1';
        const checkInCaption = document.createElement('span');
        checkInCaption.textContent = mode === 'daily' ? 'Ngày nhận' : 'Ngày đặt';
        checkInLabel.appendChild(checkInCaption);
        checkInCol.appendChild(checkInLabel);

        const checkInWrapper = document.createElement('div');
        checkInWrapper.className = 'ai-date-input-wrapper';
        
        const checkInIcon = document.createElement('i');
        checkInIcon.className = 'far fa-calendar-alt ai-input-icon';
        checkInWrapper.appendChild(checkInIcon);

        const checkInEl = document.createElement('input');
        checkInEl.type = 'date';
        checkInEl.className = 'ai-date-picker-input ai-ds-checkin';
        checkInEl.required = true;

        checkInWrapper.appendChild(checkInEl);
        checkInCol.appendChild(checkInWrapper);
        dateGroup.appendChild(checkInCol);

        const checkOutCol = document.createElement('div');
        checkOutCol.className = 'col-12';
        checkOutCol.style.display = mode === 'daily' ? 'block' : 'none';
        
        const checkOutLabel = document.createElement('label');
        checkOutLabel.className = 'ai-input-label d-block mb-1';
        const checkOutCaption = document.createElement('span');
        checkOutCaption.textContent = 'Ngày trả';
        checkOutLabel.appendChild(checkOutCaption);
        checkOutCol.appendChild(checkOutLabel);

        const checkOutWrapper = document.createElement('div');
        checkOutWrapper.className = 'ai-date-input-wrapper';
        
        const checkOutIcon = document.createElement('i');
        checkOutIcon.className = 'far fa-calendar-check ai-input-icon';
        checkOutWrapper.appendChild(checkOutIcon);

        const checkOutEl = document.createElement('input');
        checkOutEl.type = 'date';
        checkOutEl.className = 'ai-date-picker-input ai-ds-checkout';

        checkOutWrapper.appendChild(checkOutEl);
        checkOutCol.appendChild(checkOutWrapper);
        dateGroup.appendChild(checkOutCol);

        container.appendChild(dateGroup);

        checkInEl.addEventListener('change', () => {
            if (checkInEl.value) {
                checkOutEl.min = addDaysToDateString(checkInEl.value, 1);
                if (checkOutEl.value <= checkInEl.value) {
                    checkOutEl.value = checkOutEl.min;
                }
            }
        });

        const updateLayout = (isDaily) => {
            const constraints = getBookingDateConstraints(isDaily ? 'daily' : 'hourly');
            checkInEl.min = constraints.checkInMin;
            checkInEl.value = normalizeDateValue(checkInEl.value, constraints.checkInMin);

            if (isDaily) {
                checkOutCol.style.display = 'block';
                checkInCol.className = 'col-6';
                checkOutCol.className = 'col-6';
                checkInCaption.textContent = 'Ngày nhận';
                checkOutEl.min = addDaysToDateString(checkInEl.value || constraints.checkInMin, 1);
                checkOutEl.value = normalizeDateValue(checkOutEl.value, checkOutEl.min);
            } else {
                checkOutCol.style.display = 'none';
                checkInCol.className = 'col-12';
                checkInCaption.textContent = 'Ngày đặt';
            }
        };

        updateLayout(mode === 'daily');

        hourlyRadio.addEventListener('change', () => {
            updateLayout(false);
        });
        dailyRadio.addEventListener('change', () => {
            updateLayout(true);
        });

        const confirmBtn = document.createElement('button');
        confirmBtn.className = 'ai-booking-submit w-100';
        confirmBtn.type = 'button';
        confirmBtn.dataset.aiAction = 'confirm-dates';
        confirmBtn.dataset.dsMode = 'dateSelector';
        confirmBtn.textContent = 'Xác nhận ngày';
        container.appendChild(confirmBtn);

        wrapper.appendChild(container);
        scrollMessages();
    }

    function buildActionPayload(action, source) {
        const state = { ...bookingState };
        state.customerName = state.customerName || getCustomerName();
        state.branchId = state.branchId || getBranchId();
        state.bookingMode = state.bookingMode && state.bookingMode !== 'unknown'
            ? state.bookingMode
            : (contextTouched.mode ? getBookingMode() : 'unknown');
        state.guestCount = state.guestCount || getGuestCount();

        if (source && source.dataset) {
            if (source.dataset.bookingMode) state.bookingMode = source.dataset.bookingMode;
            if (source.dataset.branchId) state.branchId = toSafeNumber(source.dataset.branchId);
            if (source.dataset.checkInDate) state.checkInDate = source.dataset.checkInDate;
            if (source.dataset.checkOutDate) state.checkOutDate = source.dataset.checkOutDate;
            if (source.dataset.hourlyDate) state.hourlyDate = source.dataset.hourlyDate;
        }

        if (action === 'confirm-dates') {
            const dateBlock = source.closest('.ai-single-date-picker, .ai-date-range-picker, .ai-date-selector');
            if (dateBlock) {
                if (dateBlock.classList.contains('ai-date-selector')) {
                    const modeInput = dateBlock.querySelector('input[name="ds-booking-mode"]:checked');
                    if (modeInput) state.bookingMode = modeInput.value;
                    const checkInVal = getDateOnlyFrom(dateBlock.querySelector('.ai-ds-checkin'));
                    if (checkInVal) {
                        state.checkInDate = checkInVal;
                        state.hourlyDate = checkInVal;
                    }
                    const checkOutVal = getDateOnlyFrom(dateBlock.querySelector('.ai-ds-checkout'));
                    if (checkOutVal) state.checkOutDate = checkOutVal;
                }
                else if (dateBlock.classList.contains('ai-single-date-picker')) {
                    state.bookingMode = 'hourly';
                    const checkInVal = getDateOnlyFrom(dateBlock.querySelector('input[name="checkInDate"]'));
                    if (checkInVal) {
                        state.checkInDate = checkInVal;
                        state.hourlyDate = checkInVal;
                    }
                }
                else if (dateBlock.classList.contains('ai-date-range-picker')) {
                    state.bookingMode = 'daily';
                    const checkInVal = getDateOnlyFrom(dateBlock.querySelector('input[name="checkInDate"]'));
                    if (checkInVal) state.checkInDate = checkInVal;
                    const checkOutVal = getDateOnlyFrom(dateBlock.querySelector('input[name="checkOutDate"]'));
                    if (checkOutVal) state.checkOutDate = checkOutVal;
                }
            }
        } else {
            if (state.bookingMode === 'daily') {
                state.checkInDate = state.checkInDate || getDateOnly(checkInInput);
                state.checkOutDate = state.checkOutDate || getDateOnly(checkOutInput);
            } else if (state.bookingMode === 'hourly') {
                state.hourlyDate = state.hourlyDate || getDateOnly(checkInInput);
            }
        }

        const roomId = toSafeNumber(source.dataset.roomId);
        const slotId = toSafeNumber(source.dataset.slotId);
        if (roomId > 0) state.selectedRoomId = roomId;
        if (slotId > 0) state.selectedSlotId = slotId;

        return { sessionId, action, state };
    }

    function getDateOnlyFrom(inputEl) {
        return inputEl && inputEl.value ? inputEl.value : null;
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
        localStorage.setItem('ai_booking_state', JSON.stringify(bookingState));
        if (state.customerName && nameInput) nameInput.value = state.customerName;
        if (state.branchId && branchSelect) branchSelect.value = String(state.branchId);
        if (state.bookingMode && modeSelect && (state.bookingMode === 'hourly' || state.bookingMode === 'daily' || state.bookingMode === 'unknown')) modeSelect.value = state.bookingMode;
        if (state.guestCount && guestsInput) guestsInput.value = state.guestCount;

        // Also populate dates if present
        if (checkInInput) {
            const dateVal = state.bookingMode === 'daily' ? state.checkInDate : state.hourlyDate;
            if (dateVal) checkInInput.value = dateVal;
        }
        if (checkOutInput && state.bookingMode === 'daily' && state.checkOutDate) {
            checkOutInput.value = state.checkOutDate;
        }
        updateContextDateTimeControls();
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
        if (!modeSelect) return 'unknown';
        if (modeSelect.value === 'daily') return 'daily';
        if (modeSelect.value === 'hourly') return 'hourly';
        return 'unknown';
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
        if (getBookingMode() !== 'daily') return null;
        const value = getDateOnly(checkOutInput);
        return value ? `${value}T00:00:00` : null;
    }

    function updateContextDateTimeControls() {
        if (!checkInInput || !checkOutInput) return;
        const mode = getBookingMode();
        const hourly = mode === 'hourly';
        const daily = mode === 'daily';
        const constraints = getBookingDateConstraints(daily ? 'daily' : 'hourly');
        checkInInput.type = 'date';
        checkOutInput.type = 'date';
        checkInInput.previousElementSibling.textContent = daily ? 'Ngày nhận' : 'Ngày đặt';
        checkOutInput.previousElementSibling.textContent = 'Ngày trả';
        if (checkOutField) checkOutField.hidden = !daily;
        timeFields.forEach(field => { field.hidden = true; });
        checkInInput.min = constraints.checkInMin;
        checkInInput.value = normalizeDateValue(checkInInput.value, constraints.checkInMin);
        checkOutInput.min = daily
            ? addDaysToDateString(checkInInput.value || constraints.checkInMin, 1)
            : constraints.checkOutMin;
        if (daily) {
            checkOutInput.value = normalizeDateValue(checkOutInput.value, checkOutInput.min);
        }
        if (checkInInput.value && checkInInput.value.length > 10) checkInInput.value = checkInInput.value.slice(0, 10);
        if (checkOutInput.value && checkOutInput.value.length > 10) checkOutInput.value = checkOutInput.value.slice(0, 10);
    }

    function getSelectedBranchMetadata() {
        const branchId = getBranchId();
        if (!branchId) return null;
        return branchMetadataById.get(String(branchId)) || null;
    }

    function getBookingDateConstraints(mode) {
        const branchMetadata = getSelectedBranchMetadata();
        const today = getLocalDateInputValue(new Date());
        const fallbackHourlyMin = today;
        const fallbackDailyMin = today;
        const dailyCheckInMin = branchMetadata?.earliestAllowedDailyDate || fallbackDailyMin;
        const hourlyCheckInMin = branchMetadata?.earliestAllowedHourlyDate || fallbackHourlyMin;
        const checkInMin = mode === 'daily' ? dailyCheckInMin : hourlyCheckInMin;

        return {
            checkInMin,
            checkOutMin: addDaysToDateString(checkInMin, 1)
        };
    }

    function normalizeDateValue(value, minValue) {
        if (!value || value < minValue) {
            return minValue;
        }

        return value;
    }

    function addDaysToDateString(dateString, days) {
        const [year, month, day] = String(dateString || '').split('-').map(Number);
        if (!year || !month || !day) {
            return getLocalDateInputValue(new Date());
        }

        const date = new Date(year, month - 1, day);
        date.setDate(date.getDate() + days);
        return getLocalDateInputValue(date);
    }

    function getLocalDateInputValue(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
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

    function buildZaloLink(phone) {
        const digits = String(phone || '').replace(/\D/g, '');
        return digits ? `https://zalo.me/${digits}` : '#';
    }

    function scrollMessages() {
        messages.scrollTop = messages.scrollHeight;
    }

    function createSessionId() {
        if (window.crypto && crypto.randomUUID) return crypto.randomUUID().replace(/-/g, '');
        return `${Date.now()}${Math.random().toString(16).slice(2)}`;
    }
});
