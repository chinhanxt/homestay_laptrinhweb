# Admin AI Reply & Form Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild `/admin/ai` tab `Trả lời & Form` around flow-based sale-assist configuration, remove obsolete QR and JSON-heavy sections, and add a no-token runtime simulator.

**Architecture:** Keep the existing `AdminAIController` entrypoint, but replace the current flat studio config shape with a structured config model split into assistant profile, flows, fields, UI blocks, and runtime policies. Add a local runtime simulator service that evaluates config and session state without calling the LLM, then update the admin Razor view and `admin-ai-brain-center.js` to edit the new config and render simulator output.

**Tech Stack:** ASP.NET Core MVC, EF Core InMemory tests, System.Text.Json, Razor views, vanilla JavaScript, Bootstrap.

---

## File Structure

### Existing files to modify

- `WebHomestay/Controllers/AdminAIController.cs`
  - Keep existing knowledge/graph endpoints.
  - Update studio-config endpoints to return the new config shape.
  - Add simulator endpoint(s) for preset loading and state evaluation.
- `WebHomestay/Models/AI/AdminAIStudioConfig.cs`
  - Replace flat config sections with the new top-level model.
- `WebHomestay/Models/AI/AdminAIStudioRequests.cs`
  - Keep request/response wrappers, but point them at the new model.
- `WebHomestay/Services/AI/AdminAIStudioConfigService.cs`
  - Migrate old saved config into the new format.
  - Seed default sale-assist config.
  - Stop exposing `paymentQrRules`.
- `WebHomestay/Views/AdminAI/Index.cshtml`
  - Keep the tab shell.
  - Replace `Preview hội thoại` with `Runtime Simulator`.
- `WebHomestay/wwwroot/js/admin-ai-brain-center.js`
  - Rewrite category navigation, config editor, and preview logic for the new model.
- `WebHomestay/wwwroot/css/admin-ai-brain-center.css`
  - Style new cards, field lists, simulator panes, and CTA previews.
- `WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs`
  - Replace old assertions around `InteractionBlocks`, `PaymentQrRules`, and category names.

### New files to create

- `WebHomestay/Models/AI/AdminAIRuntimeSimulatorModels.cs`
  - DTOs for simulator state, preset, result, and UI directives.
- `WebHomestay/Services/AI/AdminAIRuntimeSimulatorService.cs`
  - Local rule engine for simulator evaluation.
- `WebHomestay/Services/AI/IAdminAIRuntimeSimulatorService.cs`
  - Interface used by controller and tests.
- `WebHomestay.Tests/Services/AdminAIRuntimeSimulatorServiceTests.cs`
  - Unit tests for presets, missing-field detection, room/slot/CTA/handoff directives.
- `WebHomestay.Tests/Controllers/AdminAIControllerTests.cs`
  - Endpoint-level tests for `studio-config` and simulator actions.

### Existing files to inspect while implementing

- `WebHomestay/Services/ContextAwareBookingConductor.cs`
  - Reuse naming for booking concepts so simulator terminology does not drift.
- `WebHomestay/Models/AI/AdminAIStudioField.cs`
  - Extend or retire depending on whether it can serve as the new field definition type.
- `WebHomestay/Models/AI/AdminAIStudioBlock.cs`
  - Extend or retire depending on whether it can serve as the new UI block type.
- `WebHomestay/Program.cs`
  - Register the simulator service.

## Task 1: Replace the Studio Config Model with the New Structured Shape

**Files:**
- Modify: `WebHomestay/Models/AI/AdminAIStudioConfig.cs`
- Modify: `WebHomestay/Models/AI/AdminAIStudioField.cs`
- Modify: `WebHomestay/Models/AI/AdminAIStudioBlock.cs`
- Modify: `WebHomestay/Models/AI/AdminAIStudioCondition.cs`
- Modify: `WebHomestay/Models/AI/AdminAIStudioRequests.cs`
- Test: `WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs`

- [x] **Step 1: Write the failing config-shape test**

