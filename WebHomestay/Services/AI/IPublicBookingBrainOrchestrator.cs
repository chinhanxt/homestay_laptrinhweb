namespace WebHomestay.Services.AI
{
    public interface IPublicBookingBrainOrchestrator
    {
        Task<AIBrainChatResponse> ChatAsync(AIBrainChatRequest request, CancellationToken cancellationToken = default);
    }
}
