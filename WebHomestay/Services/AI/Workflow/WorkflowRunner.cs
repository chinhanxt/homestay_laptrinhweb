namespace WebHomestay.Services.AI.Workflow;

public sealed class WorkflowRunner
{
    private readonly IReadOnlyList<IWorkflowNode> _nodes;

    public WorkflowRunner(IEnumerable<IWorkflowNode> nodes)
    {
        _nodes = nodes.ToList();
    }

    public async Task RunAsync(WorkflowState state, CancellationToken cancellationToken)
    {
        foreach (var node in _nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            state.StageLog.Add(node.Name);
            await node.ExecuteAsync(state, cancellationToken);
        }
    }
}
