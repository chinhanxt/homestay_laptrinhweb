namespace WebHomestay.Services.AI.Workflow;

public interface IWorkflowNode
{
    string Name { get; }
    Task ExecuteAsync(WorkflowState state, CancellationToken cancellationToken);
}