```csharp
[Fact]
public async Task GetAsync_ReturnsStructuredDefaultConfig()
{
    await using var context = CreateContext();
    var service = new AdminAIStudioConfigService(context);

    var config = await service.GetAsync();

    Assert.Equal("Phong cách trả lời", config.Categories[0]);
    Assert.NotNull(config.AssistantProfile);
    Assert.Contains(config.ConversationFlows, flow => flow.Id == "hourly");
    Assert.Contains(config.ConversationFlows, flow => flow.Id == "daily");
    Assert.Contains(config.FieldDefinitions, field => field.Key == "branchId");
    Assert.DoesNotContain(config.Categories, name => name == "Thanh toán / QR");
}
```

- [x] **Step 2: Run the targeted test to verify it fails**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIStudioConfigServiceTests.GetAsync_ReturnsStructuredDefaultConfig`

Expected: FAIL because `AssistantProfile`, `ConversationFlows`, or `FieldDefinitions` do not exist yet.

- [x] **Step 3: Define the new config model**

```csharp
public class AdminAIStudioConfig
{
    public List<string> Categories { get; set; } = new();
    public AdminAIAssistantProfile AssistantProfile { get; set; } = new();
    public List<AdminAIConversationFlow> ConversationFlows { get; set; } = new();
    public List<AdminAIFieldDefinition> FieldDefinitions { get; set; } = new();
    public List<AdminAIUiBlockDefinition> UiBlockDefinitions { get; set; } = new();
    public AdminAIRuntimePolicies RuntimePolicies { get; set; } = new();
    public AdminAIHandoffConfig Handoff { get; set; } = new();
}

public class AdminAIAssistantProfile
{
    public string RolePrompt { get; set; } = string.Empty;
    public string Tone { get; set; } = string.Empty;
    public string MissingInfoPrompt { get; set; } = string.Empty;
    public string SafetyPrompt { get; set; } = string.Empty;
}
```

- [x] **Step 4: Extend field, block, and condition types to match the new spec**

```csharp
public class AdminAIFieldDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string InputType { get; set; } = string.Empty;
    public string SourceType { get; set; } = "manual";
    public bool IsSystem { get; set; } = true;
    public bool IsRequired { get; set; } = true;
    public string CaptureMode { get; set; } = "single-question";
    public string QuestionTemplate { get; set; } = string.Empty;
    public List<string> FlowScopes { get; set; } = new();
    public List<AdminAIStudioCondition> TriggerRules { get; set; } = new();
}
```

- [x] **Step 5: Run the targeted test to verify the model compiles and the test still fails at service defaults**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIStudioConfigServiceTests.GetAsync_ReturnsStructuredDefaultConfig`

Expected: FAIL at assertions about default values, not compile failure.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Models/AI/AdminAIStudioConfig.cs WebHomestay/Models/AI/AdminAIStudioField.cs WebHomestay/Models/AI/AdminAIStudioBlock.cs WebHomestay/Models/AI/AdminAIStudioCondition.cs WebHomestay/Models/AI/AdminAIStudioRequests.cs WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs
git commit -m "refactor: introduce structured admin ai studio config model"
```

## Task 2: Migrate AdminAIStudioConfigService to Default and Persist the New Model

**Files:**
- Modify: `WebHomestay/Services/AI/AdminAIStudioConfigService.cs`
- Modify: `WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs`

- [x] **Step 1: Add failing tests for migration and QR removal**

```csharp
[Fact]
public async Task GetAsync_MigratesLegacyConfig_IntoStructuredConfig()
{
    await using var context = CreateContext();
    context.SystemSettings.Add(new SystemSetting
    {
        SettingKey = AdminAIStudioConfigService.SettingKey,
        GroupName = "AI",
        SettingValue = """{"categories":["Phong cách & Prompt"],"responseStyle":{"tone":"Cu"}}"""
    });
    await context.SaveChangesAsync();

    var service = new AdminAIStudioConfigService(context);
    var config = await service.GetAsync();

    Assert.Equal("Cu", config.AssistantProfile.Tone);
    Assert.DoesNotContain(config.Categories, name => name == "Thanh toán / QR");
}
```

- [x] **Step 2: Run the config service tests to verify failure**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIStudioConfigServiceTests`

