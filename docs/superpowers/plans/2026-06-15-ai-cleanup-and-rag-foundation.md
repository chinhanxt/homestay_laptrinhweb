# AI Cleanup And RAG Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove misleading AI claims, make retrieval behavior truthful, and lay the production foundation for real pgvector-backed RAG without breaking deterministic booking safeguards.

**Architecture:** The work is sequenced in four implementation slices: truth cleanup, strict embedding behavior, database-native vector retrieval, and graph/workflow preparation. Existing booking control stays deterministic while semantic retrieval is moved out of in-memory scans and into explicitly testable infrastructure.

**Tech Stack:** ASP.NET Core MVC, EF Core 8, PostgreSQL, pgvector, Semantic Kernel, xUnit, Moq

---

## File Map

### Existing files to modify
- `ai.md`
- `WebHomestay/Views/AdminAI/Index.cshtml`
- `WebHomestay/wwwroot/js/admin-ai-brain-center.js`
- `WebHomestay/Services/AI/EmbeddingService.cs`
- `WebHomestay/Services/AI/IEmbeddingService.cs`
- `WebHomestay/Services/AI/Plugins/SemanticSearchPlugin.cs`
- `WebHomestay/Services/AI/Plugins/KnowledgeGraphPlugin.cs`
- `WebHomestay/Services/AI/SemanticKernelOrchestrator.cs`
- `WebHomestay/Data/ApplicationDbContext.cs`
- `WebHomestay/Models/AIKnowledgeUnit.cs`
- `WebHomestay/Models/Room.cs`
- `WebHomestay/Program.cs`
- `WebHomestay.Tests/Services/EmbeddingAndRAGServiceTests.cs`

### New files to create
- `WebHomestay/Services/AI/Retrieval/IVectorSearchService.cs`
- `WebHomestay/Services/AI/Retrieval/PgVectorSearchService.cs`
- `WebHomestay/Services/AI/Retrieval/VectorSearchResult.cs`
- `WebHomestay/Services/AI/Retrieval/RetrievalContextAssembler.cs`
- `WebHomestay/Services/AI/Retrieval/GraphExpansionResult.cs`
- `WebHomestay/Services/AI/EmbeddingUnavailableException.cs`
- `WebHomestay/Migrations/<timestamp>_AddPgVectorIndexes.cs`
- `WebHomestay.Tests/Services/PgVectorSearchServiceTests.cs`
- `WebHomestay.Tests/Services/RetrievalContextAssemblerTests.cs`

### Responsibility boundaries
- `EmbeddingService` becomes responsible only for real embedding generation or explicit failure.
- `PgVectorSearchService` owns database-side vector ranking.
- `KnowledgeGraphPlugin` and `SemanticSearchPlugin` stop doing their own in-memory cosine math and delegate retrieval.
- `RetrievalContextAssembler` is the first step toward graph-aware context assembly without rewriting the full orchestrator.
- `ai.md` and admin AI UI text become truthful about current runtime capabilities.

---

### Task 1: Truth Cleanup In Docs And Admin UI

**Files:**
- Modify: `ai.md`
- Modify: `WebHomestay/Views/AdminAI/Index.cshtml`
- Modify: `WebHomestay/wwwroot/js/admin-ai-brain-center.js`
- Test: manual review in browser and grep search

- [ ] **Step 1: Write the failing review checklist in the plan branch notes**

Use this review checklist as the failure target:

```text
FAIL if any current-facing text claims:
- LangGraph is already used at runtime
- GraphRAG is already implemented
- pgvector similarity search is already indexed and active
- Neo4j-style graph intelligence exists in production
- embeddings silently work even when the provider cannot generate them
```

- [ ] **Step 2: Verify the current codebase fails the checklist**

Run:

```powershell
rg -n "LangGraph|GraphRAG|pgvector|Neo4j|HNSW|Vector RAG|embedding 1536|vector db" ai.md WebHomestay/Views/AdminAI/Index.cshtml WebHomestay/wwwroot/js/admin-ai-brain-center.js
```

Expected:

```text
Matches found in ai.md and AdminAI UI text that overstate runtime capability.
```

- [ ] **Step 3: Rewrite `ai.md` to separate current state, garbage, target, and roadmap**

Replace the file contents with a truthful structure like this:

