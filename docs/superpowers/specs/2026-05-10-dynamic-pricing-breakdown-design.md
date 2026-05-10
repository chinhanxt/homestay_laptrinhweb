# Spec: Dynamic Pricing Breakdown Visualization

Implement a transparent, per-night price breakdown in the booking UI to handle stays spanning multiple pricing tiers (Regular, Weekend, Holiday).

## User Logic Confirmation
- Pricing is calculated per **night** spent.
- Example: Fri (Check-in) to Sun (Check-out) = 2 nights (Fri night + Sat night).
- Total Price = Price(Fri) + Price(Sat).

## Proposed UI Changes

### 1. Room Details Page (`Details.cshtml`)
- Expand the `#daily-summary` panel to include:
    - **Nights Count**: e.g., "Tổng cộng: 2 đêm".
    - **Breakdown List**: A scrollable or compact list of dates and their respective prices.
    - **Live Total**: Updated immediately as the user clicks dates on the calendar.

### 2. Frontend Logic (`room-booking.js`)
- When `checkIn` and `checkOut` are selected:
    - Loop through all dates from `checkIn` to `checkOut - 1 day`.
    - Retrieve `PriceDay` for each date from the `window.RoomPricingData` object.
    - Sum the prices to get the total.
    - Generate HTML for the breakdown list.
    - Update the summary panel visibility and content.

### 3. Checkout Page (`Checkout.cshtml`)
- Ensure the sidebar summary matches the breakdown shown on the details page for consistency.

## Technical Details
- Data Source: `window.RoomPricingData` (already injected).
- Formatting: All prices should be formatted using `toLocaleString('vi-VN') + 'đ'`.
- Fallback: If a date is outside the 30-day pre-loaded range, use `window.BasePrices.day`.

## Success Criteria
- [ ] Selecting Fri-Sun shows 2 nights with Fri & Sat prices listed.
- [ ] Selecting Fri-Sat shows 1 night with Fri price listed.
- [ ] Total matches the sum of the breakdown.
- [ ] UI is clear and explains the price difference (e.g., highlighting weekend nights).
