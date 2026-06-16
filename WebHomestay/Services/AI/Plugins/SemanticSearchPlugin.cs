using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services.AI.Retrieval;

namespace WebHomestay.Services.AI.Plugins
{
    public class SemanticSearchPlugin
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorSearchService _vectorSearchService;
        private readonly AIBookingSessionState? _sessionState;
        private readonly StringBuilder _logs;

        public SemanticSearchPlugin(
            ApplicationDbContext context,
            IEmbeddingService embeddingService,
            IVectorSearchService vectorSearchService,
            AIBookingSessionState? sessionState = null)
        {
            _context = context;
            _embeddingService = embeddingService;
            _vectorSearchService = vectorSearchService;
            _sessionState = sessionState;
            _logs = new StringBuilder();
        }

        [KernelFunction("SemanticSearchRooms")]
        [Description("Tìm kiếm và đề xuất các phòng dựa trên mô tả nhu cầu, tiện ích hoặc phong cách bằng ngôn ngữ tự nhiên (VD: phòng có ban công riêng tư, phòng lãng mạn cho trăng mật, phòng giá rẻ, yên tĩnh có bồn tắm nằm).")]
        public async Task<string> SemanticSearchRooms(
            [Description("Mô tả nhu cầu, sở thích hoặc mong muốn của khách hàng đối với phòng (VD: yên tĩnh, có bồn tắm, ban công rộng, lãng mạn)")] string userPreference)
        {
            _logs.AppendLine($"[SemanticSearchRooms] query={userPreference}");
            
            try
            {
                var queryEmbedding = await _embeddingService.GetEmbeddingAsync(userPreference, CancellationToken.None);
                var searchResults = await _vectorSearchService.SearchRoomsAsync(
                    queryEmbedding,
                    _sessionState?.BranchId,
                    10,
                    CancellationToken.None);

                System.Collections.Generic.List<object> matchedRooms = new System.Collections.Generic.List<object>();

                if (searchResults.Any())
                {
                    var roomIds = searchResults.Select(r => int.Parse(r.EntityId)).ToList();
                    var roomsMap = await _context.Rooms
                        .Include(r => r.Branch)
                        .Where(r => roomIds.Contains(r.Id))
                        .ToDictionaryAsync(r => r.Id);

                    matchedRooms = searchResults
                        .Select(sr => {
                            var roomId = int.Parse(sr.EntityId);
                            if (!roomsMap.TryGetValue(roomId, out var r)) return null;

                            double similarity = sr.Score;

                            // Boost based on current session context
                            if (_sessionState != null)
                            {
                                // Boost rooms in current selected branch
                                if (_sessionState.BranchId.HasValue && r.BranchId == _sessionState.BranchId.Value)
                                {
                                    similarity += 0.15;
                                }
                                
                                // Check capacity warning (mild penalty if capacity is strictly exceeded)
                                var guests = _sessionState.GuestCount > 0 ? _sessionState.GuestCount : 1;
                                if (guests > r.MaxGuests)
                                {
                                    similarity -= 0.3; // Substantial penalty if guests exceed max capacity
                                }
                                else if (guests >= r.Capacity && guests <= r.MaxGuests)
                                {
                                    similarity += 0.05; // Slight boost if guest count fits within capacity boundaries
                                }
                            }

                            return new { Room = r, Similarity = similarity };
                        })
                        .Where(x => x is not null && x.Similarity > 0.35)
                        .OrderByDescending(x => x.Similarity)
                        .Take(3)
                        .Select(x => (object)new
                        {
                            RoomId = x!.Room.Id,
                            Name = x.Room.Name,
                            BranchName = x.Room.Branch?.Name ?? "Hệ thống",
                            PricePerHour = x.Room.PricePerHour,
                            PricePerDay = x.Room.PricePerDay,
                            Capacity = x.Room.Capacity,
                            MaxGuests = x.Room.MaxGuests,
                            Description = x.Room.Description,
                            Similarity = Math.Round(x.Similarity, 4)
                        })
                        .ToList();
                }

                if (!matchedRooms.Any())
                {
                    var failStr = "Không tìm thấy phòng phù hợp từ semantic retrieval trong nguồn dữ liệu hiện tại.";
                    _logs.AppendLine($"[Result SemanticSearchRooms] {failStr}");
                    return failStr;
                }

                var successStr = "Tìm thấy các phòng phù hợp sau:\n" + JsonSerializer.Serialize(matchedRooms);
                _logs.AppendLine($"[Result SemanticSearchRooms] {successStr}");
                return successStr;
            }
            catch (EmbeddingUnavailableException ex)
            {
                _logs.AppendLine($"[SemanticSearchRooms Error] {ex.Message}");
                return "Hệ thống semantic retrieval hiện chưa sẵn sàng vì embedding chưa khả dụng.";
            }
            catch (Exception ex)
            {
                _logs.AppendLine($"[SemanticSearchRooms Error] {ex.Message}");
                return $"Lỗi tìm kiếm phòng: {ex.Message}";
            }
        }

        public string GetLogs() => _logs.ToString();
    }
}
