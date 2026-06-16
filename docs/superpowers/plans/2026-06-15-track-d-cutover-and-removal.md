# Track D: Cutover And Legacy Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cut production traffic over to the new AI stack, preserve rollback safety during stabilization, and remove Semantic Kernel from the active runtime once the new path is proven.

**Architecture:** Introduce a compatibility switch first, run the new stack in controlled rollout, compare traces, then remove the old runtime path after stabilization. The user-facing controllers should not need to know which orchestrator is active.

**Tech Stack:** ASP.NET Core MVC, configuration-driven runtime selection, xUnit

---

## File Map

### Existing files to modify
- `WebHomestay/Program.cs`
- `WebHomestay/Controllers/AIChatController.cs`
- `WebHomestay/Controllers/AdminAIController.cs`
- `WebHomestay/Services/IAIBrainOrchestrator.cs`
- `WebHomestay/Services/AI/SemanticKernelOrchestrator.cs`
- `WebHomestay/Models/AIConversationTrace.cs`

### New files to create
- `WebHomestay/Services/AI/Compatibility/AIRuntimeSelector.cs`
- `WebHomestay/Services/AI/Compatibility/IAIRuntimeSelector.cs`
- `WebHomestay.Tests/Services/AIRuntimeSelectorTests.cs`
- `docs/note/ai-cutover-checklist.md`

---

### Task 1: Add Runtime Selector

**Files:**
- Create: `WebHomestay/Services/AI/Compatibility/IAIRuntimeSelector.cs`
- Create: `WebHomestay/Services/AI/Compatibility/AIRuntimeSelector.cs`
- Modify: `WebHomestay/Program.cs`

- [ ] **Step 1: Add selector interface**

```csharp
public interface IAIRuntimeSelector
{
    string GetActiveRuntime();
}
```

- [ ] **Step 2: Implement configuration-backed selector**

```csharp
public sealed class AIRuntimeSelector : IAIRuntimeSelector
{
    private readonly IConfiguration _configuration;

    public AIRuntimeSelector(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetActiveRuntime()
        => _configuration["AIModel:ActiveRuntime"] ?? "legacy";
}
```

- [ ] **Step 3: Register in DI**

```csharp
builder.Services.AddSingleton<IAIRuntimeSelector, AIRuntimeSelector>();
```

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build WebHomestay/WebHomestay.csproj
```

Expected:

```text
Build succeeded.
```

---

### Task 2: Add Runtime Trace Metadata

**Files:**
- Modify: `WebHomestay/Models/AIConversationTrace.cs`
- Modify: `WebHomestay/Services/AI/SemanticKernelOrchestrator.cs`
- Modify: `WebHomestay/Services/AI/Workflow/LangGraphOrchestrator.cs`

- [ ] **Step 1: Add runtime tag fields if missing**

Add:

```csharp
public string? RuntimeName { get; set; }
public string? RuntimeVersion { get; set; }
```

- [ ] **Step 2: Populate runtime fields from each orchestrator**

Legacy:

```csharp
trace.RuntimeName = "semantic-kernel";
trace.RuntimeVersion = "legacy";
```

New:

```csharp
trace.RuntimeName = "langgraph-style";
trace.RuntimeVersion = "v1";
```

- [ ] **Step 3: Build**

Run:

```powershell
dotnet build WebHomestay/WebHomestay.csproj
```

Expected:

```text
Build succeeded.
```

---

### Task 3: Add Controller-Level Compatibility Routing

**Files:**
- Modify: `WebHomestay/Controllers/AIChatController.cs`
- Modify: `WebHomestay/Controllers/AdminAIController.cs`

- [ ] **Step 1: Keep controller contract stable**

Controllers should still depend on `IAIBrainOrchestrator`; do not duplicate controller endpoints per runtime.

- [ ] **Step 2: Expose active runtime in debug/admin response**

For admin diagnostics, add runtime info to the relevant response payload:

```csharp
return Ok(new
{
    success = true,
    runtime = _runtimeSelector.GetActiveRuntime()
});
```

- [ ] **Step 3: Smoke build**

Run:

```powershell
dotnet build WebHomestay/WebHomestay.csproj
```

Expected:

```text
Build succeeded.
```

---

### Task 4: Add Cutover Tests

**Files:**
- Create: `WebHomestay.Tests/Services/AIRuntimeSelectorTests.cs`

- [ ] **Step 1: Add selector tests**

```csharp
[Fact]
public void GetActiveRuntime_DefaultsToLegacyWhenUnset()
{
}

[Fact]
public void GetActiveRuntime_ReturnsConfiguredRuntime()
{
}
```

- [ ] **Step 2: Run tests**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AIRuntimeSelectorTests
```

Expected:

```text
PASS
```

---

### Task 5: Write Rollout Checklist And Remove Legacy Path

**Files:**
- Create: `docs/note/ai-cutover-checklist.md`
- Modify: `WebHomestay/Program.cs`
- Modify: `WebHomestay/Services/AI/SemanticKernelOrchestrator.cs`

- [ ] **Step 1: Write the cutover checklist**

Include:
- retrieval tests green
- Neo4j graph tests green
- workflow tests green
- staged rollout validation complete
- rollback config verified

- [ ] **Step 2: Flip the default runtime after validation**

```csharp
"AIModel": {
  "ActiveRuntime": "langgraph"
}
```

- [ ] **Step 3: Remove Semantic Kernel from active DI registration**

Leave the file in the repo for one short deprecation window if needed, but do not keep it on the active runtime path.

- [ ] **Step 4: Final regression run**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AI
dotnet build WebHomestay/WebHomestay.csproj
```

Expected:

```text
Build succeeded and AI test slice passes.
```

- [ ] **Step 5: Commit**

```bash
git add WebHomestay/Services/AI/Compatibility WebHomestay/Program.cs WebHomestay/Controllers/AIChatController.cs WebHomestay/Controllers/AdminAIController.cs WebHomestay/Models/AIConversationTrace.cs WebHomestay.Tests/Services/AIRuntimeSelectorTests.cs docs/note/ai-cutover-checklist.md
git commit -m "refactor: cut over to new AI runtime"
```