Expected: FAIL because old JSON does not map into the new shape yet.

- [x] **Step 3: Implement default new config and legacy migration**

```csharp
private static AdminAIStudioConfigResponse BuildDefaultConfig()
{
    return new AdminAIStudioConfigResponse
    {
        Categories =
        [
            "Phong cách trả lời",
            "Flow hội thoại",
            "Kho field",
            "Khối giao diện",
            "Quy tắc hiển thị & chốt",
            "Handoff người thật"
        ],
        AssistantProfile = new AdminAIAssistantProfile
        {
            RolePrompt = "Bạn là AI sale-assist hỗ trợ khách tìm và đặt phòng.",
            Tone = "Thân thiện, rõ ràng, tư vấn như nhân viên sale.",
            MissingInfoPrompt = "Hỏi đúng thông tin còn thiếu và ưu tiên block nhập liệu phù hợp.",
            SafetyPrompt = "Không xác nhận booking, không thanh toán, không bịa dữ liệu."
        }
    };
}
```

- [x] **Step 4: Persist the new model and keep only the required legacy setting sync**

```csharp
private async Task SyncLegacySettingsAsync(AdminAIStudioConfigRequest request)
{
    await UpsertLegacySettingAsync("AIFinalSynthesizerStyle", request.AssistantProfile?.Tone ?? string.Empty, "Phong cách trả lời AI");
    await UpsertLegacySettingAsync("AIFinalBasePrompt", request.AssistantProfile?.RolePrompt ?? string.Empty, "Vai trò AI");
    await UpsertLegacySettingAsync("AIFinalMissingInfoRule", request.AssistantProfile?.MissingInfoPrompt ?? string.Empty, "Quy tắc hỏi thông tin thiếu");
}
```

- [x] **Step 5: Run the full config service test class and verify pass**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIStudioConfigServiceTests`

Expected: PASS with updated expectations around categories and saved JSON.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Services/AI/AdminAIStudioConfigService.cs WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs
git commit -m "feat: migrate admin ai studio service to structured config"
```

## Task 3: Add the Local Runtime Simulator Service

**Files:**
- Create: `WebHomestay/Models/AI/AdminAIRuntimeSimulatorModels.cs`
- Create: `WebHomestay/Services/AI/IAdminAIRuntimeSimulatorService.cs`
- Create: `WebHomestay/Services/AI/AdminAIRuntimeSimulatorService.cs`
- Modify: `WebHomestay/Program.cs`
- Test: `WebHomestay.Tests/Services/AdminAIRuntimeSimulatorServiceTests.cs`

- [x] **Step 1: Write the failing simulator tests**

```csharp
[Fact]
public async Task SimulateAsync_NewConversation_ShowsBookingModeChoice()
{
    var service = new AdminAIRuntimeSimulatorService();
    var result = await service.SimulateAsync(BuildDefaultConfig(), new AdminAIRuntimeState());

    Assert.Equal("entry", result.ConversationState.Stage);
    Assert.Contains(result.UiDirectives, item => item.Type == "bookingModeChoice");
}
```

- [x] **Step 2: Run the simulator tests to verify failure**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIRuntimeSimulatorServiceTests`

Expected: FAIL because simulator types and service do not exist.

- [x] **Step 3: Create simulator DTOs**

```csharp
public class AdminAIRuntimeState
{
    public string FlowId { get; set; } = string.Empty;
    public string LastCustomerMessage { get; set; } = string.Empty;
    public Dictionary<string, string> CapturedFields { get; set; } = new();
    public bool NeedHumanHandoff { get; set; }
}

