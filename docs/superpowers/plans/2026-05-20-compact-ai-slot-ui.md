# Compact AI Slot UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the public AI chatbot time-slot UI easier to scan by grouping hourly slots by room, compacting daily room cards, and localizing visible AM/PM picker labels where the browser exposes them.

**Architecture:** Keep the change inside the existing public chatbot frontend. `site.js` remains responsible for rendering AI UI blocks and submitting booking actions; `user-premium.css` remains responsible for card/chip styling. No backend data shape changes are needed because `hourlySlots` already includes `roomName`, `roomId`, `slotId`, `label`, and `totalPrice`.

**Tech Stack:** ASP.NET Core MVC static assets, vanilla JavaScript, CSS, Bootstrap-compatible markup.

---

## File Structure

- Modify `WebHomestay/wwwroot/js/site.js`
  - Add small DOM helper functions for grouping slots by room, formatting slot labels, and localizing AM/PM text in the chatbot context date controls.
  - Replace `renderHourlySlots` output from a flat chip grid to room-grouped slot blocks.
  - Compact `renderDailyRooms` markup.
- Modify `WebHomestay/wwwroot/css/user-premium.css`
  - Add compact styles for grouped slot cards and daily room rows.
  - Keep existing class names working; only add targeted classes used by the new JS.

---

### Task 1: Localize visible AM/PM labels in chatbot time controls

**Files:**
- Modify: `WebHomestay/wwwroot/js/site.js`

- [ ] **Step 1: Add a localized input refresh helper near `updateContextDateTimeControls`**

Add this function after `updateContextDateTimeControls()`:

```javascript
    function refreshLocalizedTimeControls() {
        if (!checkInInput || !checkOutInput || getBookingMode() !== 'hourly') return;
        [checkInInput, checkOutInput].forEach(input => {
            input.setAttribute('lang', 'vi-VN');
            input.setAttribute('aria-label', input === checkInInput ? 'Giờ nhận' : 'Giờ trả');
        });
    }
```

- [ ] **Step 2: Call the helper when time mode changes**

Change the end of `updateContextDateTimeControls()` to call the helper after type/value adjustments:

```javascript
        if (checkInInput.value && checkInInput.value.length > 10) checkInInput.value = checkInInput.value.slice(0, 10);
        if (checkOutInput.value && checkOutInput.value.length > 10) checkOutInput.value = checkOutInput.value.slice(0, 10);
        refreshLocalizedTimeControls();
```

Then add this call before the `return` inside the hourly branch:

```javascript
            refreshLocalizedTimeControls();
            return;
```

- [ ] **Step 3: Verify manually**

Run the app, open public chatbot, switch to “Theo giờ”, open the time picker. Expected: the input is marked Vietnamese (`lang="vi-VN"`). Chromium’s native picker may still render browser-controlled labels, but the app-side labels must be “Giờ nhận/Giờ trả”; if native AM/PM labels cannot be overridden by browser APIs, do not add a custom date-time picker.

---

### Task 2: Render hourly slots grouped by room

**Files:**
- Modify: `WebHomestay/wwwroot/js/site.js`

- [ ] **Step 1: Add grouping helpers before `renderHourlySlots`**

Insert these functions above `function renderHourlySlots(data)`:

```javascript
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
```

- [ ] **Step 2: Replace `renderHourlySlots` body with grouped markup**

Use this implementation:

```javascript
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

        const groupedSlots = groupSlotsByRoom(slots);
        groupedSlots.forEach(group => {
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
```

- [ ] **Step 3: Manual check**

Open chatbot, choose hourly booking, pick a branch/date with many slots. Expected: slots appear grouped by room name, each room has compact chips like `00:00-02:00 · 150.000đ`, and clicking a chip still opens the summary + booking form.

---

### Task 3: Compact daily room cards

**Files:**
- Modify: `WebHomestay/wwwroot/js/site.js`

- [ ] **Step 1: Replace `renderDailyRooms` item markup**

Inside `rooms.forEach`, use this structure:

```javascript
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
```

Keep the existing details/select button logic after this `actions` block, then append actions and wrapper as before.

- [ ] **Step 2: Manual check**

Open chatbot, choose daily booking, pick date range. Expected: each daily room card is one compact block with room name and price visible first, then “Xem chi tiết” and “Chọn phòng này”.

---

### Task 4: Add compact CSS styles

**Files:**
- Modify: `WebHomestay/wwwroot/css/user-premium.css`

- [ ] **Step 1: Add styles after existing `.ai-slot-grid` rule**

Add:

```css
.ai-slot-room-group {
    border: 1px solid rgba(203, 213, 225, 0.9);
    background: #fff;
    padding: 10px;
    margin-bottom: 8px;
}

.ai-slot-room-header {
    font-size: 0.86rem;
    font-weight: 800;
    color: var(--luxury-primary);
    margin-bottom: 8px;
    line-height: 1.25;
}

.ai-slot-chip-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(104px, 1fr));
    gap: 6px;
}

.ai-slot-chip {
    border: 1px solid rgba(203, 213, 225, 0.9);
    background: #f8fafc;
    color: var(--luxury-text);
    padding: 7px 8px;
    font-size: 0.78rem;
    font-weight: 800;
    line-height: 1.25;
    text-align: center;
}

.ai-slot-chip:hover {
    border-color: var(--luxury-accent);
    color: var(--luxury-accent);
    background: #fff;
}
```

- [ ] **Step 2: Add compact daily styles after `.ai-room-actions`**

Add:

```css
.ai-daily-room-compact {
    display: grid;
    gap: 8px;
    padding: 10px 12px;
}

.ai-daily-room-main {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
}

.ai-daily-room-main strong {
    font-size: 0.92rem;
    color: var(--luxury-primary);
}

.ai-daily-room-main span {
    flex-shrink: 0;
    font-weight: 900;
    color: var(--luxury-accent);
    font-size: 0.88rem;
}

.ai-room-actions-compact {
    margin-top: 0;
}
```

- [ ] **Step 3: Manual responsive check**

Resize browser to mobile width. Expected: chips wrap without horizontal scroll, daily cards fit inside the chatbot panel, and action buttons remain tappable.

---

### Task 5: Final validation

**Files:**
- Validate: `WebHomestay/wwwroot/js/site.js`
- Validate: `WebHomestay/wwwroot/css/user-premium.css`

- [ ] **Step 1: Check JavaScript syntax**

Run:

```bash
node --check WebHomestay/wwwroot/js/site.js
```

Expected: command exits with code 0 and no output.

- [ ] **Step 2: Build the app without touching locked running DLLs**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj -p:OutputPath=../artifacts/build-check/ -p:UseAppHost=false
```

Expected: `Build succeeded.` Existing warnings about Npgsql and nullable references may remain.

- [ ] **Step 3: Manual browser validation**

Start/restart the app, open public page, open chatbot:

1. Switch to “Theo giờ”; labels show “Giờ nhận/Giờ trả”.
2. Select branch/date/time; slots render grouped by room.
3. Click a slot; summary and Booking Form Designer form still appear.
4. Submit booking with CCCD images; payment block links to `/Bookings/Success/{id}`.
5. Open admin booking details; CCCD images appear for the newly created booking.

---

## Self-Review

- Spec coverage: all approved requirements are covered by Tasks 1-5.
- Placeholder scan: no TODO/TBD placeholders remain.
- Type consistency: helper names `groupSlotsByRoom`, `formatSlotChipLabel`, and `refreshLocalizedTimeControls` are defined before use; data properties match existing AI slot payload names.
