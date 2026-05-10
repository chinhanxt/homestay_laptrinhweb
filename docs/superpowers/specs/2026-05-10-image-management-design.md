# Spec: Image Management & CCCD Masking System

## 1. Overview
The system provides a dedicated administrative interface for managing booking-related images (Payment Receipts and Citizen Identity Cards - CCCD). It implements a privacy-first approach by automatically masking sensitive information on CCCD images for regular staff while preserving enough detail for verification.

## 2. Goals
- Create a split-view management page for images.
- Implement automatic masking for CCCD (ID number, address, QR code).
- Enforce Role-Based Access Control (RBAC) for original vs. masked images.
- Add a new permission for viewing this page in the permission matrix.

## 3. Technical Design

### 3.1. Data Model Changes
Update the `Booking` model to store paths for masked versions:
- `IdCardFrontMaskedPath` (string, nullable)
- `IdCardBackMaskedPath` (string, nullable)

### 3.2. Storage Structure
- `wwwroot/uploads/bookings/originals/`: Restricted access (Originals).
- `wwwroot/uploads/bookings/masked/`: Publicly accessible within the app (Masked versions).

### 3.3. Image Processing Service
A new `ImageMaskingService` using `SixLabors.ImageSharp`:
- **Functionality**: `MaskIdCard(Stream originalImage, bool isFront)`
- **Masking Logic (Front)**:
  - Define coordinates for:
    - ID Number (Số): `[X: 45%, Y: 20%, W: 40%, H: 8%]` (Approximate)
    - Address (Địa chỉ): `[X: 40%, Y: 60%, W: 55%, H: 20%]`
  - Action: Apply solid color overlay or heavy blur.
  - Preserve: Name, Photo, Birth Year.
- **Masking Logic (Back)**:
  - Define coordinates for:
    - QR Code: `[X: 5%, Y: 5%, W: 20%, H: 20%]`
    - MRZ/Barcode area at bottom.

### 3.4. Permissions Integration
- Add `images.view` to the `AdminMatrix` module.
- Update `AdminAuthorize` filter or views to check for this permission.

### 3.5. UI/UX - Management Page
- **Controller**: `AdminImagesController`
- **View**: `Index.cshtml`
  - Left: Filterable list of bookings with "Image Status" indicators.
  - Right: Display area for Bill, Front CCCD (Masked/Original), Back CCCD (Masked/Original).
  - Feature: Zoom/Rotate viewer.
  - No "Approve" button (as per user feedback, approval stays in Booking Details).

### 3.6. Integration into Booking Details
- Modify `AdminBookings/Details.cshtml`:
  - Check user role/permission.
  - Render `IdCardFrontMaskedPath` if not SuperAdmin.
  - Render `IdCardFrontPath` if SuperAdmin or has explicit permission.

## 4. Implementation Plan (High Level)
1. Add new fields to `Booking` model and run migration.
2. Implement `ImageMaskingService` with `ImageSharp`.
3. Update Booking upload logic to trigger masking and save both versions.
4. Create `AdminImagesController` and View.
5. Add "images.view" option to the Permission Matrix UI (`AdminStaff/Permissions.cshtml`).
6. Refactor Booking Details to handle conditional image display.

## 5. Security Considerations
- Original images must be protected from direct URL access.
- Watermarking should be applied to all images displayed in the admin panel to prevent unauthorized redistribution.
