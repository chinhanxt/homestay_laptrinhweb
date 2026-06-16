# Track A: PostgreSQL Retrieval Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove that pgvector-backed retrieval works end-to-end on real PostgreSQL and make it the verified semantic retrieval foundation for the AI system.

**Architecture:** Keep the current AI runtime in place, but add a real PostgreSQL integration test harness around `PgVectorSearchService`, verify migrations/extensions/indexes, and tighten retrieval filters so branch/room context is enforced by the database query instead of only by post-ranking boosts.

**Tech Stack:** ASP.NET Core MVC, EF Core 8, PostgreSQL, pgvector, xUnit

---

## File Map

### Existing files to modify
- `WebHomestay.Tests/WebHomestay.Tests.csproj`
- `WebHomestay.Tests/Services/PgVectorSearchServiceTests.cs`
- `WebHomestay/Services/AI/Retrieval/PgVectorSearchService.cs`
- `WebHomestay/Services/AI/Retrieval/VectorSearchResult.cs`
- `WebHomestay/Program.cs`

### New files to create
- `WebHomestay.Tests/Infrastructure/PostgresIntegrationFixture.cs`
- `WebHomestay.Tests/Infrastructure/PostgresIntegrationCollection.cs`
- `WebHomestay.Tests/Services/PgVectorSearchServicePostgresIntegrationTests.cs`
- `docs/note/ai-postgres-test-setup.md`

---

### Task 1: Add PostgreSQL Test Fixture

**Files:**
- Create: `WebHomestay.Tests/Infrastructure/PostgresIntegrationFixture.cs`
- Create: `WebHomestay.Tests/Infrastructure/PostgresIntegrationCollection.cs`
- Modify: `WebHomestay.Tests/WebHomestay.Tests.csproj`

- [ ] **Step 1: Add PostgreSQL test dependencies**

Add package references:

```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
<PackageReference Include="Pgvector.EntityFrameworkCore" Version="0.2.2" />
```

- [ ] **Step 2: Create the shared PostgreSQL fixture**

```csharp
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;

namespace WebHomestay.Tests.Infrastructure;

public sealed class PostgresIntegrationFixture : IAsyncLifetime
{
    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("WEBHOMESTAY_TEST_POSTGRES")
        ?? "Host=localhost;Port=5432;Database=web_homestay_ai_tests;Username=postgres;Password=1510";

    public async Task InitializeAsync()
    {
        using var context = CreateContext();
        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector;");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString, o => o.UseVector())
            .Options;
        return new ApplicationDbContext(options);
    }
}
```

- [ ] **Step 3: Add xUnit collection wrapper**

```csharp
namespace WebHomestay.Tests.Infrastructure;

[CollectionDefinition("postgres-integration")]
public sealed class PostgresIntegrationCollection : ICollectionFixture<PostgresIntegrationFixture>
{
}
```

- [ ] **Step 4: Build the test project**

Run:

```powershell
dotnet build WebHomestay.Tests/WebHomestay.Tests.csproj
```

Expected:

```text
Build succeeded.
```

---

### Task 2: Add Real pgvector Integration Tests

**Files:**
- Create: `WebHomestay.Tests/Services/PgVectorSearchServicePostgresIntegrationTests.cs`
- Modify: `WebHomestay.Tests/Services/PgVectorSearchServiceTests.cs`

- [ ] **Step 1: Keep InMemory tests only as guardrails**

Reduce the existing file to just the “wrong provider should fail” behavior. Leave test names explicit about provider mismatch.

- [ ] **Step 2: Add PostgreSQL ranking test**

```csharp
[Collection("postgres-integration")]
public sealed class PgVectorSearchServicePostgresIntegrationTests
{
    private readonly PostgresIntegrationFixture _fixture;

    public PgVectorSearchServicePostgresIntegrationTests(PostgresIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SearchKnowledgeAsync_OnPostgres_ReturnsNearestMatchFirst()
    {
        using var context = _fixture.CreateContext();
        await SeedKnowledgeAsync(context);

        var service = new PgVectorSearchService(context);
        var query = new float[1536];
        query[0] = 1.0f;

        var results = await service.SearchKnowledgeAsync(query, new VectorSearchFilter { ActiveOnly = true }, 3);

        Assert.NotEmpty(results);
        Assert.Equal("policy-pet", results[0].EntityId);
        Assert.True(results[0].Score >= results[1].Score);
    }
}
```

