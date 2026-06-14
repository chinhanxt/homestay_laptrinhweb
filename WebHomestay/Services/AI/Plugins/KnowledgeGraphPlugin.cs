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
    public class KnowledgeGraphPlugin
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmbeddingService _embeddingService;
        private readonly AIBookingSessionState? _sessionState;
        private readonly AIBrainChatRequest? _chatRequest;
        private readonly StringBuilder _logs;

        public KnowledgeGraphPlugin(
            ApplicationDbContext context,
            IEmbeddingService embeddingService,
            AIBookingSessionState? sessionState = null,
            AIBrainChatRequest? chatRequest = null)
        {
            _context = context;
            _embeddingService = embeddingService;
            _sessionState = sessionState;
            _chatRequest = chatRequest;
            _logs = new StringBuilder();
        }

        [KernelFunction("SearchPolicies")]
        [Description("Tìm kiếm các chính sách, nội quy, giờ nhận/trả phòng, hoặc các hướng dẫn chung của homestay.")]
        public async Task<string> SearchPolicies(
            [Description("Từ khóa tìm kiếm (VD: thú cưng, giờ nhận phòng, hủy phòng)")] string query)
        {
            _logs.AppendLine($"[SearchPolicies] query={query}");
            
            try
            {
                // Load all active units
                var units = await _context.AIKnowledgeUnits
                    .Where(k => k.IsActive)
                    .ToListAsync();

                var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query);

                var matchedSegments = units
                    .Where(k => k.Embedding != null)
                    .Select(k => {
                        double similarity = CosineSimilarity(k.Embedding, queryEmbedding);
                        
                        // Boost context if present
                        if (_sessionState != null)
                        {
                            var branchId = _sessionState.BranchId;
                            var roomId = _sessionState.SelectedRoomId ?? _sessionState.ActiveRoomContextId;

                            if (roomId.HasValue && k.Tags.Contains($"room-{roomId.Value}"))
                            {
                                similarity += 0.25; // Large boost for current room context
                            }
                            else if (branchId.HasValue && k.Tags.Contains($"branch-{branchId.Value}"))
                            {
                                similarity += 0.15; // Medium boost for current branch context
                            }
                        }

                        return new { k.Title, k.Content, Similarity = similarity };
                    })
                    .Where(x => x.Similarity > 0.35) // Similarity threshold
                    .OrderByDescending(x => x.Similarity)
                    .Take(5)
                    .Select(x => new { x.Title, x.Content })
                    .ToList();

                // Fallback to keyword split-matching if no vector match is found (or if all embeddings are null)
                if (!matchedSegments.Any())
                {
                    _logs.AppendLine("[SearchPolicies] No vector matches found. Falling back to keyword search.");
                    var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                    matchedSegments = units
                        .Where(k => terms.Any(t => 
                            k.Title.Contains(t, StringComparison.OrdinalIgnoreCase) || 
                            k.Content.Contains(t, StringComparison.OrdinalIgnoreCase) || 
                            k.Tags.Contains(t, StringComparison.OrdinalIgnoreCase)))
                        .OrderByDescending(k => k.Priority)
                        .Take(5)
                        .Select(k => new { k.Title, k.Content })
                        .ToList();
                }

                if (!matchedSegments.Any())
                {
                    var failStr = "Không tìm thấy chính sách nào liên quan đến câu hỏi này.";
                    _logs.AppendLine($"[Result SearchPolicies] {failStr}");
                    return failStr;
                }

                var successStr = "Tìm thấy các chính sách sau:\n" + JsonSerializer.Serialize(matchedSegments);
                _logs.AppendLine($"[Result SearchPolicies] {successStr}");
                return successStr;
            }
            catch (Exception ex)
            {
                _logs.AppendLine($"[SearchPolicies Error] {ex.Message}");
                return $"Lỗi tìm kiếm chính sách: {ex.Message}";
            }
        }

        [KernelFunction("SearchRoomOperationManual")]
        [Description("Tìm kiếm cẩm nang hướng dẫn sử dụng thiết bị trong phòng, mật khẩu wifi phòng, hướng dẫn mở hộp số lấy chìa khóa hoặc giải quyết các sự cố vận hành tại phòng/chi nhánh.")]
        public async Task<string> SearchRoomOperationManual(
            [Description("Câu hỏi hoặc vấn đề của khách (VD: wifi phòng, bật bình nóng lạnh, mở khóa, hộp số chìa khóa)")] string query)
        {
            _logs.AppendLine($"[SearchRoomOperationManual] query={query}");
            
            // This functions similarly to SearchPolicies but specifically operates on guest troubleshooting issues.
            // Under semantic search, we can use the same context-aware RAG pipeline, which naturally prioritizes 
            // the guest's active room or branch.
            return await SearchPolicies(query);
        }

        [KernelFunction("SearchLocationGraph")]
        [Description("Tìm kiếm thông tin về địa điểm, tiện ích xung quanh chi nhánh, bãi đậu xe, hoặc đường đi.")]
        public async Task<string> SearchLocationGraph(
            [Description("Từ khóa địa điểm hoặc chi nhánh (VD: chợ Đà Lạt, bãi xe Q1)")] string query)
        {
            _logs.AppendLine($"[SearchLocationGraph] query={query}");

            var nodes = await _context.AIGraphNodes
                .Where(n => n.IsActive)
                .ToListAsync();

            var matchedNodes = nodes
                .Where(n => query.Contains(n.Label, StringComparison.OrdinalIgnoreCase) || 
                            n.Summary.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList();

            if (!matchedNodes.Any())
            {
                var failStr = "Không tìm thấy dữ liệu địa điểm nào liên quan.";
                _logs.AppendLine($"[Result SearchLocationGraph] {failStr}");
                return failStr;
            }

            var nodeIds = matchedNodes.Select(n => n.Id).ToList();
            var edges = await _context.AIGraphEdges
                .Where(e => nodeIds.Contains(e.FromNodeId) || nodeIds.Contains(e.ToNodeId))
                .Include(e => e.FromNode)
                .Include(e => e.ToNode)
                .Select(e => new { From = e.FromNode.Label, To = e.ToNode.Label, e.RelationshipType, e.Evidence })
                .Take(5)
                .ToListAsync();

            var result = new
            {
                Nodes = matchedNodes.Select(n => new { n.Label, n.Summary }),
                Edges = edges
            };

            var successStr = "Tìm thấy thông tin Graph sau:\n" + JsonSerializer.Serialize(result);
            _logs.AppendLine($"[Result SearchLocationGraph] {successStr}");
            return successStr;
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
