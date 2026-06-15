using WebHomestay.Models.AI;

namespace WebHomestay.Services.AI.Workflow.Nodes;

public sealed class IntentClassifierNode : IWorkflowNode
{
    private readonly IConversationManager _conversationManager;
    private readonly IBookingConductor _bookingConductor;
    private readonly ILogger<IntentClassifierNode> _logger;

    public IntentClassifierNode(
        IConversationManager conversationManager,
        IBookingConductor bookingConductor,
        ILogger<IntentClassifierNode> logger)
    {
        _conversationManager = conversationManager;
        _bookingConductor = bookingConductor;
        _logger = logger;
    }

    public string Name => "intent";

    public async Task ExecuteAsync(WorkflowState state, CancellationToken cancellationToken)
    {
        var sessionState = await _conversationManager.GetOrCreateStateAsync(state.SessionId, cancellationToken);

        var request = new AIBrainChatRequest
        {
            SessionId = state.SessionId,
            Message = state.UserMessage,
            Mode = ChatMode.PublicBooking
        };

        try
        {
            var decision = await _bookingConductor.DecideAsync(state.SessionId, state.UserMessage, request, cancellationToken);
            _logger.LogInformation("Intent classified for session {SessionId}: {Action}", state.SessionId, decision.Action);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Intent classification failed for session {SessionId}", state.SessionId);
        }
    }
}
