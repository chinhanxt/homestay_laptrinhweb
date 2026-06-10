# AI Admin Rebuild Design

Date: 2026-06-10
Project: Web Homestay
Scope: Rebuild the admin AI management surface before any customer chatbot UI integration.

## 1. Goal

Rebuild the current `AdminAI` area into a cleaner admin-first AI control center with two focused workspaces:

- `Tri thức & Graph`
- `Trả lời & Form`

This rebuild is explicitly for configuration quality first. Customer-facing chatbot UI is out of scope for this phase.

The rebuild must:

- remove legacy AI admin sections that scatter responsibility
- delete current AI knowledge and graph data, then reseed a large new dataset derived only from real branch and room data
- keep the seeded data editable by admin after creation
- centralize all surviving AI behavior settings into one studio-style configuration page
- define a future-proof configuration contract for interactive AI blocks without implementing customer chat integration yet

## 2. Current Problems

The existing AI admin area has several issues:

- too many tabs with overlapping responsibilities
- config spread across multiple endpoints and `SystemSettings` keys with inconsistent grouping
- legacy sections (`Tổng quan`, `Test & Trace`, `Public Booking Config`, `Booking Form Designer`) distract from the actual admin configuration flow
- current seed logic is too small and mixes soft/default AI knowledge with system-derived data
- current graph is shallow: mostly branch-room links, not rich enough for AI grounding
- existing JS and view structure are misaligned and difficult to evolve cleanly

## 3. Product Direction

This phase optimizes for:

- admin configuration quality
- truth-based AI grounding
- visually strong configuration UX
- future compatibility with customer chat UI

This phase does not optimize for:

- direct chatbot testing in admin
- trace inspection workflows
- maintaining backward compatibility with old admin AI UX

## 4. Information Architecture

The rebuilt page keeps only two primary areas.

### 4.1 Tri thức & Graph

Purpose:

- manage the data AI sees as reference material
- separate system-derived data from editable soft knowledge
- provide one-click full reseed from real branch/room data

### 4.2 Trả lời & Form

Purpose:

- act as the single studio for AI behavior configuration
- define how AI speaks, asks, remembers, shows rooms, shows dates/slots, and emits interactive blocks
- give admins a live preview-oriented editing experience

## 5. Removed Features

The following sections are removed from the admin AI surface and their dedicated routes should be deleted:

- `Tổng quan`
- `Test & Trace`
- `Public Booking Config`
- `Booking Form Designer`

Implications:

- no dedicated admin trace browsing in this rebuild
- no dedicated public booking config screen
- no separate booking form config endpoint
- no separate preview/test console endpoints under `AdminAI`

If any old settings remain necessary for runtime compatibility, they must be absorbed into the new unified studio config model instead of keeping old admin APIs alive.

## 6. Tri thức & Graph Design

### 6.1 Data Strategy

`Tri thức & Graph` contains two data classes:

1. `System-derived knowledge/graph`
2. `Editable soft knowledge`

System-derived data is generated entirely from real DB records in `Branches` and `Rooms`.

Editable soft knowledge remains admin-editable and covers:

- policies
- sales guidance
- explanation patterns
- business rules described in prose
- any grounded knowledge not stored directly in room/branch entities

### 6.2 Reseed Behavior

A new admin action will fully wipe and regenerate the AI grounding dataset.

When reseed runs:

1. Delete all current records in:
   - `AIBrainScopes`
   - `AIKnowledgeUnits`
   - `AIGraphNodes`
   - `AIGraphEdges`
2. Recreate the base scope structure for the new seed.
3. Generate a large dataset from current `Branches` and `Rooms`.
4. Persist source metadata so the UI can distinguish seeded system data from later admin-written soft knowledge.

The reseed must not import legacy `AIKnowledgeCollections` / `AIKnowledgeArticles` content into the new dataset.

### 6.3 Seed Principles

The seed must be:

- large
- truthful
- derived from existing records only
- traceable back to source room/branch data
- editable after generation

The seed must not:

- invent amenities not present in source data
- invent branch positioning or room personality
- invent room availability or pricing logic
- carry forward stale AI soft knowledge by default

### 6.4 Seeded Knowledge Units

The seed should generate many `AIKnowledgeUnit` records from real data patterns, for example:

