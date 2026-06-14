# AI Admin Rebuild Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the admin AI area into a two-workspace control center with truthful reseedable knowledge/graph data and a unified response/form studio.

**Architecture:** Keep the current ASP.NET Core MVC + EF Core structure, but simplify `AdminAIController` around two responsibilities: data grounding and studio configuration. Reuse existing AI tables where practical, introduce a unified studio-config payload stored via `SystemSettings`, and rewrite the admin view/JS/CSS to match the new studio UX.

**Tech Stack:** ASP.NET Core MVC, EF Core + Npgsql, xUnit, Razor views, vanilla JS, Bootstrap, existing `SystemSettings` persistence.

---

## File Structure

### Existing files to modify

- `WebHomestay/Controllers/AdminAIController.cs`
  - Remove legacy routes and add unified studio-config + reseed behavior.
- `WebHomestay/Program.cs`
  - Seed any new default AI config keys or unified config bootstrap values.
- `WebHomestay/Views/AdminAI/Index.cshtml`
  - Replace the current tabbed AI admin UI with the two-workspace layout.
- `WebHomestay/wwwroot/js/admin-ai-brain-center.js`
  - Replace legacy admin AI logic with focused knowledge/graph + studio behavior.
- `WebHomestay/wwwroot/css/admin-ai-brain-center.css`
  - Restyle the page as a studio with data vs behavior separation.
- `WebHomestay/Views/Shared/_Layout.cshtml`
  - Update label/navigation text only if needed to reflect the rebuilt page.
- `WebHomestay.Tests/WebHomestay.Tests.csproj`
  - Ensure any new test files are included if the project needs explicit globs.

### Existing files to review while implementing

- `WebHomestay/Data/ApplicationDbContext.cs`
  - Confirm current AI entity mappings and constraints.
- `WebHomestay/Models/AIGraphNode.cs`
  - Confirm fields available for metadata tagging.
- `WebHomestay/Models/AIGraphEdge.cs`
  - Confirm fields available for evidence/source tagging.
- `WebHomestay/Models/SystemSetting.cs`
  - Confirm how unified config should be persisted.
- `WebHomestay/Models/AIConversationTrace.cs`
  - Check runtime dependencies before removing admin trace routes.

### New files to create

- `WebHomestay/Models/AI/AdminAIStudioConfig.cs`
  - Unified config document model for the admin AI studio.
- `WebHomestay/Models/AI/AdminAIStudioBlock.cs`
  - Contract for interaction blocks and editor preview payloads.
- `WebHomestay/Models/AI/AdminAIStudioCondition.cs`
  - Lightweight condition model for block triggers.
- `WebHomestay/Models/AI/AdminAIStudioField.cs`
  - Structured field model for checklist and interaction blocks.
- `WebHomestay/Models/AI/AdminAIStudioRequests.cs`
  - Request/response DTOs for loading and saving studio config.
- `WebHomestay/Services/AI/AdminAIStudioConfigService.cs`
  - Load/save unified config to `SystemSettings`.
- `WebHomestay/Services/AI/AdminAIReseedService.cs`
  - Wipe and regenerate scopes, knowledge, nodes, and edges from real branch/room data.
- `WebHomestay/Services/AI/IAdminAIStudioConfigService.cs`
  - Interface for studio config persistence.
- `WebHomestay/Services/AI/IAdminAIReseedService.cs`
  - Interface for reseed workflow.
- `WebHomestay.Tests/Services/AdminAIReseedServiceTests.cs`
  - Tests for truthful wipe-and-reseed behavior.
- `WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs`
  - Tests for unified config round-trip behavior.
- `WebHomestay.Tests/Controllers/AdminAIControllerTests.cs`
  - Tests for route surface and controller behavior if controller tests already exist as a pattern in the repo.

## Task 1: Lock the Current AI Admin Surface and Runtime Dependencies

**Files:**
- Modify: `WebHomestay/Controllers/AdminAIController.cs`
- Review: `WebHomestay/Services/AI/SemanticKernelOrchestrator.cs`
- Review: `WebHomestay/Services/ContextAwareBookingConductor.cs`
- Test: `WebHomestay.Tests/Controllers/AdminAIControllerTests.cs`

