# Cancellation Mail Preview Design

## Goal

Add a mandatory email preview step when staff approve or reject a booking cancellation request in the admin chat monitor. The preview must use the default cancellation email templates configured in Admin Settings under Vận hành & Tự động hóa, allow staff to adjust only the customer-facing reason or small note, and send the final email only after explicit confirmation.

## Current state

Cancellation processing is handled from the admin chat monitor. `BookingCancellationService` currently approves or rejects a pending request, saves the processing fields, and sends a hard-coded email immediately. Approval already requires a refund bill image and stores it as a protected upload. Admin settings currently configure cancellation handling mode, notice hours, refund percentages, and policy message, but not the approval/rejection email templates.

## Requirements

- Staff must preview the email before approval or rejection is finalized.
- Pressing Approve or Reject must not change the request status or send mail directly.
- Approval email must include a refund bill as an attachment.
- Approval cannot be finalized without a bill attachment.
- The preview must show the attachment that will be sent.
- Staff may remove the selected bill in preview, but the final send button must stay disabled until another bill is uploaded.
- Rejection does not require a bill attachment.
- Staff should not edit the whole template. They may edit the dynamic customer-facing reason or small note.
- The final email subject, body, sent time, and attachment name must be saved for audit.

## Recommended approach

Use server-side email preview generation. The browser sends the current staff inputs to preview endpoints. The server renders the email from settings templates and authoritative cancellation/booking data, then returns the preview payload. After staff confirms, a final processing endpoint repeats validation, saves the request state, sends the email, and stores audit fields.

This keeps the preview aligned with the real email, avoids client-side template drift, and keeps settings as the source of truth.

## Settings

Add cancellation email template settings in the existing Admin Settings cancellation section:

- `CancellationApprovalEmailSubject`
- `CancellationApprovalEmailBody`
- `CancellationRejectionEmailSubject`
- `CancellationRejectionEmailBody`

Templates should support a small set of placeholders:

- `{CustomerName}`
- `{BookingId}`
- `{RoomName}`
- `{StartTime}`
- `{EndTime}`
- `{RefundPercent}`
- `{StaffReason}`
- `{PolicyMessage}`

`Program.cs` should seed defaults so the feature works before an admin customizes the copy.

## Admin chat monitor flow

In the cancellation detail modal:

1. Staff enters the reason/note, refund percent, and bill for approval.
2. Staff clicks Approve or Reject.
3. The client validates obvious local requirements:
   - approval requires a selected bill.
   - reason is required for both approval and rejection.
4. The client calls the matching preview endpoint.
5. A mail preview modal opens with:
   - recipient email.
   - subject rendered from template.
   - rendered body.
   - an editable reason/note field only.
   - attachment preview for approval.
   - remove and replace controls for the bill.
6. Staff clicks the final confirmation button:
   - `Gửi mail & chấp nhận hủy`, or
   - `Gửi mail & từ chối hủy`.
7. Only this final action updates the cancellation status and sends email.

## Backend API design

Add preview endpoints to `AdminChatMonitorController`:

- `POST /admin/chat-monitor/cancellations/{id}/approval-preview`
- `POST /admin/chat-monitor/cancellations/{id}/rejection-preview`

They should return a payload like:

```json
{
  "recipientEmail": "customer@example.com",
  "subject": "...",
  "body": "...",
  "editableReason": "...",
  "attachmentName": "bill.png",
  "requiresAttachment": true
}
```

Update final processing endpoints so they accept the final editable note/body inputs and the final selected attachment. The final endpoints remain responsible for all authoritative validation. Preview validation is only for UX.

## Service design

Refactor `BookingCancellationService` so email construction is not hard-coded inside approve/reject methods. Add clear service operations for:

- building approval preview.
- building rejection preview.
- finalizing approval with email data and bill attachment.
- finalizing rejection with email data.

The service should load templates from `ISettingService`, render placeholders with request and booking data, enforce approval bill requirements, and save audit fields after a successful send.

## Mail attachments

Extend `IMailService`/`MailService` to support attachments while preserving the existing simple send method for other callers. The approval email should attach the refund bill image. The preview should show the staff-selected file name/image, but the file should be stored as the official protected upload only when final confirmation succeeds.

## Data model and audit

Add fields to `BookingCancellationRequest` and create an EF migration:

- `NotificationEmailSubject`
- `NotificationEmailBody`
- `NotificationEmailSentAt`
- `NotificationEmailAttachmentName`
- `NotificationEmailType`

These fields capture the exact final notification that was sent to the customer. For rejected requests, attachment name remains empty.

## Error handling

- If the request is missing, return NotFound.
- If the request is no longer pending during final confirmation, reject the action with a clear message.
- If approval has no bill attachment, reject the action.
- If template settings are empty, fall back to seeded/default template values.
- If sending email fails, do not mark the cancellation as approved/rejected. The staff should see the error and be able to retry.

## Testing

Add or update tests around cancellation processing:

- Approval preview renders template placeholders and includes attachment metadata.
- Rejection preview renders template placeholders without requiring attachment.
- Approval finalization fails without a bill.
- Approval finalization sends email with attachment and saves audit fields.
- Rejection finalization sends email without attachment and saves audit fields.
- Finalization does not update status before the preview confirmation step.

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj
```

Because this is a UI flow, also run the app and manually verify the admin monitor path if the local database and mail configuration allow it:

```bash
dotnet run --project WebHomestay/WebHomestay.csproj
```

## Out of scope

- A full free-form email editor.
- Persistent draft emails before final confirmation.
- Inline image embedding in the email body.
- Customer-facing secure bill download links.
