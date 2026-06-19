using System.Threading;
using System.Threading.Tasks;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Entities.AI;

namespace WebHomestay.Services.AI;

public interface IIntentClassifierService
{
    Task<IntentClassificationResult> ClassifyAsync(
        string message, 
        AIBookingSessionState sessionState, 
        CancellationToken cancellationToken = default);
}
