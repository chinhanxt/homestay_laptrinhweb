using RoomEntity = WebHomestay.Models.Entities.Core.Room;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Entities.AI;

namespace WebHomestay.Services.AI;

public interface IRoomRecommendationEngine
{
    Task<List<ScoredRoom>> ScoreAndRankRoomsAsync(
        List<RoomEntity> availableRooms,
        RecommendationCriteria criteria,
        CancellationToken cancellationToken = default);
}
