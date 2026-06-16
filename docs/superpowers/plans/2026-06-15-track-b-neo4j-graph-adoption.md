# Track B: Neo4j Graph Adoption Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce Neo4j as the graph reasoning store, seed it from current project data, and make graph expansion runtime-ready for GraphRAG.

**Architecture:** Keep PostgreSQL as the transactional and vector source, but move graph persistence and traversal into a new Neo4j module. The existing relational graph remains a temporary seed source until Neo4j is validated.

**Tech Stack:** ASP.NET Core MVC, Neo4j.Driver, EF Core, xUnit

---

## File Map

### Existing files to modify
- `WebHomestay/WebHomestay.csproj`
- `WebHomestay/Program.cs`
- `WebHomestay/Services/AI/Retrieval/RetrievalContextAssembler.cs`
- `WebHomestay/Controllers/AdminAIController.cs`

### New files to create
- `WebHomestay/Services/AI/Graph/Neo4jOptions.cs`
- `WebHomestay/Services/AI/Graph/INeo4jGraphClient.cs`
- `WebHomestay/Services/AI/Graph/Neo4jGraphClient.cs`
- `WebHomestay/Services/AI/Graph/GraphSeedDocument.cs`
- `WebHomestay/Services/AI/Graph/Neo4jGraphSeedService.cs`
- `WebHomestay/Services/AI/Graph/Neo4jGraphExpansionService.cs`
- `WebHomestay.Tests/Services/Neo4jGraphExpansionServiceTests.cs`
- `docs/note/neo4j-graph-schema.md`

---

### Task 1: Add Neo4j Runtime Wiring

**Files:**
- Modify: `WebHomestay/WebHomestay.csproj`
- Modify: `WebHomestay/Program.cs`
- Create: `WebHomestay/Services/AI/Graph/Neo4jOptions.cs`

- [ ] **Step 1: Add Neo4j package**

```xml
<PackageReference Include="Neo4j.Driver" Version="5.28.1" />
```

- [ ] **Step 2: Add options class**

```csharp
namespace WebHomestay.Services.AI.Graph;

public sealed class Neo4jOptions
{
    public string Uri { get; set; } = "bolt://localhost:7687";
    public string Username { get; set; } = "neo4j";
    public string Password { get; set; } = "password";
    public string Database { get; set; } = "neo4j";
}
```

- [ ] **Step 3: Register options and services**

Add to `Program.cs`:

```csharp
builder.Services.Configure<Neo4jOptions>(builder.Configuration.GetSection("Neo4j"));
builder.Services.AddSingleton<INeo4jGraphClient, Neo4jGraphClient>();
builder.Services.AddScoped<Neo4jGraphSeedService>();
builder.Services.AddScoped<Neo4jGraphExpansionService>();
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

### Task 2: Add Neo4j Client And Seed Service

**Files:**
- Create: `WebHomestay/Services/AI/Graph/INeo4jGraphClient.cs`
- Create: `WebHomestay/Services/AI/Graph/Neo4jGraphClient.cs`
- Create: `WebHomestay/Services/AI/Graph/GraphSeedDocument.cs`
- Create: `WebHomestay/Services/AI/Graph/Neo4jGraphSeedService.cs`

- [ ] **Step 1: Create the client contract**

```csharp
namespace WebHomestay.Services.AI.Graph;

public interface INeo4jGraphClient : IAsyncDisposable
{
    Task ExecuteWriteAsync(string cypher, object parameters, CancellationToken cancellationToken);
    Task<IReadOnlyList<T>> ExecuteReadAsync<T>(string cypher, object parameters, Func<IRecord, T> map, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Implement the client**

Wrap `IDriver` and session creation with `ExecuteWriteAsync` / `ExecuteReadAsync`.

- [ ] **Step 3: Create a seed DTO**

```csharp
public sealed class GraphSeedDocument
{
    public string NodeType { get; set; } = string.Empty;
    public string NodeKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

- [ ] **Step 4: Build the seed service**

The service should read:
- `AIGraphNodes`
- `AIGraphEdges`
- branch and room summaries

and upsert them into Neo4j using deterministic keys.

- [ ] **Step 5: Add an admin-only seed trigger**

Add a controller endpoint:

```csharp
[HttpPost("neo4j-seed")]
public async Task<IActionResult> SeedNeo4j(CancellationToken cancellationToken)
```

- [ ] **Step 6: Run and verify**

Run:

```powershell
dotnet build WebHomestay/WebHomestay.csproj
```

Expected:

```text
Build succeeded.
```

---

### Task 3: Add Neo4j Graph Expansion Service

**Files:**
- Create: `WebHomestay/Services/AI/Graph/Neo4jGraphExpansionService.cs`
- Create: `WebHomestay.Tests/Services/Neo4jGraphExpansionServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task ExpandAsync_ReturnsBoundedNeighborsForSeedNodes()
{
    // mock INeo4jGraphClient
    // assert hop-bound, edge count, and relevance ordering
}
```

- [ ] **Step 2: Implement bounded expansion**

The service should accept:
- seed node keys
- max hops
- allowed relationship types
- max returned nodes/edges

and return:

```csharp
public sealed class GraphExpansionContext
{
    public IReadOnlyList<string> SeedNodeKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NodeSummaries { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> EdgeSummaries { get; init; } = Array.Empty<string>();
}
```

- [ ] **Step 3: Run the graph tests**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~Neo4jGraphExpansionServiceTests
```

Expected:

```text
PASS
```

---

### Task 4: Switch Graph Expansion Source In Retrieval Assembly

**Files:**
- Modify: `WebHomestay/Services/AI/Retrieval/RetrievalContextAssembler.cs`

- [ ] **Step 1: Inject `Neo4jGraphExpansionService`**

Add it to the constructor and remove direct traversal against `AIGraphNodes/AIGraphEdges` for runtime expansion.

- [ ] **Step 2: Translate vector hits into Neo4j seeds**

Derive seeds from:
- `Tags`
- `EntityId`
- `BranchId`
- `RoomId`

- [ ] **Step 3: Return a richer graph context**

Update the result so it includes:
- seed node keys
- expanded node summaries
- expanded edge summaries

- [ ] **Step 4: Re-run retrieval assembly tests**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~RetrievalContextAssemblerTests
```

Expected:

```text
PASS
```

---

### Task 5: Document The Neo4j Graph Schema And Commit

**Files:**
- Create: `docs/note/neo4j-graph-schema.md`

- [ ] **Step 1: Write the schema doc**

Include:
- node labels
- key properties
- relationship types
- seed sources from PostgreSQL

- [ ] **Step 2: Commit**

```bash
git add WebHomestay/WebHomestay.csproj WebHomestay/Program.cs WebHomestay/Services/AI/Graph WebHomestay/Services/AI/Retrieval/RetrievalContextAssembler.cs WebHomestay/Controllers/AdminAIController.cs WebHomestay.Tests/Services/Neo4jGraphExpansionServiceTests.cs docs/note/neo4j-graph-schema.md
git commit -m "feat: adopt Neo4j for graph expansion"
```

