# AI PostgreSQL Test Setup

## Required
- PostgreSQL local instance
- `vector` extension available on the server
- test database `web_homestay_ai_tests`

## Optional env var
- `WEBHOMESTAY_TEST_POSTGRES`

If this variable is not provided, the tests default to:

```text
Host=localhost;Port=5432;Database=web_homestay_ai_tests;Username=postgres;Password=1510
```

## Command

```powershell
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~PgVectorSearchServicePostgresIntegrationTests
```

## What This Verifies
- PostgreSQL migration runs successfully
- `pgvector` extension exists
- `PgVectorSearchService` ranks vectors in PostgreSQL
- branch and room filters are enforced by the database-backed retrieval path
