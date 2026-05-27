# Booking Cancellation Request Design

## Context

The system currently has booking management, AI chat, Chat Monitor, email sending, and booking status `Cancelled`, but it does not have a customer-facing cancellation request flow. Customers will request cancellation from the AI chat widget. Staff will review the request in Chat Monitor, decide whether cancellation is allowed, and email the result to the customer.

This design keeps the AI assistant as a guide only. AI may open the cancellation form and explain the process, but it must not approve cancellation, invent refund terms, or change booking state by itself.

## Goals

- Add a customer cancellation request flow inside the AI chat widget.
- Create a separate cancellation request record for tracking, filtering, approval, rejection, refund evidence, and audit history.
- Integrate cancellation review into Chat Monitor because staff already monitor customer messages there.
- Add configurable cancellation settings to the existing Admin Settings page.
- Support manual and automatic handling modes for booking state changes.
- Protect sensitive customer evidence, QR images, bank details, and refund bills.

## Non-goals

- No automatic payment gateway refund integration.
- No AI-driven approval or rejection.
- No separate public cancellation page outside the AI widget for the first version.
- No branch-specific or room-specific cancellation policy in the first version.

## Recommended Approach

Use a dedicated `BookingCancellationRequest` entity integrated into Chat Monitor.

Rejected alternatives:

1. Store cancellation as a special chat message only. This is faster but hard to filter, audit, and track refund status.
2. Handle cancellation only from the Booking admin page. This is cleaner for booking state but does not match the operational flow where staff receive customer requests in Chat Monitor.

The selected approach creates durable cancellation records while still showing them inside chat conversations.

## Customer Flow

1. The AI chat widget always shows a quick action button: `Yêu cầu hủy phòng`.
2. If a customer sends a message that clearly indicates cancellation intent, AI replies briefly that it cannot cancel directly and shows the same cancellation action.
3. Clicking the action switches the widget into a cancellation form screen.
4. The form asks for:
   - Booking code/id if the customer knows it.
   - Customer name.
   - Phone number.
   - Email address.
   - Image proof of the successful booking confirmation email.
   - Refund receiving information: QR image and/or bank name, account number, account holder name.
5. The form displays the cancellation policy from Admin Settings before submission.
6. If a matching booking can be inferred, the UI may show the expected refund percentage based on the booking check-in time. If no booking is found yet, the UI shows the general policy only.
7. Customers may still submit even after the configured notice threshold. The warning must explain that refund may be reduced or unavailable.
8. After submission, the widget confirms that the request was sent and staff will respond by email.

## Admin Chat Monitor Flow

1. Chat session list shows a badge when the session has a pending cancellation request.
2. The conversation timeline shows each cancellation request as a special card/message.
3. Chat Monitor adds a filter or tab for cancellation requests with statuses: `Pending`, `Approved`, and `Rejected`.
4. Staff can open the cancellation request detail from either the chat timeline or the cancellation filter.
5. The detail view shows:
   - Customer-submitted data.
   - Suggested booking matches.
   - Selected/linked booking if already matched.
   - Secure links/previews for confirmation email proof and refund QR image.
   - Cancellation policy snapshot and warning based on check-in time.
   - Refund receiving bank information.
6. If the request is not linked to the correct booking, staff can select the matching booking before approval.
7. Staff must enter a processing reason/note for both approval and rejection.
8. Approval requires uploading a refund bill/proof image before confirmation.
9. When staff confirms approval or rejection, the system updates the request, sends email to the email address submitted in the cancellation form, and optionally updates the booking depending on settings.

## Settings

Add a `Chính sách hủy đơn` section to the existing Admin Settings page. Do not create a new settings page.

Settings:

- `CancellationHandlingMode`: `Manual` or `Auto`.
- `CancellationNoticeHours`: minimum hours before check-in for normal cancellation handling.
- `CancellationRefundPercentBeforeNotice`: refund percentage before the notice threshold.
- `CancellationRefundPercentAfterNotice`: refund percentage after the notice threshold. This can be `0`.
- `CancellationPolicyMessage`: configurable text shown in the customer form and included in cancellation emails.

Use default email templates in the first version and inject the configured policy message, customer data, booking data, staff reason, refund percentage, and refund bill information. Full email-template editing is intentionally deferred to keep the settings page manageable.

## Data Model

Add `BookingCancellationRequest`.

Core fields:

- `Id`
- `ChatSessionId`
- `BookingId` nullable
- `SubmittedBookingCode` nullable
- `CustomerName`
- `CustomerPhone`
- `CustomerEmail`
- `ConfirmationEmailProofPath`
- `RefundQrImagePath` nullable
- `RefundBankName` nullable
- `RefundBankAccountNumber` nullable
- `RefundBankAccountHolder` nullable
- `Status`: `Pending`, `Approved`, `Rejected`
- `SuggestedBookingIdsJson` nullable
- `PolicyNoticeHoursSnapshot`
- `RefundPercentBeforeNoticeSnapshot`
- `RefundPercentAfterNoticeSnapshot`
- `PolicyMessageSnapshot`
- `AppliedRefundPercent` nullable until processed
- `RefundStatus`: `NotRefunded`, `Refunded`, or equivalent simple values
- `RefundBillProofPath` nullable, required when approving
- `StaffReason` nullable until processed, required on approval or rejection
- `ProcessedBy` nullable
- `ProcessedAt` nullable
- `CreatedAt`
- `UpdatedAt`

The policy snapshot fields make each request auditable even if settings change later.

