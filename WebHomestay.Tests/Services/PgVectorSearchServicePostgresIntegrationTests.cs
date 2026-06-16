using Microsoft.EntityFrameworkCore;
using Pgvector;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services.AI.Retrieval;
using WebHomestay.Tests.Infrastructure;
using Xunit;

namespace WebHomestay.Tests.Services;

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
        _fixture.EnsureAvailable();
        await using var context = _fixture.CreateContext();
        await ResetAiDataAsync(context);
        await SeedKnowledgeAsync(context);

        var service = new PgVectorSearchService(context);
        var query = CreateVector(1.0f, 0.0f, 0.0f);

        var results = await service.SearchKnowledgeAsync(
            query,
            new VectorSearchFilter { ActiveOnly = true },
            3,
            CancellationToken.None);

        Assert.NotEmpty(results);
        Assert.Equal("Pet Policy", results[0].Title);
        Assert.True(results.Count >= 2);
        Assert.True(results[0].Score >= results[1].Score);
    }

    [Fact]
    public async Task SearchKnowledgeAsync_OnPostgres_RespectsBranchAndRoomFilters()
    {
        _fixture.EnsureAvailable();
        await using var context = _fixture.CreateContext();
        await ResetAiDataAsync(context);
        await SeedKnowledgeAsync(context);

        var service = new PgVectorSearchService(context);
        var query = CreateVector(1.0f, 0.0f, 0.0f);

        var results = await service.SearchKnowledgeAsync(
            query,
            new VectorSearchFilter
            {
                BranchId = 1,
                RoomId = 101,
                ActiveOnly = true
            },
            5,
            CancellationToken.None);

        Assert.NotEmpty(results);
        Assert.All(results, row =>
            Assert.True(
                string.IsNullOrWhiteSpace(row.Tags)
                || row.Tags.Contains("branch-1", StringComparison.OrdinalIgnoreCase)
                || row.Tags.Contains("room-101", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(results, row => row.Title == "Smoking Policy Branch 2");
    }

    [Fact]
    public async Task SearchRoomsAsync_OnPostgres_RespectsBranchFilterAndRanking()
    {
        _fixture.EnsureAvailable();
        await using var context = _fixture.CreateContext();
        await ResetAiDataAsync(context);
        await SeedRoomsAsync(context);

        var service = new PgVectorSearchService(context);
        var query = CreateVector(1.0f, 0.0f, 0.0f);

        var results = await service.SearchRoomsAsync(query, 1, 5, CancellationToken.None);

        Assert.NotEmpty(results);
        Assert.Equal("Romantic Room Q1", results[0].Title);
        Assert.DoesNotContain(results, row => row.Title == "Family Room Q2");
        Assert.All(results, row => Assert.Equal("room", row.EntityType));
    }

    private static async Task ResetAiDataAsync(ApplicationDbContext context)
    {
        context.AIGraphEdges.RemoveRange(context.AIGraphEdges);
        context.AIGraphNodes.RemoveRange(context.AIGraphNodes);
        context.AIKnowledgeUnits.RemoveRange(context.AIKnowledgeUnits);
        context.AIBrainScopes.RemoveRange(context.AIBrainScopes);
        context.Rooms.RemoveRange(context.Rooms);
        context.Branches.RemoveRange(context.Branches);
        await context.SaveChangesAsync();
    }

    private static async Task SeedKnowledgeAsync(ApplicationDbContext context)
    {
        var utcNow = DateTime.UtcNow;
        var scope = new AIBrainScope
        {
            Name = "Policy Scope",
            Description = "Policies for test",
            CreatedAt = utcNow
        };
        context.AIBrainScopes.Add(scope);

        context.AIKnowledgeUnits.AddRange(
            new AIKnowledgeUnit
            {
                Scope = scope,
                Title = "Pet Policy",
                Content = "Small pets are allowed in room 101 at branch 1.",
                Tags = "branch-1,room-101,policy-pet",
                Priority = 10,
                IsActive = true,
                LastUpdated = utcNow,
                Embedding = new Vector(CreateVector(1.0f, 0.0f, 0.0f))
            },
            new AIKnowledgeUnit
            {
                Scope = scope,
                Title = "Bath Policy",
                Content = "Bathtub cleaning procedure for romantic rooms.",
                Tags = "branch-1,room-101,procedure-bath",
                Priority = 8,
                IsActive = true,
                LastUpdated = utcNow,
                Embedding = new Vector(CreateVector(0.8f, 0.2f, 0.0f))
            },
            new AIKnowledgeUnit
            {
                Scope = scope,
                Title = "Smoking Policy Branch 2",
                Content = "Smoking is forbidden in branch 2 family rooms.",
                Tags = "branch-2,room-202,policy-smoking",
                Priority = 9,
                IsActive = true,
                LastUpdated = utcNow,
                Embedding = new Vector(CreateVector(0.0f, 1.0f, 0.0f))
            });

        await context.SaveChangesAsync();
    }

    private static async Task SeedRoomsAsync(ApplicationDbContext context)
    {
        var branch1 = new Branch
        {
            Id = 1,
            Name = "Lumi Q1",
            Address = "Q1"
        };
        var branch2 = new Branch
        {
            Id = 2,
            Name = "Lumi Q2",
            Address = "Q2"
        };

        context.Branches.AddRange(branch1, branch2);
        context.Rooms.AddRange(
            new Room
            {
                Id = 101,
                BranchId = 1,
                Name = "Romantic Room Q1",
                Description = "Balcony, bathtub, romantic setup.",
                Status = "Available",
                Capacity = 2,
                MaxGuests = 2,
                Embedding = new Vector(CreateVector(1.0f, 0.0f, 0.0f))
            },
            new Room
            {
                Id = 102,
                BranchId = 1,
                Name = "Quiet Room Q1",
                Description = "Minimal room for quiet stay.",
                Status = "Available",
                Capacity = 2,
                MaxGuests = 3,
                Embedding = new Vector(CreateVector(0.6f, 0.4f, 0.0f))
            },
            new Room
            {
                Id = 202,
                BranchId = 2,
                Name = "Family Room Q2",
                Description = "Large room for families.",
                Status = "Available",
                Capacity = 6,
                MaxGuests = 8,
                Embedding = new Vector(CreateVector(0.0f, 1.0f, 0.0f))
            });

        await context.SaveChangesAsync();
    }

    private static float[] CreateVector(float x, float y, float z)
    {
        var vector = new float[1536];
        vector[0] = x;
        vector[1] = y;
        vector[2] = z;
        return vector;
    }
}
