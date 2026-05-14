namespace WebHomestay.Services
{
    public interface IAIBrainOrchestrator
    {
        Task<AIBrainChatResponse> ChatAsync(AIBrainChatRequest request, CancellationToken cancellationToken = default);
    }
}