- [ ] **Step 1: Write the failing controller tests for the new route surface**

```csharp
[Fact]
public async Task GetStudioConfig_ReturnsUnifiedConfig()
{
    var controller = BuildController();

    var result = await controller.GetStudioConfig();

    var ok = Assert.IsType<OkObjectResult>(result);
    Assert.NotNull(ok.Value);
}

[Fact]
public void AdminAIController_DoesNotExposeLegacyPublicBookingRoutes()
{
    var methods = typeof(AdminAIController)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Select(m => m.Name)
        .ToHashSet(StringComparer.Ordinal);

    Assert.DoesNotContain("GetPublicBookingConfig", methods);
    Assert.DoesNotContain("SavePublicBookingConfig", methods);
    Assert.DoesNotContain("GetBookingFormConfig", methods);
    Assert.DoesNotContain("SaveBookingFormConfig", methods);
    Assert.DoesNotContain("GetBrainTraces", methods);
    Assert.DoesNotContain("GetBrainTrace", methods);
    Assert.DoesNotContain("TestAgent", methods);
    Assert.DoesNotContain("BrainPreview", methods);
}
```

- [ ] **Step 2: Run the controller tests to verify they fail**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIControllerTests`

Expected: FAIL because `GetStudioConfig` does not exist yet and legacy methods still exist.

- [ ] **Step 3: Review runtime usage before deleting legacy endpoints**

Check these calls before editing:

```powershell
rg -n "public-booking-config|booking-form-config|brain-traces|brain-trace|test-agent|brain-preview" WebHomestay -S
```

Expected: confirm only admin UI paths depend on the routes being removed, while customer chat runtime still depends on lower-level settings/services, not the old admin endpoints.

- [ ] **Step 4: Remove legacy route methods and add placeholders for the new surface**

Add these method signatures in `AdminAIController.cs`:

```csharp
[AdminAuthorize(Permission = "ai.response")]
[HttpGet("studio-config")]
public async Task<IActionResult> GetStudioConfig()
{
    var config = await _studioConfigService.GetAsync();
    return Ok(config);
}

[AdminAuthorize(Permission = "ai.edit")]
[HttpPost("studio-config")]
public async Task<IActionResult> SaveStudioConfig([FromBody] AdminAIStudioConfigRequest request)
{
    await _studioConfigService.SaveAsync(request);
    return Ok(new { success = true });
}

[AdminAuthorize(Permission = "ai.edit")]
[HttpPost("reseed-system-knowledge")]
public async Task<IActionResult> ReseedSystemKnowledge()
{
    var result = await _reseedService.ReseedAsync();
    return Ok(result);
}
```

- [ ] **Step 5: Re-run the controller tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIControllerTests`

Expected: partial pass or new failures for missing services/DTOs, which is acceptable before later tasks.

- [ ] **Step 6: Commit the route-surface cleanup**

```bash
git add WebHomestay/Controllers/AdminAIController.cs WebHomestay.Tests/Controllers/AdminAIControllerTests.cs
git commit -m "refactor: replace legacy admin ai route surface"
```

## Task 2: Define the Unified Studio Config Contract

**Files:**
- Create: `WebHomestay/Models/AI/AdminAIStudioConfig.cs`
- Create: `WebHomestay/Models/AI/AdminAIStudioBlock.cs`
- Create: `WebHomestay/Models/AI/AdminAIStudioCondition.cs`
- Create: `WebHomestay/Models/AI/AdminAIStudioField.cs`
- Create: `WebHomestay/Models/AI/AdminAIStudioRequests.cs`
- Test: `WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs`

- [ ] **Step 1: Write the failing config serialization tests**

