using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebHomestay.Models;
using WebHomestay.Models.AI;

namespace WebHomestay.Services.AI;

public interface IRoomRecommendationEngine
{
    Task<List<ScoredRoom>> ScoreAndRankRoomsAsync(
        List<Room> availableRooms,
        RecommendationCriteria criteria,
        CancellationToken cancellationToken = default);
}
