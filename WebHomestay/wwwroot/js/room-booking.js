// WebHomestay/wwwroot/js/room-booking.js
document.addEventListener('DOMContentLoaded', function () {
    const modeButtons = document.querySelectorAll('.mode-tab-lux, .mode-btn');
    const panels = document.querySelectorAll('.booking-mode-panel');

    // Global switcher function
    window.switchBookingMode = function(mode) {
        // Update buttons
        modeButtons.forEach(b => {
            if (b.getAttribute('data-mode') === mode) {
                b.classList.add('active');
            } else {
                b.classList.remove('active');
            }
        });

        // Update panels
        panels.forEach(p => {
            if (p.getAttribute('data-panel') === mode) {
                p.classList.remove('d-none');
            } else {
                p.classList.add('d-none');
            }
        });
    };

    // Attach listeners as backup
    modeButtons.forEach(btn => {
        btn.addEventListener('click', function () {
            const mode = this.getAttribute('data-mode');
            switchBookingMode(mode);
        });
    });

    // Daily Calendar Logic
    let checkIn = null;
    let checkOut = null;
    const calendarDays = document.querySelectorAll('.lux-cal-day, .calendar-day-item');
    const checkInInput = document.getElementById('checkin-input');
    const checkOutInput = document.getElementById('checkout-input');
    const rangeLabel = document.getElementById('range-label');
    const dailySummary = document.getElementById('daily-summary');
    const displayCheckin = document.getElementById('display-checkin');
    const displayCheckout = document.getElementById('display-checkout');
    const submitBtn = document.getElementById('daily-submit-btn');
    const clearBtn = document.getElementById('clear-selection-btn');

    calendarDays.forEach(day => {
        day.addEventListener('click', function() {
            if (this.classList.contains('blocked') || this.classList.contains('day-blocked')) return;

            const dateStr = this.getAttribute('data-date');

            if (dateStr === checkIn || dateStr === checkOut) {
                resetSelection();
                return;
            }

            if (!checkIn) {
                checkIn = dateStr;
                if (rangeLabel) rangeLabel.innerText = "Chọn ngày trả";
                if (clearBtn) clearBtn.classList.remove('d-none');
            } else if (!checkOut) {
                if (dateStr < checkIn) {
                    checkIn = dateStr;
                } else {
                    if (isRangeBlocked(checkIn, dateStr)) {
                        alert("Khoảng ngày này có ngày đã hết phòng.");
                        return;
                    }
                    checkOut = dateStr;
                    if (rangeLabel) rangeLabel.innerText = "Đã chọn dải ngày";
                }
            } else {
                if (dateStr > checkOut) {
                    if (isRangeBlocked(checkOut, dateStr)) {
                        alert("Không thể mở rộng vì vướng ngày đã hết phòng.");
                        return;
                    }
                    checkOut = dateStr;
                } else if (dateStr < checkIn) {
                    if (isRangeBlocked(dateStr, checkIn)) {
                        alert("Không thể mở rộng vì vướng ngày đã hết phòng.");
                        return;
                    }
                    checkIn = dateStr;
                } else {
                    checkIn = dateStr;
                    checkOut = null;
                    if (rangeLabel) rangeLabel.innerText = "Chọn ngày trả";
                }
            }

            updateUI();
        });
    });

    if (clearBtn) {
        clearBtn.addEventListener('click', resetSelection);
    }

    function resetSelection() {
        checkIn = null;
        checkOut = null;
        if (rangeLabel) rangeLabel.innerText = "Chọn ngày nhận";
        if (clearBtn) clearBtn.classList.add('d-none');
        updateUI();
    }

    function updateUI() {
        calendarDays.forEach(day => {
            const date = day.getAttribute('data-date');
            day.classList.remove('selected', 'in-range');

            if (date === checkIn || date === checkOut) {
                day.classList.add('selected');
            } else if (checkIn && checkOut && date > checkIn && date < checkOut) {
                day.classList.add('in-range');
            }
        });

        if (checkInInput) checkInInput.value = checkIn || '';
        if (checkOutInput) checkOutInput.value = checkOut || '';
        if (displayCheckin) displayCheckin.innerText = checkIn ? formatDate(checkIn) : '-';
        if (displayCheckout) displayCheckout.innerText = checkOut ? formatDate(checkOut) : '-';

        if (checkIn && checkOut) {
            if (dailySummary) dailySummary.classList.remove('d-none');
            if (submitBtn) submitBtn.disabled = false;
        } else {
            if (dailySummary) dailySummary.classList.add('d-none');
            if (submitBtn) submitBtn.disabled = true;
        }
    }

    function isRangeBlocked(start, end) {
        let blocked = false;
        calendarDays.forEach(day => {
            const date = day.getAttribute('data-date');
            if (date >= start && date <= end && (day.classList.contains('blocked') || day.classList.contains('day-blocked'))) {
                blocked = true;
            }
        });
        return blocked;
    }

    function formatDate(isoStr) {
        const parts = isoStr.split('-');
        return `${parts[2]}/${parts[1]}/${parts[0]}`;
    }
});
