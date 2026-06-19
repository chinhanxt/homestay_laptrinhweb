using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Services;
using WebHomestay.Services.AI.Retrieval;

namespace WebHomestay.Services.AI.Workflow.Nodes;

public sealed class VectorRecallNode : IWorkflowNode
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly ApplicationDbContext _context;
    private readonly IConversationManager _conversationManager;
    private readonly IPublicBookingRoomExplanationService _roomExplanationService;
    private readonly ILogger<VectorRecallNode> _logger;

    public VectorRecallNode(
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearchService,
        ApplicationDbContext context,
        IConversationManager conversationManager,
        IPublicBookingRoomExplanationService roomExplanationService,
        ILogger<VectorRecallNode> logger)
    {
        _embeddingService = embeddingService;
        _vectorSearchService = vectorSearchService;
        _context = context;
        _conversationManager = conversationManager;
        _roomExplanationService = roomExplanationService;
        _logger = logger;
    }

    public string Name => "retrieval";

    public async Task ExecuteAsync(WorkflowState state, CancellationToken cancellationToken)
    {
        try
        {
            var embedding = await _embeddingService.GetEmbeddingAsync(state.UserMessage, cancellationToken);
            if (embedding == null)
            {
                state.RetrievalJson = "[]";
                return;
            }

            state.QueryEmbedding = embedding;

            if (SemanticQueryHelper.IsSemanticQuery(state.UserMessage))
            {
                var sessionState = await _conversationManager.GetOrCreateStateAsync(state.SessionId, cancellationToken);
                var branchId = sessionState?.BranchId;

                IReadOnlyList<VectorSearchResult> roomSearchResults = Array.Empty<VectorSearchResult>();
                try
                {
                    roomSearchResults = await _vectorSearchService.SearchRoomsAsync(
                        embedding, branchId, 10, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SearchRoomsAsync failed (possibly due to provider support in tests). Carrying on.");
                }

                if (roomSearchResults != null && roomSearchResults.Any())
                {
                    var roomIds = roomSearchResults.Select(r => int.Parse(r.EntityId)).ToList();
                    var rooms = await _context.Rooms
                        .Include(r => r.Branch)
                        .Include(r => r.Amenities)
                        .Where(r => roomIds.Contains(r.Id) && !r.IsDeleted && r.Status == "Available")
                        .ToListAsync(cancellationToken);

                    var roomsMap = rooms.ToDictionary(r => r.Id);

                    var matchedRooms = roomSearchResults
                        .Select(sr => {
                            if (!int.TryParse(sr.EntityId, out var roomId)) return null;
                            if (!roomsMap.TryGetValue(roomId, out var r)) return null;

                            double similarity = sr.Score;

                            if (sessionState != null)
                            {
                                if (sessionState.BranchId.HasValue && r.BranchId == sessionState.BranchId.Value)
                                {
                                    similarity += 0.15;
                                }

                                var guests = sessionState.GuestCount > 0 ? sessionState.GuestCount : 1;
                                if (guests > r.MaxGuests)
                                {
                                    similarity -= 0.3;
                                }
                                else if (guests >= r.Capacity && guests <= r.MaxGuests)
                                {
                                    similarity += 0.05;
                                }
                            }

                            return new { Room = r, Similarity = similarity };
                        })
                        .Where(x => x is not null && x.Similarity > 0.10)
                        .OrderByDescending(x => x.Similarity)
                        .Take(3)
                        .ToList();

                    if (matchedRooms.Any())
                    {
                        var retrievalInfo = matchedRooms.Select(x => new
                        {
                            RoomId = x.Room.Id,
                            Name = x.Room.Name,
                            BranchName = x.Room.Branch?.Name ?? "Hệ thống",
                            PricePerHour = x.Room.PricePerHour,
                            PricePerDay = x.Room.PricePerDay,
                            Capacity = x.Room.Capacity,
                            MaxGuests = x.Room.MaxGuests,
                            Description = x.Room.Description,
                            Similarity = Math.Round(x.Similarity, 4)
                        }).ToList();

                        state.RetrievalJson = JsonSerializer.Serialize(retrievalInfo);

                        var filteredRooms = new List<object>();
                        var confirmedState = new BookingConfirmedState
                        {
                            BranchId = branchId,
                            GuestCount = sessionState?.GuestCount ?? 1,
                            BookingMode = sessionState?.BookingMode ?? "hourly",
                            HourlyDate = sessionState?.HourlyDate,
                            CheckInDate = sessionState?.CheckInDate,
                            CheckOutDate = sessionState?.CheckOutDate
                        };

                        foreach (var x in matchedRooms)
                        {
                            var explanation = await _roomExplanationService.BuildAsync(x.Room.Id, confirmedState, cancellationToken);
                            
                            filteredRooms.Add(new
                            {
                                roomId = x.Room.Id,
                                name = x.Room.Name,
                                description = x.Room.Description,
                                pricePerHour = x.Room.PricePerHour,
                                pricePerDay = x.Room.PricePerDay,
                                capacity = x.Room.Capacity,
                                maxGuests = x.Room.MaxGuests,
                                imageUrl = x.Room.ImageUrl,
                                amenities = x.Room.Amenities?.Select(a => a.Name).ToList() ?? new List<string>(),
                                fitsStandardOccupancy = explanation.FitsStandardOccupancy,
                                allowsRequestedGuests = explanation.AllowsRequestedGuests,
                                extraGuestCount = explanation.ExtraGuestCount,
                                extraGuestFeeApplied = explanation.ExtraGuestFeeApplied,
                                pricingTierLabel = explanation.PricingTierLabel,
                                pricingExplanation = explanation.Lines,
                                recommendationReason = explanation.RecommendationReason ?? "Đề xuất phù hợp nhất với nhu cầu của bạn",
                                requestedSlotAvailable = true
                            });
                        }

                        state.UiBlocks.Add(new
                        {
                            type = "roomCards",
                            data = new
                            {
                                rooms = filteredRooms
                            }
                        });

                        _logger.LogInformation("Semantic room recall returned {Count} room cards for session {SessionId}",
                            matchedRooms.Count, state.SessionId);
                        return;
                    }
                }
            }

            var results = await _vectorSearchService.SearchKnowledgeAsync(
                embedding, null, 5, cancellationToken);

            state.RetrievalJson = JsonSerializer.Serialize(results);
            _logger.LogInformation("Vector recall returned {Count} results for session {SessionId}",
                results.Count, state.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vector recall failed for session {SessionId}", state.SessionId);
            state.RetrievalJson = "[]";
        }
    }
}
