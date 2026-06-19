using WebHomestay.Models.Configuration;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Entities.AI;

namespace WebHomestay.Services.AI;

public interface IConversationManager
{
    Task<AIBookingSessionState> GetOrCreateStateAsync(string sessionId, CancellationToken cancellationToken = default);
    Task UpdateStateAsync(string sessionId, AIBookingSessionState state, CancellationToken cancellationToken = default);
    Task<List<ConversationTurn>> GetHistoryAsync(string sessionId, int maxTurns = 50, CancellationToken cancellationToken = default);
    Task AddTurnAsync(string sessionId, string userMessage, string aiResponse, CancellationToken cancellationToken = default);
    Task<T?> ResolveReferenceAsync<T>(string sessionId, string reference, int lookbackTurns = 5) where T : class;
}
