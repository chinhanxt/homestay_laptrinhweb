using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services.AI.Workflow.Nodes;

namespace WebHomestay.Services.AI.Workflow;

public class LangGraphOrchestrator : IAIBrainOrchestrator
{
    private readonly IConversationManager _conversationManager;
    private readonly WorkflowRunner _runner;
    private readonly BookingGuardNode _guardNode;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LangGraphOrchestrator> _logger;

    public LangGraphOrchestrator(
        IConversationManager conversationManager,
        WorkflowRunner runner,
        BookingGuardNode guardNode,
        ApplicationDbContext context,
        ILogger<LangGraphOrchestrator> logger)
    {
        _conversationManager = conversationManager;
        _runner = runner;
        _guardNode = guardNode;
        _context = context;
        _logger = logger;
    }

    public async Task<AIBrainChatResponse> ChatAsync(AIBrainChatRequest request, CancellationToken cancellationToken = default)
    {
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var state = new WorkflowState
        {
            SessionId = sessionId,
            UserMessage = request.Message
        };

        _guardNode.Reset();

        await _runner.RunAsync(state, cancellationToken);

        var sessionState = await _conversationManager.GetOrCreateStateAsync(sessionId, cancellationToken);

        stopwatch.Stop();

        var guardResult = _guardNode.ShortCircuited ? "booking-guard" : "workflow-composer";
        var provider = _guardNode.ShortCircuited ? "langgraph-guard" : "langgraph-workflow";

        var trace = await PersistTraceAsync(
            sessionId,
            request.Message,
            guardResult,
            state.RetrievalJson,
            state.GraphJson,
            state.FinalAnswer,
            provider,
            state.StageLog,
            stopwatch.ElapsedMilliseconds,
            cancellationToken);

        await _conversationManager.UpdateStateAsync(sessionId, sessionState, cancellationToken);

        return new AIBrainChatResponse
        {
            TraceId = trace.Id,
            Answer = state.FinalAnswer,
            PersonaSummary = "LangGraph Workflow",
            GuardResult = guardResult,
            ModelProvider = provider,
            IsMock = false,
            FormSchema = "[]",
            BookingAction = "reply",
            BookingState = sessionState
        };
    }

    private async Task<AIConversationTrace> PersistTraceAsync(
        string sessionId,
        string customerMessage,
        string guardResult,
        string retrievedKnowledgeJson,
        string graphReasoningJson,
        string finalAnswer,
        string provider,
        List<string> stageLogs,
        long responseTimeMs,
        CancellationToken cancellationToken)
    {
        var trace = new AIConversationTrace
        {
            SessionId = sessionId,
            CustomerMessage = customerMessage,
            PersonaSummary = "LangGraph Workflow",
            LiveSystemSnapshot = guardResult,
            RetrievedKnowledgeJson = retrievedKnowledgeJson,
            GraphReasoningJson = graphReasoningJson,
            GuardResult = guardResult,
            FinalAnswer = finalAnswer,
            ModelProvider = provider,
            ResponseTimeMs = (int)responseTimeMs,
            PerformanceLog = string.Join(",", stageLogs)
        };

        _context.AIConversationTraces.Add(trace);
        await _context.SaveChangesAsync(cancellationToken);
        return trace;
    }
}
