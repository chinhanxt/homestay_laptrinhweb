using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services.AI.Graph;
using WebHomestay.Services.AI.Retrieval;
using Xunit;

namespace WebHomestay.Tests.Services
{
    public class RetrievalContextAssemblerTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task BuildPolicyContextAsync_CombinesVectorHitsAndRelatedGraphNodes()
        {
            using var context = CreateContext();

            // Seed graph nodes & edge
            var node1 = new AIGraphNode
            {
                NodeType = "branch",
                Label = "branch-1",
                Summary = "Lumi Branch Q1",
                IsActive = true
            };

            var node2 = new AIGraphNode
            {
                NodeType = "issue",
                Label = "issue-wifi",
                Summary = "Wifi Issues info",
                IsActive = true
            };

            context.AIGraphNodes.Add(node1);
            context.AIGraphNodes.Add(node2);
            await context.SaveChangesAsync();

            var edge = new AIGraphEdge
            {
                FromNodeId = node1.Id,
                FromNode = node1,
                ToNodeId = node2.Id,
                ToNode = node2,
                RelationshipType = "HAS_TOPIC",
                Weight = 1.0m,
                Evidence = "Wifi is in branch-1"
            };

            context.AIGraphEdges.Add(edge);
            await context.SaveChangesAsync();

            // Mock vector search service
            var mockVectorSearch = new Mock<IVectorSearchService>();
            mockVectorSearch.Setup(x => x.SearchKnowledgeAsync(
                It.IsAny<float[]>(),
                It.IsAny<VectorSearchFilter?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new VectorSearchResult
                    {
                        EntityType = "knowledge",
                        EntityId = Guid.NewGuid().ToString(),
                        Title = "Wifi Policy",
                        Content = "Wifi password is 123456",
                        Tags = "branch-1,issue-wifi",
                        Score = 0.9
                    }
                });

            var mockExpansion = new Mock<Neo4jGraphExpansionService>(Mock.Of<INeo4jGraphClient>());
            mockExpansion.Setup(x => x.ExpandAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GraphExpansionContext
                {
                    SeedNodeKeys = new List<string> { "branch-1", "issue-wifi" },
                    NodeSummaries = new List<string> { "[branch] branch-1: Lumi Branch Q1", "[issue] issue-wifi: Wifi Issues info" },
                    EdgeSummaries = new List<string> { "branch-1 -[HAS_TOPIC]-> issue-wifi" }
                });

            var assembler = new RetrievalContextAssembler(context, mockVectorSearch.Object, mockExpansion.Object);
            var queryEmbedding = new float[1536];

            var (hits, graph) = await assembler.BuildPolicyContextAsync(queryEmbedding, null, CancellationToken.None);

            // Verify hits
            Assert.Single(hits);
            Assert.Equal("Wifi Policy", hits[0].Title);

            // Verify graph expansion
            Assert.NotNull(graph);
            Assert.Contains("branch-1", graph.RelatedNodeLabels);
            Assert.Contains("issue-wifi", graph.RelatedNodeLabels);
            Assert.Single(graph.RelatedEdges);
            Assert.Equal("branch-1 -[HAS_TOPIC]-> issue-wifi", graph.RelatedEdges[0]);
        }
    }
}
