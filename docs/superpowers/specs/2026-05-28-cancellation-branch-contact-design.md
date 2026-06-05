# Cancellation Branch Contact Design

## Goal

When a customer starts a cancellation flow in the botchat, offer two choices:

1. Submit the existing cancellation request form.
2. Contact the branch directly through Zalo or email.

Branch contact data is managed from the admin branches page.

## Admin Branches

The existing branch `Hotline` field becomes the branch Zalo phone number in UI labels and tables.

Add branch email support:

- Add an `Email` property to `Branch` if not already present in the model.
- Persist it through admin branch create/edit forms.
- Show it as a separate column on `/admin/branches`.

The admin branch list should show:

- Branch name
- Address
- Zalo phone number
- Email
- Actions

## Botchat Cancellation Flow

When cancellation intent is detected, show a prompt explaining that AI cannot cancel rooms directly and present two buttons:

- `Gửi yêu cầu hủy` opens the existing cancellation request form unchanged.
- `Liên hệ Zalo/email` opens a branch selector.

The branch selector loads available branches and asks the customer to choose the branch where they booked.

After a branch is selected, botchat displays:

- Branch name
- Zalo phone number with a clickable `https://zalo.me/<normalized-phone>` link
- Branch email with a clickable `mailto:` link if email exists

No cancellation request is created when the customer chooses direct contact.

## Data Flow

1. Admin maintains branch Zalo phone and email in `/admin/branches`.
2. Botchat calls a lightweight public endpoint to fetch branch contact options.
3. Customer selects a branch.
4. Botchat renders contact details for that branch.

## Endpoint

Add a read-only public endpoint for botchat branch contacts. It returns only safe public fields:

- `id`
- `name`
- `address`
- `zaloPhone`
- `email`

## Validation and Safety

- Zalo phone is stored in the existing `Hotline` field, but UI labels call it SĐT Zalo.
- Zalo link is generated client-side by removing non-digits from the phone number.
- Email is optional. If missing, botchat says the branch has not configured email.
- Do not expose admin-only branch data.
- Do not create logs, tickets, or cancellation records for the direct-contact option.

## Testing

- Build the web app.
- Run existing tests.
- Manually verify:
  - `/admin/branches` shows Zalo phone and email columns.
  - Create/edit branch can save Zalo phone and email.
  - Botchat cancellation prompt shows both options.
  - Direct contact option shows branch selector and correct Zalo/email info.