- one unit per branch
- one unit per room
- one unit per branch summarizing all rooms under that branch
- one unit per capacity band present in current room data
- one unit per price band present in current room data
- one unit per room status group present in current room data

If room descriptions contain real repeated tokens that can be safely extracted, the system may also generate:

- amenity/theme summary units based on deterministic text extraction

Each generated unit should include:

- title
- grounded content summary
- tags
- priority
- metadata indicating it came from `system-seed`

### 6.5 Seeded Graph

The seed should generate a richer graph than the current branch-room-only structure.

Expected node types include:

- `branch`
- `room`
- `capacity_band`
- `price_band`
- `status`
- `amenity_tag` if and only if derived deterministically from real room text

Expected edge types include:

- `branch_contains_room`
- `room_belongs_to_branch`
- `room_has_capacity_band`
- `room_has_price_band`
- `room_has_status`
- `room_has_amenity_tag` when applicable

Every graph node and edge should be explainable by data already stored in the system.

### 6.6 Admin Editing Model

After reseed, admin can still edit everything:

- knowledge units
- graph nodes
- graph edges

This matches the requested operating model: seed a strong truthful baseline first, then allow manual enhancement.

### 6.7 Tri thức & Graph UI

The page should provide:

- a prominent `Xóa sạch và seed lại từ dữ liệu hiện có` action
- a warning state explaining the wipe-and-rebuild effect
- top-level counters for scopes, knowledge units, nodes, edges
- source filters such as `Hệ thống` and `Chỉnh tay`
- searchable lists for knowledge units and graph entities
- editing modals or side panels for units/nodes/edges

The reseed action should feel deliberate and operationally significant, not like a minor button.

## 7. Trả lời & Form Studio Design

### 7.1 Layout

This area becomes a three-panel studio:

- left panel: configuration categories
- center panel: active configuration editor
- right panel: live preview of AI response and interaction blocks

This is a studio, not a plain form page.

### 7.2 Studio Categories

The left panel should expose at least these groups:

- `Phong cách & Prompt`
- `Luật an toàn`
- `Nhớ ngữ cảnh`
- `Hiển thị phòng`
- `Hiển thị khung giờ`
- `Hiển thị ngày`
- `Checklist thu thập thông tin`
- `Khối form tương tác`
- `Thanh toán / QR`
- `Điều kiện kích hoạt block`

These groups replace the fragmented old screens.

### 7.3 Editing Model

The center panel should use mixed controls, not only raw textareas.

Required editing patterns:

- textareas for prompts and writing rules
- toggles/radios/selects for behavior modes
- card repeaters for checklist items and interaction blocks
- lightweight condition builder UI for block triggers
- field editors for labels, help text, required flags, value sources, submit behavior

Text configuration is still allowed, but should be presented through a structured UI wherever possible.

### 7.4 Preview Model

The right panel should preview:

- sample AI text output
- the interaction block shown beneath the message
- the synthesized text that would be pushed into the customer chat input later
- room list block preview
- slot/day selection preview
- payment QR preview

The preview is for admin understanding only in this phase. It does not connect to real customer chat yet.

## 8. New Unified Config Contract

### 8.1 Intent

The old model centered too much around isolated settings like public booking config or booking form schema.

The new model should unify all surviving behavior into one studio contract. Storage can still use `SystemSettings`, but the admin code should treat the config as one logical document.

### 8.2 Suggested Logical Structure

The logical `studio-config` document should contain sections similar to:

- `responseStyle`
- `safetyRules`
- `memoryRules`
- `roomDisplayRules`
- `slotDisplayRules`
- `dateDisplayRules`
- `checklistRules`
- `interactionBlocks`
- `paymentQrRules`
- `blockTriggerRules`

The internal persistence may map these into one JSON setting or a controlled set of AI settings, but the controller/view model should expose one unified configuration payload.

### 8.3 Interaction Blocks

This rebuild introduces a clearer contract than `formSchema`.

Each block should support fields such as:

- `type`
- `label`
- `description`
- `fields` or `items`
- `triggerConditions`
- `submitBehavior`
- `messageTemplate`
- `displayMode`
- `source`

Expected block types:

- `branchSelector`
- `datePicker`
- `guestCount`
- `roomList`
- `slotList`
- `stayType`
- `paymentQr`
- `summaryConfirm`

### 8.4 Checklist Behavior

Checklist in this design is not a passive admin checklist.

It is a runtime interaction design contract for AI prompts, where:

