namespace WebHomestay.Services
{
    public interface IAIModelClient
    {
        Task<AIModelResponse> CompleteAsync(AIModelRequest request, CancellationToken cancellationToken = default);
    }
}
