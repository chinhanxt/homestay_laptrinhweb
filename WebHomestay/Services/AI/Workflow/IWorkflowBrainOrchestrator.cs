namespace WebHomestay.Services.AI.Workflow
{
    public interface IWorkflowBrainOrchestrator
    {
        Task<AIBrainChatResponse> ChatAsync(AIBrainChatRequest request, CancellationToken cancellationToken = default);
    }
}
