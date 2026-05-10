# Dynamic Pricing Breakdown Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a transparent, per-night price breakdown in the booking UI to handle stays spanning multiple pricing tiers (Regular vs. Weekend/Holiday).

**Architecture:** Expand the booking summary UI on the Room Details page and update the Javascript logic to perform client-side calculation of the price breakdown using pre-loaded room pricing data.

**Tech Stack:** HTML, Javascript (Vanilla), ASP.NET Core (Razor Pages).

---

### Task 1: Update Details Page Layout

**Files:**
- Modify: `WebHomestay/Views/Rooms/Details.cshtml`

- [x] **Step 1: Add breakdown container to daily summary**
Modify the `#daily-summary` div to include placeholders for the breakdown list and total nights.

```html
<!-- Inside #daily-summary -->
<div id="price-breakdown-container" class="mt-4 pt-4 border-t border-dashed border-gray-200 hidden">
    <div class="flex justify-between items-center mb-3">
        <span class="text-xs uppercase font-bold text-gray-500 tracking-wider">Chi tiết giá theo đêm</span>
        <span id="total-nights-count" class="text-sm font-semibold text-gray-700">0 đêm</span>
    </div>
    <div id="price-breakdown-list" class="space-y-2 text-sm">
        <!-- JS will populate this -->
    </div>
    <div class="flex justify-between items-center mt-4 pt-3 border-t-2 border-gray-50">
        <span class="text-xs uppercase font-bold text-gray-800">Tổng cộng dự kiến</span>
        <span id="range-total-price" class="text-xl font-bold text-primary">0đ</span>
    </div>
</div>
```

- [x] **Step 2: Commit changes**

### Task 2: Implement Range Calculation Logic

**Files:**
- Modify: `WebHomestay/wwwroot/js/room-booking.js`

- [x] **Step 1: Add helper to get dates in range**
Add a utility function to calculate the nights between two dates.

```javascript
function getDatesInRange(startDate, endDate) {
    const dates = [];
    let currentDate = new Date(startDate);
    const stopDate = new Date(endDate);
    
    // We only count nights, so we stop BEFORE the end date (check-out day)
    while (currentDate < stopDate) {
        dates.push(new Date(currentDate).toISOString().split('T')[0]);
        currentDate.setDate(currentDate.getDate() + 1);
    }
    return dates;
}
```

- [x] **Step 2: Commit changes**

### Task 3: Update Summary UI with Breakdown

**Files:**
- Modify: `WebHomestay/wwwroot/js/room-booking.js`

- [x] **Step 1: Update updateUI function to render breakdown**
In the `updateUI()` function, when both `checkIn` and `checkOut` are set, calculate the breakdown and update the DOM.

```javascript
// Inside updateUI() for daily mode
if (checkIn && checkOut) {
    const dates = getDatesInRange(checkIn, checkOut);
    const container = document.getElementById('price-breakdown-container');
    const list = document.getElementById('price-breakdown-list');
    const totalCountEl = document.getElementById('total-nights-count');
    const totalValEl = document.getElementById('range-total-price');
    
    if (container && list) {
        container.classList.remove('hidden');
        let total = 0;
        let html = '';
        
        dates.forEach((dateStr, index) => {
            const pricing = window.RoomPricingData ? window.RoomPricingData[dateStr] : null;
            const basePrice = window.BasePrices ? window.BasePrices.day : 0;
            const price = pricing ? (pricing.priceDay || pricing.PriceDay || basePrice) : basePrice;
            
            const dateObj = new Date(dateStr);
            const isWeekend = dateObj.getDay() === 0 || dateObj.getDay() === 6;
            const label = isWeekend ? "Cuối tuần" : "Ngày thường";
            const colorClass = isWeekend ? "text-orange-500 font-medium" : "text-gray-600";
            
            html += `
                <div class="flex justify-between items-center">
                    <span class="${colorClass}">Đêm ${index + 1}: ${new Date(dateStr).toLocaleDateString('vi-VN')} (${label})</span>
                    <span class="font-semibold">${new Number(price).toLocaleString('vi-VN')}đ</span>
                </div>
            `;
            total += price;
        });
        
        list.innerHTML = html;
        totalCountEl.innerText = `${dates.length} đêm`;
        totalValEl.innerText = new Number(total).toLocaleString('vi-VN') + 'đ';
    }
}
```

- [x] **Step 2: Commit changes**

### Task 4: Final Validation and Polish

**Files:**
- Modify: `WebHomestay/wwwroot/js/room-booking.js`

- [x] **Step 1: Ensure breakdown hides on clear**
Update `clearSelection()` or the state where dates are removed to hide the breakdown container.

- [x] **Step 2: Final testing**
Test selecting T6-T7 (1 night), T6-CN (2 nights), and T6-T2 (3 nights) to verify price aggregation and labels.

- [x] **Step 3: Commit**
