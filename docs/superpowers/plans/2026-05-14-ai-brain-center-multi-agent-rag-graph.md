# AI Brain Center Multi-Agent RAG+Graph Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** `docs/superpowers/specs/2026-05-14-ai-brain-center-multi-agent-rag-graph.md`  
**Goal:** Build the AI Brain Center as a standout Admin feature: a multi-agent Hospitality Brain that combines live system data, scoped knowledge, graph relationships, persona analysis, guard validation, traceable reasoning, and a premium Admin UI.  
**Architecture:** ASP.NET Core MVC + Entity Framework Core + PostgreSQL + Razor Views + Bootstrap/jQuery + phased mock-to-real AI runtime.  
**Principle:** Keep the ambitious multi-agent RAG+Graph concept visible from Phase 1. Do not reduce this into a generic FAQ chatbot.

---

## Phase 0: Pre-Implementation Alignment

**Purpose:** Confirm current code structure before modifying files.

**Files to inspect:**
- `WebHomestay/Data/ApplicationDbContext.cs`
- `WebHomestay/Models/`
- `WebHomestay/Controllers/`
- `WebHomestay/Views/AdminSettings/Index.cshtml`
- Existing AI-related CSS/JS files under `WebHomestay/wwwroot/`

- [ ] **Step 0.1: Inspect existing Admin AI tab**
  - Find the current AI configuration/AI Booking Consultant UI.
  - Identify whether the AI page is inside `AdminSettings/Index.cshtml` or a dedicated view.
  - Verify existing IDs/classes so the new UI does not break unrelated Admin tabs.

- [ ] **Step 0.2: Inspect existing AI Knowledge Hub entities**
  - Check whether `AIKnowledgeCollection`, `AIKnowledgeArticle`, or similar models already exist.
  - Decide whether to extend existing entities or create new `AIBrain*` entities.
  - Prefer extending only if it does not flatten the new Brain Center concept.

- [ ] **Step 0.3: Verify build baseline**
  - Run the project build before changes.
  - Record any pre-existing errors separately.

**Success criteria:** Current AI/Admin structure is understood and the implementation path avoids duplicate models/controllers where possible.

---

## Phase 1: Data Model Foundation

**Purpose:** Add the database shape for scoped brain knowledge, graph relationships, agent definitions, and trace logs.

**Files:**
- Create: `WebHomestay/Models/AIBrainScope.cs`
- Create: `WebHomestay/Models/AIKnowledgeUnit.cs`
- Create: `WebHomestay/Models/AIGraphNode.cs`
- Create: `WebHomestay/Models/AIGraphEdge.cs`
- Create: `WebHomestay/Models/AIAgentDefinition.cs`
- Create: `WebHomestay/Models/AIConversationTrace.cs`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`

- [ ] **Step 1.1: Create `AIBrainScope` model**

```csharp
using System.ComponentModel.DataAnnotations;

namespace WebHomestay.Models
{
    public class AIBrainScope
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(40)]
        public string ScopeType { get; set; } = "Global";

        public int? ScopeRefId { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<AIKnowledgeUnit> KnowledgeUnits { get; set; } = new List<AIKnowledgeUnit>();
        public ICollection<AIGraphNode> GraphNodes { get; set; } = new List<AIGraphNode>();
        public ICollection<AIGraphEdge> GraphEdges { get; set; } = new List<AIGraphEdge>();
    }
}
```

- [ ] **Step 1.2: Create `AIKnowledgeUnit` model**

```csharp
using System.ComponentModel.DataAnnotations;