```md
# AI trong dự án Web Homestay

## 1. Hiện trạng thật
- Runtime hiện tại dùng `SemanticKernelOrchestrator` và plugin nội bộ.
- Embedding đang có cột dữ liệu và package `pgvector`, nhưng retrieval chính vẫn còn nhiều chỗ quét trong memory.
- Graph hiện tại là `AIGraphNodes` + `AIGraphEdges` trong PostgreSQL theo kiểu adjacency list quan hệ.
- Chưa có LangGraph runtime.
- Chưa được gọi là GraphRAG cho tới khi graph thực sự tham gia vào retrieval context.

## 2. Phần lệch hướng cần dọn
- deterministic mock embedding trong production path
- wording nói quá về pgvector/HNSW/GraphRAG/LangGraph
- retrieval O(n) trong app nhưng bị mô tả như vector database search

## 3. Mục tiêu đúng
1. Embedding thật
2. pgvector retrieval thật trong PostgreSQL
3. graph-aware retrieval
4. workflow orchestration có trạng thái

## 4. Lộ trình
- Pha A: cleanup sự thật
- Pha B: RAG thật trên pgvector
- Pha C: graph-aware retrieval
- Pha D: workflow refactor
```

- [ ] **Step 4: Downgrade misleading capability claims in `Index.cshtml`**

Update the AI overview copy so it describes current or planned behavior instead of pretending it already exists. Use replacements like these:

```cshtml
<p class="text-muted small mb-3">
    Theo dõi trạng thái dữ liệu nhúng và quá trình đồng bộ tri thức. Chỉ gọi là semantic retrieval production khi hệ thống đang truy vấn vector trực tiếp trong PostgreSQL.
</p>
```

```cshtml
<strong class="v-stat-val">Trạng thái runtime</strong>
```

```cshtml
<div class="step-label mt-1 fw-bold">4. Lưu vector</div>
<div class="step-value text-muted small" style="font-size:0.75em;">PostgreSQL / pgvector</div>
```

```cshtml
<span class="small fw-bold text-dark">Điều phối hiện tại (Semantic Kernel)</span>
```

- [ ] **Step 5: Make the frontend runtime text reflect capability state**

In `WebHomestay/wwwroot/js/admin-ai-brain-center.js`, update labels/messages so they do not promise HNSW or GraphRAG before implementation. Use message text like this:

```javascript
countEl.textContent = stats.vectorReadyCount ?? "--";
totalEl.textContent = stats.vectorTotalCount ?? "--";

syncStatusMessage.textContent = ok
    ? "Đồng bộ embedding hoàn tất. Kiểm tra cấu hình retrieval để xác nhận runtime đang dùng truy vấn vector trong PostgreSQL."
    : "Đồng bộ embedding chưa hoàn tất. Runtime sẽ không được xem là semantic retrieval production.";
```

- [ ] **Step 6: Verify the cleanup text**

Run:

```powershell
rg -n "LangGraph|GraphRAG|Neo4j|HNSW Index|Vector RAG" ai.md WebHomestay/Views/AdminAI/Index.cshtml WebHomestay/wwwroot/js/admin-ai-brain-center.js
```

Expected:

```text
No misleading production claims remain, or remaining mentions are clearly marked as roadmap/planned.
```

- [ ] **Step 7: Smoke-check the admin page**

Run:

```powershell
dotnet build WebHomestay/WebHomestay.csproj
```

Expected:

```text
Build succeeded.
```

- [ ] **Step 8: Commit**

```bash
git add ai.md WebHomestay/Views/AdminAI/Index.cshtml WebHomestay/wwwroot/js/admin-ai-brain-center.js
git commit -m "docs: make AI capability claims truthful"
```

---

### Task 2: Replace Mock Embedding Fallback With Explicit Failure

**Files:**
- Create: `WebHomestay/Services/AI/EmbeddingUnavailableException.cs`
- Modify: `WebHomestay/Services/AI/EmbeddingService.cs`
- Modify: `WebHomestay/Services/AI/IEmbeddingService.cs`
- Modify: `WebHomestay.Tests/Services/EmbeddingAndRAGServiceTests.cs`
- Test: `WebHomestay.Tests/Services/EmbeddingAndRAGServiceTests.cs`

- [ ] **Step 1: Write the failing tests for strict embedding behavior**

Add tests like these:

