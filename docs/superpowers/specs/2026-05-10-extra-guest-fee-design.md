# Design Spec: Extra Guest Fee Feature

This document outlines the implementation of an "Extra Guest" surcharge feature in the homestay booking system.

## 1. Requirements
- Add a checkbox to the booking checkout form to allow extra guests beyond standard capacity.
- The surcharge is calculated per person for each guest exceeding the room's `Capacity` up to `MaxGuests`.
- The total price must update in real-time on the UI when the guest count or checkbox changes.
- The server must validate and calculate the final price including the surcharge during booking creation.

## 2. Architecture & Data Flow

### 2.1. Data Model Changes
No database changes are required as the `Room` model already contains:
- `Capacity` (Standard capacity)
- `MaxGuests` (Maximum allowed guests)
- `ExtraGuestFee` (Fee per extra person)

### 2.2. View Model Updates
- **`BookingCheckoutViewModel`**: Needs to expose `Capacity`, `MaxGuests`, and `ExtraGuestFee` to the view for client-side calculations.

### 2.3. UI Components (`Checkout.cshtml`)
- **Checkbox**: `[ ] Tôi muốn thêm người ở (có phụ phí)`
- **GuestCount Input**: 
    - Default `max` = `Room.Capacity`.
    - If checkbox is checked, `max` = `Room.MaxGuests`.
- **Javascript Logic**:
    - Listen for changes on the checkbox and GuestCount input.
    - Toggle the `max` attribute and adjust value if it exceeds current limit.
    - Calculate `ExtraCharge = Math.max(0, guestCount - Capacity) * ExtraGuestFee`.
    - Update the "Tổng cộng" display in the sidebar.

### 2.4. Server-Side Logic (`BookingCreationService.cs`)
- Update `CreateHourlyBookingAsync` and `CreateDailyBookingAsync` to:
    - Retrieve `Room` details.
    - Calculate surcharge: `(request.GuestCount - room.Capacity) * room.ExtraGuestFee` (if `GuestCount > Capacity`).
    - Add surcharge to the base price before saving the `Booking`.

## 3. Implementation Steps

1.  **Model Enhancement**: Add necessary properties to `BookingCheckoutViewModel`.
2.  **UI Implementation**: 
    - Modify `Checkout.cshtml` to add the checkbox and surcharge display row.
    - Add Javascript for real-time calculations.
3.  **Service Update**: Modify `RoomBookingViewService` to populate the new ViewModel properties.
4.  **Backend Logic**: Update `BookingCreationService` to handle the surcharge calculation.

## 4. Test Plan
- **UI Test**: Verify that the GuestCount limit changes when the checkbox is toggled.
- **Price Test**: Verify that the total price updates correctly in the sidebar for both Hourly and Daily modes.
- **Backend Test**: Verify that the saved `Booking` record has the correct `TotalPrice` including the surcharge.
- **Edge Case**: Ensure 0 surcharge if `GuestCount <= Capacity`.
- **Edge Case**: Ensure the surcharge is applied correctly for both hourly and daily bookings.
