namespace WebHomestay.Services.AI.Workflow.Nodes;

public sealed class IntentClassifierNode : IWorkflowNode
{
    private readonly IConversationManager _conversationManager;
    private readonly ILogger<IntentClassifierNode> _logger;

    public IntentClassifierNode(
        IConversationManager conversationManager,
        ILogger<IntentClassifierNode> logger)
    {
        _conversationManager = conversationManager;
        _logger = logger;
    }

    public string Name => "intent";

    public async Task ExecuteAsync(WorkflowState state, CancellationToken cancellationToken)
    {
        // Just ensure the session state exists in conversation manager.
        // The actual intent classification + booking decision happens in BookingGuardNode,
        // which calls DecideAsync. Doing it here too would be a duplicate LLM call (~2s wasted).
        await _conversationManager.GetOrCreateStateAsync(state.SessionId, cancellationToken);
        _logger.LogInformation("Session state initialized for {SessionId}", state.SessionId);
    }
}
