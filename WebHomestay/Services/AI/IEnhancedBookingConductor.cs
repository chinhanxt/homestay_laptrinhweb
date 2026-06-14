using System.Threading;
using System.Threading.Tasks;
using WebHomestay.Models.AI;

namespace WebHomestay.Services.AI;

public interface IEnhancedBookingConductor : IBookingConductor
{
    Task<ConductorDecision> MakeDecisionAsync(
        string sessionId, 
        string message, 
        AIBookingSessionState sessionState,
        CancellationToken cancellationToken = default);
}
