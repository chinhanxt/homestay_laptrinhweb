using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services.AI.Retrieval;
using Xunit;

namespace WebHomestay.Tests.Services
{
    public class PgVectorSearchServiceTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task SearchKnowledgeAsync_WhenUsingInMemoryDatabase_ThrowsExplicitProviderError()
        {
            using var context = CreateContext();

            var scope = new AIBrainScope { Name = "Scope 1", Description = "Description" };
            context.AIBrainScopes.Add(scope);
            context.AIKnowledgeUnits.Add(new AIKnowledgeUnit
            {
                Scope = scope,
                Title = "Title",
                Content = "Content",
                Embedding = new Pgvector.Vector(new float[1536]),
                IsActive = true
            });
            await context.SaveChangesAsync();

            var service = new PgVectorSearchService(context);
            var queryEmbedding = new float[1536];

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SearchKnowledgeAsync(queryEmbedding, new VectorSearchFilter { ActiveOnly = true }, 5, CancellationToken.None));

            Assert.Contains("Pgvector search requires the PostgreSQL provider", exception.Message);
        }

        [Fact]
        public async Task SearchRoomsAsync_WhenUsingInMemoryDatabase_ThrowsExplicitProviderError()
        {
            using var context = CreateContext();

            var branch = new Branch { Id = 1, Name = "Branch 1", Address = "Addr" };
            context.Branches.Add(branch);
            context.Rooms.Add(new Room
            {
                Id = 1,
                BranchId = 1,
                Branch = branch,
                Name = "Room",
                Status = "Available",
                Embedding = new Pgvector.Vector(new float[1536])
            });
            await context.SaveChangesAsync();

            var service = new PgVectorSearchService(context);
            var queryEmbedding = new float[1536];

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SearchRoomsAsync(queryEmbedding, null, 5, CancellationToken.None));

            Assert.Contains("Pgvector search requires the PostgreSQL provider", exception.Message);
        }
    }
}
