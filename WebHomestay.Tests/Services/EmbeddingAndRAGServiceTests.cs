using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using WebHomestay.Services.AI;
using WebHomestay.Services.AI.Plugins;
using Xunit;

namespace WebHomestay.Tests.Services
{
    public class EmbeddingAndRAGServiceTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private static EmbeddingService CreateEmbeddingService()
        {
            var options = Options.Create(new AIModelOptions
            {
                Provider = "mock",
                ApiKey = "mock-key"
            });
            var mockLogger = new Mock<ILogger<EmbeddingService>>();
            return new EmbeddingService(new HttpClient(), options, mockLogger.Object);
        }

        [Fact]
        public async Task GetEmbeddingAsync_DeterministicFallback_ReturnsNormalized1536Vector()
        {
            var service = CreateEmbeddingService();
            var text = "Hướng dẫn tự check-in bằng khóa thông minh";

            var result = await service.GetEmbeddingAsync(text);

            Assert.NotNull(result);
            Assert.Equal(1536, result.Length);

            // Compute L2 Norm (length)
            double sumOfSquares = 0;
            foreach (var val in result)
            {
                sumOfSquares += val * val;
            }
            double norm = Math.Sqrt(sumOfSquares);

            // Norm should be extremely close to 1.0 (normalized unit vector)
            Assert.True(Math.Abs(norm - 1.0) < 1e-4, $"Vector was not normalized. Norm: {norm}");
        }

        [Fact]
        public async Task GetEmbeddingAsync_DeterministicFallback_IdenticalInputReturnsIdenticalVector()
        {
            var service = CreateEmbeddingService();
            var text = "Chính sách hủy đặt phòng homestay";

            var result1 = await service.GetEmbeddingAsync(text);
            var result2 = await service.GetEmbeddingAsync(text);

            Assert.Equal(result1.Length, result2.Length);
            for (int i = 0; i < result1.Length; i++)
            {
                Assert.Equal(result1[i], result2[i]);
            }
        }

        [Fact]
        public async Task KnowledgeGraphPlugin_SearchPolicies_RanksAndBoostsCorrectly()
        {
            using var context = CreateContext();

            var scope = new AIBrainScope { Name = "Policy Scope", Description = "Homestay Policies" };
            context.AIBrainScopes.Add(scope);

            // Seed two knowledge units
            var unit1 = new AIKnowledgeUnit
            {
                Scope = scope,
                Title = "Chính sách Mang Thú Cưng",
                Content = "Homestay đồng ý mang thú cưng nhỏ dưới 5kg.",
                Tags = "pet,room-10",
                IsActive = true,
                Priority = 1,
                // Embedding will be generated deterministically for testing
                Embedding = new float[1536]
            };
            unit1.Embedding[0] = 0.9f;
            unit1.Embedding[1] = 0.1f;

            var unit2 = new AIKnowledgeUnit
            {
                Scope = scope,
                Title = "Nội Quy Phòng Hút Thuốc",
                Content = "Nghiêm cấm hút thuốc lá trong phòng nghỉ.",
                Tags = "smoking,room-20",
                IsActive = true,
                Priority = 1,
                Embedding = new float[1536]
            };
            unit2.Embedding[0] = 0.1f;
            unit2.Embedding[1] = 0.9f;

            context.AIKnowledgeUnits.Add(unit1);
            context.AIKnowledgeUnits.Add(unit2);
            await context.SaveChangesAsync();

            // Set up mock embedding service that returns a vector closest to unit1 (Pet policy)
            var mockEmbeddingService = new Mock<IEmbeddingService>();
            var queryEmbedding = new float[1536];
            queryEmbedding[0] = 0.95f;
            queryEmbedding[1] = 0.05f;
            mockEmbeddingService.Setup(s => s.GetEmbeddingAsync("thú cưng", It.IsAny<CancellationToken>()))
                .ReturnsAsync(queryEmbedding);

            // Instantiate plugin with context session: user is currently viewing room 10 (Pet room)
            var sessionState = new AIBookingSessionState
            {
                BranchId = 1,
                ActiveRoomContextId = 10
            };

            var plugin = new KnowledgeGraphPlugin(context, mockEmbeddingService.Object, sessionState);

            var result = await plugin.SearchPolicies("thú cưng");

            var jsonPart = result.Substring(result.IndexOf('\n') + 1);
            var items = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(jsonPart);
            Assert.Single(items);
            Assert.Equal("Chính sách Mang Thú Cưng", items[0]["Title"]);
        }

        [Fact]
        public async Task SemanticSearchPlugin_SemanticSearchRooms_FiltersAndPenalizesCapacityCorrectly()
        {
            using var context = CreateContext();

            var branch = new Branch { Id = 1, Name = "Chi nhánh Lumi Q1", Address = "Q1 HCM" };
            context.Branches.Add(branch);

            // Seed two rooms
            var room1 = new Room
            {
                Id = 101,
                BranchId = 1,
                Branch = branch,
                Name = "Phòng Trăng Mật Lãng Mạn",
                Description = "Có bồn tắm nằm và view ban công lãng mạn cho cặp đôi.",
                Capacity = 2,
                MaxGuests = 2,
                Status = "Available",
                Embedding = new float[1536]
            };
            room1.Embedding[0] = 1.0f;
            room1.Embedding[1] = 0.0f;

            var room2 = new Room
            {
                Id = 102,
                BranchId = 1,
                Branch = branch,
                Name = "Căn Hộ Gia Đình Lớn",
                Description = "Căn hộ rộng rãi, thích hợp cho nhóm 6-8 người đi du lịch.",
                Capacity = 6,
                MaxGuests = 8,
                Status = "Available",
                Embedding = new float[1536]
            };
            room2.Embedding[0] = 0.0f;
            room2.Embedding[1] = 1.0f;

            context.Rooms.Add(room1);
            context.Rooms.Add(room2);
            await context.SaveChangesAsync();

            var mockEmbeddingService = new Mock<IEmbeddingService>();
            var queryEmbedding = new float[1536];
            queryEmbedding[0] = 0.4f;
            queryEmbedding[1] = 0.9165f;
            // The query looks for romantic bath
            mockEmbeddingService.Setup(s => s.GetEmbeddingAsync("phòng lãng mạn bồn tắm", It.IsAny<CancellationToken>()))
                .ReturnsAsync(queryEmbedding);

            // Scenario A: Guest count is 2 (fits room 1 perfectly)
            var sessionStateA = new AIBookingSessionState
            {
                GuestCount = 2,
                BranchId = 1
            };
            var pluginA = new SemanticSearchPlugin(context, mockEmbeddingService.Object, sessionStateA);
            var resultA = await pluginA.SemanticSearchRooms("phòng lãng mạn bồn tắm");

            Assert.Contains("\"RoomId\":101", resultA);

            // Scenario B: Guest count is 5 (strictly exceeds room 1's MaxGuests = 2, so it should be penalized heavily and not returned)
            var sessionStateB = new AIBookingSessionState
            {
                GuestCount = 5,
                BranchId = 1
            };
            var pluginB = new SemanticSearchPlugin(context, mockEmbeddingService.Object, sessionStateB);
            var resultB = await pluginB.SemanticSearchRooms("phòng lãng mạn bồn tắm");

            // Since Room 1 was penalized, it shouldn't meet the similarity threshold or match
            Assert.DoesNotContain("\"RoomId\":101", resultB);
        }
    }
}
