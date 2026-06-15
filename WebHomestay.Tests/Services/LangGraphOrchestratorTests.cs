using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.AI;
using WebHomestay.Services;
using WebHomestay.Services.AI;
using WebHomestay.Services.AI.Graph;
using WebHomestay.Services.AI.Retrieval;
using WebHomestay.Services.AI.Workflow;
using WebHomestay.Services.AI.Workflow.Nodes;
using Xunit;
using ConductorAction = WebHomestay.Services.ConductorAction;

namespace WebHomestay.Tests.Services;

public class LangGraphOrchestratorTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ChatAsync_RecordsWorkflowStagesInOrder()
    {
        using var context = CreateContext();

        var mockConversationManager = new Mock<IConversationManager>();
        var sessionState = new AIBookingSessionState();
        mockConversationManager.Setup(c => c.GetOrCreateStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);

        var mockLogger = new Mock<ILogger<LangGraphOrchestrator>>();

        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1536]);

        var mockVectorSearch = new Mock<IVectorSearchService>();
        mockVectorSearch.Setup(v => v.SearchKnowledgeAsync(
                It.IsAny<float[]>(), It.IsAny<VectorSearchFilter>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<VectorSearchResult>());

        var mockGraphClient = new Mock<INeo4jGraphClient>();
        var graphExpansionService = new Neo4jGraphExpansionService(mockGraphClient.Object);

        var mockBookingConductor = new Mock<IBookingConductor>();
        mockBookingConductor.Setup(b => b.DecideAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AIBrainChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConductorResult
            {
                Action = ConductorAction.Reply,
                Reason = "general",
                State = new BookingSessionContainer
                {
                    Confirmed = new BookingConfirmedState(),
                    Progress = new BookingProgressState()
                }
            });

        var mockAiModelClient = new Mock<IAIModelClient>();
        mockAiModelClient.Setup(m => m.CompleteAsync(It.IsAny<AIModelRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIModelResponse { Content = "Xin chào, tôi có thể giúp gì cho bạn?" });

        var mockBookingLogger = new Mock<ILogger<BookingGuardNode>>();
        var mockGraphLogger = new Mock<ILogger<GraphExpansionNode>>();
        var mockVectorLogger = new Mock<ILogger<VectorRecallNode>>();
        var mockIntentLogger = new Mock<ILogger<IntentClassifierNode>>();
        var mockComposerLogger = new Mock<ILogger<ResponseComposerNode>>();

        var nodes = new IWorkflowNode[]
        {
            new IntentClassifierNode(mockConversationManager.Object, mockBookingConductor.Object, mockIntentLogger.Object),
            new VectorRecallNode(mockEmbeddingService.Object, mockVectorSearch.Object, mockVectorLogger.Object),
            new GraphExpansionNode(graphExpansionService, mockGraphLogger.Object),
            new BookingGuardNode(mockBookingConductor.Object, mockConversationManager.Object, mockBookingLogger.Object),
            new ResponseComposerNode(mockAiModelClient.Object, mockComposerLogger.Object)
        };

        var runner = new WorkflowRunner(nodes);

        var guardNode = (BookingGuardNode)nodes[3];

        var orchestrator = new LangGraphOrchestrator(
            mockConversationManager.Object,
            runner,
            guardNode,
            context,
            mockLogger.Object);

        var request = new AIBrainChatRequest
        {
            SessionId = "test-session",
            Message = "Tôi muốn hỏi về chính sách đặt phòng",
            Mode = ChatMode.PublicBooking
        };

        var response = await orchestrator.ChatAsync(request, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("Xin chào, tôi có thể giúp gì cho bạn?", response.Answer);

        var trace = await context.AIConversationTraces
            .FirstOrDefaultAsync(t => t.SessionId == "test-session");
        Assert.NotNull(trace);
        Assert.NotNull(trace.PerformanceLog);
        Assert.Contains("intent", trace.PerformanceLog);
        Assert.Contains("retrieval", trace.PerformanceLog);
        Assert.Contains("graph", trace.PerformanceLog);
        Assert.Contains("guard", trace.PerformanceLog);
        Assert.Contains("composition", trace.PerformanceLog);

        var stageOrder = trace.PerformanceLog.Split(',');
        Assert.Equal(5, stageOrder.Length);
        Assert.Equal("intent", stageOrder[0]);
        Assert.Equal("retrieval", stageOrder[1]);
        Assert.Equal("graph", stageOrder[2]);
        Assert.Equal("guard", stageOrder[3]);
        Assert.Equal("composition", stageOrder[4]);
    }

    [Fact]
    public async Task ChatAsync_WhenBookingGuardOwnsDecision_SkipsGenerationNode()
    {
        using var context = CreateContext();

        var mockConversationManager = new Mock<IConversationManager>();
        var sessionState = new AIBookingSessionState();
        mockConversationManager.Setup(c => c.GetOrCreateStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);

        var mockLogger = new Mock<ILogger<LangGraphOrchestrator>>();

        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService.Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1536]);

        var mockVectorSearch = new Mock<IVectorSearchService>();
        mockVectorSearch.Setup(v => v.SearchKnowledgeAsync(
                It.IsAny<float[]>(), It.IsAny<VectorSearchFilter>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<VectorSearchResult>());

        var mockGraphClient = new Mock<INeo4jGraphClient>();
        var graphExpansionService = new Neo4jGraphExpansionService(mockGraphClient.Object);

        var mockBookingConductor = new Mock<IBookingConductor>();
        mockBookingConductor.Setup(b => b.DecideAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AIBrainChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConductorResult
            {
                Action = ConductorAction.AskInfo,
                Reason = "missing-branch",
                State = new BookingSessionContainer
                {
                    Confirmed = new BookingConfirmedState
                    {
                        MissingRequiredFields = new List<string> { "branchId" }
                    },
                    Progress = new BookingProgressState()
                }
            });

        var mockAiModelClient = new Mock<IAIModelClient>();

        var mockBookingLogger = new Mock<ILogger<BookingGuardNode>>();
        var mockGraphLogger = new Mock<ILogger<GraphExpansionNode>>();
        var mockVectorLogger = new Mock<ILogger<VectorRecallNode>>();
        var mockIntentLogger = new Mock<ILogger<IntentClassifierNode>>();
        var mockComposerLogger = new Mock<ILogger<ResponseComposerNode>>();

        var nodes = new IWorkflowNode[]
        {
            new IntentClassifierNode(mockConversationManager.Object, mockBookingConductor.Object, mockIntentLogger.Object),
            new VectorRecallNode(mockEmbeddingService.Object, mockVectorSearch.Object, mockVectorLogger.Object),
            new GraphExpansionNode(graphExpansionService, mockGraphLogger.Object),
            new BookingGuardNode(mockBookingConductor.Object, mockConversationManager.Object, mockBookingLogger.Object),
            new ResponseComposerNode(mockAiModelClient.Object, mockComposerLogger.Object)
        };

        var runner = new WorkflowRunner(nodes);

        var guardNode = (BookingGuardNode)nodes[3];

        var orchestrator = new LangGraphOrchestrator(
            mockConversationManager.Object,
            runner,
            guardNode,
            context,
            mockLogger.Object);

        var request = new AIBrainChatRequest
        {
            SessionId = "test-session-guard",
            Message = "Tôi muốn đặt phòng",
            Mode = ChatMode.PublicBooking
        };

        var response = await orchestrator.ChatAsync(request, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Contains("thông tin", response.Answer);
        Assert.DoesNotContain("Xin chào", response.Answer);

        var trace = await context.AIConversationTraces
            .FirstOrDefaultAsync(t => t.SessionId == "test-session-guard");
        Assert.NotNull(trace);
        Assert.Contains("intent", trace.PerformanceLog);
        Assert.Contains("retrieval", trace.PerformanceLog);
        Assert.Contains("graph", trace.PerformanceLog);
        Assert.Contains("guard", trace.PerformanceLog);

        var stageOrder = trace.PerformanceLog.Split(',');
        Assert.Equal(5, stageOrder.Length);
        Assert.Equal("composition", stageOrder[4]);
    }
}
