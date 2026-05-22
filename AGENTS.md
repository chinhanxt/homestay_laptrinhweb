# AGENTS.md — Web Homestay

Before making changes, read **MANDATORY_CONTEXT.md** and **docs/yeucau/TONG_QUAN_DU_AN.md**.

## Commands

| Command | Action |
|---------|--------|
| `dotnet build WebHomestay/WebHomestay.csproj` | Build web app |
| `dotnet run --project WebHomestay/WebHomestay.csproj` | Run web app (localhost:5000) |
| `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj` | Run all tests |
| `dotnet test ... --filter FullyQualifiedName~ClassName` | Run single test class |
| `dotnet ef migrations add <Name> --project WebHomestay/WebHomestay.csproj` | Add EF migration |
| `dotnet ef database update --project WebHomestay/WebHomestay.csproj` | Apply EF migrations |
| `.\r.ps1 up` | Restore + build + run |
| `.\r.ps1 watch` or `.\w.bat` | Hot reload |
| `make up` / `make watch` | Alternative via Makefile |

**Note:** No `.sln` at root — run commands per-project.

## Architecture essentials

- **Stack:** ASP.NET Core MVC net10.0, PostgreSQL (EF Core + Npgsql 8.0.0), vanilla HTML/CSS/Bootstrap/jQuery
- **Auth:** Session-based admin auth (no cookie auth middleware). `AdminAuthorizeAttribute` filter (in `Filters/`) checks session keys: `AdminUser`, `AdminRole`. Uses `IPermissionResolveService` for fine-grained permission checks via `[AdminAuthorize(Permission = "module.action")]`.
- **DB:** Auto-migrates on startup via `context.Database.Migrate()` at `Program.cs:61-66`. All table names are lowercase in `OnModelCreating` (e.g. `ai_knowledge_units`). EF Core + Npgsql with `EnableLegacyTimestampBehavior`.
- **AI key:** Read from `../key.md` at startup (`Program.cs:6-16`). Always a Groq key. Listed in `.gitignore`. Never commit secrets.
- **Mock mode removed:** `AIModelClient` now throws if provider is `"mock"` — no fallback. Provider defaults to `"groq"` but can be overridden to `"openrouter"` or `"9router"`.

## AI: two separate systems

### 1. Public AI Chat — deterministic booking flow
`AIChatController` (route `/ai`) → `AIBookingFlowOrchestrator`

- **Does NOT call the LLM.** It's a state-machine chatbot that extracts intent, filters rooms/slots via `AvailabilityService`, collects customer info, and hands off to `BookingCreationService`.
- Steps: `intent` → `select-room`/`select-daily-room` → `select-slot` → `submit-booking-form` → `payment`
- Session state cached in `IMemoryCache` with 30min TTL under key `ai-booking-flow:{sessionId}`.
- UI blocks returned as JSON `UiBlocks` (types: `roomCards`, `hourlySlots`, `dailyRooms`, `bookingSummary`, `bookingForm`, `paymentQr`).
- Booking form fields configurable via `AIBookingFormSchema` in `SystemSettings` (GroupName = "AI"); defaults to 7 fields including `idCardFront`/`idCardBack` (type: image).
- Upload endpoint: `POST /ai/booking-id-card` + `POST /ai/payment-proof`.

### 2. Admin AI Brain Center — multi-agent/RAG/Graph (preserve this)
`AdminAIController` (route `/admin/ai`) → `AIBrainOrchestrator` → `AIModelClient`

- Do NOT simplify to basic chatbot. Current implementation has 5 agents orchestrated sequentially:
  - **Persona** (rule-based: price sensitivity, urgency, group signals)
  - **Live Snapshot** (queries `RoomSlotInventories` + `AvailabilityService` in real time)
  - **Knowledge RAG** (matches message terms against `AIKnowledgeUnits` ordered by priority)
  - **Graph Reasoning** (matches `AIGraphNode` labels/summaries, fetches connected `AIGraphEdge` relationships)
  - **Safety Guard** (rules: no booking confirmation, no fake data, validates time range)
  - **Final Synthesizer** (calls `AIModelClient` with all agent outputs as system prompt context)
