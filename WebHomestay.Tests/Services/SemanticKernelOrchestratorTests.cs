using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.AI;
using WebHomestay.Services;
using WebHomestay.Services.AI;
using WebHomestay.Services.AI.Graph;
using WebHomestay.Services.AI.Plugins;
using WebHomestay.Services.AI.Retrieval;
using Xunit;

namespace WebHomestay.Tests.Services
{
    public class SemanticKernelOrchestratorTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task ChatAsync_PersistsWorkflowStageMetadata()
        {
            // Arrange
            using var context = CreateContext();

            var aiOptions = Options.Create(new AIModelOptions
            {
                Provider = "gemini",
                ApiKey = "mock-key",
                Model = "gemini-2.5-flash"
            });

            var serviceProviderMock = new Mock<IServiceProvider>();

            var mockEmbeddingService = new Mock<IEmbeddingService>();
            mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[1536]);

            var mockVectorSearch = new Mock<IVectorSearchService>();
            mockVectorSearch.Setup(v => v.SearchKnowledgeAsync(It.IsAny<float[]>(), It.IsAny<VectorSearchFilter>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<VectorSearchResult>());

            var mockAvailability = new Mock<IAvailabilityService>();
            var mockSlotGen = new Mock<ISlotGenerationService>();

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

            serviceProviderMock.Setup(sp => sp.GetService(typeof(ApplicationDbContext))).Returns(context);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IEmbeddingService))).Returns(mockEmbeddingService.Object);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IVectorSearchService))).Returns(mockVectorSearch.Object);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(RetrievalContextAssembler))).Returns(assembler);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IAvailabilityService))).Returns(mockAvailability.Object);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(ISlotGenerationService))).Returns(mockSlotGen.Object);

            var mockLogger = new Mock<ILogger<SemanticKernelOrchestrator>>();

            var mockConversationManager = new Mock<IConversationManager>();
            var sessionState = new AIBookingSessionState();
            mockConversationManager.Setup(c => c.GetOrCreateStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sessionState);

            var mockStudioConfig = new Mock<IAdminAIStudioConfigService>();
            mockStudioConfig.Setup(s => s.GetAsync()).ReturnsAsync(new AdminAIStudioConfigResponse
            {
                AssistantProfile = new AdminAIAssistantProfile
                {
                    Tone = "Tư vấn",
                    RolePrompt = "B",
                    MissingInfoPrompt = "C",
                    SafetyPrompt = "D"
                },
                Handoff = new AdminAIHandoffConfig { Enabled = false },
                ConversationFlows = new List<AdminAIConversationFlow>()
            });

            var mockBookingConductor = new Mock<IBookingConductor>();
            var decision = new ConductorResult
            {
                Action = WebHomestay.Services.ConductorAction.Reply,
                Reason = "general",
                State = new BookingSessionContainer
                {
                    Confirmed = new BookingConfirmedState(),
                    Progress = new BookingProgressState()
                }
            };
            mockBookingConductor.Setup(b => b.DecideAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AIBrainChatRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(decision);

            var mockRoomExplanation = new Mock<IPublicBookingRoomExplanationService>();

            var orchestrator = new SemanticKernelOrchestrator(
                context,
                aiOptions,
                serviceProviderMock.Object,
                mockLogger.Object,
                mockConversationManager.Object,
                mockStudioConfig.Object,
                mockBookingConductor.Object,
                mockRoomExplanation.Object
            );

            var request = new AIBrainChatRequest
            {
                SessionId = "test-sess",
                Message = "Hello, I want to ask about cancellation policies.",
                Mode = ChatMode.PublicBooking
            };

            // Act
            AIBrainChatResponse response = null;
            Exception caughtEx = null;
            try
            {
                response = await orchestrator.ChatAsync(request, CancellationToken.None);
            }
            catch (Exception ex)
            {
                caughtEx = ex;
            }

            // Assert
            Assert.Null(caughtEx);
            Assert.NotNull(response);

            var trace = await context.AIConversationTraces.FirstOrDefaultAsync(t => t.SessionId == "test-sess");
            if (trace == null)
            {
                throw new Exception($"Trace is null. Response Answer: '{response.Answer}', Provider: '{response.ModelProvider}', Persona: '{response.PersonaSummary}'");
            }
            
            // Check that trace.PerformanceLog contains the stage labels we expect
            Assert.NotNull(trace.PerformanceLog);
            Assert.Contains("retrieval", trace.PerformanceLog);
            Assert.Contains("guard", trace.PerformanceLog);
            Assert.Contains("composition", trace.PerformanceLog);
        }

        [Fact]
        public async Task ChatAsync_SemanticRoomDescription_DoesNotReturnStudioEntryBlock()
        {
            using var context = CreateContext();

            var aiOptions = Options.Create(new AIModelOptions
            {
                Provider = "gemini",
                ApiKey = "mock-key",
                Model = "gemini-2.5-flash"
            });

            var serviceProviderMock = new Mock<IServiceProvider>();

            var mockEmbeddingService = new Mock<IEmbeddingService>();
            mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[1536]);

            var mockVectorSearch = new Mock<IVectorSearchService>();
            mockVectorSearch.Setup(v => v.SearchKnowledgeAsync(It.IsAny<float[]>(), It.IsAny<VectorSearchFilter>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<VectorSearchResult>());
            mockVectorSearch.Setup(v => v.SearchRoomsAsync(It.IsAny<float[]>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<VectorSearchResult>());

            var mockAvailability = new Mock<IAvailabilityService>();
            var mockSlotGen = new Mock<ISlotGenerationService>();
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

            serviceProviderMock.Setup(sp => sp.GetService(typeof(ApplicationDbContext))).Returns(context);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IEmbeddingService))).Returns(mockEmbeddingService.Object);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IVectorSearchService))).Returns(mockVectorSearch.Object);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(RetrievalContextAssembler))).Returns(assembler);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IAvailabilityService))).Returns(mockAvailability.Object);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(ISlotGenerationService))).Returns(mockSlotGen.Object);

            var mockConversationManager = new Mock<IConversationManager>();
            mockConversationManager.Setup(c => c.GetOrCreateStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AIBookingSessionState());

            var mockStudioConfig = new Mock<IAdminAIStudioConfigService>();
            mockStudioConfig.Setup(s => s.GetAsync()).ReturnsAsync(new AdminAIStudioConfigResponse
            {
                AssistantProfile = new AdminAIAssistantProfile
                {
                    MissingInfoPrompt = "Bạn muốn đặt theo giờ hay theo ngày?"
                },
                Handoff = new AdminAIHandoffConfig { Enabled = false },
                ConversationFlows = new List<AdminAIConversationFlow>()
            });

            var mockBookingConductor = new Mock<IBookingConductor>();
            mockBookingConductor.Setup(b => b.DecideAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AIBrainChatRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConductorResult
                {
                    Action = WebHomestay.Services.ConductorAction.Reply,
                    Reason = "intent=BrowsingRooms, showCount=0",
                    State = new BookingSessionContainer
                    {
                        Confirmed = new BookingConfirmedState(),
                        Progress = new BookingProgressState()
                    },
                    UiBlocks = new List<object>()
                });

            var orchestrator = new SemanticKernelOrchestrator(
                context,
                aiOptions,
                serviceProviderMock.Object,
                Mock.Of<ILogger<SemanticKernelOrchestrator>>(),
                mockConversationManager.Object,
                mockStudioConfig.Object,
                mockBookingConductor.Object,
                Mock.Of<IPublicBookingRoomExplanationService>());

            var response = await orchestrator.ChatAsync(new AIBrainChatRequest
            {
                SessionId = "semantic-no-entry",
                Message = "cần tìm phòng lãng mạn",
                Mode = ChatMode.PublicBooking
            }, CancellationToken.None);

            Assert.DoesNotContain(response.UiBlocks ?? new List<object>(), block => System.Text.Json.JsonSerializer.Serialize(block).Contains("bookingModeChoice"));
            Assert.NotEqual("entry", response.BookingAction);
        }

        [Fact]
        public async Task ChatAsync_LocationQuestion_UsesBranchFallbackWithoutDebugText()
        {
            using var context = CreateContext();
            context.Branches.Add(new Branch
            {
                Id = 7,
                Name = "LumiStay Riverside Quận 7",
                Address = "123 Nguyen Van Linh, Quan 7, TP.HCM"
            });
            context.SaveChanges();

            var aiOptions = Options.Create(new AIModelOptions
            {
                Provider = "gemini",
                ApiKey = "mock-key",
                Model = "gemini-2.5-flash"
            });

            var serviceProviderMock = new Mock<IServiceProvider>();
            var mockEmbeddingService = new Mock<IEmbeddingService>();
            mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new float[1536]);
            var mockVectorSearch = new Mock<IVectorSearchService>();
            var mockAvailability = new Mock<IAvailabilityService>();
            var mockSlotGen = new Mock<ISlotGenerationService>();
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

            serviceProviderMock.Setup(sp => sp.GetService(typeof(ApplicationDbContext))).Returns(context);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IEmbeddingService))).Returns(mockEmbeddingService.Object);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IVectorSearchService))).Returns(mockVectorSearch.Object);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(RetrievalContextAssembler))).Returns(assembler);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IAvailabilityService))).Returns(mockAvailability.Object);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(ISlotGenerationService))).Returns(mockSlotGen.Object);

            var mockConversationManager = new Mock<IConversationManager>();
            mockConversationManager.Setup(c => c.GetOrCreateStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AIBookingSessionState());

            var mockStudioConfig = new Mock<IAdminAIStudioConfigService>();
            mockStudioConfig.Setup(s => s.GetAsync()).ReturnsAsync(new AdminAIStudioConfigResponse
            {
                AssistantProfile = new AdminAIAssistantProfile(),
                Handoff = new AdminAIHandoffConfig { Enabled = false },
                ConversationFlows = new List<AdminAIConversationFlow>()
            });

            var mockBookingConductor = new Mock<IBookingConductor>();
            mockBookingConductor.Setup(b => b.DecideAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AIBrainChatRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConductorResult
                {
                    Action = WebHomestay.Services.ConductorAction.Reply,
                    Reason = "intent=LocationQuestion, showCount=0",
                    State = new BookingSessionContainer
                    {
                        Confirmed = new BookingConfirmedState(),
                        Progress = new BookingProgressState()
                    },
                    UiBlocks = new List<object>()
                });

            var orchestrator = new SemanticKernelOrchestrator(
                context,
                aiOptions,
                serviceProviderMock.Object,
                Mock.Of<ILogger<SemanticKernelOrchestrator>>(),
                mockConversationManager.Object,
                mockStudioConfig.Object,
                mockBookingConductor.Object,
                Mock.Of<IPublicBookingRoomExplanationService>());

            var response = await orchestrator.ChatAsync(new AIBrainChatRequest
            {
                SessionId = "location-direct",
                Message = "địa chỉ chi nhánh q7",
                Mode = ChatMode.PublicBooking
            }, CancellationToken.None);

            Assert.Contains("Quận 7", response.Answer, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("[DEBUG]", response.Answer, StringComparison.OrdinalIgnoreCase);
        }
    }
}