public class AdminAIRuntimeSimulationResult
{
    public AdminAIRuntimeConversationState ConversationState { get; set; } = new();
    public string AssistantReply { get; set; } = string.Empty;
    public List<AdminAIUiDirective> UiDirectives { get; set; } = new();
    public List<string> NextActions { get; set; } = new();
}
```

- [x] **Step 4: Implement a minimal local rule engine**

```csharp
public Task<AdminAIRuntimeSimulationResult> SimulateAsync(AdminAIStudioConfig config, AdminAIRuntimeState state)
{
    var result = new AdminAIRuntimeSimulationResult();

    if (string.IsNullOrWhiteSpace(state.FlowId))
    {
        result.ConversationState.Stage = "entry";
        result.AssistantReply = "Bạn muốn đặt theo giờ hay theo ngày ạ?";
        result.UiDirectives.Add(new AdminAIUiDirective { Type = "bookingModeChoice", Label = "Chọn hình thức đặt" });
        result.NextActions.Add("wait_for_booking_mode");
        return Task.FromResult(result);
    }

    return Task.FromResult(result);
}
```

- [x] **Step 5: Register the simulator service and run tests**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIRuntimeSimulatorServiceTests
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: PASS simulator tests and successful web project build.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Models/AI/AdminAIRuntimeSimulatorModels.cs WebHomestay/Services/AI/IAdminAIRuntimeSimulatorService.cs WebHomestay/Services/AI/AdminAIRuntimeSimulatorService.cs WebHomestay/Program.cs WebHomestay.Tests/Services/AdminAIRuntimeSimulatorServiceTests.cs
git commit -m "feat: add admin ai runtime simulator service"
```

## Task 4: Add Presets, Handoff, Slot, Room, and CTA Decisions to the Simulator

**Files:**
- Modify: `WebHomestay/Services/AI/AdminAIRuntimeSimulatorService.cs`
- Modify: `WebHomestay/Models/AI/AdminAIRuntimeSimulatorModels.cs`
- Test: `WebHomestay.Tests/Services/AdminAIRuntimeSimulatorServiceTests.cs`

- [x] **Step 1: Add failing tests for flow-specific directives**

```csharp
[Fact]
public async Task SimulateAsync_HourlyFlowWithBranchButMissingDate_ShowsDatePicker()
{
    var service = new AdminAIRuntimeSimulatorService();
    var state = new AdminAIRuntimeState
    {
        FlowId = "hourly",
        CapturedFields = new Dictionary<string, string> { ["branchId"] = "1" }
    };

    var result = await service.SimulateAsync(BuildDefaultConfig(), state);

    Assert.Contains(result.UiDirectives, item => item.Type == "singleDatePicker");
}
```

- [x] **Step 2: Run the simulator tests and verify failure**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIRuntimeSimulatorServiceTests`

Expected: FAIL because the minimal simulator only handles entry state.

- [x] **Step 3: Implement presets and missing-field resolution**

```csharp
private static readonly Dictionary<string, Action<AdminAIRuntimeState>> Presets = new()
{
    ["new-chat"] = state => { },
    ["hourly-missing-date"] = state =>
    {
        state.FlowId = "hourly";
        state.CapturedFields["branchId"] = "1";
    }
};
```

- [x] **Step 4: Implement directive rules for slot grid, room cards, booking CTA, and handoff**

```csharp
if (state.NeedHumanHandoff)
{
    result.AssistantReply = "Mình sẽ nối bạn với chi nhánh phù hợp nhé.";
    result.UiDirectives.Add(new AdminAIUiDirective { Type = "handoffContact", Label = "Liên hệ chi nhánh" });
    result.NextActions.Add("collect_customer_phone");
    return result;
}

if (state.FlowId == "hourly" && Has(state, "branchId") && Has(state, "hourlyDate") && !Has(state, "hourlySlot"))
{
    result.UiDirectives.Add(new AdminAIUiDirective { Type = "timeSlotGrid", Label = "Khung giờ trống" });
}
```

- [x] **Step 5: Run the full simulator suite**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIRuntimeSimulatorServiceTests`

