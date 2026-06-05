# Public Botchat UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve the public AI botchat panel layout and cancellation UI while keeping backend behavior and contracts unchanged.

**Architecture:** Keep the existing shared layout widget and public site script. Add a collapsed context toggle in Razor, wire expand/collapse state in JavaScript, and polish the existing CSS classes plus cancellation-specific classes.

**Tech Stack:** ASP.NET Core MVC Razor, vanilla JavaScript, CSS, Bootstrap/Font Awesome already loaded by the layout.

---

## File Structure

- Modify `WebHomestay/Views/Shared/_Layout.cshtml`: add the context toggle button, wrap existing context fields with collapsed default state, and replace the single cancellation chip with a two-button action bar.
- Modify `WebHomestay/wwwroot/js/site.js`: add context expand/collapse behavior, make booking action expand the context form, and keep cancellation action from expanding it.
- Modify `WebHomestay/wwwroot/css/user-premium.css`: style the compact toggle, collapsed form state, two-action bar, cancellation guidance card, and cancellation form spacing/buttons.

## Task 1: Markup update

**Files:**
- Modify: `WebHomestay/Views/Shared/_Layout.cshtml`

- [ ] Add a real `button.ai-context-toggle` below `.ai-chat-header` with `aria-expanded="false"`, title text `Thông tin tư vấn`, hint `Tên, chi nhánh, ngày đặt, số khách`, and a chevron icon.
- [ ] Add `id="ai-chat-context-form"` to the existing `.ai-chat-context` div and mark it hidden/collapsed by default with `class="ai-chat-context is-collapsed"` and `hidden`.
- [ ] Replace `.ai-chat-quick-actions` contents with two buttons: `#ai-booking-consult-action` primary and existing `#ai-cancel-booking-action` secondary.

## Task 2: Interaction wiring

**Files:**
- Modify: `WebHomestay/wwwroot/js/site.js`

- [ ] Query `.ai-context-toggle`, `.ai-context-toggle-hint`, `.ai-context-toggle-icon`, `.ai-chat-context`, and `#ai-booking-consult-action` near existing chatbot element queries.
- [ ] Add `setContextExpanded(expanded)` to toggle `hidden`, `is-collapsed`, `aria-expanded`, hint text, and chevron classes.
- [ ] Initialize with `setContextExpanded(false)` after branches/date controls setup.
- [ ] Wire context toggle click to invert expanded state.
- [ ] Wire booking action click to `setContextExpanded(true)` and focus the first context input.
- [ ] Keep cancellation click rendering only the cancellation prompt.

## Task 3: Visual polish

**Files:**
- Modify: `WebHomestay/wwwroot/css/user-premium.css`

- [ ] Add styles for `.ai-context-toggle`, inner text, hint, and chevron icon.
- [ ] Add `.ai-chat-context.is-collapsed { display: none; }` while preserving mobile one-column behavior when visible.
- [ ] Add `.ai-chat-quick-actions`, `.ai-action-btn`, `.ai-action-primary`, and `.ai-action-secondary` styles.
- [ ] Add cancellation card/form styles for `.ai-cancellation-prompt`, `.ai-cancellation-form-block`, `.ai-cancellation-contact-block`, and `.ai-cancellation-form` without changing form field names.

## Task 4: Verification

**Files:**
- No source changes unless verification reveals a UI bug.

- [ ] Run `dotnet build WebHomestay/WebHomestay.csproj`.
- [ ] If possible, run `dotnet run --project WebHomestay/WebHomestay.csproj` and verify in a browser: collapsed default, toggle expand/collapse, booking action expands, cancellation action does not expand, and mobile width keeps one-column fields.

## Self-review

- Spec coverage: context toggle, collapsed default, action bar, cancellation card/form polish, accessibility attributes, and no backend contract changes are covered.
- Placeholder scan: no TBD/TODO placeholders.
- Scope check: single UI subsystem; no decomposition needed.
