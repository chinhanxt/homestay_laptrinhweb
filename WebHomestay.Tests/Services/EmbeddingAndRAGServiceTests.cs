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
using WebHomestay.Services.AI.Graph;
using WebHomestay.Services.AI.Plugins;
using WebHomestay.Services.AI.Retrieval;
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

        private static EmbeddingService CreateEmbeddingService(string provider = "gemma4", string apiKey = "mock-key", HttpClient httpClient = null)
        {
            var options = Options.Create(new AIModelOptions
            {
                Provider = provider,
                ApiKey = apiKey
            });
            var mockLogger = new Mock<ILogger<EmbeddingService>>();
            return new EmbeddingService(httpClient ?? new HttpClient(), options, mockLogger.Object);
        }

        private class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }

        [Fact]
        public async Task GetEmbeddingAsync_WhenProviderIsUnsupported_ThrowsEmbeddingUnavailableException()
        {
            var service = CreateEmbeddingService("legacy-provider", "test-key");

            await Assert.ThrowsAsync<EmbeddingUnavailableException>(
                () => service.GetEmbeddingAsync("chinh sach huy phong"));
        }

        [Fact]
        public async Task GetEmbeddingAsync_WhenApiReturnsFailure_ThrowsEmbeddingUnavailableException()
        {
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest));
            var client = new HttpClient(handler);
            var service = CreateEmbeddingService("gemma4", "x", client);

            await Assert.ThrowsAsync<EmbeddingUnavailableException>(
                () => service.GetEmbeddingAsync("wifi phong"));
        }

        [Fact]
        public async Task GetEmbeddingAsync_WhenApiReturns1536Vector_ReturnsVector()
        {
            var vector = new float[1536];
            vector[0] = 0.5f;
            vector[1535] = -0.5f;
            var responseJson = new
            {
                data = new[]
                {
                    new { embedding = vector }
                }
            };
            var payload = JsonSerializer.Serialize(responseJson);
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            });
            var client = new HttpClient(handler);
            var service = CreateEmbeddingService("gemma4", "x", client);

            var result = await service.GetEmbeddingAsync("wifi phong");

            Assert.NotNull(result);
            Assert.Equal(1536, result.Length);
            Assert.Equal(0.5f, result[0]);
            Assert.Equal(-0.5f, result[1535]);
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
                Embedding = new Pgvector.Vector(new float[1536])
            };

            var unit2 = new AIKnowledgeUnit
            {
                Scope = scope,
                Title = "Nội Quy Phòng Hút Thuốc",
                Content = "Nghiêm cấm hút thuốc lá trong phòng nghỉ.",
                Tags = "smoking,room-20",
                IsActive = true,
                Priority = 1,
                Embedding = new Pgvector.Vector(new float[1536])
            };

            context.AIKnowledgeUnits.Add(unit1);
            context.AIKnowledgeUnits.Add(unit2);
            await context.SaveChangesAsync();

            // Set up mock embedding service
            var mockEmbeddingService = new Mock<IEmbeddingService>();
            var queryEmbedding = new float[1536];
            mockEmbeddingService.Setup(s => s.GetEmbeddingAsync("thú cưng", It.IsAny<CancellationToken>()))
                .ReturnsAsync(queryEmbedding);

            // Set up mock vector search service that returns unit1
            var mockVectorSearch = new Mock<IVectorSearchService>();
            mockVectorSearch.Setup(v => v.SearchKnowledgeAsync(
                It.IsAny<float[]>(),
                It.IsAny<VectorSearchFilter>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new VectorSearchResult
                    {
                        EntityType = "knowledge",
                        EntityId = unit1.Id.ToString(),
                        Title = unit1.Title,
                        Content = unit1.Content,
                        Tags = unit1.Tags,
                        Score = 0.9
                    }
                });

            // Instantiate plugin with context session: user is currently viewing room 10 (Pet room)
            var sessionState = new AIBookingSessionState
            {
                BranchId = 1,
                ActiveRoomContextId = 10
            };

            var mockExpansion = new Mock<Neo4jGraphExpansionService>(Mock.Of<INeo4jGraphClient>());
            mockExpansion.Setup(x => x.ExpandAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GraphExpansionContext());

            var assembler = new RetrievalContextAssembler(context, mockVectorSearch.Object, mockExpansion.Object);
            var plugin = new KnowledgeGraphPlugin(context, mockEmbeddingService.Object, mockVectorSearch.Object, assembler, sessionState);

            var result = await plugin.SearchPolicies("thú cưng");

            var jsonPart = result.Substring(result.IndexOf('\n') + 1);
            using var doc = JsonDocument.Parse(jsonPart);
            var root = doc.RootElement;
            Assert.Equal(JsonValueKind.Array, root.ValueKind);
            var firstPayload = root[0];
            var matches = firstPayload.GetProperty("Matches");
            Assert.Equal(1, matches.GetArrayLength());
            Assert.Equal("Chính sách Mang Thú Cưng", matches[0].GetProperty("Title").GetString());
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
                Embedding = new Pgvector.Vector(new float[1536])
            };

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
                Embedding = new Pgvector.Vector(new float[1536])
            };

            context.Rooms.Add(room1);
            context.Rooms.Add(room2);
            await context.SaveChangesAsync();

            var mockEmbeddingService = new Mock<IEmbeddingService>();
            var queryEmbedding = new float[1536];
            mockEmbeddingService.Setup(s => s.GetEmbeddingAsync("phòng lãng mạn bồn tắm", It.IsAny<CancellationToken>()))
                .ReturnsAsync(queryEmbedding);

            var mockVectorSearch = new Mock<IVectorSearchService>();
            var searchResults = new[]
            {
                new VectorSearchResult { EntityType = "room", EntityId = "101", Title = room1.Name, Content = room1.Description, Score = 0.45 },
                new VectorSearchResult { EntityType = "room", EntityId = "102", Title = room2.Name, Content = room2.Description, Score = 0.4 }
            };
            mockVectorSearch.Setup(v => v.SearchRoomsAsync(
                It.IsAny<float[]>(),
                It.IsAny<int?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResults);

            // Scenario A: Guest count is 2 (fits room 1 perfectly)
            var sessionStateA = new AIBookingSessionState
            {
                GuestCount = 2,
                BranchId = 1
            };
            var pluginA = new SemanticSearchPlugin(context, mockEmbeddingService.Object, mockVectorSearch.Object, sessionStateA);
            var resultA = await pluginA.SemanticSearchRooms("phòng lãng mạn bồn tắm");

            Assert.Contains("\"RoomId\":101", resultA);

            // Scenario B: Guest count is 5 (strictly exceeds room 1's MaxGuests = 2, so it should be penalized heavily and not returned)
            var sessionStateB = new AIBookingSessionState
            {
                GuestCount = 5,
                BranchId = 1
            };
            var pluginB = new SemanticSearchPlugin(context, mockEmbeddingService.Object, mockVectorSearch.Object, sessionStateB);
            var resultB = await pluginB.SemanticSearchRooms("phòng lãng mạn bồn tắm");

            // Since Room 1 was penalized, it shouldn't meet the similarity threshold or match
            Assert.DoesNotContain("\"RoomId\":101", resultB);
        }
    }
}