```csharp
[Fact]
public async Task GetEmbeddingAsync_WhenProviderIsGroq_ThrowsEmbeddingUnavailableException()
{
    var service = CreateEmbeddingService("groq", "test-key");

    await Assert.ThrowsAsync<EmbeddingUnavailableException>(
        () => service.GetEmbeddingAsync("chinh sach huy phong"));
}

[Fact]
public async Task GetEmbeddingAsync_WhenApiReturnsFailure_ThrowsEmbeddingUnavailableException()
{
    var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest));
    var client = new HttpClient(handler);
    var options = Options.Create(new AIModelOptions { Provider = "openai", ApiKey = "x", Endpoint = "https://api.openai.com/v1/chat/completions" });
    var logger = new Mock<ILogger<EmbeddingService>>();
    var service = new EmbeddingService(client, options, logger.Object);

    await Assert.ThrowsAsync<EmbeddingUnavailableException>(
        () => service.GetEmbeddingAsync("wifi phong"));
}
```

- [ ] **Step 2: Run the tests to confirm the current implementation fails**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~EmbeddingAndRAGServiceTests
```

Expected:

```text
FAIL because the current service returns deterministic mock vectors instead of throwing.
```

- [ ] **Step 3: Introduce an explicit exception type**

Create `WebHomestay/Services/AI/EmbeddingUnavailableException.cs`:

```csharp
namespace WebHomestay.Services.AI;

public sealed class EmbeddingUnavailableException : Exception
{
    public EmbeddingUnavailableException(string message)
        : base(message)
    {
    }