- AI asks a natural-language question
- an interaction block appears beneath the AI message
- the customer would later select values from structured controls
- those selected values are transformed into a synthesized chat input message

Example intent:

AI asks:

`Chào bạn, bạn muốn đặt phòng ở chi nhánh nào và ngày nào ạ? Bạn đi mấy người để mình tìm phòng phù hợp nhé.`

Configured runtime effect:

- show branch selector choices
- show date input
- show guest count input
- assemble a text summary from selected values
- push the assembled content into the customer input/send flow in a later phase

This rebuild only defines and previews that behavior on the admin side.

## 9. Backend Changes

### 9.1 Controller Simplification

`AdminAIController` should be significantly reduced and reorganized around:

- knowledge/graph management
- reseed operation
- unified studio config load/save

Legacy endpoints to remove:

- public booking config routes
- booking form config routes
- trace list/detail routes
- agent test routes
- preview/test routes that only served old admin workflows

### 9.2 New Endpoints

The new controller surface should resemble:

- `GET /admin/ai/studio-config`
- `POST /admin/ai/studio-config`
- `POST /admin/ai/reseed-system-knowledge`
- existing or refactored CRUD endpoints for knowledge units, graph nodes, graph edges

Exact route naming may vary, but the responsibility split should remain.

### 9.3 Persistence

Persistence may continue using `SystemSettings`, but with stricter organization.

The admin layer should behave as if it reads and writes a unified config document rather than dozens of unrelated settings.

Acceptable persistence options:

- one JSON blob setting for the studio config
- a small set of clearly grouped JSON settings

Not acceptable:

- continuing to scatter logically related AI behavior across many separate legacy keys without a higher-level contract

## 10. Frontend Changes

### 10.1 View Rewrite

`Views/AdminAI/Index.cshtml` should be rewritten substantially.

The new page should:

- remove tab overload
- feel like a configuration studio
- emphasize operational clarity
- keep a strong visual hierarchy between system data and behavior configuration

### 10.2 Script Rewrite

The current AI admin script is inconsistent with the view structure and contains legacy flows.

The rebuild should replace it with a new purpose-built script for:

- loading and saving unified studio config
- reseeding knowledge/graph
- managing list/search/edit interactions for knowledge and graph
- rendering live admin previews for interaction blocks

### 10.3 Visual Direction

The admin studio should feel intentional and high-control, not generic Bootstrap tabs.

Desired feel:

- operations console for AI behavior
- premium but practical
- strong grouping
- obvious “data vs behavior” separation
- visual preview that makes future customer interaction understandable

## 11. Testing Strategy

Required tests:

- reseed deletes old knowledge/graph data and recreates a new truthful dataset
- reseed produces expected entities from current branch/room fixtures
- studio config save/load round-trips correctly
- interaction block config serializes and deserializes correctly

Nice-to-have tests:

- metadata tags/source markers correctly identify seeded records
- deterministic extraction logic for optional amenity tags behaves safely

Out of scope for this phase:

- customer chat rendering tests
- full end-to-end chatbot interaction tests
- trace viewer tests, since that workflow is being removed from admin

## 12. Risks and Constraints

### 12.1 Seed Volume Risk

If the seed expands too aggressively from weak text parsing, it can introduce noisy nodes. Deterministic extraction rules must be conservative.

### 12.2 Legacy Runtime Compatibility Risk

Customer chatbot runtime may still depend on some old setting keys. The rebuild should avoid breaking runtime unexpectedly, but the admin API surface should still be cleaned up.

### 12.3 Scope Risk

This rebuild must stay focused on admin configuration and truthful AI grounding. It should not expand into customer chat implementation in the same phase.

## 13. Implementation Boundaries

Included:

- admin AI page restructure
- legacy admin AI section removal
- truthful reseed workflow
- richer seeded knowledge and graph
- unified response/form studio
- preview-oriented interaction block configuration

Excluded:

- wiring the new block contract into customer chat UI
- implementing final customer-side interactive block rendering
- bringing back admin test/trace surfaces during this phase

## 14. Recommendation

Proceed with a focused rebuild of the current `AdminAI` area using existing data tables where practical, while introducing:

- a new reseed strategy for truthful large grounding data
- a new studio config contract
- a new admin-first UX

This gives a strong configuration foundation without prematurely rebuilding the customer chatbot.