Expected: PASS for entry, hourly, daily, handoff, and CTA cases.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Services/AI/AdminAIRuntimeSimulatorService.cs WebHomestay/Models/AI/AdminAIRuntimeSimulatorModels.cs WebHomestay.Tests/Services/AdminAIRuntimeSimulatorServiceTests.cs
git commit -m "feat: expand admin ai simulator directives and presets"
```

## Task 5: Expose the New Studio Config and Simulator from AdminAIController

**Files:**
- Modify: `WebHomestay/Controllers/AdminAIController.cs`
- Create: `WebHomestay.Tests/Controllers/AdminAIControllerTests.cs`

- [x] **Step 1: Write the failing controller tests**

```csharp
[Fact]
public async Task GetStudioConfig_ReturnsStructuredConfig()
{
    var controller = BuildController();
    var result = await controller.GetStudioConfig();

    var ok = Assert.IsType<OkObjectResult>(result);
    var payload = Assert.IsType<AdminAIStudioConfigResponse>(ok.Value);
    Assert.NotEmpty(payload.ConversationFlows);
}
```

- [x] **Step 2: Run the controller tests to verify failure**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIControllerTests`

Expected: FAIL because the test fixture or simulator endpoints do not exist yet.

- [x] **Step 3: Inject the simulator service and add simulator actions**

```csharp
[HttpPost("studio-simulator/run")]
public async Task<IActionResult> RunStudioSimulator([FromBody] AdminAIRuntimeState request)
{
    var config = await _studioConfigService.GetAsync();
    var result = await _runtimeSimulatorService.SimulateAsync(config, request);
    return Ok(result);
}
```

- [x] **Step 4: Add a preset endpoint for quick scenario loading**

```csharp
[HttpGet("studio-simulator/presets")]
public IActionResult GetStudioSimulatorPresets()
{
    return Ok(AdminAIRuntimePresetCatalog.All);
}
```

- [x] **Step 5: Run controller tests and targeted regression tests**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIControllerTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIStudioConfigServiceTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Controllers/AdminAIController.cs WebHomestay.Tests/Controllers/AdminAIControllerTests.cs
git commit -m "feat: add admin ai studio simulator endpoints"
```

## Task 6: Rebuild the Admin AI Razor View Around the New Categories and Simulator

**Files:**
- Modify: `WebHomestay/Views/AdminAI/Index.cshtml`
- Modify: `WebHomestay/wwwroot/css/admin-ai-brain-center.css`

- [x] **Step 1: Add a view regression test note and snapshot checklist**

```text
Manual UI assertions:
- Left nav shows 6 categories only
- No "Thanh toán / QR"
- Right pane heading reads "Runtime Simulator"
- Preset buttons and state editor are visible
```

- [x] **Step 2: Update the Razor markup**

```cshtml
<aside class="studio-preview">
    <div class="studio-preview-head">
        <h3>Runtime Simulator</h3>
        <p>Chạy rule engine local để test flow, field và block mà không tốn token.</p>
    </div>
    <div id="studio-simulator" class="studio-simulator"></div>
</aside>
```

- [x] **Step 3: Add CSS hooks for simulator, field cards, and config grids**

```css
.studio-simulator {
    display: grid;
    gap: 16px;
}

.simulator-preset-grid,
.field-definition-list,
.flow-card-list {
    display: grid;
    gap: 12px;
}
```

- [x] **Step 4: Build and visually inspect the page**

Run:

```powershell
dotnet build WebHomestay/WebHomestay.csproj
dotnet run --project WebHomestay/WebHomestay.csproj
```

Expected: `/admin/ai` loads with the new headings and no QR preview in the `Trả lời & Form` tab.

- [ ] **Step 5: Commit**

```bash
git add WebHomestay/Views/AdminAI/Index.cshtml WebHomestay/wwwroot/css/admin-ai-brain-center.css
git commit -m "feat: redesign admin ai studio shell for runtime simulator"
```

## Task 7: Rewrite admin-ai-brain-center.js for Structured Editing and Simulation

**Files:**
- Modify: `WebHomestay/wwwroot/js/admin-ai-brain-center.js`

- [x] **Step 1: Write a manual browser checklist before refactor**

```text
Interaction checks:
- Switching categories preserves unsaved local edits until save
- Save posts the structured config payload
- Clicking a preset loads state into the simulator
- Clicking "Chạy mô phỏng" refreshes assistantReply, uiDirectives, nextActions
- No client code references paymentQrRules or renderPaymentQr
```

- [x] **Step 2: Replace old category templates and JSON editors with structured renderers**

```javascript
const studioCategories = {
    "Phong cách trả lời": renderAssistantProfileEditor,
    "Flow hội thoại": renderConversationFlowsEditor,
    "Kho field": renderFieldDefinitionsEditor,
    "Khối giao diện": renderUiBlocksEditor,
    "Quy tắc hiển thị & chốt": renderRuntimePoliciesEditor,
    "Handoff người thật": renderHandoffEditor
};
```

- [x] **Step 3: Add simulator state, preset loading, and run actions**

```javascript
const simulatorState = {
    presets: [],
    state: { flowId: "", lastCustomerMessage: "", capturedFields: {}, needHumanHandoff: false },
    result: null
};

