using System.Text.Json;
using WebHomestay.Services.AI.Graph;

namespace WebHomestay.Services.AI.Workflow.Nodes;

public sealed class GraphExpansionNode : IWorkflowNode
{
    private readonly Neo4jGraphExpansionService _graphExpansionService;
    private readonly ILogger<GraphExpansionNode> _logger;

    public GraphExpansionNode(
        Neo4jGraphExpansionService graphExpansionService,
        ILogger<GraphExpansionNode> logger)
    {
        _graphExpansionService = graphExpansionService;
        _logger = logger;
    }

    public string Name => "graph";

    public async Task ExecuteAsync(WorkflowState state, CancellationToken cancellationToken)
    {
        try
        {
            var seeds = ExtractSeedKeys(state.UserMessage);
            if (seeds.Count == 0)
            {
                state.GraphJson = "{}";
                return;
            }

            var context = await _graphExpansionService.ExpandAsync(
                seeds, maxHops: 2, cancellationToken: cancellationToken);

            state.GraphJson = JsonSerializer.Serialize(context);
            _logger.LogInformation("Graph expansion returned {NodeCount} nodes, {EdgeCount} edges for session {SessionId}",
                context.NodeSummaries.Count, context.EdgeSummaries.Count, state.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graph expansion failed for session {SessionId}", state.SessionId);
            state.GraphJson = "{}";
        }
    }

    private static IReadOnlyList<string> ExtractSeedKeys(string message)
    {
        var words = (message ?? string.Empty)
            .Split(new[] { ' ', ',', '.', '!', '?', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim().ToLowerInvariant())
            .Where(w => w.Length > 2)
            .Distinct()
            .Take(5)
            .ToList();

        return words;
    }
}
