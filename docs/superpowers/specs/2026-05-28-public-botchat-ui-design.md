# Public botchat UI design

## Goal

Improve the public AI botchat panel so the chat area feels less crowded, the booking information form is available without dominating the panel, and the cancellation flow looks more intentional.

## Approved direction

Use a compact default layout with a collapsible booking information form.

The form is collapsed when the chatbot opens. A compact bar appears under the chat header with the title "Thông tin tư vấn", a short hint such as "Tên, chi nhánh, ngày đặt, số khách", and a chevron. Clicking this bar expands the existing context fields. Clicking it again collapses the fields.

## Layout

The panel keeps the existing structure and backend behavior:

1. Header: existing chinhan Assistant header and close button.
2. Context toggle: new compact information bar below the header.
3. Context form: existing customer name, branch, booking mode, guest count, check-in, and check-out fields, hidden by default.
4. Messages: existing conversation log remains the main visual area.
5. Action bar: two prominent actions above the chat input.
6. Chat input: existing text input and send button.
7. Note: existing availability/payment note remains at the bottom.

## Context form behavior

- Default state: collapsed.
- Expand triggers:
  - user clicks the "Thông tin tư vấn" bar;
  - user clicks the "Tư vấn đặt phòng" action.
- Collapse trigger: user clicks the bar while the form is expanded.
- Visual state:
  - collapsed state shows a downward chevron and text indicating the form can be opened;
  - expanded state shows an upward chevron and text indicating the form can be collapsed.
- Mobile behavior: keep the existing one-column responsive layout for the form fields.

## Action bar

Replace the current single quick-action chip with two styled actions:

- "Tư vấn đặt phòng": primary action using the dark brand color. It expands the context form and should keep the user in the booking consultation flow.
- "Yêu cầu hủy phòng": secondary action using a warm cream/gold treatment. It starts the cancellation guidance flow and must not expand the booking context form.

The action bar sits below the messages/status area and above the chat input. It should feel like a stable mode/action selector, not a loose text chip.

## Cancellation flow polish

The existing cancellation behavior remains:

- AI does not cancel bookings directly.
- The user can send a cancellation request for staff review.
- The user can choose branch contact information for Zalo/email.

Visual improvements:

- Render the cancellation guidance message as a warm, bordered card instead of plain text.
- Keep the existing "Gửi yêu cầu hủy" and "Liên hệ Zalo/email" actions, but style them consistently with the new botchat action buttons.
- Keep the existing cancellation request form fields and policy fetch behavior.
- Improve spacing, borders, and button styling inside the cancellation form without changing the submission contract.

## Visual style

Use the current public luxury theme:

- dark navy/primary color for the booking action and header alignment;
- gold/accent color for highlights, chevrons, send button, and hover/focus states;
- warm cream background for cancellation-related cards;
- soft borders and subtle shadows;
- rounded controls where it improves the premium widget feel.

Do not introduce a new color system or a major redesign of the site.

## Accessibility and interaction

- The context toggle should be a real button with `aria-expanded` reflecting the current state.
- The context form should remain keyboard accessible when expanded.
- The collapsed form should not trap focus in hidden fields.
- Keep existing chat form submission behavior.
- The close/open chatbot behavior remains unchanged.

## Implementation scope

Expected files:

- `WebHomestay/Views/Shared/_Layout.cshtml`: add the context toggle and second action button, keep existing context fields.
- `WebHomestay/wwwroot/js/site.js`: add context expand/collapse behavior and wire the booking consultation action.
- `WebHomestay/wwwroot/css/user-premium.css`: add styles for the compact context toggle, collapsed context state, two-action bar, and cancellation card polish.

No backend API, database, AI orchestrator, booking logic, or cancellation submission contract should change.

## Testing

Manual UI verification is required because this is a frontend change:

1. Open a public page and open the chatbot.
2. Confirm the context form is collapsed by default.
3. Click "Thông tin tư vấn" and confirm the form expands/collapses.
4. Click "Tư vấn đặt phòng" and confirm the form expands.
5. Click "Yêu cầu hủy phòng" and confirm the cancellation prompt appears without expanding the booking form.
6. Check mobile width to confirm the expanded form remains one column and usable.

Run the app locally for browser verification if possible. If browser verification cannot be completed, report that clearly.