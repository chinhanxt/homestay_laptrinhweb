using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Entities.AI;

namespace WebHomestay.Services.AI;

public interface IRoomDisplayLogic
{
    Task<List<object>> GenerateRoomUiBlocksAsync(
        List<ScoredRoom> recommendedRooms,
        AIBookingSessionState sessionState,
        CancellationToken cancellationToken = default);
        
    Task<bool> CanShowRoomsAsync(AIBookingSessionState sessionState, CancellationToken cancellationToken = default);
        
    Task<List<object>> GenerateAlternativeSuggestionsAsync(
        RecommendationCriteria criteria,
        CancellationToken cancellationToken = default);
}