```csharp
[Fact]
public async Task GetAsync_ReturnsDefaultStudioConfig_WhenSettingMissing()
{
    using var db = BuildDbContext();
    var service = new AdminAIStudioConfigService(db);

    var config = await service.GetAsync();

    Assert.NotNull(config);
    Assert.NotEmpty(config.Categories);
    Assert.Contains(config.InteractionBlocks, b => b.Type == "branchSelector");
}

[Fact]
public async Task SaveAsync_PersistsUnifiedJsonDocument()
{
    using var db = BuildDbContext();
    var service = new AdminAIStudioConfigService(db);
    var request = new AdminAIStudioConfigRequest
    {
        ResponseStyle = new AdminAIStudioResponseStyle
        {
            Tone = "Rõ ràng, thân thiện"
        }
    };

    await service.SaveAsync(request);

    var setting = await db.SystemSettings.SingleAsync(s => s.SettingKey == "AIStudioConfig");
    Assert.Contains("Rõ ràng, thân thiện", setting.SettingValue);
}
```

- [ ] **Step 2: Run the config service tests to verify they fail**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIStudioConfigServiceTests`

Expected: FAIL because the models and service do not exist.

- [ ] **Step 3: Add the studio config model types**

Define focused models like:

```csharp
public class AdminAIStudioConfig
{
    public AdminAIStudioResponseStyle ResponseStyle { get; set; } = new();
    public AdminAIStudioSafetyRules SafetyRules { get; set; } = new();
    public AdminAIStudioMemoryRules MemoryRules { get; set; } = new();
    public AdminAIStudioDisplayRules RoomDisplayRules { get; set; } = new();
    public AdminAIStudioDisplayRules SlotDisplayRules { get; set; } = new();
    public AdminAIStudioDisplayRules DateDisplayRules { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<AdminAIStudioField> ChecklistFields { get; set; } = new();
    public List<AdminAIStudioBlock> InteractionBlocks { get; set; } = new();
    public AdminAIStudioPaymentRules PaymentQrRules { get; set; } = new();
}
```

```csharp
public class AdminAIStudioBlock
{
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SubmitBehavior { get; set; } = "compose_and_send";
    public string MessageTemplate { get; set; } = string.Empty;
    public string DisplayMode { get; set; } = "inline";
    public string Source { get; set; } = "system";
    public List<AdminAIStudioField> Fields { get; set; } = new();
    public List<AdminAIStudioCondition> TriggerConditions { get; set; } = new();
}
```

- [ ] **Step 4: Add request DTOs for controller/service usage**

```csharp
public class AdminAIStudioConfigRequest : AdminAIStudioConfig
{
}

public class AdminAIStudioConfigResponse : AdminAIStudioConfig
{
    public DateTime? LastUpdated { get; set; }
}
```

- [ ] **Step 5: Re-run the config service tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIStudioConfigServiceTests`

Expected: still FAIL because the service implementation is not written yet, but model-related compile errors should be gone.

- [ ] **Step 6: Commit the config contract models**

```bash
git add WebHomestay/Models/AI/AdminAIStudioConfig.cs WebHomestay/Models/AI/AdminAIStudioBlock.cs WebHomestay/Models/AI/AdminAIStudioCondition.cs WebHomestay/Models/AI/AdminAIStudioField.cs WebHomestay/Models/AI/AdminAIStudioRequests.cs WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs
git commit -m "feat: add admin ai studio config contract"
```

## Task 3: Implement Unified Studio Config Persistence

**Files:**
- Create: `WebHomestay/Services/AI/IAdminAIStudioConfigService.cs`
- Create: `WebHomestay/Services/AI/AdminAIStudioConfigService.cs`
- Modify: `WebHomestay/Program.cs`
- Test: `WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs`

- [ ] **Step 1: Write the failing service registration test if the repo has DI coverage**

```csharp
[Fact]
public void Program_RegistersAdminAIStudioConfigService()
{
    var services = BuildServiceCollection();
    var provider = services.BuildServiceProvider();

    var service = provider.GetService<IAdminAIStudioConfigService>();

    Assert.NotNull(service);
}
```

If there is no DI test pattern, skip this test and rely on compile + controller tests.

- [ ] **Step 2: Implement the interface**

```csharp
public interface IAdminAIStudioConfigService
{
    Task<AdminAIStudioConfigResponse> GetAsync();
    Task SaveAsync(AdminAIStudioConfigRequest request);
}
```

- [ ] **Step 3: Implement the service with one unified `SystemSettings` JSON document**

```csharp
public class AdminAIStudioConfigService : IAdminAIStudioConfigService
{
    private const string SettingKey = "AIStudioConfig";
    private readonly ApplicationDbContext _context;

    public AdminAIStudioConfigService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminAIStudioConfigResponse> GetAsync()
    {
        var setting = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.SettingKey == SettingKey);

        if (setting == null || string.IsNullOrWhiteSpace(setting.SettingValue))
        {
            return BuildDefaultResponse();
        }

        var config = JsonSerializer.Deserialize<AdminAIStudioConfigResponse>(setting.SettingValue)
            ?? BuildDefaultResponse();
        config.LastUpdated = setting.LastUpdated;
        return config;
    }

    public async Task SaveAsync(AdminAIStudioConfigRequest request)
    {
        var payload = JsonSerializer.Serialize(request);
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == SettingKey);

        if (setting == null)
        {
            setting = new SystemSetting
            {
                SettingKey = SettingKey,
                GroupName = "AI",
                Description = "Unified Admin AI studio configuration"
            };
            _context.SystemSettings.Add(setting);
        }

        setting.SettingValue = payload;
        setting.LastUpdated = DateTime.Now;
        await _context.SaveChangesAsync();
    }
}
```

- [ ] **Step 4: Register the new service in `Program.cs`**

```csharp
builder.Services.AddScoped<WebHomestay.Services.AI.IAdminAIStudioConfigService, WebHomestay.Services.AI.AdminAIStudioConfigService>();
```

- [ ] **Step 5: Re-run the config tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIStudioConfigServiceTests`

Expected: PASS.

- [ ] **Step 6: Commit the config persistence layer**

```bash
git add WebHomestay/Services/AI/IAdminAIStudioConfigService.cs WebHomestay/Services/AI/AdminAIStudioConfigService.cs WebHomestay/Program.cs WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs
git commit -m "feat: persist unified admin ai studio config"
```

## Task 4: Implement Truthful Wipe-and-Reseed Services

**Files:**
- Create: `WebHomestay/Services/AI/IAdminAIReseedService.cs`
- Create: `WebHomestay/Services/AI/AdminAIReseedService.cs`
- Modify: `WebHomestay/Controllers/AdminAIController.cs`
- Test: `WebHomestay.Tests/Services/AdminAIReseedServiceTests.cs`

- [ ] **Step 1: Write the failing reseed tests**

```csharp
[Fact]
public async Task ReseedAsync_DeletesOldKnowledgeGraphData()
{
    using var db = BuildDbContextWithOldAiData();
    SeedBranchAndRoomData(db);
    var service = new AdminAIReseedService(db);

    await service.ReseedAsync();

    Assert.DoesNotContain(db.AIKnowledgeUnits, x => x.Title == "Old Unit");
    Assert.DoesNotContain(db.AIGraphNodes, x => x.Label == "Old Node");
}

[Fact]
public async Task ReseedAsync_CreatesBranchAndRoomGroundingData()
{
    using var db = BuildDbContextWithRoomFixtures();
    var service = new AdminAIReseedService(db);

    var result = await service.ReseedAsync();

    Assert.True(result.CreatedKnowledgeUnits > 0);
    Assert.Contains(db.AIGraphNodes, x => x.NodeType == "branch");
    Assert.Contains(db.AIGraphNodes, x => x.NodeType == "room");
    Assert.Contains(db.AIGraphEdges, x => x.RelationshipType == "branch_contains_room");
}
```

- [ ] **Step 2: Run the reseed tests to verify they fail**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIReseedServiceTests`

Expected: FAIL because the service does not exist yet.

- [ ] **Step 3: Implement the reseed interface and result DTO**

```csharp
public interface IAdminAIReseedService
{
    Task<AdminAIReseedResult> ReseedAsync();
}

public class AdminAIReseedResult
{
    public int CreatedScopes { get; set; }
    public int CreatedKnowledgeUnits { get; set; }
    public int CreatedNodes { get; set; }
    public int CreatedEdges { get; set; }
}
```

- [ ] **Step 4: Implement full wipe of the four AI grounding tables**

```csharp
_context.AIGraphEdges.RemoveRange(_context.AIGraphEdges);
_context.AIGraphNodes.RemoveRange(_context.AIGraphNodes);
_context.AIKnowledgeUnits.RemoveRange(_context.AIKnowledgeUnits);
_context.AIBrainScopes.RemoveRange(_context.AIBrainScopes);
await _context.SaveChangesAsync();
```

- [ ] **Step 5: Implement deterministic seed generation from `Branches` and `Rooms`**

Use patterns like:

```csharp
foreach (var branch in branches)
{
    var branchNode = new AIGraphNode
    {
        Id = Guid.NewGuid(),
        NodeType = "branch",
        Label = branch.Name,
        Summary = $"Chi nhánh tại {branch.Address}. Hotline: {branch.Hotline}.",
        MetadataJson = JsonSerializer.Serialize(new { source = "system-seed", branchId = branch.Id })
    };

    foreach (var room in branch.Rooms)
    {
        var roomNode = new AIGraphNode
        {
            Id = Guid.NewGuid(),
            NodeType = "room",
            Label = room.Name,
            Summary = $"Sức chứa {room.Capacity}, tối đa {room.MaxGuests}, giá giờ {room.PricePerHour}, giá ngày {room.PricePerDay}.",
            MetadataJson = JsonSerializer.Serialize(new { source = "system-seed", branchId = branch.Id, roomId = room.Id })
        };

        _context.AIGraphEdges.Add(new AIGraphEdge
        {
            FromNodeId = branchNode.Id,
            ToNodeId = roomNode.Id,
            RelationshipType = "branch_contains_room",
            Weight = 1,
            Evidence = "Derived from Branch.Rooms during reseed."
        });
    }
}
```

Also generate capacity-band, price-band, and status nodes/edges from real values only.

- [ ] **Step 6: Re-run the reseed tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIReseedServiceTests`

Expected: PASS.

- [ ] **Step 7: Commit the reseed service**

```bash
git add WebHomestay/Services/AI/IAdminAIReseedService.cs WebHomestay/Services/AI/AdminAIReseedService.cs WebHomestay.Tests/Services/AdminAIReseedServiceTests.cs WebHomestay/Controllers/AdminAIController.cs
git commit -m "feat: add truthful admin ai reseed service"
```

## Task 5: Finish the Controller Around Services, CRUD, and Responses

**Files:**
- Modify: `WebHomestay/Controllers/AdminAIController.cs`
- Test: `WebHomestay.Tests/Controllers/AdminAIControllerTests.cs`

- [ ] **Step 1: Write failing tests for reseed and studio-config controller behavior**

```csharp
[Fact]
public async Task SaveStudioConfig_ReturnsSuccess()
{
    var controller = BuildController();
    var request = new AdminAIStudioConfigRequest
    {
        ResponseStyle = new AdminAIStudioResponseStyle { Tone = "Ngắn gọn" }
    };

    var result = await controller.SaveStudioConfig(request);

    var ok = Assert.IsType<OkObjectResult>(result);
    Assert.Equal(true, ok.Value.GetType().GetProperty("success")?.GetValue(ok.Value));
}
```

- [ ] **Step 2: Run the controller tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIControllerTests`

Expected: FAIL if the controller still uses old constructor dependencies or has not injected the new services.

- [ ] **Step 3: Refactor constructor injection and keep only needed admin dependencies**

```csharp
private readonly ApplicationDbContext _context;
private readonly IAdminAIStudioConfigService _studioConfigService;
private readonly IAdminAIReseedService _reseedService;

public AdminAIController(
    ApplicationDbContext context,
    IAdminAIStudioConfigService studioConfigService,
    IAdminAIReseedService reseedService)
{
    _context = context;
    _studioConfigService = studioConfigService;
    _reseedService = reseedService;
}
```

- [ ] **Step 4: Keep the knowledge/graph CRUD routes and remove dead private helper code**

Delete old helpers that only supported:

```csharp
GetFinalSynthesizerConfig
GetBookingFormConfig
SaveBookingFormConfig
SavePublicBookingConfig
UploadPaymentQr
TestAgent
BrainChat
BrainPreview
GetCollections
GetArticles
GetArticle
SaveArticle
DeleteArticle
```

Retain or refactor only the endpoints needed by:

- `Tri thức & Graph`
- `Trả lời & Form`

- [ ] **Step 5: Re-run the controller tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIControllerTests`

Expected: PASS.

- [ ] **Step 6: Commit the controller refactor**

```bash
git add WebHomestay/Controllers/AdminAIController.cs WebHomestay.Tests/Controllers/AdminAIControllerTests.cs
git commit -m "refactor: simplify admin ai controller around studio and reseed"
```

## Task 6: Rewrite the Admin AI Razor View Into Two Workspaces

**Files:**
- Modify: `WebHomestay/Views/AdminAI/Index.cshtml`

- [ ] **Step 1: Replace the old tab layout with two top-level workspace sections**

Use a structure like:

```cshtml
<div class="ai-studio-page">
    <section class="ai-studio-hero">
        <div>
            <p class="eyebrow">AI Operations</p>
            <h1>AI Configuration Studio</h1>
            <p>Quản trị tri thức thật và hành vi phản hồi trong một màn hình tập trung.</p>
        </div>
    </section>

