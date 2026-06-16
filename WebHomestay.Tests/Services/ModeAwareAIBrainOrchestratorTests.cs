using Moq;
using WebHomestay.Services;
using WebHomestay.Services.AI;
using WebHomestay.Services.AI.Compatibility;
using WebHomestay.Services.AI.Workflow;
using Xunit;

namespace WebHomestay.Tests.Services;

public class ModeAwareAIBrainOrchestratorTests
{
    [Fact]
    public async Task ChatAsync_PublicBooking_UsesLegacyPublicBookingOrchestrator()
    {
        var publicBooking = new Mock<IPublicBookingBrainOrchestrator>();
        var workflow = new Mock<IWorkflowBrainOrchestrator>();

        publicBooking
            .Setup(x => x.ChatAsync(It.IsAny<AIBrainChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIBrainChatResponse { Answer = "public" });

        var orchestrator = new ModeAwareAIBrainOrchestrator(publicBooking.Object, workflow.Object);

        var response = await orchestrator.ChatAsync(new AIBrainChatRequest
        {
            Mode = ChatMode.PublicBooking,
            Message = "tui cần tìm 1 phòng lãng mạn"
        });

        Assert.Equal("public", response.Answer);
        publicBooking.Verify(x => x.ChatAsync(It.IsAny<AIBrainChatRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        workflow.Verify(x => x.ChatAsync(It.IsAny<AIBrainChatRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChatAsync_AdminAssistant_UsesWorkflowOrchestrator()
    {
        var publicBooking = new Mock<IPublicBookingBrainOrchestrator>();
        var workflow = new Mock<IWorkflowBrainOrchestrator>();

        workflow
            .Setup(x => x.ChatAsync(It.IsAny<AIBrainChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIBrainChatResponse { Answer = "workflow" });

        var orchestrator = new ModeAwareAIBrainOrchestrator(publicBooking.Object, workflow.Object);

        var response = await orchestrator.ChatAsync(new AIBrainChatRequest
        {
            Mode = ChatMode.AdminAssistant,
            Message = "tổng hợp insight"
        });

        Assert.Equal("workflow", response.Answer);
        workflow.Verify(x => x.ChatAsync(It.IsAny<AIBrainChatRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        publicBooking.Verify(x => x.ChatAsync(It.IsAny<AIBrainChatRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