- [ ] **Step 3: Add room filter test**

```csharp
[Fact]
public async Task SearchKnowledgeAsync_OnPostgres_RespectsBranchAndRoomFilters()
{
    using var context = _fixture.CreateContext();
    await SeedKnowledgeAsync(context);

    var service = new PgVectorSearchService(context);
    var query = new float[1536];
    query[0] = 1.0f;

    var results = await service.SearchKnowledgeAsync(
        query,
        new VectorSearchFilter { BranchId = 1, RoomId = 101, ActiveOnly = true },
        5);

    Assert.All(results, row =>
        Assert.True(
            string.IsNullOrWhiteSpace(row.Tags)
            || row.Tags.Contains("branch-1")
            || row.Tags.Contains("room-101")));
}
```

- [ ] **Step 4: Run the integration tests**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~PgVectorSearchServicePostgresIntegrationTests
```

Expected:

```text
PASS on a machine with PostgreSQL running and the vector extension available.
```

---

### Task 3: Tighten Retrieval Query Semantics

**Files:**
- Modify: `WebHomestay/Services/AI/Retrieval/PgVectorSearchService.cs`
- Modify: `WebHomestay/Services/AI/Retrieval/VectorSearchResult.cs`

- [ ] **Step 1: Add explicit tag match helpers**

Implement local helpers inside `PgVectorSearchService`:

```csharp
private static string BuildRoomTag(int roomId) => $"room-{roomId}";
private static string BuildBranchTag(int branchId) => $"branch-{branchId}";
```

- [ ] **Step 2: Apply hard filters before ranking**

Make branch/room filtering happen before `OrderBy`:

```csharp
if (effectiveFilter.RoomId.HasValue)
{
    var roomTag = BuildRoomTag(effectiveFilter.RoomId.Value);
    baseQuery = baseQuery.Where(x => x.Tags == "" || x.Tags.Contains(roomTag));
}

if (effectiveFilter.BranchId.HasValue)
{
    var branchTag = BuildBranchTag(effectiveFilter.BranchId.Value);
    baseQuery = baseQuery.Where(x => x.Tags == "" || x.Tags.Contains(branchTag));
}
```

- [ ] **Step 3: Add a debug projection field if needed**

If troubleshooting is hard, add:

```csharp
public double Distance { get; set; }
```

to `VectorSearchResult` and populate it only in tests or diagnostics.

- [ ] **Step 4: Re-run the integration tests**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~PgVectorSearchService
```

Expected:

```text
All pgvector service tests pass.
```

---

### Task 4: Document The PostgreSQL Test Harness

**Files:**
- Create: `docs/note/ai-postgres-test-setup.md`

- [ ] **Step 1: Write the setup doc**

Document:

```md
# AI PostgreSQL Test Setup

## Required
- PostgreSQL local instance
- `vector` extension enabled
- test database `web_homestay_ai_tests`

## Optional env var
- `WEBHOMESTAY_TEST_POSTGRES`

## Command
`dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~PgVectorSearchServicePostgresIntegrationTests`
```

- [ ] **Step 2: Verify the file is readable and committed with the plan later**

Run:

```powershell
Get-Content -Raw docs/note/ai-postgres-test-setup.md
```

Expected:

```text
The setup document renders the required local test flow.
```

---

### Task 5: Final Verification And Commit

**Files:**
- Modify/Create: all files above

- [ ] **Step 1: Run the focused retrieval suite**

Run:

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter "FullyQualifiedName~PgVectorSearchService|FullyQualifiedName~EmbeddingAndRAGServiceTests"
```

Expected:

```text
Focused retrieval tests pass.
```

- [ ] **Step 2: Commit**

```bash
git add WebHomestay.Tests/WebHomestay.Tests.csproj WebHomestay.Tests/Infrastructure WebHomestay.Tests/Services/PgVectorSearchServiceTests.cs WebHomestay.Tests/Services/PgVectorSearchServicePostgresIntegrationTests.cs WebHomestay/Services/AI/Retrieval/PgVectorSearchService.cs WebHomestay/Services/AI/Retrieval/VectorSearchResult.cs docs/note/ai-postgres-test-setup.md
git commit -m "test: verify pgvector retrieval on PostgreSQL"
```