    <div class="ai-studio-workspaces">
        <section id="workspace-knowledge-graph" class="workspace-card"></section>
        <section id="workspace-response-form" class="workspace-card"></section>
    </div>
</div>
```

- [ ] **Step 2: Build the `Tri thức & Graph` workspace skeleton**

Include:

```cshtml
<div class="workspace-toolbar">
    <button id="reseed-ai-grounding" class="btn btn-danger">Xóa sạch và seed lại từ dữ liệu hiện có</button>
    <div id="ai-grounding-stats"></div>
</div>
<div class="workspace-split">
    <div id="knowledge-unit-list"></div>
    <div id="graph-entity-list"></div>
</div>
```

- [ ] **Step 3: Build the `Trả lời & Form` studio skeleton**

Include:

```cshtml
<div class="studio-shell">
    <aside id="studio-category-nav"></aside>
    <main id="studio-config-editor"></main>
    <aside id="studio-live-preview"></aside>
</div>
```

- [ ] **Step 4: Remove the old overview/test/public-booking/form-designer markup**

Delete the tab panes and modals that only supported the removed workflows.

- [ ] **Step 5: Smoke-check the Razor file by building**

Run: `dotnet build WebHomestay/WebHomestay.csproj`

Expected: `Build succeeded.`

- [ ] **Step 6: Commit the Razor rewrite**

```bash
git add WebHomestay/Views/AdminAI/Index.cshtml
git commit -m "feat: rebuild admin ai razor workspace layout"
```

## Task 7: Rewrite Admin AI JavaScript Around the New Studio

**Files:**
- Modify: `WebHomestay/wwwroot/js/admin-ai-brain-center.js`

- [ ] **Step 1: Replace startup logic with a focused studio boot sequence**

Use:

```javascript
document.addEventListener('DOMContentLoaded', async () => {
    await Promise.all([
        loadStudioConfig(),
        loadBrainKnowledge(),
        loadBrainGraph()
    ]);
    bindStudioEvents();
});
```

- [ ] **Step 2: Implement unified config fetch/save**

```javascript
async function loadStudioConfig() {
    const response = await fetch('/admin/ai/studio-config');
    studioState.config = await response.json();
    renderStudioCategoryNav();
    renderStudioEditor();
    renderStudioPreview();
}

async function saveStudioConfig() {
    await fetch('/admin/ai/studio-config', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(studioState.config)
    });
}
```

- [ ] **Step 3: Implement reseed trigger and post-reseed refresh**

```javascript
async function reseedSystemKnowledge() {
    if (!confirm('Thao tác này sẽ xóa sạch tri thức và graph AI hiện tại.')) return;

    const response = await fetch('/admin/ai/reseed-system-knowledge', { method: 'POST' });
    const result = await response.json();
    renderReseedSummary(result);
    await Promise.all([loadBrainKnowledge(), loadBrainGraph()]);
}
```

- [ ] **Step 4: Implement three-panel studio rendering**

Provide functions like:

```javascript
function renderStudioCategoryNav() {}
function renderStudioEditor() {}
function renderStudioPreview() {}
function renderInteractionBlockPreview(block) {}
```

The preview must show a sample AI message plus inline block output.

- [ ] **Step 5: Delete dead legacy JS flows**

Remove functions tied only to:

- old tab switching
- old public booking config
- old form designer
- old trace browser
- old test console

- [ ] **Step 6: Build to validate bundling/static references**

Run: `dotnet build WebHomestay/WebHomestay.csproj`

Expected: `Build succeeded.`

- [ ] **Step 7: Commit the JS rewrite**

```bash
git add WebHomestay/wwwroot/js/admin-ai-brain-center.js
git commit -m "feat: rebuild admin ai studio javascript"
```

## Task 8: Rewrite Admin AI CSS for the Studio UX

**Files:**
- Modify: `WebHomestay/wwwroot/css/admin-ai-brain-center.css`

- [ ] **Step 1: Add a clear visual system for the rebuilt studio**

Define top-level tokens such as:

```css
:root {
  --ai-ink: #1f3028;
  --ai-sand: #f4efe7;
  --ai-panel: #fffdf9;
  --ai-line: #d9cbb9;
  --ai-accent: #9f6b2f;
  --ai-danger: #9d3e33;
}
```

- [ ] **Step 2: Style the two-workspace layout and three-panel studio**

Add focused rules for:

```css
.ai-studio-page {}
.ai-studio-workspaces {}
.workspace-card {}
.studio-shell {}
.studio-category-nav {}
.studio-config-editor {}
.studio-live-preview {}
```

- [ ] **Step 3: Style the reseed controls and system-vs-editable separation**

Add classes for:

```css
.system-badge {}
.manual-badge {}
.reseed-warning {}
.stat-chip {}
```

- [ ] **Step 4: Style the interactive block preview**

Add classes for:

```css
.preview-bubble-ai {}
.preview-inline-block {}
.preview-chip-grid {}
.preview-generated-message {}
```

- [ ] **Step 5: Check the layout manually in desktop and mobile widths**

Run: `dotnet run --project WebHomestay/WebHomestay.csproj`

Expected: page loads and the admin AI screen remains readable at narrow widths.

- [ ] **Step 6: Commit the CSS rewrite**

```bash
git add WebHomestay/wwwroot/css/admin-ai-brain-center.css
git commit -m "feat: restyle admin ai configuration studio"
```

## Task 9: Wire Defaults and Backward-Safe Runtime Settings

**Files:**
- Modify: `WebHomestay/Program.cs`
- Modify: `WebHomestay/Services/ContextAwareBookingConductor.cs`
- Modify: `WebHomestay/Services/AI/SemanticKernelOrchestrator.cs`
- Test: `WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs`

- [ ] **Step 1: Write the failing test for default config bootstrap**

```csharp
[Fact]
public async Task GetAsync_ReturnsBlocksNeededForBranchDateGuestFlow()
{
    using var db = BuildDbContext();
    var service = new AdminAIStudioConfigService(db);

    var config = await service.GetAsync();

    Assert.Contains(config.InteractionBlocks, x => x.Type == "branchSelector");
    Assert.Contains(config.InteractionBlocks, x => x.Type == "datePicker");
    Assert.Contains(config.InteractionBlocks, x => x.Type == "guestCount");
}
```

- [ ] **Step 2: Run the targeted config test**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~GetAsync_ReturnsBlocksNeededForBranchDateGuestFlow`

