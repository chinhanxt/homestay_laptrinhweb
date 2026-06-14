using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Services.AI.Plugins
{
    public class SemanticSearchPlugin
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmbeddingService _embeddingService;
        private readonly AIBookingSessionState? _sessionState;
        private readonly StringBuilder _logs;

        public SemanticSearchPlugin(
            ApplicationDbContext context,
            IEmbeddingService embeddingService,
            AIBookingSessionState? sessionState = null)
        {
            _context = context;
            _embeddingService = embeddingService;
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
                // Fetch active rooms
                var rooms = await _context.Rooms
                    .Include(r => r.Branch)
                    .Where(r => r.Status == "Available")
                    .ToListAsync();

                var queryEmbedding = await _embeddingService.GetEmbeddingAsync(userPreference);

                var matchedRooms = rooms
                    .Where(r => r.Embedding != null)
                    .Select(r => {
                        double similarity = CosineSimilarity(r.Embedding, queryEmbedding);
                        
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
                    .Where(x => x.Similarity > 0.35) // similarity threshold
                    .OrderByDescending(x => x.Similarity)
                    .Take(3)
                    .Select(x => new
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
                    })
                    .ToList();

                if (!matchedRooms.Any())
                {
                    _logs.AppendLine("[SemanticSearchRooms] No semantic room matches found. Falling back to keyword search.");
                    
                    // Simple fallback keyword match
                    var terms = userPreference.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                    matchedRooms = rooms
                        .Where(r => terms.Any(t => 
                            r.Name.Contains(t, StringComparison.OrdinalIgnoreCase) || 
                            (r.Description != null && r.Description.Contains(t, StringComparison.OrdinalIgnoreCase))))
                        .Take(3)
                        .Select(r => new
                        {
                            RoomId = r.Id,
                            Name = r.Name,
                            BranchName = r.Branch?.Name ?? "Hệ thống",
                            PricePerHour = r.PricePerHour,
                            PricePerDay = r.PricePerDay,
                            Capacity = r.Capacity,
                            MaxGuests = r.MaxGuests,
                            Description = r.Description,
                            Similarity = 0.5 // Static fallback score
                        })
                        .ToList();
                }

                if (!matchedRooms.Any())
                {
                    var failStr = "Không tìm thấy phòng nào phù hợp với mô tả của bạn.";
                    _logs.AppendLine($"[Result SemanticSearchRooms] {failStr}");
                    return failStr;
                }

                var successStr = "Tìm thấy các phòng phù hợp sau:\n" + JsonSerializer.Serialize(matchedRooms);
                _logs.AppendLine($"[Result SemanticSearchRooms] {successStr}");
                return successStr;
            }
            catch (Exception ex)
            {
                _logs.AppendLine($"[SemanticSearchRooms Error] {ex.Message}");
                return $"Lỗi tìm kiếm phòng: {ex.Message}";
            }
        }

        public string GetLogs() => _logs.ToString();

        private static double CosineSimilarity(float[]? v1, float[]? v2)
        {
            if (v1 == null || v2 == null || v1.Length != v2.Length) return 0.0;
            double dot = 0.0;
            double n1 = 0.0;
            double n2 = 0.0;
            for (int i = 0; i < v1.Length; i++)
            {
                dot += v1[i] * v2[i];
                n1 += v1[i] * v1[i];
                n2 += v2[i] * v2[i];
            }
            return (n1 == 0.0 || n2 == 0.0) ? 0.0 : dot / (Math.Sqrt(n1) * Math.Sqrt(n2));
        }
    }
}
