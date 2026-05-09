# Kế hoạch triển khai: Bảo toàn trạng thái đặt phòng & Thông báo Toast

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Mục tiêu:** Khắc phục lỗi mất trạng thái ngày đã đặt khi chuyển tháng hoặc chọn tay, đồng thời thay thế Alert hệ thống bằng Toast Message chuyên nghiệp.

**Kiến trúc:** 
- Sử dụng đối tượng `BookingState` tập trung để quản lý `blockedDates` và `selection`.
- Khởi tạo bộ nhớ đệm bằng cách quét dữ liệu từ lưới lịch được Server render khi tải trang.
- Tích hợp logic kiểm tra vào cả Grid và Manual Input, sử dụng Toast để phản hồi người dùng.

**Công nghệ:** JavaScript (Vanilla), CSS3, HTML5.

---

### Task 1: CSS & Giao diện thông báo Toast

**Files:**
- Modify: `WebHomestay/wwwroot/css/user-premium.css`

- [ ] **Bước 1: Thêm style cho Toast Notification**
Thêm các lớp CSS để hiển thị thông báo Toast mượt mà ở góc màn hình.

```css
/* Toast Notification */
.lux-toast-container {
    position: fixed;
    top: 20px;
    right: 20px;
    z-index: 9999;
    display: flex;
    flex-direction: column;
    gap: 10px;
}

.lux-toast {
    background: #ffffff;
    border-left: 4px solid var(--luxury-accent);
    box-shadow: 0 10px 25px rgba(0,0,0,0.1);
    padding: 16px 20px;
    border-radius: 8px;
    display: flex;
    align-items: center;
    gap: 12px;
    animation: toastSlideIn 0.3s ease forwards;
    min-width: 300px;
}

.lux-toast.error {
    border-left-color: #ef4444;
}

.lux-toast-content {
    font-size: 0.9rem;
    color: #333;
}

@keyframes toastSlideIn {
    from { transform: translateX(100%); opacity: 0; }
    to { transform: translateX(0); opacity: 1; }
}

@keyframes toastFadeOut {
    to { transform: translateY(-20px); opacity: 0; }
}

.lux-toast.fade-out {
    animation: toastFadeOut 0.3s ease forwards;
}
```

- [ ] **Bước 2: Cam kết thay đổi**
```bash
git add WebHomestay/wwwroot/css/user-premium.css
git commit -m "style: add toast notification styles"
```

---

### Task 2: Quản lý trạng thái & Hệ thống Toast (JavaScript)

**Files:**
- Modify: `WebHomestay/wwwroot/js/room-booking.js`

- [ ] **Bước 1: Khởi tạo đối tượng State và hàm showToast**
Định nghĩa đối tượng quản lý trạng thái và công cụ hiển thị thông báo.

```javascript
// Thêm vào đầu scope của DOMContentLoaded
const BookingState = {
    blockedDates: [],
    checkIn: null,
    checkOut: null
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
    toast.innerHTML = `
        <div class="lux-toast-content">${message}</div>
    `;

    container.appendChild(toast);

    setTimeout(() => {
        toast.classList.add('fade-out');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

// Hàm quét ngày bị khóa ban đầu
function initBlockedDates() {
    document.querySelectorAll('.lux-cal-day.blocked, .calendar-day-item.day-blocked').forEach(el => {
        const date = el.getAttribute('data-date');
        if (date && !BookingState.blockedDates.includes(date)) {
            BookingState.blockedDates.push(date);
        }
    });
}
```

- [ ] **Bước 2: Thực thi quét dữ liệu khi load**
Gọi `initBlockedDates()` ngay sau khi định nghĩa.

- [ ] **Bước 3: Cam kết thay đổi**
```bash
git add WebHomestay/wwwroot/js/room-booking.js
git commit -m "feat: implement centralized state and toast system"
```

---

### Task 3: Cập nhật Logic xác thực & Vẽ lịch động

**Files:**
- Modify: `WebHomestay/wwwroot/js/room-booking.js`

- [ ] **Bước 1: Cập nhật hàm renderDynamicGrid**
Đảm bảo khi vẽ lịch mới, các ngày trong `BookingState.blockedDates` luôn được khóa.

```javascript
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
        
        // Render UI Blocked hoặc Available
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
```

- [ ] **Bước 2: Cập nhật logic Manual Input với Toast**
Thay thế `alert` bằng `showToast` và thêm kiểm tra ngày nhận đơn lẻ.

```javascript
// Trong listener manualCheckIn
if (BookingState.blockedDates.includes(dateStr)) {
    showToast("Ngày này đã có khách đặt. Vui lòng chọn ngày khác.");
    this.value = '';
    checkIn = null;
    updateUI();
    return;
}
```

- [ ] **Bước 3: Cam kết hoàn tất**
```bash
git add WebHomestay/wwwroot/js/room-booking.js
git commit -m "feat: complete robust validation and persistent grid UI"
```
