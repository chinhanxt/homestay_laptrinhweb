# Branch QR Payment Settings Design

## Goal
Replace the duplicated AI booking consultant tab inside the System Settings page with a branch-specific QR payment configuration screen. The main AI Brain Center remains the place for AI configuration, while System Settings gains the operational payment settings needed for booking bills.

## Scope
- Rename/remove the current System Settings sidebar item for AI booking consultant and replace it with "Cấu hình mã QR".
- Configure QR payment data per branch using `SystemSettings`, with keys namespaced by branch id.
- Allow admin to upload a QR image per branch.
- Allow admin to enter a manual QR image URL.
- Allow admin to enter bank data so the payment page can generate VietQR when no uploaded image or manual URL is configured.
- Configure countdown duration, defaulting to 5 minutes.
- Configure payment/bill messages shown before and after proof submission, plus expiry text.
- Use the configured values on the booking success/payment page instead of hard-coded QR and countdown values.

## Data Model
No migration is required. Settings are stored in `SystemSettings` with branch-scoped keys:

- `PaymentQr:{BranchId}:QrImagePath`
- `PaymentQr:{BranchId}:QrImageUrl`
- `PaymentQr:{BranchId}:BankCode`
- `PaymentQr:{BranchId}:BankAccountNumber`
- `PaymentQr:{BranchId}:BankAccountName`
- `PaymentQr:{BranchId}:TransferContentTemplate`
- `PaymentQr:{BranchId}:CountdownMinutes`
- `PaymentQr:{BranchId}:BeforeBillMessage`
- `PaymentQr:{BranchId}:AfterBillMessage`
- `PaymentQr:{BranchId}:ExpiredMessage`

The transfer content template can use booking placeholders such as booking id and branch name. The first implementation should support only the placeholders needed by the existing booking success page to avoid broad template complexity.

## Admin UI
The System Settings page keeps the current sidebar style. The old AI booking consultant item becomes "Cấu hình mã QR".

The QR settings panel includes:
- Branch selector or one card per branch, matching the existing settings page layout.
- QR preview.
- Upload control for QR image.
- Manual QR URL field.
- Bank code, account number, and account name fields.
- Transfer content template field.
- Countdown minutes field with default value 5.
- Text areas for before-bill, after-bill, and expired messages.
- Save action for the selected branch.

QR selection priority is shown in the UI: uploaded QR image first, manual QR URL second, generated VietQR third.

## Payment Page Behavior
On the booking success/payment page, load the QR settings for the booking branch.

QR source priority:
1. Uploaded QR image path.
2. Manual QR image URL.
3. Generated VietQR URL from bank data and transfer content.
4. Existing safe default/fallback text if the branch has no usable QR config.

Countdown duration comes from `PaymentQr:{BranchId}:CountdownMinutes`. If missing or invalid, use 5 minutes.

Messages shown around bill/proof submission come from the branch QR settings. If a setting is empty, preserve the current page wording.

## File Upload Rules
Uploaded QR files are saved under `wwwroot/uploads/payment-qr/` with branch-specific, non-user-controlled file names. Only image extensions are accepted. The saved public path is written to `SystemSettings` as `QrImagePath`.

## Error Handling
- Missing branch: show a validation error and do not save.
- Invalid countdown: reject values below 1 minute and keep the previous value.
- Invalid upload type: reject with a validation message.
- Empty bank fields: allowed if uploaded image or QR URL exists.
- No QR source available: payment page keeps working and shows a clear missing-configuration message instead of a broken image.

## Testing
- Run `dotnet build WebHomestay/WebHomestay.csproj`.
- Run relevant tests with `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj` if backend payment/settings behavior changes are covered.
- Manually run the app and verify:
  - The System Settings sidebar no longer shows the AI booking consultant tab.
  - "Cấu hình mã QR" can save settings for different branches independently.
  - QR upload updates preview and is used on the booking success page.
  - Manual QR URL and generated VietQR fallback work when no upload exists.
  - Countdown uses configured minutes and defaults to 5.
  - Before/after bill messages appear in the payment flow.
