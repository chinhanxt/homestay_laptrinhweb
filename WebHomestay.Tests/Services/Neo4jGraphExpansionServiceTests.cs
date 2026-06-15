using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Neo4j.Driver;
using WebHomestay.Services.AI.Graph;
using Xunit;

namespace WebHomestay.Tests.Services
{
    public class Neo4jGraphExpansionServiceTests
    {
        [Fact]
        public async Task ExpandAsync_ReturnsBoundedNeighborsForSeedNodes()
        {
            // Arrange
            var mockNode1 = new Mock<INode>();
            mockNode1.Setup(x => x.ElementId).Returns("node-1-id");
            mockNode1.Setup(x => x.Id).Returns(1L);
            mockNode1.Setup(x => x.Properties).Returns(new Dictionary<string, object>
            {
                { "key", "branch-1" },
                { "nodeType", "branch" },
                { "label", "branch-1" },
                { "summary", "Lumi Branch Q1" }
            });

            var mockNode2 = new Mock<INode>();
            mockNode2.Setup(x => x.ElementId).Returns("node-2-id");
            mockNode2.Setup(x => x.Id).Returns(2L);
            mockNode2.Setup(x => x.Properties).Returns(new Dictionary<string, object>
            {
                { "key", "issue-wifi" },
                { "nodeType", "issue" },
                { "label", "issue-wifi" },
                { "summary", "Wifi Issues info" }
            });

            var mockRel = new Mock<IRelationship>();
            mockRel.Setup(x => x.Type).Returns("HAS_TOPIC");
            mockRel.Setup(x => x.StartNodeElementId).Returns("node-1-id");
            mockRel.Setup(x => x.StartNodeId).Returns(1L);
            mockRel.Setup(x => x.EndNodeElementId).Returns("node-2-id");
            mockRel.Setup(x => x.EndNodeId).Returns(2L);
            mockRel.Setup(x => x.Properties).Returns(new Dictionary<string, object>());

            var mockPath = new Mock<IPath>();
            mockPath.Setup(x => x.Nodes).Returns(new List<INode> { mockNode1.Object, mockNode2.Object });
            mockPath.Setup(x => x.Relationships).Returns(new List<IRelationship> { mockRel.Object });

            var mockClient = new Mock<INeo4jGraphClient>();
            mockClient.Setup(x => x.ExecuteReadAsync<IPath>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Func<IRecord, IPath>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IPath> { mockPath.Object });

            var service = new Neo4jGraphExpansionService(mockClient.Object);

            // Act
            var result = await service.ExpandAsync(new[] { "branch-1" }, maxHops: 2, cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.SeedNodeKeys);
            Assert.Contains("branch-1", result.SeedNodeKeys);
            Assert.Contains("[branch] branch-1: Lumi Branch Q1", result.NodeSummaries);
            Assert.Contains("[issue] issue-wifi: Wifi Issues info", result.NodeSummaries);
            Assert.Single(result.EdgeSummaries);
            Assert.Equal("branch-1 -[HAS_TOPIC]-> issue-wifi", result.EdgeSummaries[0]);
        }
    }
}
