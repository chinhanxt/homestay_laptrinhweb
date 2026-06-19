using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services.AI;
using Xunit;

namespace WebHomestay.Tests.Services;

public class AdminAIReseedServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedBranchAndRooms(ApplicationDbContext context)
    {
        var collection = new AIKnowledgeCollection
        {
            Id = Guid.NewGuid(),
            Name = "Legacy Collection",
            Description = "Should not be imported",
            Icon = "fa-book",
            Order = 1,
            Articles = new List<AIKnowledgeArticle>()
        };

        var branch = new Branch
        {
            Id = 1,
            Name = "Lumi Q1",
            Address = "123 Nguyen Hue",
            Hotline = "0909000001",
            BookingLeadTimeHours = 2,
            Rooms =
            [
                new Room
                {
                    Id = 10,
                    Name = "Moon Room",
                    BranchId = 1,
                    Capacity = 2,
                    MaxGuests = 3,
                    PricePerHour = 250000,
                    PricePerDay = 700000,
                    ExtraGuestFee = 50000,
                    Status = "Available"
                },
                new Room
                {
                    Id = 11,
                    Name = "Sun Room",
                    BranchId = 1,
                    Capacity = 4,
                    MaxGuests = 6,
                    PricePerHour = 650000,
                    PricePerDay = 1600000,
                    ExtraGuestFee = 100000,
                    Status = "Maintenance"
                }
            ]
        };

        context.Branches.Add(branch);
        context.AIKnowledgeCollections.Add(collection);
        context.AIKnowledgeArticles.Add(new AIKnowledgeArticle
        {
            CollectionId = collection.Id,
            Collection = collection,
            Title = "Legacy Article",
            Content = "Must not be carried into the new seed."
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task ReseedAsync_DeletesOldKnowledgeGraphData()
    {
        await using var context = CreateContext();
        SeedBranchAndRooms(context);
        var scope = new AIBrainScope { Name = "Old Scope", Description = "Old" };
        context.AIBrainScopes.Add(scope);
        context.AIKnowledgeUnits.Add(new AIKnowledgeUnit { Scope = scope, Title = "Old Unit", Content = "Old content" });
        context.AIGraphNodes.Add(new AIGraphNode { NodeType = "old", Label = "Old Node", Summary = "Old summary" });
        await context.SaveChangesAsync();

        var service = new AdminAIReseedService(context);

        await service.ReseedAsync();

        Assert.DoesNotContain(context.AIKnowledgeUnits, unit => unit.Title == "Old Unit");
        Assert.DoesNotContain(context.AIGraphNodes, node => node.Label == "Old Node");
        Assert.Single(context.AIBrainScopes);
    }

    [Fact]
    public async Task ReseedAsync_CreatesTruthfulBranchAndRoomGroundingData()
    {
        await using var context = CreateContext();
        SeedBranchAndRooms(context);
        var service = new AdminAIReseedService(context);

        var result = await service.ReseedAsync();

        Assert.True(result.CreatedKnowledgeUnits > 0);
        Assert.Contains(context.AIGraphNodes, node => node.NodeType == "branch" && node.Label == "Lumi Q1");
        Assert.Contains(context.AIGraphNodes, node => node.NodeType == "room" && node.Label == "Moon Room");
        Assert.Contains(context.AIGraphNodes, node => node.NodeType == "capacity_band");
        Assert.Contains(context.AIGraphNodes, node => node.NodeType == "price_band");
        Assert.Contains(context.AIGraphNodes, node => node.NodeType == "status");
        Assert.Contains(context.AIGraphEdges, edge => edge.RelationshipType == "branch_contains_room");
        Assert.Contains(context.AIGraphEdges, edge => edge.RelationshipType == "room_has_capacity_band");
        Assert.Contains(context.AIGraphEdges, edge => edge.RelationshipType == "room_has_price_band");
        Assert.Contains(context.AIGraphEdges, edge => edge.RelationshipType == "room_has_status");
    }

    [Fact]
    public async Task ReseedAsync_DoesNotImportLegacyArticleKnowledge()
    {
        await using var context = CreateContext();
        SeedBranchAndRooms(context);
        var service = new AdminAIReseedService(context);

        await service.ReseedAsync();

        Assert.DoesNotContain(context.AIKnowledgeUnits, unit => unit.Title == "Legacy Article");
    }

    [Fact]
    public async Task ReseedAsync_AlignsWordingWithLeadTimeUnit()
    {
        await using var context = CreateContext();
        
        var branch = new Branch
        {
            Id = 1,
            Name = "Lumi Q1",
            Address = "123 Nguyen Hue",
            Hotline = "0909000001",
            BookingLeadTimeValue = 7,
            BookingLeadTimeUnit = BranchLeadTimeUnit.Days,
            BookingLeadTimeDays = 7
        };
        context.Branches.Add(branch);
        await context.SaveChangesAsync();

        var service = new AdminAIReseedService(context);
        await service.ReseedAsync();

        var branchSummary = context.AIKnowledgeUnits.First(unit => unit.Title == "Chi nhánh Lumi Q1").Content;
        Assert.Contains("7 ngày", branchSummary);
    }
}