## Booking Matching

When the request is submitted, the system attempts to suggest matching bookings using:

1. Booking id/code if provided.
2. Customer email.
3. Customer phone.
4. Customer name.

A strong exact booking id match can set `BookingId` immediately if customer email/phone also matches. Otherwise, store suggested matches for staff selection. Staff remains the final decision-maker.

## Cancellation Processing Rules

### Approval

Approval requires:

- A linked booking.
- A required staff reason/note.
- A required refund bill/proof image.
- An applied refund percentage.

When `CancellationHandlingMode` is `Auto`:

- Set booking status to `Cancelled`.
- If booking is hourly and has a slot inventory id, set the slot status back to `Available`.
- Do not issue or change smart lock codes.

When `CancellationHandlingMode` is `Manual`:

- Do not change booking status or slot status automatically.
- Record the approval and send email.
- Staff can manually update booking state from booking management.

In both modes:

- Set request status to `Approved`.
- Set refund status to `Refunded` because a refund bill is required.
- Send approval email to `CustomerEmail` from the request.

### Rejection

Rejection requires:

- A staff reason/note.

On rejection:

- Set request status to `Rejected`.
- Do not change booking status or slot status.
- Send rejection email to `CustomerEmail` from the request.

## Email Behavior

Approval email includes:

- Cancellation result.
- Booking id and room/check-in summary when linked.
- Staff reason/note.
- Applied refund percentage.
- Cancellation policy message snapshot.
- Refund bill/proof reference or secure attachment/link strategy, depending on implementation capability.

Rejection email includes:

- Cancellation result.
- Booking id and room/check-in summary when linked.
- Staff reason/note.
- Cancellation policy message snapshot.

Emails must not include new check-in codes, lock codes, or unrelated sensitive booking secrets.

## File Security

Sensitive uploads include:

- Confirmation email proof image.
- Customer refund QR image.
- Staff refund bill image.

Rules:

- Store these files outside `wwwroot`, similar to secure ID card uploads.
- Generate GUID-based filenames.
- Accept only image uploads.
- Enforce a file size limit.
- Serve previews/downloads through authenticated admin endpoints with permission checks.
- Keep the files as part of cancellation history for the first version.

## Backend Components

Add a `BookingCancellationService` to centralize logic:

- Load cancellation settings.
- Build policy display data.
- Save secure uploaded images.
- Create cancellation request.
- Suggest booking matches.
- Approve cancellation.
- Reject cancellation.
- Apply automatic booking cancellation when configured.
- Send approval/rejection emails.

Public endpoints can live under `AIChatController` or a small controller under `/ai/cancellations`:

- Get policy.
- Submit cancellation form with image upload.

Admin endpoints should be integrated with Chat Monitor:

- List/filter cancellation requests.
- Get request detail.
- Link/select booking.
- Approve request with refund bill upload.
- Reject request.
- Securely view protected images.

## UI Components

Customer AI widget changes:

- Add persistent `Yêu cầu hủy phòng` quick action.
- Add cancellation form screen.
- Add policy warning block.
- Add upload controls for email proof and QR image.
- Add success state after submission.

Chat Monitor changes:

- Add pending cancellation badge to session list.
- Add special cancellation card in conversation timeline.
- Add cancellation filter/tab.
- Add request detail/action panel.
- Add approval form requiring refund bill upload.
- Add rejection form requiring reason.

Admin Settings changes:

- Add a cancellation policy card/section to the existing settings page.
- Keep it compact and consistent with existing bento/card styling.

## Permissions

Use existing admin authorization patterns.

Minimum permissions:

- Staff who can view Chat Monitor can see cancellation cards and basic request data.
- Processing approval/rejection should require an edit/operate permission such as the existing chat permission or a new cancellation-specific permission if the current permission matrix supports adding one cleanly.
- Viewing protected images must require authenticated admin access and appropriate permission.

## Testing Plan

Service tests:

- Create request with booking id and matching email/phone.
- Create request without booking id and verify suggested matches.
- Calculate refund percent before notice threshold.
- Calculate refund percent after notice threshold.
- Approve in auto mode changes booking to `Cancelled`.
- Approve in auto mode returns hourly slot to `Available`.
- Approve in manual mode does not change booking or slot.
- Reject does not change booking or slot.
- Approval fails without refund bill proof.
- Approval and rejection require staff reason.

Controller/UI validation tests where practical:

- Public submission rejects missing required fields.
- Public submission rejects non-image uploads.
- Admin approval rejects missing refund bill.
- Protected image endpoint requires admin authorization.

Manual verification after implementation:

- Run the app.
- Open AI widget and click `Yêu cầu hủy phòng`.
- Submit a request with proof image and refund QR/bank info.
- Confirm Chat Monitor shows the request in the conversation and cancellation filter.
- Approve with refund bill and verify email sending path and request status.
- Verify auto mode updates booking and hourly slot.
- Verify manual mode does not update booking automatically.
- Reject and verify email sending path and no booking state change.

## Implementation Order Recommendation

1. Add settings keys and display them in Admin Settings.
2. Add data model, DbContext mapping, and migration.
3. Add service logic for policy, creation, matching, approval, rejection, and secure file handling.
4. Add public cancellation form endpoints.
5. Add AI widget UI for cancellation action and form.
6. Add Chat Monitor cancellation cards, filter, and action forms.
7. Add email bodies and connect approval/rejection flows.
8. Add tests and run relevant test suite.
9. Run the app and manually verify the customer and staff flows.