async function runStudioSimulator() {
    syncStudioInputs();
    const response = await fetch("/admin/ai/studio-simulator/run", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(simulatorState.state)
    });
    simulatorState.result = await response.json();
    renderStudioSimulator();
}
```

- [x] **Step 4: Remove dead QR and old preview code paths**

```javascript
// Delete:
// - renderPreviewBlock()
// - renderPreviewField()
// - paymentQrRules reads
// - categories for "Thanh toán / QR", "Checklist thu thập thông tin", "Hiển thị ngày"
```

- [x] **Step 5: Run manual verification in browser**

Run:

```powershell
dotnet run --project WebHomestay/WebHomestay.csproj
```

Expected:
- `Trả lời & Form` only shows the new 6 categories.
- Simulator presets and action buttons work without network calls to the LLM provider.
- Saving config still succeeds.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/wwwroot/js/admin-ai-brain-center.js
git commit -m "feat: rebuild admin ai studio editor and simulator ui"
```

## Task 8: Run Full Regression and Document the Result

**Files:**
- Modify: `docs/superpowers/specs/2026-06-10-admin-ai-reply-form-design.md`
  - Only if implementation reveals a required spec clarification.
- Modify: `docs/superpowers/plans/2026-06-10-admin-ai-reply-form.md`
  - Check boxes during execution, do not change scope.

- [x] **Step 1: Run the backend test classes**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIStudioConfigServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIRuntimeSimulatorServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIControllerTests
```

Expected: PASS.

- [x] **Step 2: Run the full test suite**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj`

Expected: PASS with no regressions in unrelated booking tests.

- [x] **Step 3: Run the web app and verify the admin workflow manually**

Run: `dotnet run --project WebHomestay/WebHomestay.csproj`

Expected manual pass:
- `/admin/ai` loads
- `Trả lời & Form` saves config
- Simulator loads presets
- Simulator shows booking mode, date, slot, room, handoff, and CTA cases
- No QR config or QR preview appears in this tab

- [ ] **Step 4: Commit the finished implementation**

```bash
git add WebHomestay/Controllers/AdminAIController.cs WebHomestay/Models/AI/*.cs WebHomestay/Services/AI/*.cs WebHomestay/Views/AdminAI/Index.cshtml WebHomestay/wwwroot/js/admin-ai-brain-center.js WebHomestay/wwwroot/css/admin-ai-brain-center.css WebHomestay.Tests/Controllers/AdminAIControllerTests.cs WebHomestay.Tests/Services/AdminAIStudioConfigServiceTests.cs WebHomestay.Tests/Services/AdminAIRuntimeSimulatorServiceTests.cs
git commit -m "feat: redesign admin ai reply and form studio"
```

## Self-Review

### Spec coverage

- Config redesign: covered by Tasks 1-2.
- Simulator no-token flow: covered by Tasks 3-4 and Task 7.
- Controller/runtime contract: covered by Task 5.
- Admin UI rebuild: covered by Tasks 6-7.
- Migration and QR removal: covered by Task 2.
- Testing and regression safety: covered by Task 8.

### Placeholder scan

- No `TODO`, `TBD`, or “implement later” placeholders remain.
- Each task includes exact files, commands, and code examples.

### Type consistency

- `AssistantProfile`, `ConversationFlows`, `FieldDefinitions`, `UiBlockDefinitions`, `RuntimePolicies`, and simulator DTO names are used consistently across tasks.
- Endpoint names stay aligned with `/admin/ai/studio-simulator/*`.
