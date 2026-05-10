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
    const BookingState = {
        blockedDates: [],
        checkIn: null,
        checkOut: null,
        currentHourlyDate: document.getElementById('hourly-manual-date')?.value || new Date().toISOString().split('T')[0]
    };

    function showToast(message, type = 'error') {
        let container = document.querySelector('.lux-toast-container');
        if (!container) {
            container = document.createElement('div');
            container.className = 'lux-toast-container';
            document.body.appendChild(container);
        }

        const toast = document.createElement('div');
        toast.className = `lux-toast ${type}`;
        toast.innerHTML = `<div class="lux-toast-content">${message}</div>`;

        container.appendChild(toast);

        // Auto remove
        setTimeout(() => {
            toast.classList.add('fade-out');
            setTimeout(() => toast.remove(), 300);
        }, 3000);
    }

    function initBlockedDates() {
        document.querySelectorAll('.lux-cal-day.blocked, .calendar-day-item.day-blocked').forEach(el => {
            const date = el.getAttribute('data-date');
            if (date && !BookingState.blockedDates.includes(date)) {
                BookingState.blockedDates.push(date);
            }
        });
    }
    initBlockedDates();

    let calendarDays = document.querySelectorAll('.lux-cal-day, .calendar-day-item');
    const checkInInput = document.getElementById('checkin-input');
    const checkOutInput = document.getElementById('checkout-input');
    const rangeLabel = document.getElementById('range-label');
    const dailySummary = document.getElementById('daily-summary');
    const displayCheckin = document.getElementById('display-checkin');
    const displayCheckout = document.getElementById('display-checkout');
    const submitBtn = document.getElementById('daily-submit-btn');
    const clearBtn = document.getElementById('clear-selection-btn');
    const hourlyPriceDisplay = document.getElementById('hourly-price-display');
    const dailyPriceDisplay = document.getElementById('daily-price-display');

    const manualCheckIn = document.getElementById('manual-checkin');
    const manualCheckOut = document.getElementById('manual-checkout');

    if (manualCheckIn) {
        manualCheckIn.addEventListener('change', function() {
            const dateStr = this.value;
            
            if (BookingState.blockedDates.includes(dateStr)) {
                showToast("Ngày này đã có khách đặt. Vui lòng chọn ngày khác.");
                this.value = '';
                BookingState.checkIn = null;
                updateUI();
                return;
            }

            if (BookingState.checkOut && dateStr > BookingState.checkOut) {
                BookingState.checkOut = null; // Reset checkout if in is after out
                if (manualCheckOut) manualCheckOut.value = '';
            }
            
            BookingState.checkIn = dateStr;
            updateUI();
        });
    }

    if (manualCheckOut) {
        manualCheckOut.addEventListener('change', function() {
            const dateStr = this.value;
            if (dateStr <= BookingState.checkIn) {
                showToast("Ngày trả phải sau ngày nhận ít nhất 1 ngày.");
                this.value = BookingState.checkOut || '';
                return;
            }

            if (isRangeBlocked(BookingState.checkIn, dateStr)) {
                showToast("Dải ngày bạn chọn có vướng ngày đã hết phòng.");
                this.value = '';
                BookingState.checkOut = null;
                updateUI();
                return;
            }

            BookingState.checkOut = dateStr;
            updateUI();
        });
    }

    // --- Hourly Selection Logic ---
    const hourlyManual = document.getElementById('hourly-manual-date');
    const hourlyBar = document.getElementById('hourly-date-bar');

    if (hourlyManual) {
        hourlyManual.addEventListener('change', function() {
            const date = this.value;
            BookingState.currentHourlyDate = date;
            renderHourlyDateBar(date);
            loadHourlySlots(date);
            updatePriceLabelsByDate(date, 'hourly');
        });
    }

    function initHourlyChips() {
        const chips = document.querySelectorAll('.luxury-date-chip');
        chips.forEach(chip => {
            chip.addEventListener('click', function() {
                const date = this.getAttribute('data-date');
                BookingState.currentHourlyDate = date;
                
                // Update active state immediately for UI response
                chips.forEach(c => c.classList.remove('active'));
                this.classList.add('active');

                scrollToChip(this, true);
                loadHourlySlots(date);
                updatePriceLabels(this, 'hourly');
            });
        });
        
        const activeChip = document.querySelector('.luxury-date-chip.active');
        if (activeChip) {
            scrollToChip(activeChip, false);
        }
    }
    initHourlyChips();

    function loadHourlySlots(date) {
        const container = document.getElementById('hourly-slots-container');
        const roomId = document.querySelector('.booking-space')?.getAttribute('data-room-id');
        if (!container || !roomId) return;

        // Visual feedback
        container.style.opacity = '0.4';
        container.style.pointerEvents = 'none';

        fetch(`/Rooms/GetHourlySlots/${roomId}?hourlyDate=${date}`)
            .then(res => res.text())
            .then(html => {
                container.innerHTML = html;
                container.style.opacity = '1';
                container.style.pointerEvents = 'auto';
                
                // Re-run any reveal animations if necessary
                if (window.ScrollReveal) {
                    window.ScrollReveal().reveal('.reveal-up', { distance: '30px', duration: 800 });
                }
            })
            .catch(err => {
                console.error("Error loading hourly slots:", err);
                container.style.opacity = '1';
                container.style.pointerEvents = 'auto';
            });
    }

    function scrollToChip(chip, smooth = true) {
        if (!hourlyBar || !chip) return;
        
        const barWidth = hourlyBar.offsetWidth;
        const chipLeft = chip.offsetLeft;
        const chipWidth = chip.offsetWidth;
        
        // Calculate target to put chip at roughly 1/3 of the bar
        const targetScroll = chipLeft - (barWidth / 4);
        
        hourlyBar.scrollTo({
            left: targetScroll,
            behavior: smooth ? 'smooth' : 'auto'
        });
    }

    function renderHourlyDateBar(startDateStr) {
        if (!hourlyBar) return;
        hourlyBar.innerHTML = '';
        
        const baseDate = new Date(startDateStr);
        const todayStr = new Date().toISOString().split('T')[0];

        for (let i = 0; i < 14; i++) {
            const current = new Date(baseDate);
            current.setDate(baseDate.getDate() + i);
            
            const y = current.getFullYear();
            const m = String(current.getMonth() + 1).padStart(2, '0');
            const d = String(current.getDate()).padStart(2, '0');
            const dateIso = `${y}-${m}-${d}`;
            
            const dayName = current.toLocaleDateString('vi-VN', { weekday: 'short' }).toUpperCase();
            const dayNum = current.getDate();
            const isActive = dateIso === BookingState.currentHourlyDate;

            const chip = document.createElement('div');
            chip.className = `luxury-date-chip ${isActive ? 'active' : ''}`;
            chip.setAttribute('data-date', dateIso);
            chip.innerHTML = `
                <div class="chip-day-name label" style="font-size: 0.65rem; margin-bottom: 5px;">${dayName}</div>
                <strong class="chip-day-num" style="font-size: 1.2rem;">${dayNum}</strong>
                ${dateIso === todayStr ? '<div class="chip-today-label">HÔM NAY</div>' : ''}
            `;
            
            chip.addEventListener('click', function() {
                BookingState.currentHourlyDate = dateIso;
                // Update UI state
                document.querySelectorAll('.luxury-date-chip').forEach(c => c.classList.remove('active'));
                this.classList.add('active');
                
                scrollToChip(this, true);
                loadHourlySlots(dateIso);
                updatePriceLabelsByDate(dateIso, 'hourly');
            });
            
            hourlyBar.appendChild(chip);
        }

        // After rendering, ensure the active one is focused
        const active = hourlyBar.querySelector('.luxury-date-chip.active');
        if (active) {
            setTimeout(() => scrollToChip(active, true), 100);
        }
    }

    calendarDays.forEach(day => {
        day.addEventListener('click', function() {
            if (this.classList.contains('blocked') || this.classList.contains('day-blocked')) return;
            handleDayClick(this.getAttribute('data-date'));
        });
    });

    if (clearBtn) {
        clearBtn.addEventListener('click', resetSelection);
    }

    function resetSelection() {
        BookingState.checkIn = null;
        BookingState.checkOut = null;
        if (rangeLabel) rangeLabel.innerText = "Chọn ngày nhận";
        if (clearBtn) clearBtn.classList.add('d-none');
        updateUI();
    }

    function updateUI() {
        if (BookingState.checkIn) {
            const currentItems = document.querySelectorAll('.lux-cal-day');
            const firstDateInGrid = currentItems[0]?.getAttribute('data-date');
            const lastDateInGrid = currentItems[currentItems.length - 1]?.getAttribute('data-date');
            
            if (firstDateInGrid && lastDateInGrid && (BookingState.checkIn < firstDateInGrid || BookingState.checkIn > lastDateInGrid)) {
                renderDynamicGrid(BookingState.checkIn);
                return; 
            }
        }

        const currentDays = document.querySelectorAll('.lux-cal-day, .calendar-day-item');
        currentDays.forEach(day => {
            const date = day.getAttribute('data-date');
            day.classList.remove('selected', 'in-range');

            if (date === BookingState.checkIn || date === BookingState.checkOut) {
                day.classList.add('selected');
            } else if (BookingState.checkIn && BookingState.checkOut && date > BookingState.checkIn && date < BookingState.checkOut) {
                day.classList.add('in-range');
            }
        });

        // Update Date Chips (Horizontal Bar)
        const chips = document.querySelectorAll('.lux-date-bar-horizontal .luxury-date-chip');
        chips.forEach(chip => {
            const date = chip.getAttribute('data-date');
            chip.classList.remove('active', 'in-range');

            if (date === BookingState.checkIn || date === BookingState.checkOut) {
                chip.classList.add('active');
            } else if (BookingState.checkIn && BookingState.checkOut && date > BookingState.checkIn && date < BookingState.checkOut) {
                chip.classList.add('in-range');
            }
        });

        if (checkInInput) checkInInput.value = BookingState.checkIn || '';
        if (checkOutInput) checkOutInput.value = BookingState.checkOut || '';
        if (manualCheckIn) manualCheckIn.value = BookingState.checkIn || '';
        if (manualCheckOut) manualCheckOut.value = BookingState.checkOut || '';
        
        if (displayCheckin) displayCheckin.innerText = BookingState.checkIn ? formatDate(BookingState.checkIn) : '-';
        if (displayCheckout) displayCheckout.innerText = BookingState.checkOut ? formatDate(BookingState.checkOut) : '-';

        if (BookingState.checkIn && BookingState.checkOut && BookingState.checkIn !== BookingState.checkOut) {
            if (dailySummary) {
                dailySummary.classList.remove('d-none');
                dailySummary.style.display = 'block';

                // Render Price Breakdown
                const dates = getDatesInRange(BookingState.checkIn, BookingState.checkOut);
                const container = document.getElementById('price-breakdown-container');
                const list = document.getElementById('price-breakdown-list');
                const totalCountEl = document.getElementById('total-nights-count');
                const totalValEl = document.getElementById('range-total-price');

                if (container && list) {
                    container.classList.remove('d-none');
                    let total = 0;
                    let html = '';

                    dates.forEach((dateStr, index) => {
                        const pricing = window.RoomPricingData ? window.RoomPricingData[dateStr] : null;
                        const basePrice = window.BasePrices ? window.BasePrices.day : 0;
                        const price = pricing ? (pricing.priceDay || pricing.PriceDay || basePrice) : basePrice;

                        const dateObj = parseDate(dateStr);
                        const isWeekend = dateObj.getDay() === 0 || dateObj.getDay() === 6;
                        const label = isWeekend ? "Cuối tuần" : "Ngày thường";
                        const colorStyle = isWeekend ? "color: #e29b61; font-weight: 500;" : "color: #6c757d;";

                        html += `
                            <div class="d-flex justify-content-between align-items-center mb-1">
                                <span style="${colorStyle}">Đêm ${index + 1}: ${dateStr.split('-').reverse().join('/')} (${label})</span>
                                <span class="fw-bold">${new Number(price).toLocaleString('vi-VN')}đ</span>
                            </div>
                        `;
                        total += price;
                    });

                    list.innerHTML = html;
                    if (totalCountEl) totalCountEl.innerText = `${dates.length} đêm`;
                    if (totalValEl) totalValEl.innerText = new Number(total).toLocaleString('vi-VN') + 'đ';
                }
            }
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.classList.remove('opacity-50');
            }
            if (rangeLabel) rangeLabel.innerText = "Đã chọn dải ngày";
        } else {
            if (dailySummary) {
                dailySummary.classList.add('d-none');
                dailySummary.style.display = 'none';
                
                const container = document.getElementById('price-breakdown-container');
                if (container) container.classList.add('d-none');
            }
            if (submitBtn) {
                submitBtn.disabled = true;
            }
        }
    }

    function renderDynamicGrid(baseDateStr) {
        const gridContainer = document.querySelector('.luxury-calendar-grid');
        if (!gridContainer) return;

        gridContainer.innerHTML = '';
        const baseDate = new Date(baseDateStr);
        
        for (let i = 0; i < 35; i++) {
            const current = new Date(baseDate);
            current.setDate(baseDate.getDate() + i);
            
            const y = current.getFullYear();
            const m = String(current.getMonth() + 1).padStart(2, '0');
            const d = String(current.getDate()).padStart(2, '0');
            const dateIso = `${y}-${m}-${d}`;
            
            const isBlocked = BookingState.blockedDates.includes(dateIso);
            const dayName = current.toLocaleDateString('vi-VN', { weekday: 'short' });
            const dayNum = current.getDate();

            const dayEl = document.createElement('div');
            dayEl.className = `lux-cal-day ${isBlocked ? 'blocked' : 'available'}`;
            dayEl.setAttribute('data-date', dateIso);
            
            if (dateIso === BookingState.checkIn || dateIso === BookingState.checkOut) {
                dayEl.classList.add('selected');
            } else if (BookingState.checkIn && BookingState.checkOut && dateIso > BookingState.checkIn && dateIso < BookingState.checkOut) {
                dayEl.classList.add('in-range');
            }

            if (isBlocked) {
                dayEl.innerHTML = `
                    <span class="booked-label">Đã đặt</span>
                    <div class="day-num fw-bold" style="opacity: 0.5; margin-top: -12px;">${dayNum}</div>
                `;
            } else {
                dayEl.innerHTML = `
                    <div class="opacity-50" style="font-size: 0.6rem;">${dayName}</div>
                    <div class="fw-bold">${dayNum}</div>
                `;
                dayEl.addEventListener('click', () => handleDayClick(dateIso));
            }
            
            gridContainer.appendChild(dayEl);
        }
        
        calendarDays = document.querySelectorAll('.lux-cal-day, .calendar-day-item');
        updateUI();
    }

    // Refactored click handler for re-use
    function handleDayClick(dateStr) {
        if (BookingState.blockedDates.includes(dateStr)) return;

        if (!BookingState.checkIn) {
            BookingState.checkIn = dateStr;
            if (rangeLabel) rangeLabel.innerText = "Chọn ngày trả";
            if (clearBtn) clearBtn.classList.remove('d-none');
        } else if (!BookingState.checkOut) {
            if (dateStr < BookingState.checkIn) {
                BookingState.checkIn = dateStr;
            } else {
                if (isRangeBlocked(BookingState.checkIn, dateStr)) {
                    showToast("Khoảng ngày này có ngày đã hết phòng.");
                    return;
                }
                BookingState.checkOut = dateStr;
                if (rangeLabel) rangeLabel.innerText = "Đã chọn dải ngày";
            }
        } else {
            if (dateStr > BookingState.checkOut) {
                if (isRangeBlocked(BookingState.checkOut, dateStr)) {
                    showToast("Không thể mở rộng vì vướng ngày đã hết phòng.");
                    return;
                }
                BookingState.checkOut = dateStr;
            } else if (dateStr < BookingState.checkIn) {
                if (isRangeBlocked(dateStr, BookingState.checkIn)) {
                    showToast("Không thể mở rộng vì vướng ngày đã hết phòng.");
                    return;
                }
                BookingState.checkIn = dateStr;
            } else {
                BookingState.checkIn = dateStr;
                BookingState.checkOut = null;
                if (rangeLabel) rangeLabel.innerText = "Chọn ngày trả";
            }
        }
        updateUI();
        updatePriceLabels(document.querySelector(`.lux-cal-day[data-date="${dateStr}"]`), 'daily');
    }

    function isRangeBlocked(start, end) {
        // Use BookingState instead of DOM
        return BookingState.blockedDates.some(date => date >= start && date <= end);
    }

    function getDatesInRange(startDate, endDate) {
        const dates = [];
        let currentDate = parseDate(startDate);
        const stopDate = parseDate(endDate);
        
        while (currentDate < stopDate) {
            const y = currentDate.getFullYear();
            const m = String(currentDate.getMonth() + 1).padStart(2, '0');
            const d = String(currentDate.getDate()).padStart(2, '0');
            dates.push(`${y}-${m}-${d}`);
            currentDate.setDate(currentDate.getDate() + 1);
        }
        return dates;
    }

    function parseDate(str) {
        if (!str) return new Date();
        const parts = str.split('-');
        return new Date(parts[0], parts[1] - 1, parts[2]);
    }

    function formatDate(isoStr) {
        const parts = isoStr.split('-');
        return `${parts[2]}/${parts[1]}/${parts[0]}`;
    }

    function updatePriceLabelsByDate(dateStr, mode) {
        const pricing = window.RoomPricingData ? window.RoomPricingData[dateStr] : null;
        const basePrices = window.BasePrices || { day: 0, hour: 0 };
        
        // Support both camelCase (priceHour) and PascalCase (PriceHour)
        let priceHour = basePrices.hour;
        let priceDay = basePrices.day;

        if (pricing) {
            priceHour = pricing.priceHour !== undefined ? pricing.priceHour : (pricing.PriceHour !== undefined ? pricing.PriceHour : basePrices.hour);
            priceDay = pricing.priceDay !== undefined ? pricing.priceDay : (pricing.PriceDay !== undefined ? pricing.PriceDay : basePrices.day);
        }
        
        const noteEl = mode === 'hourly' ? document.getElementById('hourly-price-note') : document.getElementById('daily-price-note');
        const displayEl = mode === 'hourly' ? document.getElementById('hourly-price-display') : document.getElementById('daily-price-display');
        
        if (displayEl) {
            const finalPrice = mode === 'hourly' ? priceHour : priceDay;
            displayEl.innerText = new Number(finalPrice).toLocaleString('vi-VN') + (mode === 'hourly' ? 'đ/h' : 'đ');
        }

        if (noteEl) {
            const date = new Date(dateStr);
            const isWeekend = date.getDay() === 0 || date.getDay() === 6;
            const currentPrice = mode === 'hourly' ? priceHour : priceDay;
            const basePrice = mode === 'hourly' ? basePrices.hour : basePrices.day;
            
            if (currentPrice > basePrice) {
                noteEl.innerText = isWeekend ? "Giá cuối tuần" : "Giá ngày lễ";
                noteEl.style.color = "#e29b61";
                noteEl.style.fontWeight = "bold";
            } else {
                noteEl.innerText = "Giá ngày thường";
                noteEl.style.color = "";
                noteEl.style.fontWeight = "normal";
            }
        }
    }

    function updatePriceLabels(element, mode) {
        if (!element) return;
        const dateStr = element.getAttribute('data-date');
        if (dateStr) updatePriceLabelsByDate(dateStr, mode);
    }

    // Global scroll function for hourly bar
    window.scrollHourlyBar = function(offset) {
        const bar = document.getElementById('hourly-date-bar');
        if (bar) {
            bar.scrollBy({
                left: offset,
                behavior: 'smooth'
            });
        }
    };
});
