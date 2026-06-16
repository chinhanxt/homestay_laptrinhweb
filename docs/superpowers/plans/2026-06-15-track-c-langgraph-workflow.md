# Track C: LangGraph Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace plugin-centric AI orchestration with a LangGraph-style workflow runtime that explicitly models state, retrieval, graph expansion, booking truth, and response composition.

**Architecture:** Introduce a workflow core alongside the legacy orchestrator first. The new workflow is stateful, stage-driven, and uses adapters to the existing booking and retrieval services before full cutover.

**Tech Stack:** ASP.NET Core MVC, C#, workflow runtime abstractions, xUnit

---

## File Map

### Existing files to modify
- `WebHomestay/Services/AI/SemanticKernelOrchestrator.cs`
- `WebHomestay/Services/IAIBrainOrchestrator.cs`
- `WebHomestay/Program.cs`

### New files to create
- `WebHomestay/Services/AI/Workflow/WorkflowState.cs`
- `WebHomestay/Services/AI/Workflow/IWorkflowNode.cs`
- `WebHomestay/Services/AI/Workflow/WorkflowRunner.cs`
- `WebHomestay/Services/AI/Workflow/Nodes/IntentClassifierNode.cs`
- `WebHomestay/Services/AI/Workflow/Nodes/VectorRecallNode.cs`
- `WebHomestay/Services/AI/Workflow/Nodes/GraphExpansionNode.cs`
- `WebHomestay/Services/AI/Workflow/Nodes/BookingGuardNode.cs`
- `WebHomestay/Services/AI/Workflow/Nodes/ResponseComposerNode.cs`
- `WebHomestay/Services/AI/Workflow/LangGraphOrchestrator.cs`
- `WebHomestay.Tests/Services/LangGraphOrchestratorTests.cs`

---

### Task 1: Add Workflow Core Types

**Files:**
- Create: `WebHomestay/Services/AI/Workflow/WorkflowState.cs`
- Create: `WebHomestay/Services/AI/Workflow/IWorkflowNode.cs`
- Create: `WebHomestay/Services/AI/Workflow/WorkflowRunner.cs`

- [ ] **Step 1: Define state**

```csharp
public sealed class WorkflowState
{
    public string SessionId { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    public List<string> StageLog { get; } = new();
    public float[]? QueryEmbedding { get; set; }
    public string RetrievalJson { get; set; } = "[]";
    public string GraphJson { get; set; } = "{}";
    public string FinalAnswer { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Define node contract**

```csharp
public interface IWorkflowNode
{
    string Name { get; }
    Task ExecuteAsync(WorkflowState state, CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Build a simple runner**

Iterate nodes in order and append `Name` to `StageLog`.

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

### Task 2: Add Workflow Nodes

**Files:**
- Create: `.../IntentClassifierNode.cs`
- Create: `.../VectorRecallNode.cs`
- Create: `.../GraphExpansionNode.cs`
- Create: `.../BookingGuardNode.cs`
- Create: `.../ResponseComposerNode.cs`

- [ ] **Step 1: Intent node**

Use existing request text and booking mode heuristics.

- [ ] **Step 2: Vector recall node**

Call `IEmbeddingService` and `IVectorSearchService`, then serialize retrieval output.

- [ ] **Step 3: Graph expansion node**

Call the Neo4j graph expansion service and populate `GraphJson`.

- [ ] **Step 4: Booking guard node**

Call existing deterministic booking services and decide whether to short-circuit.

- [ ] **Step 5: Response composer node**

Use the AI model client and the structured context from prior nodes to produce the answer.

- [ ] **Step 6: Build**

Run:

```powershell
dotnet build WebHomestay/WebHomestay.csproj
```

Expected:

```text
Build succeeded.
```

---

### Task 3: Add LangGraph-Compatible Orchestrator

**Files:**
- Create: `WebHomestay/Services/AI/Workflow/LangGraphOrchestrator.cs`
- Modify: `WebHomestay/Program.cs`

- [ ] **Step 1: Implement the new orchestrator**

Make it implement `IAIBrainOrchestrator` and internally run:
- intent
- vector recall
- graph expansion
- booking guard
- response composition

- [ ] **Step 2: Register it in DI behind a feature toggle**

```csharp
var useWorkflow = builder.Configuration.GetValue<bool>("AIModel:UseWorkflowRuntime");
if (useWorkflow)
{
    builder.Services.AddScoped<IAIBrainOrchestrator, LangGraphOrchestrator>();
}
else
{
    builder.Services.AddScoped<IAIBrainOrchestrator, SemanticKernelOrchestrator>();
}
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

### Task 4: Add Workflow Tests

**Files:**
- Create: `WebHomestay.Tests/Services/LangGraphOrchestratorTests.cs`

- [ ] **Step 1: Add stage-order test**

```csharp
[Fact]
public async Task ChatAsync_RecordsWorkflowStagesInOrder()
{
    // assert intent -> retrieval -> graph -> guard -> composition
}
```

- [ ] **Step 2: Add deterministic short-circuit test**

```csharp
[Fact]
public async Task ChatAsync_WhenBookingGuardOwnsDecision_SkipsGenerationNode()
{
    // assert final answer comes from guard path
}
```

- [ ] **Step 3: Run workflow tests**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~LangGraphOrchestratorTests
```

Expected:

```text
PASS
```

---

### Task 5: Commit

- [ ] **Step 1: Commit**

```bash
git add WebHomestay/Services/AI/Workflow WebHomestay/Program.cs WebHomestay.Tests/Services/LangGraphOrchestratorTests.cs
git commit -m "feat: add LangGraph-style workflow runtime"
```