- All configurable rules stored in `SystemSettings` with `GroupName == "AI"`:
  - `AIFinalSynthesizerStyle`, `AIFinalSynthesizerFormSchema`, `AIFinalConditionOptions`
  - `AIFinalBasePrompt`, `AIFinalLanguageRule`, `AIFinalDataTruthRule`, etc.
- Every request saves an `AIConversationTrace` with full agent outputs for debugging.
- Default model: `llama-3.3-70b-versatile` via Groq API.
- AI tables in DB: `ai_knowledge_collections`, `ai_knowledge_articles`, `ai_brain_scopes`, `ai_knowledge_units`, `ai_graph_nodes`, `ai_graph_edges`, `ai_agent_definitions`, `ai_conversation_traces`.
- Admin management UI at `/Views/AdminAI/Index.cshtml` with JS at `/wwwroot/js/admin-ai-brain-center.js`.

### AI rules (both systems)
- Never confirm bookings, never invent prices/availability, never generate check-in codes. Always route to official booking flow.
- Live snapshot comes from DB via `AvailabilityService`, not from LLM.

## Permission Matrix (dual-mode)

**Role template** (Manager/Staff, stored in `RolePermissionTemplate.PermissionsJson`) + **Account override** (stored in `AdminUser.PermissionsJson`, synced to session `AdminPermissions`).

### Permission keys: `{module}.{action}`
```
module    │ parent key     │ child keys
──────────┼────────────────┼─────────────────────────────
matrix    │ matrix.view    │ (none)
bookings  │ bookings.view  │ detail, create, edit, delete, trash, restore
branches  │ branches.view  │ detail, create, edit, delete
rooms     │ rooms.view     │ detail, create, edit, delete
images    │ images.view    │ detail
statistics│ statistics.view│ export
staff     │ staff.view     │ create, edit, delete, permissions, logs
settings  │ settings.view  │ update, holidays, slots
ai        │ ai.view        │ manage
```

### Resolve order: SuperAdmin (bypass) → Role template (MemoryCache 5 min) → Account override (from session `AdminPermissions`). Children require parent (validated both server-side and client-side).

### Key files:
- `Models/RolePermissionTemplate.cs` — role defaults
- `Services/PermissionResolveService.cs` — template + override merge
- `Filters/AdminAuthorizeAttribute.cs` — uses `IPermissionResolveService`
- `Helpers/PermissionHelper.cs` — for views (uses `IPermissionResolveService` from DI)
- `Controllers/AdminStaffController.cs` — `PermissionMatrix` GET/POST
- `Views/AdminStaff/PermissionMatrix.cshtml` — role/account toggle, parent-child JS validation

## Tests (xUnit + EF InMemory)

- Each test gets a fresh DB via `Guid.NewGuid().ToString()` for context name.
- Session mocking: `WebHomestay.Tests/FakeSession.cs` — implements `ISession` plus `SetString`/`GetString`/`SetInt32`/`GetInt32`.
- Scope mocking: `WebHomestay.Tests/FakeServiceScopeFactory.cs`.
- Test directories: `Services/`, `Domain/`, `Admin/`.
- Check existing test files (especially `AIBookingFlowOrchestratorTests.cs`, `PermissionResolveServiceTests.cs`) for patterns before writing new tests.

## Booking & availability

Not based on simple room status. Key services:
- `AvailabilityService` — find free rooms by branch + time range
- `SlotGenerationService` / `SlotManagementService` — hourly/daily inventory slots
- `BookingCreationService` / `BookingTimeRules` / `PricingService` — create booking, validate time, compute price
- `BookingCleanupService` — hosted background service for stale bookings
- `AdminMatrixController` — "Ma trận vận hành" with auto check-in/out logic
- Booking lifecycle: `PendingPayment → AwaitingApproval → Confirmed → CheckedIn → CheckedOut` (or `Cancelled`). Soft delete via `IsDeleted` flag.