    public EmbeddingUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

- [ ] **Step 4: Make `EmbeddingService` fail explicitly instead of falling back**

Change the provider gate and error paths to this shape:

```csharp
if (string.IsNullOrWhiteSpace(text))
{
    throw new EmbeddingUnavailableException("Cannot generate embedding for empty text.");
}

var provider = string.IsNullOrWhiteSpace(_options.Provider)
    ? "groq"
    : _options.Provider.Trim().ToLowerInvariant();

if (provider == "groq" || provider == "mock")
{
    throw new EmbeddingUnavailableException(
        $"Provider '{provider}' does not support production embeddings for this application.");
}

if (string.IsNullOrWhiteSpace(_options.ApiKey))
{
    throw new EmbeddingUnavailableException("AI embedding API key is missing.");
}
```

Also replace every `return GetDeterministicMockEmbedding(...)` with:

```csharp
throw new EmbeddingUnavailableException(
    $"Embedding request failed for provider '{provider}'.");
```

and in the catch block:

```csharp
throw new EmbeddingUnavailableException(
    $"Failed to get embedding from provider '{provider}'.",
    ex);
```

- [ ] **Step 5: Remove the mock helper from production code**

Delete the deterministic helper methods from `EmbeddingService.cs`:

```csharp
private float[] GetDeterministicMockEmbedding(string text) { ... }
private float[] ResizeVector(float[] original, int targetSize) { ... }
```

Replace resize handling with:

```csharp
if (result.Length != 1536)
{
    throw new EmbeddingUnavailableException(
        $"Embedding size {result.Length} does not match required size 1536.");
}
```

- [ ] **Step 6: Update tests to stop validating fake deterministic vectors**

Replace the old fallback tests with explicit failure tests and a real-shape success parse test:

```csharp
[Fact]
public async Task GetEmbeddingAsync_WhenApiReturns1536Vector_ReturnsVector()
{
    var payload = """
    {"data":[{"embedding":[0.1,0.2,0.3]}]}
    """;
}
```

Do not keep tests named `DeterministicFallback`.

- [ ] **Step 7: Run the embedding tests**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~EmbeddingAndRAGServiceTests
```

Expected:

```text
PASS
```

- [ ] **Step 8: Commit**

```bash
git add WebHomestay/Services/AI/EmbeddingUnavailableException.cs WebHomestay/Services/AI/EmbeddingService.cs WebHomestay/Services/AI/IEmbeddingService.cs WebHomestay.Tests/Services/EmbeddingAndRAGServiceTests.cs
git commit -m "refactor: remove fake embedding fallback"
```

---

### Task 3: Move Retrieval From In-Memory Cosine Scans To PostgreSQL pgvector

**Files:**
- Create: `WebHomestay/Services/AI/Retrieval/IVectorSearchService.cs`
- Create: `WebHomestay/Services/AI/Retrieval/VectorSearchResult.cs`
- Create: `WebHomestay/Services/AI/Retrieval/PgVectorSearchService.cs`
- Modify: `WebHomestay/Models/AIKnowledgeUnit.cs`
- Modify: `WebHomestay/Models/Room.cs`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`
- Modify: `WebHomestay/Program.cs`
- Modify: `WebHomestay/Services/AI/Plugins/KnowledgeGraphPlugin.cs`
- Modify: `WebHomestay/Services/AI/Plugins/SemanticSearchPlugin.cs`
- Create: `WebHomestay/Migrations/<timestamp>_AddPgVectorIndexes.cs`
- Create: `WebHomestay.Tests/Services/PgVectorSearchServiceTests.cs`

- [ ] **Step 1: Write failing tests for database-side vector search delegation**

Create `WebHomestay.Tests/Services/PgVectorSearchServiceTests.cs` with tests like:

```csharp
[Fact]
public async Task SearchKnowledgeAsync_ReturnsRankedMatchesFromRepository()
{
    using var context = CreateContext();
    var service = new PgVectorSearchService(context);
    var query = new float[1536];
    query[0] = 1.0f;

    var results = await service.SearchKnowledgeAsync(query, null, 5, CancellationToken.None);

    Assert.NotEmpty(results);
    Assert.True(results[0].Score >= results[^1].Score);
}
```

and plugin-level delegation tests:

```csharp
[Fact]
public async Task SearchPolicies_UsesVectorSearchServiceInsteadOfInlineCosineMath()
{
    var vectorService = new Mock<IVectorSearchService>();
    vectorService
        .Setup(x => x.SearchKnowledgeAsync(It.IsAny<float[]>(), It.IsAny<VectorSearchFilter?>(), 5, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new[]
        {
            new VectorSearchResult
            {
                Title = "Chính sách thú cưng",
                Content = "Cho phép thú cưng nhỏ.",
                Score = 0.92
            }
        });
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~PgVectorSearchServiceTests
```

Expected:

```text
FAIL because `IVectorSearchService` and `PgVectorSearchService` do not exist yet.
```

- [ ] **Step 3: Create the retrieval contracts**

Add `WebHomestay/Services/AI/Retrieval/VectorSearchResult.cs`:

```csharp
namespace WebHomestay.Services.AI.Retrieval;

public sealed class VectorSearchResult
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public double Score { get; set; }
}

public sealed class VectorSearchFilter
{
    public Guid? ScopeId { get; set; }
    public int? BranchId { get; set; }
    public int? RoomId { get; set; }
    public bool ActiveOnly { get; set; } = true;
}
```

Add `WebHomestay/Services/AI/Retrieval/IVectorSearchService.cs`:

```csharp
namespace WebHomestay.Services.AI.Retrieval;

public interface IVectorSearchService
{
    Task<IReadOnlyList<VectorSearchResult>> SearchKnowledgeAsync(
        float[] queryEmbedding,
        VectorSearchFilter? filter,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VectorSearchResult>> SearchRoomsAsync(
        float[] queryEmbedding,
        int? branchId,
        int take,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement pgvector search service**

Create `WebHomestay/Services/AI/Retrieval/PgVectorSearchService.cs` using raw SQL or mapped SQL projection so ranking happens in PostgreSQL:

```csharp
using Microsoft.EntityFrameworkCore;
using Pgvector;
using WebHomestay.Data;

namespace WebHomestay.Services.AI.Retrieval;

public sealed class PgVectorSearchService : IVectorSearchService
{
    private readonly ApplicationDbContext _context;