Expected: FAIL if defaults are still sparse.

- [ ] **Step 3: Add rich default studio config values**

Bootstrap defaults in the service or `Program.cs` with patterns like:

```csharp
new AdminAIStudioBlock
{
    Type = "branchSelector",
    Label = "Chi nhánh",
    Description = "Cho khách chọn chi nhánh trực tiếp dưới tin nhắn AI.",
    SubmitBehavior = "compose_and_send",
    MessageTemplate = "Em chọn chi nhánh {{branchName}}.",
    Source = "system"
}
```

- [ ] **Step 4: Review runtime readers and keep them backward-safe for this phase**

Check if runtime still depends on old keys:

```powershell
rg -n "AIPublicBooking|AIBookingFormSchema|AIFinal" WebHomestay/Services -S
```

If needed, maintain fallback reading until customer chat integration is rebuilt later.

- [ ] **Step 5: Re-run the config tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIStudioConfigServiceTests`

Expected: PASS.

- [ ] **Step 6: Commit the defaults and fallback handling**

```bash
git add WebHomestay/Program.cs WebHomestay/Services/ContextAwareBookingConductor.cs WebHomestay/Services/AI/SemanticKernelOrchestrator.cs WebHomestay/Services/AI/AdminAIStudioConfigService.cs WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs
git commit -m "feat: add default admin ai studio behaviors"
```

## Task 10: Run Full Validation and Clean Up the Admin Surface

**Files:**
- Modify: `WebHomestay/Views/Shared/_Layout.cshtml`
- Modify: `WebHomestay/Views/AdminAI/Index.cshtml`
- Modify: `WebHomestay/wwwroot/js/admin-ai-brain-center.js`
- Modify: `WebHomestay/wwwroot/css/admin-ai-brain-center.css`
- Test: `WebHomestay.Tests/Services/AdminAIReseedServiceTests.cs`
- Test: `WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs`
- Test: `WebHomestay.Tests/Controllers/AdminAIControllerTests.cs`

- [ ] **Step 1: Run the focused AI admin test suite**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIReseedServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIStudioConfigServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIControllerTests
```

