# Extra Guest Fee Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a surcharge for guests exceeding standard capacity in the booking form.

**Architecture:** Update the view model to include fee details, modify the checkout UI for real-time calculation, and update the creation service to apply the fee server-side.

**Tech Stack:** ASP.NET Core MVC, C#, JavaScript, Vanilla CSS.

---

### Task 1: ViewModel and Service Population

**Files:**
- Modify: `WebHomestay/Models/ViewModels/BookingCheckoutViewModel.cs`
- Modify: `WebHomestay/Services/RoomBookingViewService.cs`

- [ ] **Step 1: Update BookingCheckoutViewModel**
Add `Capacity`, `MaxGuests`, and `ExtraGuestFee` to the view model.
```csharp
public class BookingCheckoutViewModel
{
    // ... existing properties ...
    public int Capacity { get; set; }
    public int MaxGuests { get; set; }
    public decimal ExtraGuestFee { get; set; }
}
```

- [ ] **Step 2: Update RoomBookingViewService**
Populate these values in `BuildHourlyCheckoutAsync` and `BuildDailyCheckoutAsync`.
```csharp
// In BuildHourlyCheckoutAsync
return new BookingCheckoutViewModel {
    // ...
    Capacity = slot.Room.Capacity,
    MaxGuests = slot.Room.MaxGuests,
    ExtraGuestFee = slot.Room.ExtraGuestFee
};

// In BuildDailyCheckoutAsync
return new BookingCheckoutViewModel {
    // ...
    Capacity = room.Capacity,
    MaxGuests = room.MaxGuests,
    ExtraGuestFee = room.ExtraGuestFee
};
```

- [ ] **Step 3: Commit**
`git add . && git commit -m "feat: add fee metadata to checkout view model"`

---

### Task 2: UI Implementation (Checkout.cshtml)

**Files:**
- Modify: `WebHomestay/Views/Bookings/Checkout.cshtml`

- [ ] **Step 1: Add the checkbox and surcharge display**
Add the checkbox below the `GuestCount` input and a row for the surcharge in the sidebar.

- [ ] **Step 2: Implement Javascript logic**
Write a script to:
- Handle checkbox toggle (enable/disable max guests).
- Calculate surcharge: `(guests - capacity) * fee`.
- Update total price display.

- [ ] **Step 3: Commit**
`git add . && git commit -m "feat: add extra guest checkbox and real-time calculation"`

---

### Task 3: Backend Logic (BookingCreationService.cs)

**Files:**
- Modify: `WebHomestay/Services/BookingCreationService.cs`

- [ ] **Step 1: Update CreateHourlyBookingAsync**
Calculate and add the extra guest fee to `TotalPrice`.
```csharp
var surcharge = Math.Max(0, request.GuestCount - inventory.Room.Capacity) * inventory.Room.ExtraGuestFee;
booking.TotalPrice = inventory.Room.PricePerHour + surcharge;
```

- [ ] **Step 2: Update CreateDailyBookingAsync**
Calculate and add the extra guest fee to `TotalPrice`.
```csharp
var nights = (request.CheckOutDate!.Value.DayNumber - request.CheckInDate!.Value.DayNumber);
if (nights < 1) nights = 1;
var surcharge = Math.Max(0, request.GuestCount - room.Capacity) * room.ExtraGuestFee;
booking.TotalPrice = (room.PricePerDay * nights) + surcharge;
```

- [ ] **Step 3: Commit**
`git add . && git commit -m "feat: apply extra guest fee in booking creation"`

---

### Task 4: Cleanup & Verification

- [ ] **Step 1: Verify calculations**
Manually test the form with different guest counts.
- [ ] **Step 2: Stop brainstorm server**
Run: `scripts/stop-server.sh`
- [ ] **Step 3: Commit final changes**
`git add . && git commit -m "chore: finalize extra guest fee feature"`
