# AI Chat Booking Flow Design

## Goal

Make the public AI assistant behave like a real booking consultant inside the chat: understand the pre-chat form context, check real room/slot availability, guide the guest through room and slot selection, collect booking details, create a pending booking, and show the next payment/success step.

The assistant must not guess availability, price, booking status, lock codes, or check-in codes. Booking truth comes from server-side availability, slot, pricing, and booking services.

## Context and problem

The current assistant experience is too verbose and can answer that rooms are available even when the requested day has no available slot. The root issue is that availability is not enforced by a deterministic AI booking backend. The chat needs a server-owned booking flow where the LLM is only used for short phrasing, while the application decides availability and booking state.

The existing pre-chat form has these fields:

- Customer name
- Branch
- Booking mode: hourly or daily
- Guest count

The assistant must use that form as conversation context instead of asking for the same information again.

## Recommended approach

Use a deterministic `AIBookingFlowOrchestrator` behind the public chat endpoints.

The orchestrator owns booking-flow state and calls existing domain services to answer these questions:

- Which branch is selected?
- Is the guest asking for hourly or daily booking?
- Which date and time range should be checked?
- Which rooms and slots are actually available?
- Has the guest selected a room and slot?
- Has the guest submitted enough information to create a booking?

The LLM can produce short, friendly Vietnamese copy, but it must not decide whether a room is available. If the AI provider fails, deterministic templates still complete the flow.

## Public chat flow

1. Guest opens the chat and fills the pre-chat form.
2. Guest asks a booking question.
3. `/ai/chat` receives the message plus form context.
4. The orchestrator resolves intent, date, time range, branch, mode, and guest count.
5. The orchestrator checks real availability.
6. The response includes a short message and typed UI blocks such as room cards, slot options, nearby slot suggestions, booking form, or payment block.
7. Guest clicks room/slot/form buttons in chat.
8. `/ai/booking-action` advances the flow.
9. Booking is created only after the guest submits the booking form.
10. The created booking starts in a pending or awaiting-payment state.

## Context rules

When the guest asks a vague question such as “còn phòng không?”, “có phòng không?”, or “còn phòng giờ này không?”, the assistant should use the pre-chat form context first.

Rules:

- Branch comes from the form.
- Booking mode comes from the form.
- Guest count comes from the form.
- If no date is mentioned, default to today.
- If the form or session already has a selected time range, use it.
- If hourly mode has no specific time range, list available slots for today and invite the guest to choose.
- Do not answer generic “còn phòng” unless the response is backed by real available rooms or slots.

## Time and nearby-slot rules

For hourly booking:

- If the guest gives a specific range such as “8h30-9h30”, check that exact range first.
- If the exact range is available, show the matching room/slot options.
- If the exact range is unavailable, suggest only available slots within plus or minus 2 hours on the same day.
- If no nearby slots exist, say clearly that no suitable nearby time is available and suggest changing time, date, or branch.

For date-only questions:

- If the guest asks “ngày mai có phòng không?”, the backend must check tomorrow’s slots/availability.
- If tomorrow has no valid slot, the assistant must say there is no suitable slot; it must not infer availability from room existence.

## Components

### AIChatController

Responsibilities:

- Accept `/ai/chat` requests from the public widget.
- Accept `/ai/booking-action` requests from chat UI buttons and forms.
- Pass all decisions to `AIBookingFlowOrchestrator`.
- Return typed UI blocks for the frontend to render.

### AIBookingFlowOrchestrator

Responsibilities:

- Maintain and normalize session state.
- Use form context before asking follow-up questions.
- Parse date/time intent from Vietnamese booking messages.
- Check real room and slot availability.
- Return room cards, slot buttons, nearby suggestions, booking forms, and payment/success blocks.
- Create bookings only after form submission.
- Revalidate room and slot availability immediately before creating a booking.

### Availability and booking services

Availability validation must ensure:

- Slot belongs to the selected room.
- Slot belongs to the requested date and branch.
- Slot status is available.
- Slot is not blocked.
- Room is not already booked for an overlapping time.
- Lead-time rules are enforced consistently.
- Guest count does not exceed room capacity rules.

Booking creation must repeat the critical availability checks so clients cannot bypass the chat flow.

### Public chat UI

The public chat JavaScript should:

- Send pre-chat form context with every `/ai/chat` request.
- Store session state returned by the backend.
- Render room cards, exact slot options, nearby slot suggestions, booking forms, and payment/success blocks.
- Send button/form actions to `/ai/booking-action`.
- Keep responses short and action-oriented.

## Error handling

- If a service or database check fails, the assistant says it cannot verify availability right now and asks the guest to retry.
- If the AI provider fails, deterministic templates continue the booking flow.
- If a slot is taken between selection and form submission, the assistant reports that the slot just became unavailable and refreshes available alternatives.
- If required form fields are missing, ask only for the missing field.

## Testing requirements

Backend tests should cover:

- “Còn phòng không?” uses form context and defaults to today.
- “Ngày mai có phòng không?” returns no availability when tomorrow has no valid slot.
- A specific time range is checked before nearby suggestions.
- Nearby suggestions are limited to plus or minus 2 hours on the same day.
- Blocked slots and overlapping bookings are never reported as available.
- Booking is not created before the guest submits the booking form.
- Booking form submission creates a pending or awaiting-payment booking.
- Booking creation revalidates slot availability.

Manual UI checks should cover:

- Pre-chat form context is sent correctly.
- Room and slot buttons advance the chat state.
- The booking form creates a real booking.
- Unavailable cases are short, clear, and do not ramble.

## Out of scope

- The assistant does not create check-in codes.
- The assistant does not mark bookings as paid without the existing payment flow.
- The assistant does not replace the existing booking pages; it adds a chat-driven path that uses the same booking truth.
- The assistant does not simplify or remove the broader multi-agent, RAG, and Graph direction in the admin AI architecture.

## Acceptance criteria

- The assistant never claims availability without a server-side availability result.
- Vague availability questions use the pre-chat form context.
- Today is the default date when no date is provided.
- Specific hourly requests check the exact time first, then same-day nearby slots within plus or minus 2 hours.
- Full chat booking can create a pending or awaiting-payment booking after form submission.
- The response style is concise and guides the guest to the next action.
