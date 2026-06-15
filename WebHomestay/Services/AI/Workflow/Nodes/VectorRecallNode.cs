using System.Text.Json;
using WebHomestay.Services.AI.Retrieval;

namespace WebHomestay.Services.AI.Workflow.Nodes;

public sealed class VectorRecallNode : IWorkflowNode
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly ILogger<VectorRecallNode> _logger;

    public VectorRecallNode(
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearchService,
        ILogger<VectorRecallNode> logger)
    {
        _embeddingService = embeddingService;
        _vectorSearchService = vectorSearchService;
        _logger = logger;
    }

    public string Name => "retrieval";

    public async Task ExecuteAsync(WorkflowState state, CancellationToken cancellationToken)
    {
        try
        {
            var embedding = await _embeddingService.GetEmbeddingAsync(state.UserMessage, cancellationToken);
            if (embedding == null)
            {
                state.RetrievalJson = "[]";
                return;
            }

            state.QueryEmbedding = embedding;

            var results = await _vectorSearchService.SearchKnowledgeAsync(
                embedding, null, 5, cancellationToken);

            state.RetrievalJson = JsonSerializer.Serialize(results);
            _logger.LogInformation("Vector recall returned {Count} results for session {SessionId}",
                results.Count, state.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vector recall failed for session {SessionId}", state.SessionId);
            state.RetrievalJson = "[]";
        }
    }
}
