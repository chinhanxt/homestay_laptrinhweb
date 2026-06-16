using WebHomestay.Services.AI.Workflow;

namespace WebHomestay.Services.AI.Compatibility;

public sealed class ModeAwareAIBrainOrchestrator : IAIBrainOrchestrator
{
    private readonly IPublicBookingBrainOrchestrator _publicBookingOrchestrator;
    private readonly IWorkflowBrainOrchestrator _workflowOrchestrator;

    public ModeAwareAIBrainOrchestrator(
        IPublicBookingBrainOrchestrator publicBookingOrchestrator,
        IWorkflowBrainOrchestrator workflowOrchestrator)
    {
        _publicBookingOrchestrator = publicBookingOrchestrator;
        _workflowOrchestrator = workflowOrchestrator;
    }

    public Task<AIBrainChatResponse> ChatAsync(AIBrainChatRequest request, CancellationToken cancellationToken = default)
    {
        return request.Mode == ChatMode.PublicBooking
            ? _publicBookingOrchestrator.ChatAsync(request, cancellationToken)
            : _workflowOrchestrator.ChatAsync(request, cancellationToken);
    }
}