namespace WebHomestay.Models
{
    public class AIKnowledgeUnit
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid BrainScopeId { get; set; }
        public AIBrainScope? BrainScope { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        [MaxLength(60)]
        public string KnowledgeType { get; set; } = "FAQ";

        public int Priority { get; set; } = 50;

        public string TagsJson { get; set; } = "[]";

        public DateTime? EffectiveFrom { get; set; }

        public DateTime? EffectiveTo { get; set; }

        [Required]
        [MaxLength(40)]
        public string Status { get; set; } = "Draft";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 1.3: Create `AIGraphNode` model**

```csharp
using System.ComponentModel.DataAnnotations;

namespace WebHomestay.Models
{
    public class AIGraphNode
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid BrainScopeId { get; set; }
        public AIBrainScope? BrainScope { get; set; }

        [Required]
        [MaxLength(60)]
        public string NodeType { get; set; } = "Policy";

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(800)]
        public string? Description { get; set; }

        [MaxLength(80)]
        public string? ReferenceType { get; set; }

        public int? ReferenceId { get; set; }

        public string MetadataJson { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 1.4: Create `AIGraphEdge` model**

```csharp
using System.ComponentModel.DataAnnotations;

namespace WebHomestay.Models
{
    public class AIGraphEdge
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid BrainScopeId { get; set; }
        public AIBrainScope? BrainScope { get; set; }

        public Guid SourceNodeId { get; set; }
        public AIGraphNode? SourceNode { get; set; }

        public Guid TargetNodeId { get; set; }
        public AIGraphNode? TargetNode { get; set; }

        [Required]
        [MaxLength(80)]
        public string EdgeType { get; set; } = "RELATED_TO";

        public double Weight { get; set; } = 1.0;

        public string MetadataJson { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 1.5: Create `AIAgentDefinition` model**

```csharp
using System.ComponentModel.DataAnnotations;

namespace WebHomestay.Models
{
    public class AIAgentDefinition
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(80)]
        public string AgentKey { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(800)]
        public string RoleDescription { get; set; } = string.Empty;

        public string SystemInstruction { get; set; } = string.Empty;

        public string AllowedToolsJson { get; set; } = "[]";

        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 1.6: Create `AIConversationTrace` model**

```csharp
using System.ComponentModel.DataAnnotations;

namespace WebHomestay.Models
{
    public class AIConversationTrace
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(120)]
        public string ConversationId { get; set; } = string.Empty;

        public string UserMessage { get; set; } = string.Empty;

        public string FinalResponse { get; set; } = string.Empty;

        public Guid? SelectedScopeId { get; set; }

        public string TraceJson { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 1.7: Register DbSets in `ApplicationDbContext`**

```csharp
public DbSet<AIBrainScope> AIBrainScopes { get; set; }
public DbSet<AIKnowledgeUnit> AIKnowledgeUnits { get; set; }
public DbSet<AIGraphNode> AIGraphNodes { get; set; }
public DbSet<AIGraphEdge> AIGraphEdges { get; set; }
public DbSet<AIAgentDefinition> AIAgentDefinitions { get; set; }
public DbSet<AIConversationTrace> AIConversationTraces { get; set; }
```

- [ ] **Step 1.8: Add EF relationship configuration if needed**
  - Configure graph edge relationships explicitly if EF reports ambiguous cascade paths.
  - Prefer `OnDelete(DeleteBehavior.Restrict)` for `AIGraphEdge.SourceNode` and `AIGraphEdge.TargetNode`.

- [ ] **Step 1.9: Create and apply migration**
  - Create an EF migration named `AddAIBrainCenter`.
  - Apply it to the local PostgreSQL database if this project normally uses local migrations during development.

- [ ] **Step 1.10: Verify build**
  - Run build.
  - Fix model/DbContext compile errors only.

**Success criteria:** Database has the core AI Brain Center tables and the application builds successfully.

---

## Phase 2: Seed Default Brain and Agent Definitions

**Purpose:** Make the Admin UI immediately feel like a multi-agent system even before real LLM integration.

**Files:**
- Create or modify a seed location used by the project.
- If no seed pattern exists, seed from an Admin service/controller initialization path carefully.

- [ ] **Step 2.1: Seed default brain scopes**
  - Global Brain.
  - Optional branch scopes for existing branches.
  - Optional room scopes for high-value rooms only if easy to derive.

- [ ] **Step 2.2: Seed default agent definitions**

Agent keys:

```text
orchestrator
live-system
knowledge-brain
customer-persona
safety-guard
response-synthesizer
```

Display names:

```text
AI A - Orchestrator Agent
AI B - Live System Agent
AI C - Knowledge Brain Agent
AI D - Customer Persona Agent
AI Guard - Safety/Policy Agent
AI E - Final Response Synthesizer
```

- [ ] **Step 2.3: Seed starter knowledge units**
  - “Quy tắc dùng dữ liệu thật cho phòng trống”.
  - “Phong cách tư vấn lễ tân homestay”.
  - “Nguyên tắc không bịa giá hoặc trạng thái phòng”.

- [ ] **Step 2.4: Verify no duplicate seed records**
  - Use stable `AgentKey` and `ScopeType` checks.
  - Do not create duplicate agents on every app start.

**Success criteria:** Fresh database contains Global Brain and the six named agents.

---

## Phase 3: Backend ViewModels and DTOs

**Purpose:** Keep controller logic clean and provide structured data for the AJAX Admin UI.

**Files:**
- Create: `WebHomestay/ViewModels/AIBrain/AIBrainOverviewViewModel.cs`
- Create: `WebHomestay/ViewModels/AIBrain/AIKnowledgeUnitViewModel.cs`
- Create: `WebHomestay/ViewModels/AIBrain/AIGraphViewModels.cs`
- Create: `WebHomestay/ViewModels/AIBrain/AIAgentStatusViewModel.cs`
- Create: `WebHomestay/ViewModels/AIBrain/AIPlaygroundViewModels.cs`

- [ ] **Step 3.1: Create overview view model**

Fields:

```text
TotalScopes
TotalKnowledgeUnits
TotalGraphNodes
TotalGraphEdges
TotalAgents
EnabledAgents
LastTraceAt
Scopes
AgentStatuses
```

- [ ] **Step 3.2: Create knowledge unit request/response models**

Fields:

```text
Id
BrainScopeId
Title
Content
KnowledgeType
Priority
Tags
EffectiveFrom
EffectiveTo
Status
```

- [ ] **Step 3.3: Create graph view models**

Models:

```text
AIGraphNodeDto
AIGraphEdgeDto
AIGraphResponse
SaveGraphNodeRequest
SaveGraphEdgeRequest
```

- [ ] **Step 3.4: Create agent status view model**

Fields:

```text
AgentKey
DisplayName
RoleDescription
Status
LastRunLabel
IsEnabled
```

- [ ] **Step 3.5: Create playground view models**

Models:

```text
AIPlaygroundRequest
AIPlaygroundResult
AIAgentTraceStep
AISuggestedAction
```

**Success criteria:** Controllers can return strongly shaped data without exposing EF entities directly to the frontend.

---

## Phase 4: Multi-Agent Service Skeleton

**Purpose:** Implement the multi-agent runtime shape with deterministic mock logic first.

**Files:**
- Create: `WebHomestay/Services/AIBrain/AIBrainOrchestratorService.cs`
- Create: `WebHomestay/Services/AIBrain/AILiveSystemAgentService.cs`
- Create: `WebHomestay/Services/AIBrain/AIKnowledgeBrainService.cs`
- Create: `WebHomestay/Services/AIBrain/AICustomerPersonaService.cs`
- Create: `WebHomestay/Services/AIBrain/AISafetyGuardService.cs`
- Create: `WebHomestay/Services/AIBrain/AIResponseSynthesizerService.cs`
- Create: `WebHomestay/Services/AIBrain/AIBrainTraceService.cs`
- Modify: `WebHomestay/Program.cs`

- [ ] **Step 4.1: Create orchestrator service**
  - Method: `Task<AIPlaygroundResult> RunPlaygroundAsync(AIPlaygroundRequest request)`.
  - Analyze message with keyword-based intent detection for now.
  - Call other services in the same conceptual order as the spec.
  - Always return agent trace steps.

- [ ] **Step 4.2: Create Live System Agent service**
  - Method: `Task<object> GetLiveContextAsync(AIPlaygroundRequest request)`.
  - Query existing room/branch/booking data if straightforward.
  - If availability logic is complex, return a clearly labeled mock in Phase 4 and wire real logic in Phase 8.

- [ ] **Step 4.3: Create Knowledge Brain service**
  - Method: `Task<object> SearchKnowledgeAsync(AIPlaygroundRequest request)`.
  - Search `AIKnowledgeUnits` by scope, title, content, tags, and status.
  - Order by exact scope match, priority descending, then updated time.

- [ ] **Step 4.4: Create Customer Persona service**
  - Method: `object AnalyzePersona(string message)`.
  - Use deterministic rules:
    - “2 người”, “cặp đôi”, “riêng tư” → `CoupleWeekend`.
    - “gia đình”, “trẻ em” → `FamilyStay`.
    - “công tác” → `BusinessTraveler`.

- [ ] **Step 4.5: Create Safety Guard service**
  - Method: `object ValidateResponseContext(...)`.
  - Block or warn if final response tries to confirm availability without Live System data.
  - Add trace status: `Approved`, `NeedsRevision`, or `Blocked`.

- [ ] **Step 4.6: Create Response Synthesizer service**
  - Method: `string ComposeResponse(...)`.
  - Combine live context + knowledge context + persona result.
  - Keep response in Vietnamese.
  - Include suggested next action labels when appropriate.

- [ ] **Step 4.7: Create Trace service**
  - Save `AIConversationTrace` with `TraceJson`.
  - Use a generated `ConversationId` for playground tests.

- [ ] **Step 4.8: Register services in DI**

```csharp
builder.Services.AddScoped<AIBrainOrchestratorService>();
builder.Services.AddScoped<AILiveSystemAgentService>();
builder.Services.AddScoped<AIKnowledgeBrainService>();
builder.Services.AddScoped<AICustomerPersonaService>();
builder.Services.AddScoped<AISafetyGuardService>();
builder.Services.AddScoped<AIResponseSynthesizerService>();
builder.Services.AddScoped<AIBrainTraceService>();
```

**Success criteria:** A playground request can run through all named agents and return a trace even without external AI API.

---

## Phase 5: Admin Controllers and JSON API

**Purpose:** Expose AI Brain Center data to the Admin UI.

**Files:**
- Create: `WebHomestay/Controllers/AdminAIBrainController.cs`
- Create: `WebHomestay/Controllers/AIBrainApiController.cs`

- [ ] **Step 5.1: Create `AdminAIBrainController`**

Actions:

```text
Index()
Overview()
KnowledgeStudio()
GraphBuilder()
AgentCommandCenter()
Playground()
TrainingQueue()
```

- [ ] **Step 5.2: Create `AIBrainApiController` route**

Route base:

```csharp
[Route("admin/ai-brain/api")]
```

- [ ] **Step 5.3: Implement overview endpoint**

```text
GET /admin/ai-brain/api/overview
```

Return counts, scopes, and agent statuses.

- [ ] **Step 5.4: Implement scope endpoints**

```text
GET /admin/ai-brain/api/scopes
POST /admin/ai-brain/api/scopes
```

- [ ] **Step 5.5: Implement knowledge endpoints**

```text
GET /admin/ai-brain/api/knowledge?scopeId={id}
GET /admin/ai-brain/api/knowledge/{id}
POST /admin/ai-brain/api/knowledge
DELETE /admin/ai-brain/api/knowledge/{id}
```

- [ ] **Step 5.6: Implement graph endpoints**

```text
GET /admin/ai-brain/api/graph?scopeId={id}
POST /admin/ai-brain/api/graph/nodes
POST /admin/ai-brain/api/graph/edges
DELETE /admin/ai-brain/api/graph/nodes/{id}
DELETE /admin/ai-brain/api/graph/edges/{id}
```

- [ ] **Step 5.7: Implement agent endpoints**

```text
GET /admin/ai-brain/api/agents
POST /admin/ai-brain/api/agents/{id}/toggle
```

- [ ] **Step 5.8: Implement playground endpoint**

```text
POST /admin/ai-brain/api/playground/run
```

- [ ] **Step 5.9: Implement trace endpoint**

```text
GET /admin/ai-brain/api/traces/recent
```

**Success criteria:** API endpoints return JSON for all Admin Brain Center panels.

---

## Phase 6: Admin UI Shell

**Purpose:** Build the premium AI Brain Center interface where the concept is immediately visible.

**Files:**
- Create: `WebHomestay/Views/AdminAIBrain/Index.cshtml`
- Create: `WebHomestay/wwwroot/css/admin-ai-brain.css`
- Create: `WebHomestay/wwwroot/js/admin-ai-brain.js`
- Modify: Admin navigation/sidebar file if needed.

- [ ] **Step 6.1: Add Admin navigation entry**
  - Label: `AI Brain Center`.
  - Icon: brain/robot/network icon consistent with existing FontAwesome usage.
  - Route to `AdminAIBrainController.Index`.

- [ ] **Step 6.2: Create page header**

Copy direction:

```text
AI Brain Center
Bộ não AI vận hành, tư vấn và hỗ trợ đặt phòng cho toàn hệ thống.
```

- [ ] **Step 6.3: Create layout grid**

Sections:

```text
Brain Scope sidebar
Brain Workspace
Agent Monitor
Testing Playground
Trace Viewer
```

- [ ] **Step 6.4: Create tab navigation**

Tabs:

```text
Overview
Knowledge Studio
Graph Builder
Agent Command Center
Testing Playground
Training Queue
```

- [ ] **Step 6.5: Add empty states**
  - Empty knowledge state.
  - Empty graph state.
  - No trace state.
  - No selected scope state.

- [ ] **Step 6.6: Add CSS visual identity**
  - Bento cards.
  - Agent status pills.
  - Scope chips.
  - Graph relation cards.
  - Trace timeline.

**Success criteria:** Admin can open a polished AI Brain Center page that clearly communicates the multi-agent product concept.

---

## Phase 7: Overview and Agent Command Center UI

**Purpose:** Make the multi-agent architecture visible before deep CRUD work.

**Files:**
- Modify: `WebHomestay/Views/AdminAIBrain/Index.cshtml`
- Modify: `WebHomestay/wwwroot/js/admin-ai-brain.js`
- Modify: `WebHomestay/wwwroot/css/admin-ai-brain.css`

- [ ] **Step 7.1: Load overview data**
  - Fetch `/admin/ai-brain/api/overview` on page load.
  - Render counts for scopes, knowledge units, graph nodes, graph edges, agents, and traces.

- [ ] **Step 7.2: Render agent monitor**
  - Show all six agents.
  - Status examples: `Online`, `Connected`, `Indexed`, `Active`, `Mock Mode`.

- [ ] **Step 7.3: Render Agent Command Center tab**
  - Agent card with name, role, enabled state, allowed tools summary.
  - Toggle enabled state if endpoint is implemented.

- [ ] **Step 7.4: Add visual agent pipeline**

```text
AI A → AI B / AI C / AI D / AI Guard → AI E
```

**Success criteria:** The page visually sells the “many AI agents working together” idea.

---

## Phase 8: Knowledge Studio UI and CRUD

**Purpose:** Let Admin feed scoped knowledge into the Brain Center.

**Files:**
- Modify: `WebHomestay/Views/AdminAIBrain/Index.cshtml`
- Modify: `WebHomestay/wwwroot/js/admin-ai-brain.js`
- Modify: `WebHomestay/wwwroot/css/admin-ai-brain.css`

- [ ] **Step 8.1: Render scope selector**
  - Global Brain.
  - Branch scopes.
  - Room scopes if available.

- [ ] **Step 8.2: Render knowledge list by selected scope**
  - Title.
  - Knowledge type.
  - Priority.
  - Status.
  - Updated time.

- [ ] **Step 8.3: Create knowledge editor modal/panel**

Fields:

```text
Title
Knowledge Type
Scope
Priority
Tags
Effective From
Effective To
Status
Content
```

- [ ] **Step 8.4: Implement save knowledge**
  - POST to `/admin/ai-brain/api/knowledge`.
  - Refresh list after save.
  - Show toast/success feedback using existing UI pattern.

- [ ] **Step 8.5: Implement delete/archive knowledge**
  - Prefer soft status `Archived` if the project avoids hard deletes.
  - If hard delete is used, confirm in UI.

- [ ] **Step 8.6: Add “Preview AI Understanding” panel**
  - Show scope, tags, inferred use case, and sample retrieval text.
  - This can be deterministic/mock in this phase.

**Success criteria:** Admin can create, edit, and test scoped knowledge units.

---

## Phase 9: Graph Builder UI and CRUD

**Purpose:** Add the Graph part of RAG+Graph without needing a complex canvas first.

**Files:**
- Modify: `WebHomestay/Views/AdminAIBrain/Index.cshtml`
- Modify: `WebHomestay/wwwroot/js/admin-ai-brain.js`
- Modify: `WebHomestay/wwwroot/css/admin-ai-brain.css`

- [ ] **Step 9.1: Render graph node list**
  - Group by NodeType.
  - Show name, description, reference type.

- [ ] **Step 9.2: Render graph relation list**

Example display:

```text
[Branch: Quận 1] --HAS_POLICY--> [Policy: Self check-in sau 14:00]
```

- [ ] **Step 9.3: Create node editor**

Fields:

```text
Node Type
Name
Description
Reference Type
Reference Id
Metadata JSON
```

- [ ] **Step 9.4: Create edge editor**

Fields:

```text
Source Node
Edge Type
Target Node
Weight
Metadata JSON
```

- [ ] **Step 9.5: Add suggested relation presets**

Presets:

```text
HAS_ROOM
HAS_POLICY
HAS_AMENITY
HAS_FAQ
APPLIES_TO
RECOMMENDED_FOR
CONFLICTS_WITH
REQUIRES
CAN_TRIGGER_ACTION
```

- [ ] **Step 9.6: Add simple graph path preview**
  - When an edge is saved, show a readable relationship sentence.
  - Do not build a complex graph canvas yet unless already available.

**Success criteria:** Admin can create graph nodes/edges and see relationships as readable knowledge paths.

---

## Phase 10: Testing Playground and Trace Viewer

**Purpose:** Let Admin test the multi-agent flow and see why AI answered that way.

**Files:**
- Modify: `WebHomestay/Views/AdminAIBrain/Index.cshtml`
- Modify: `WebHomestay/wwwroot/js/admin-ai-brain.js`
- Modify: `WebHomestay/wwwroot/css/admin-ai-brain.css`

- [ ] **Step 10.1: Create playground input UI**
  - Message textbox.
  - Scope dropdown.
  - Test button.
  - Example prompt chips.

Example chips:

```text
Tối nay còn phòng riêng tư cho 2 người ở Quận 1 không?
Chi nhánh Đà Lạt có cho check-in khuya không?
Có phòng nào phù hợp gia đình 4 người không?
```

- [ ] **Step 10.2: Call playground endpoint**
  - POST `/admin/ai-brain/api/playground/run`.
  - Disable button while running.
  - Show loading states per agent.

- [ ] **Step 10.3: Render final response**
  - Message.
  - Suggested actions.
  - Sources.

- [ ] **Step 10.4: Render agent trace timeline**

Trace steps:

```text
AI A - Intent detected
AI B - Live data checked
AI C - Knowledge matched
AI D - Persona analyzed
AI Guard - Safety approved
AI E - Final response composed
```

- [ ] **Step 10.5: Render raw trace JSON toggle**
  - Useful for debugging.
  - Collapsed by default.

**Success criteria:** Admin can run the demo scenarios from the spec and see multi-agent trace output.

---

## Phase 11: Live System Agent Integration

**Purpose:** Replace mock live data with real system data where possible.

**Files:**
- Modify: `WebHomestay/Services/AIBrain/AILiveSystemAgentService.cs`
- Possibly inspect/modify existing room/booking services.

- [ ] **Step 11.1: Identify existing availability logic**
  - Find where public room availability is calculated.
  - Reuse existing service/query if available.
  - Do not duplicate complex booking rules if a reliable implementation already exists.

- [ ] **Step 11.2: Implement branch lookup**
  - Match selected scope to branch if `ScopeType == Branch`.
  - Match user message keyword to branch name if no scope is selected.

- [ ] **Step 11.3: Implement room availability result**
  - Return available rooms with room id, name, branch, price, time range, amenities.
  - If time parsing is not implemented yet, support simple demo defaults for “tối nay”, “hôm nay”, “ngày mai”.

- [ ] **Step 11.4: Mark trace as real or mock**
  - Trace must clearly show whether AI B used real database data.

- [ ] **Step 11.5: Guard no-data cases**
  - If no availability data can be confirmed, final response must not claim a room is available.

**Success criteria:** Playground can answer at least one availability scenario using actual room/booking data.

---

## Phase 12: Knowledge Retrieval and Graph Context

**Purpose:** Make AI C actually use scoped knowledge and graph relationships.

**Files:**
- Modify: `WebHomestay/Services/AIBrain/AIKnowledgeBrainService.cs`

- [ ] **Step 12.1: Implement scoped retrieval priority**

Priority order:

```text
Room-specific
Branch-specific
Campaign-specific
Global
Fallback
```

- [ ] **Step 12.2: Implement keyword/full-text style search**
  - Search title, content, tags.
  - Filter active/effective knowledge.
  - Order by scope match and priority.

- [ ] **Step 12.3: Implement simple graph traversal**
  - For selected scope, load nodes and edges.
  - Include one-hop relationships in context.
  - For matched nodes, include related policies, amenities, recommended segments.

- [ ] **Step 12.4: Return source metadata**
  - Knowledge title.
  - Scope.
  - Confidence/mock score.
  - Graph path text.

- [ ] **Step 12.5: Display retrieval details in trace**
  - Matched articles.
  - Graph paths.
  - Scope priority used.

**Success criteria:** Playground trace proves AI C used both knowledge units and graph relationships.

---

## Phase 13: Customer Chatbot Integration

**Purpose:** Connect the Brain Center runtime to the customer-facing chatbot after Admin tooling works.

**Files:**
- Inspect existing chat controller/view first.
- Modify current AI Chat/Booking Consultant endpoint rather than creating a competing chatbot if one exists.

- [ ] **Step 13.1: Locate existing chatbot flow**
  - Find current AI assistant controller/API.
  - Find frontend chat JS.

- [ ] **Step 13.2: Route customer messages through `AIBrainOrchestratorService`**
  - Preserve existing UI contract if possible.
  - Add suggested actions only if current UI supports them or can ignore them safely.

- [ ] **Step 13.3: Select scope from customer context**
  - If customer is viewing a branch, use branch brain.
  - If customer is viewing a room, use room brain.
  - Otherwise use global brain.

- [ ] **Step 13.4: Store trace for admin debugging**
  - Use `AIConversationTrace`.
  - Do not expose internal trace to customers.

**Success criteria:** Customer chatbot can use the AI Brain Center pipeline without exposing Admin-only details.

---

## Phase 14: Optional Real LLM Integration

**Purpose:** Upgrade deterministic mock agents into real LLM-backed agents while preserving traceability.

**Files:**
- Add AI provider service files according to the chosen provider.
- Add secure configuration through existing appsettings/user-secrets pattern.

- [ ] **Step 14.1: Choose provider and configuration pattern**
  - Use environment variables or user secrets for API keys.
  - Do not commit secrets.

- [ ] **Step 14.2: Create provider abstraction**

Interface:

```csharp
public interface IAIModelClient
{
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 14.3: Add prompt per agent**
  - Orchestrator prompt.
  - Knowledge Brain prompt.
  - Customer Persona prompt.
  - Safety Guard prompt.
  - Synthesizer prompt.

- [ ] **Step 14.4: Keep deterministic fallback**
  - If no API key exists, Playground should still run in mock mode.

- [ ] **Step 14.5: Add prompt caching if using Claude API**
  - Cache stable system prompts and large knowledge context.
  - Keep dynamic user message outside cached blocks.

**Success criteria:** Real LLM mode can be enabled without breaking mock/demo mode.

---

## Phase 15: Validation, Build, and UI Testing

**Purpose:** Verify the feature works end-to-end.

- [ ] **Step 15.1: Build project**
  - Fix compile errors.

- [ ] **Step 15.2: Apply migrations and launch app**
  - Confirm database tables exist.

- [ ] **Step 15.3: Test Admin UI in browser**
  - Open AI Brain Center.
  - Create knowledge.
  - Create graph node.
  - Create graph edge.
  - Run playground prompt.
  - Confirm trace timeline renders.

- [ ] **Step 15.4: Test demo scenario 1**

Prompt:

```text
Tối nay mình đi 2 người, muốn phòng riêng tư ở Quận 1, giá vừa phải, có self check-in không?
```

Expected:

- Intent includes availability + recommendation + policy.
- Live System Agent runs.
- Knowledge Brain returns policy/sales knowledge if present.
- Persona Agent identifies couple/private intent.
- Guard approves or warns.
- Final response does not invent room data.

- [ ] **Step 15.5: Test demo scenario 2**

Prompt:

```text
Chi nhánh Đà Lạt có cho check-in khuya không?
```

Expected:

- Knowledge Brain uses branch-scoped policy if available.
- Response cites policy context in trace.

- [ ] **Step 15.6: Test no-data safety**

Prompt:

```text
Chắc chắn còn phòng rẻ nhất tối nay đúng không?
```

Expected:

- Guard prevents overclaiming if live data is missing.

**Success criteria:** The app builds, Admin UI works in browser, and the multi-agent trace demonstrates the standout concept.

---

## Phase 16: Documentation and Handoff

**Purpose:** Make future implementation/debugging easier without over-documenting code.

- [ ] **Step 16.1: Update the spec only if implementation decisions changed**
  - Do not duplicate implementation details unnecessarily.

- [ ] **Step 16.2: Add short Admin usage note if project already has docs for Admin features**
  - Explain how to create knowledge, graph relations, and run playground.

- [ ] **Step 16.3: Prepare demo script**
  - 3-minute pitch.
  - 2 playground prompts.
  - 1 trace explanation.

**Success criteria:** Another developer or presenter can understand how to demo AI Brain Center.

---

## Implementation Order Recommendation

Do not attempt real LLM integration first. Build in this order:

1. Data models.
2. Seed agents/scopes.
3. Service skeleton with mock multi-agent trace.
4. Admin UI shell.
5. Playground trace.
6. Knowledge CRUD.
7. Graph CRUD.
8. Live System Agent integration.
9. Customer chatbot integration.
10. Real LLM integration.

This order keeps the distinctive product visible early while reducing risk.

---

## Non-Goals for First Implementation

- Do not build a complex drag-and-drop graph canvas initially.
- Do not require vector embeddings before keyword retrieval works.
- Do not replace the entire booking system.
- Do not expose internal agent trace to customers.
- Do not let AI confirm price/availability without database-backed Live System Agent data.

---

## Final Acceptance Criteria

- Admin can open **AI Brain Center**.
- Admin sees six named agents and their roles.
- Admin can create scoped knowledge.
- Admin can create basic graph nodes and edges.
- Admin can run a playground message.
- Playground returns final answer plus multi-agent trace.
- Live System Agent can use real room/branch/booking data for at least one scenario.
- Knowledge Brain retrieves scoped knowledge and graph context.
- Safety Guard prevents unsupported claims.
- Customer chatbot can optionally route through the Brain Center pipeline.
