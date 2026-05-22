# AGENTS.md — Web Homestay

Before making any changes, read **MANDATORY_CONTEXT.md** and **docs/yeucau/TONG_QUAN_DU_AN.md**.

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

## Architecture (essential)

- **Stack:** ASP.NET Core MVC net10.0, PostgreSQL (EF Core + Npgsql 8.0), vanilla HTML/CSS/Bootstrap/jQuery
- **Auth:** Session-based admin auth (no cookie auth middleware). `AdminAuthorizeAttribute` filter checks session keys: `AdminUser`, `AdminRole`, `AdminPermissions`.
- **DB:** Auto-migrates on startup via `context.Database.Migrate()` in `Program.cs:61-66`
- **Npgsql quirk:** Legacy timestamp enabled: `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)`
- **AI key:** Read from `../key.md` at startup. Listed in `.gitignore`.

## Permission Matrix System (dual-mode)

Replacing old per-account permissions with **Role template** (Manager/Staff) + **Account override**.

### Permission keys structure: `{module}.{action}`
```
module    │ parent key    │ child keys
──────────┼───────────────┼─────────────────────────────
matrix    │ matrix.view   │ (none)
bookings  │ bookings.view │ detail, create, edit, delete, trash, restore
branches  │ branches.view │ detail, create, edit, delete
rooms     │ rooms.view    │ detail, create, edit, delete
images    │ images.view   │ detail
statistics│ statistics.view│ export
staff     │ staff.view    │ create, edit, delete, permissions, logs
settings  │ settings.view │ update, holidays, slots
ai        │ ai.view       │ manage
```

### Resolve order: SuperAdmin (bypass) → Role template (MemoryCache 5 min) → Account override (from session `AdminPermissions`)

### Key files:
- `Models/RolePermissionTemplate.cs` — entity for role defaults
- `Services/PermissionResolveService.cs` — runtime resolve (template + override merge)
- `Filters/AdminAuthorizeAttribute.cs` — uses `IPermissionResolveService`
- `Helpers/PermissionHelper.cs` — static helper for views (uses `IPermissionResolveService` from DI)
- `Controllers/AdminStaffController.cs` — `PermissionMatrix` GET/POST actions, server-side inheritance validation
- `Views/AdminStaff/PermissionMatrix.cshtml` — matrix UI with role/account toggle, parent-child JS validation
- `Views/Shared/_Layout.cshtml` — menu uses `PermissionHelper.HasPermission()` (not hardcoded roles)

**Inheritance rules:** Children require parent. UI + server both validate. SuperAdmin column is disabled.

**To add a new permission:** update `parentChildMap` in `AdminStaffController.ValidateInheritance()`, `PermissionInheritanceTests` in test project, and seed data.

## AI Architecture (preserve this)

Multi-agent/RAG/Graph architecture — do NOT simplify to basic chatbot:
- `Services/AIBrainOrchestrator.cs` — orchestrates Persona, Live Snapshot, Knowledge Retrieval, Graph Reasoning, Guard, Final Synthesizer
- `Services/AIModelClient.cs` — OpenAI-compatible chat completions (Groq/OpenRouter)
- `Services/AIBookingFlowOrchestrator.cs` — deterministic booking flow
- `Controllers/AIChatController.cs` — public chat endpoint
- AI never confirms bookings, never invents prices/availability, never generates check-in codes — always routes to official flow.

## Tests

- **Framework:** xUnit with EF InMemory (each test gets fresh DB via `Guid.NewGuid().ToString()`)
- **Session mocking:** `FakeSession` in `WebHomestay.Tests/FakeSession.cs`
- **Scope mocking:** `FakeServiceScopeFactory` in `WebHomestay.Tests/FakeServiceScopeFactory.cs`
- Test locations: `Services/`, `Domain/`, `Admin/`
- Before creating new tests, check existing tests for patterns.

## Booking & Availability

Not based on simple room status. Key services:
- `AvailabilityService` — find free rooms by branch + time range
- `SlotGenerationService` / `SlotManagementService` — hourly/daily inventory slots
- `BookingCreationService` / `BookingTimeRules` / `PricingService` — create booking, validate time, compute price
- `AdminMatrixController` — "Ma trận vận hành" with auto check-in/out logic