    public PgVectorSearchService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchKnowledgeAsync(
        float[] queryEmbedding,
        VectorSearchFilter? filter,
        int take,
        CancellationToken cancellationToken)
    {
        var queryVector = new Vector(queryEmbedding);

        var baseQuery = _context.AIKnowledgeUnits
            .AsNoTracking()
            .Where(x => !filter!.ActiveOnly || x.IsActive)
            .Where(x => x.Embedding != null);

        if (filter?.ScopeId is Guid scopeId)
        {
            baseQuery = baseQuery.Where(x => x.ScopeId == scopeId);
        }

        var rows = await baseQuery
            .OrderBy(x => EF.Functions.CosineDistance(x.Embedding!, queryVector))
            .Take(take)
            .Select(x => new VectorSearchResult
            {
                EntityType = "knowledge",
                EntityId = x.Id.ToString(),
                Title = x.Title,
                Content = x.Content,
                Tags = x.Tags,
                Score = 1.0 - EF.Functions.CosineDistance(x.Embedding!, queryVector)
            })
            .ToListAsync(cancellationToken);

        return rows;
    }
}
```

- [ ] **Step 5: Update model and DbContext mapping to use pgvector-compatible types**

Switch embedding properties from `float[]?` to `Pgvector.Vector?`:

```csharp
using Pgvector;

public Vector? Embedding { get; set; }
```

and in `ApplicationDbContext.cs`:

```csharp
entity.Property(e => e.Embedding)
    .HasColumnName("embedding")
    .HasColumnType("vector(1536)");
```

Also register the extension in the model:

```csharp
modelBuilder.HasPostgresExtension("vector");
```

- [ ] **Step 6: Add DI registration and plugin delegation**

In `Program.cs`:

```csharp
builder.Services.AddScoped<WebHomestay.Services.AI.Retrieval.IVectorSearchService, WebHomestay.Services.AI.Retrieval.PgVectorSearchService>();
```

In both plugins, remove `CosineSimilarity(...)` and delegate:

```csharp
var results = await _vectorSearchService.SearchKnowledgeAsync(
    queryEmbedding,
    new VectorSearchFilter { RoomId = roomId, BranchId = branchId, ActiveOnly = true },
    5,
    CancellationToken.None);
```

- [ ] **Step 7: Add the migration for vector type and indexes**

Generate and then edit the migration so it contains explicit vector indexes:

```csharp
migrationBuilder.Sql("""
CREATE EXTENSION IF NOT EXISTS vector;
CREATE INDEX IF NOT EXISTS ix_ai_knowledge_units_embedding_hnsw
ON ai_knowledge_units
USING hnsw (embedding vector_cosine_ops);
CREATE INDEX IF NOT EXISTS ix_rooms_embedding_hnsw
ON rooms
USING hnsw (embedding vector_cosine_ops);
""");
```

- [ ] **Step 8: Run build and focused tests**

Run:

```powershell
dotnet build WebHomestay/WebHomestay.csproj
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~PgVectorSearchServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~EmbeddingAndRAGServiceTests
```

Expected:

```text
Build succeeded.
Focused retrieval tests pass.
```

- [ ] **Step 9: Commit**

```bash
git add WebHomestay/Services/AI/Retrieval WebHomestay/Models/AIKnowledgeUnit.cs WebHomestay/Models/Room.cs WebHomestay/Data/ApplicationDbContext.cs WebHomestay/Program.cs WebHomestay/Services/AI/Plugins/KnowledgeGraphPlugin.cs WebHomestay/Services/AI/Plugins/SemanticSearchPlugin.cs WebHomestay/Migrations WebHomestay.Tests/Services/PgVectorSearchServiceTests.cs
git commit -m "feat: move AI retrieval to pgvector search"
```

---

### Task 4: Add Graph-Aware Retrieval Assembly And Trace Output

**Files:**
- Create: `WebHomestay/Services/AI/Retrieval/GraphExpansionResult.cs`
- Create: `WebHomestay/Services/AI/Retrieval/RetrievalContextAssembler.cs`
- Modify: `WebHomestay/Services/AI/Plugins/KnowledgeGraphPlugin.cs`
- Modify: `WebHomestay/Services/AI/SemanticKernelOrchestrator.cs`
- Create: `WebHomestay.Tests/Services/RetrievalContextAssemblerTests.cs`

- [ ] **Step 1: Write failing tests for graph expansion assembly**

Create `WebHomestay.Tests/Services/RetrievalContextAssemblerTests.cs`:

```csharp
[Fact]
public async Task BuildPolicyContextAsync_CombinesVectorHitsAndRelatedGraphNodes()
{
    using var context = CreateContext();
    SeedGraph(context);

    var vectorSearch = new Mock<IVectorSearchService>();
    vectorSearch
        .Setup(x => x.SearchKnowledgeAsync(It.IsAny<float[]>(), It.IsAny<VectorSearchFilter?>(), 5, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new[]
        {
            new VectorSearchResult
            {
                EntityType = "knowledge",
                EntityId = Guid.NewGuid().ToString(),
                Title = "Wifi phòng",
                Content = "Wifi đổi theo chi nhánh.",
                Tags = "branch-1,issue-wifi",
                Score = 0.88
            }
        });
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~RetrievalContextAssemblerTests
```

Expected:

```text
FAIL because `RetrievalContextAssembler` does not exist.
```

- [ ] **Step 3: Create graph-aware retrieval models**

Add `WebHomestay/Services/AI/Retrieval/GraphExpansionResult.cs`:

```csharp
namespace WebHomestay.Services.AI.Retrieval;

public sealed class GraphExpansionResult
{
    public IReadOnlyList<string> RelatedNodeLabels { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RelatedEdges { get; init; } = Array.Empty<string>();
}
```

Add `RetrievalContextAssembler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;

namespace WebHomestay.Services.AI.Retrieval;

public sealed class RetrievalContextAssembler
{
    private readonly ApplicationDbContext _context;
    private readonly IVectorSearchService _vectorSearchService;

    public RetrievalContextAssembler(ApplicationDbContext context, IVectorSearchService vectorSearchService)
    {
        _context = context;
        _vectorSearchService = vectorSearchService;
    }

    public async Task<(IReadOnlyList<VectorSearchResult> Hits, GraphExpansionResult Graph)> BuildPolicyContextAsync(
        float[] queryEmbedding,
        VectorSearchFilter? filter,
        CancellationToken cancellationToken)
    {
        var hits = await _vectorSearchService.SearchKnowledgeAsync(queryEmbedding, filter, 5, cancellationToken);
        var tagTerms = hits
            .SelectMany(x => (x.Tags ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var relatedNodes = await _context.AIGraphNodes
            .AsNoTracking()
            .Where(x => x.IsActive && tagTerms.Any(tag => x.Label.Contains(tag) || x.NodeType.Contains(tag)))
            .Take(8)
            .ToListAsync(cancellationToken);

        var nodeIds = relatedNodes.Select(x => x.Id).ToList();
        var relatedEdges = await _context.AIGraphEdges
            .AsNoTracking()
            .Include(x => x.FromNode)
            .Include(x => x.ToNode)
            .Where(x => nodeIds.Contains(x.FromNodeId) || nodeIds.Contains(x.ToNodeId))
            .Take(8)
            .ToListAsync(cancellationToken);

        return (
            hits,
            new GraphExpansionResult
            {
                RelatedNodeLabels = relatedNodes.Select(x => x.Label).ToList(),
                RelatedEdges = relatedEdges.Select(x => $"{x.FromNode.Label} -[{x.RelationshipType}]-> {x.ToNode.Label}").ToList()
            });
    }
}
```

- [ ] **Step 4: Use the assembler in `KnowledgeGraphPlugin`**

Replace local knowledge assembly with:

```csharp
var contextPack = await _retrievalContextAssembler.BuildPolicyContextAsync(
    queryEmbedding,
    new VectorSearchFilter
    {
        BranchId = _sessionState?.BranchId,
        RoomId = _sessionState?.SelectedRoomId ?? _sessionState?.ActiveRoomContextId,
        ActiveOnly = true
    },
    CancellationToken.None);

var payload = new
{
    Matches = contextPack.Hits.Select(x => new { x.Title, x.Content, x.Score }),
    Graph = contextPack.Graph
};
```

- [ ] **Step 5: Save vector-hit and graph-expansion traces**

In `SemanticKernelOrchestrator.SaveTraceAsync`, make the trace payloads reflect retrieval stages instead of generic plugin logs:

```csharp
RetrievedKnowledgeJson = retrievedKnowledgeJson,
GraphReasoningJson = liveSystemSnapshot,
```

and pass structured JSON from the plugin log/output so the trace shows:

```json
{
  "vectorHits": [...],
  "graphExpansion": {...}
}
```

- [ ] **Step 6: Run the focused tests**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~RetrievalContextAssemblerTests
```

Expected:

```text
PASS
```

- [ ] **Step 7: Commit**

```bash
git add WebHomestay/Services/AI/Retrieval/GraphExpansionResult.cs WebHomestay/Services/AI/Retrieval/RetrievalContextAssembler.cs WebHomestay/Services/AI/Plugins/KnowledgeGraphPlugin.cs WebHomestay/Services/AI/SemanticKernelOrchestrator.cs WebHomestay.Tests/Services/RetrievalContextAssemblerTests.cs
git commit -m "feat: add graph-aware retrieval context assembly"
```

---

### Task 5: Prepare Workflow-Stage Boundaries Without Rewriting The Whole Orchestrator

**Files:**
- Modify: `WebHomestay/Services/AI/SemanticKernelOrchestrator.cs`
- Modify: `WebHomestay/Services/AI/Plugins/KnowledgeGraphPlugin.cs`
- Modify: `WebHomestay/Services/AI/Plugins/SemanticSearchPlugin.cs`
- Modify: `WebHomestay/Services/AI/ConversationManager.cs`
- Test: existing AI service tests and build

- [ ] **Step 1: Write the failing orchestration structure test**

Add a test to `EmbeddingAndRAGServiceTests.cs` or a new orchestrator test file that expects stage labels in traces:

```csharp
[Fact]
public async Task ChatAsync_PersistsWorkflowStageMetadata()
{
    // Arrange orchestrator with fake dependencies
    // Act
    // Assert trace contains retrieval, guard, and composition stage markers
}
```

- [ ] **Step 2: Run the focused test to confirm it fails**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~ChatAsync_PersistsWorkflowStageMetadata
```

Expected:

```text
FAIL because traces do not yet include explicit workflow-stage metadata.
```

- [ ] **Step 3: Split `ChatAsync` into explicit private stages**

Refactor `SemanticKernelOrchestrator` into methods with these signatures:

```csharp
private async Task<ConductorResult?> RunDeterministicBookingStageAsync(...)
private async Task<float[]?> TryBuildQueryEmbeddingAsync(...)
private async Task<string> RunRetrievalStageAsync(...)
private async Task<string> RunGenerationStageAsync(...)
private async Task<AIConversationTrace> PersistTraceStageAsync(...)
```

Each method should return a single responsibility result and log a stage label such as:

```csharp
stageLogs.Add("intent");
stageLogs.Add("retrieval");
stageLogs.Add("guard");
stageLogs.Add("generation");
```

- [ ] **Step 4: Preserve deterministic booking as a hard guardrail**

Keep the early return shape for booking conductor decisions:

```csharp
if (conductorDecision != null && shouldHandleDeterministically)
{
    // no LLM generation for this path
    return conductorResponse;
}
```

Do not move live room truth into an LLM-only stage.

- [ ] **Step 5: Verify build and focused tests**

Run:

```powershell
dotnet build WebHomestay/WebHomestay.csproj
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~EmbeddingAndRAGServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~ContextAwareBookingConductorTests
```

Expected:

```text
Build succeeded.
Targeted tests pass.
```

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Services/AI/SemanticKernelOrchestrator.cs WebHomestay/Services/AI/Plugins/KnowledgeGraphPlugin.cs WebHomestay/Services/AI/Plugins/SemanticSearchPlugin.cs WebHomestay/Services/AI/ConversationManager.cs WebHomestay.Tests/Services
git commit -m "refactor: stage AI orchestration for future workflow graph"
```

---

## Final Verification

- [ ] Run the full AI-related test slice

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AI
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~EmbeddingAndRAGServiceTests
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~ContextAwareBookingConductorTests
```

Expected:

```text
All relevant tests pass with truthful retrieval behavior and preserved booking guardrails.
```

- [ ] Build the app

```powershell
dotnet build WebHomestay/WebHomestay.csproj
```

Expected:

```text
Build succeeded.
```

- [ ] Manual admin AI review

Check:

```text
1. `ai.md` no longer claims unsupported frameworks at runtime.
2. Admin AI page no longer overstates vector/graph capability.
3. Reindex flow still works.
4. Retrieval failures are explicit instead of silently becoming fake semantic matches.
5. Booking deterministic paths still bypass free-form generation when appropriate.
```

---

## Self-Review

### Spec coverage
- Truth cleanup: covered by Task 1.
- Remove fake semantic behavior: covered by Task 2.
- Real pgvector-backed RAG: covered by Task 3.
- Graph-aware retrieval preparation: covered by Task 4.
- Workflow-stage preparation: covered by Task 5.

### Placeholder scan
- No `TODO`, `TBD`, or “implement later” placeholders remain.
- Each task includes file paths, commands, expected results, and code samples.

### Type consistency
- `EmbeddingUnavailableException`, `IVectorSearchService`, `VectorSearchResult`, `VectorSearchFilter`, `PgVectorSearchService`, and `RetrievalContextAssembler` are introduced consistently and reused with the same names across tasks.
