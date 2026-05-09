# Kế hoạch triển khai: Thanh chọn ngày Premium & Chọn ngày xa (Mode Giờ)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Mục tiêu:** Nâng cấp thanh chọn ngày ngang của chế độ đặt theo giờ lên đẳng cấp "Premium" và hỗ trợ chọn ngày xa qua ô nhập thủ công.

**Kiến trúc:** 
- Quản lý ngày đang chọn của chế độ giờ qua `BookingState.currentHourlyDate`.
- Tách biệt logic vẽ thanh ngang (`renderHourlyDateBar`) để có thể tái sử dụng khi ngày thay đổi.
- Sử dụng CSS nâng cao để tạo hiệu ứng nổi khối và mượt mà cho các Date Chips.

**Công nghệ:** JavaScript (Vanilla), CSS3 (Flexbox/Gradients), HTML5.

---

### Task 1: Cấu trúc HTML & CSS cho thanh chọn ngày Premium

**Files:**
- Modify: `WebHomestay/Views/Rooms/Details.cshtml`
- Modify: `WebHomestay/wwwroot/css/user-premium.css`

- [ ] **Bước 1: Cập nhật cấu trúc trong Details.cshtml**
Tìm khu vực `booking-mode-panel` dành cho `hourly` và bổ sung ô nhập ngày thủ công.

```html
<!-- Trong panel hourly -->
<div class="hourly-date-selection-container mb-4">
    <div class="manual-date-picker-lux p-3 mb-3 d-flex justify-content-between align-items-center">
        <span class="label mb-0">CHỌN NGÀY KHÁC</span>
        <input type="date" id="hourly-manual-date" class="border-0 bg-transparent fw-bold" style="color: var(--luxury-accent); outline: none;">
    </div>
    
    <div class="lux-date-bar-wrapper position-relative">
        <div class="lux-scroll-gradient left"></div>
        <div class="lux-scroll-gradient right"></div>
        <div id="hourly-date-bar" class="lux-date-bar-horizontal d-flex gap-3 overflow-auto">
            <!-- Chips sẽ được render bởi JS -->
        </div>
    </div>
</div>
```

- [ ] **Bước 2: Thêm CSS Premium trong user-premium.css**
Định nghĩa phong cách cho các Date Chips mới.

```css
.lux-date-bar-wrapper { padding: 10px 0; }
.lux-scroll-gradient {
    position: absolute; top: 0; bottom: 0; width: 40px; z-index: 2; pointer-events: none;
}
.lux-scroll-gradient.left { left: 0; background: linear-gradient(to right, #fff, transparent); }
.lux-scroll-gradient.right { right: 0; background: linear-gradient(to left, #fff, transparent); }

.lux-date-bar-horizontal { scrollbar-width: none; padding: 10px 5px; }
.lux-date-bar-horizontal::-webkit-scrollbar { display: none; }

.luxury-date-chip {
    min-width: 75px; height: 95px; border: 1px solid #f1f5f9; border-radius: 18px;
    display: flex; flex-direction: column; align-items: center; justify-content: center;
    transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    background: #fff; cursor: pointer; flex-shrink: 0;
}

.luxury-date-chip.active {
    transform: scale(1.1);
    box-shadow: 0 10px 30px rgba(184, 146, 94, 0.25);
    border-color: rgba(184, 146, 94, 0.4);
    z-index: 3;
}

.luxury-date-chip.active .chip-day-name { color: var(--luxury-accent); font-weight: 700; }
.luxury-date-chip.active .chip-day-num { color: var(--luxury-accent); font-size: 1.4rem; }

.chip-today-label { font-size: 0.6rem; color: #999; margin-top: 2px; }
```

- [ ] **Bước 3: Cam kết thay đổi UI**
```bash
git add WebHomestay/Views/Rooms/Details.cshtml WebHomestay/wwwroot/css/user-premium.css
git commit -m "style: implement premium hourly date bar UI"
```

---

### Task 2: Logic JavaScript - Render & Đồng bộ

**Files:**
- Modify: `WebHomestay/wwwroot/js/room-booking.js`

- [ ] **Bước 1: Bổ sung trạng thái và hàm render thanh ngang**
Cập nhật `BookingState` và thêm hàm vẽ linh hoạt.

```javascript
// Thêm vào BookingState
BookingState.currentHourlyDate = new Date().toISOString().split('T')[0];

function renderHourlyDateBar(startDateStr) {
    const bar = document.getElementById('hourly-date-bar');
    if (!bar) return;
    bar.innerHTML = '';
    
    const baseDate = new Date(startDateStr);
    for (let i = 0; i < 14; i++) {
        const current = new Date(baseDate);
        current.setDate(baseDate.getDate() + i);
        const iso = current.toISOString().split('T')[0];
        const isToday = iso === new Date().toISOString().split('T')[0];
        const isActive = iso === BookingState.currentHourlyDate;
        
        const dayName = current.toLocaleDateString('vi-VN', { weekday: 'short' }).toUpperCase();
        const dayNum = current.getDate();

        const chip = document.createElement('div');
        chip.className = `luxury-date-chip ${isActive ? 'active' : ''}`;
        chip.setAttribute('data-date', iso);
        chip.innerHTML = `
            <div class="chip-day-name label" style="font-size: 0.65rem; margin-bottom: 5px;">${dayName}</div>
            <strong class="chip-day-num" style="font-size: 1.2rem;">${dayNum}</strong>
            ${isToday ? '<div class="chip-today-label">HÔM NAY</div>' : ''}
        `;
        
        chip.addEventListener('click', () => {
            BookingState.currentHourlyDate = iso;
            renderHourlyDateBar(startDateStr);
            // logic tải lại grid giờ ở đây
        });
        
        bar.appendChild(chip);
    }
}
```

- [ ] **Bước 2: Kết nối ô nhập thủ công**
Lắng nghe sự thay đổi của `#hourly-manual-date`.

```javascript
const hourlyManual = document.getElementById('hourly-manual-date');
if (hourlyManual) {
    hourlyManual.addEventListener('change', function() {
        const date = this.value;
        BookingState.currentHourlyDate = date;
        renderHourlyDateBar(date); // Vẽ lại thanh ngang bắt đầu từ ngày này
        // logic tải lại grid giờ
    });
}
```

- [ ] **Bước 3: Cam kết hoàn tất logic**
```bash
git add WebHomestay/wwwroot/js/room-booking.js
git commit -m "feat: implement dynamic hourly date bar logic and sync"
```