Expected: PASS on all three test groups.

- [ ] **Step 2: Run a project build**

Run: `dotnet build WebHomestay/WebHomestay.csproj`

Expected: `Build succeeded.`

- [ ] **Step 3: Run a manual admin smoke test**

Run: `dotnet run --project WebHomestay/WebHomestay.csproj`

Verify:

- `/admin/ai` only shows `Tri thức & Graph` and `Trả lời & Form`
- reseed action wipes and rebuilds the AI grounding dataset
- studio config saves and reloads
- preview panel updates when editing interaction blocks

- [ ] **Step 4: Final cleanup pass for dead references**

Run:

```powershell
rg -n "Test & Trace|Public Booking Config|Booking Form Designer|tab-overview|brain-traces|public-booking-config|booking-form-config" WebHomestay -S
```

Expected: no remaining active references in the rebuilt admin AI surface.

- [ ] **Step 5: Commit the validated rebuild**

```bash
git add WebHomestay/Controllers/AdminAIController.cs WebHomestay/Program.cs WebHomestay/Views/AdminAI/Index.cshtml WebHomestay/wwwroot/js/admin-ai-brain-center.js WebHomestay/wwwroot/css/admin-ai-brain-center.css WebHomestay/Views/Shared/_Layout.cshtml WebHomestay/Models/AI WebHomestay/Services/AI WebHomestay.Tests/Services/AdminAIReseedServiceTests.cs WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs WebHomestay.Tests/Controllers/AdminAIControllerTests.cs
git commit -m "feat: rebuild admin ai configuration studio"
```

## Self-Review

### Spec coverage

- Two-workspace admin IA: covered by Tasks 6, 7, 8.
- Remove legacy admin sections/routes: covered by Tasks 1, 5, 10.
- Truthful wipe-and-reseed from branch/room data: covered by Task 4.
- Editable post-seed knowledge/graph: preserved in Tasks 4 and 5 by retaining CRUD.
- Unified studio config contract: covered by Tasks 2 and 3.
- Preview-oriented response/form studio: covered by Tasks 6, 7, 8.
- Backward-safe runtime handling before customer UI work: covered by Task 9.
- Test coverage for reseed/config/controller: covered by Tasks 1, 3, 4, 5, 10.

### Placeholder scan

No `TODO`, `TBD`, or “implement later” placeholders remain in tasks.

### Type consistency

The plan consistently uses:

- `AdminAIStudioConfigRequest`
- `AdminAIStudioConfigResponse`
- `IAdminAIStudioConfigService`
- `IAdminAIReseedService`
- `AdminAIReseedResult`

These names are introduced before later tasks depend on them.
