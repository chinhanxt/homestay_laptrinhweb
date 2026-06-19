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
using WebHomestay.Services;
using WebHomestay.Services.AI.Retrieval;

namespace WebHomestay.Services.AI.Plugins
{
    public class KnowledgeGraphPlugin
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorSearchService _vectorSearchService;
        private readonly RetrievalContextAssembler _retrievalContextAssembler;
        private readonly AIBookingSessionState? _sessionState;
        private readonly AIBrainChatRequest? _chatRequest;
        private readonly StringBuilder _logs;
        public string RetrievedKnowledgeJson { get; private set; } = "[]";
        public string GraphReasoningJson { get; private set; } = "{}";

        public KnowledgeGraphPlugin(
            ApplicationDbContext context,
            IEmbeddingService embeddingService,
            IVectorSearchService vectorSearchService,
            RetrievalContextAssembler retrievalContextAssembler,
            AIBookingSessionState? sessionState = null,
            AIBrainChatRequest? chatRequest = null)
        {
            _context = context;
            _embeddingService = embeddingService;
            _vectorSearchService = vectorSearchService;
            _retrievalContextAssembler = retrievalContextAssembler;
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
                var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query, CancellationToken.None);
                var contextPack = await _retrievalContextAssembler.BuildPolicyContextAsync(
                    queryEmbedding,
                    new VectorSearchFilter
                    {
                        BranchId = _sessionState?.BranchId,
                        RoomId = _sessionState?.SelectedRoomId ?? _sessionState?.ActiveRoomContextId,
                        ActiveOnly = true
                    },
                    CancellationToken.None);

                var boostedHits = contextPack.Hits
                    .Select(sr => {
                        double similarity = sr.Score;
                        
                        if (_sessionState != null)
                        {
                            var branchId = _sessionState.BranchId;
                            var roomId = _sessionState.SelectedRoomId ?? _sessionState.ActiveRoomContextId;

                            if (roomId.HasValue && sr.Tags.Contains($"room-{roomId.Value}"))
                            {
                                similarity += 0.25;
                            }
                            else if (branchId.HasValue && sr.Tags.Contains($"branch-{branchId.Value}"))
                            {
                                similarity += 0.15;
                            }
                        }

                        return new VectorSearchResult
                        {
                            EntityType = sr.EntityType,
                            EntityId = sr.EntityId,
                            Title = sr.Title,
                            Content = sr.Content,
                            Tags = sr.Tags,
                            Score = similarity
                        };
                    })
                    .Where(x => x.Score > 0.35)
                    .OrderByDescending(x => x.Score)
                    .ToList();

                if (!boostedHits.Any())
                {
                    var failStr = "Không tìm thấy tri thức ngữ nghĩa phù hợp với câu hỏi này trong nguồn dữ liệu hiện tại.";
                    _logs.AppendLine($"[Result SearchPolicies] {failStr}");
                    RetrievedKnowledgeJson = "[]";
                    GraphReasoningJson = "{}";
                    return failStr;
                }

                var payload = new
                {
                    Matches = boostedHits.Select(x => new { x.Title, x.Content, Score = Math.Round(x.Score, 4) }).ToList(),
                    Graph = contextPack.Graph
                };

                RetrievedKnowledgeJson = JsonSerializer.Serialize(payload.Matches);
                GraphReasoningJson = JsonSerializer.Serialize(payload.Graph);

                var successStr = "Tìm thấy các chính sách sau:\n" + JsonSerializer.Serialize(new[] { payload });
                _logs.AppendLine($"[Result SearchPolicies] {successStr}");
                return successStr;
            }
            catch (EmbeddingUnavailableException ex)
            {
                _logs.AppendLine($"[SearchPolicies Error] {ex.Message}");
                RetrievedKnowledgeJson = "[]";
                GraphReasoningJson = "{}";
                return "Hệ thống semantic retrieval hiện chưa sẵn sàng vì embedding chưa khả dụng.";
            }
            catch (Exception ex)
            {
                _logs.AppendLine($"[SearchPolicies Error] {ex.Message}");
                RetrievedKnowledgeJson = "[]";
                GraphReasoningJson = "{}";
                return $"Lỗi tìm kiếm chính sách: {ex.Message}";
            }
        }

        [KernelFunction("SearchRoomOperationManual")]
        [Description("Tìm kiếm cẩm nang hướng dẫn sử dụng thiết bị trong phòng, mật khẩu wifi phòng, hướng dẫn mở hộp số lấy chìa khóa hoặc giải quyết các sự cố vận hành tại phòng/chi nhánh.")]
        public async Task<string> SearchRoomOperationManual(
            [Description("Câu hỏi hoặc vấn đề của khách (VD: wifi phòng, bật bình nóng lạnh, mở khóa, hộp số chìa khóa)")] string query)
        {
            _logs.AppendLine($"[SearchRoomOperationManual] query={query}");
            return await SearchPolicies(query);
        }

        [KernelFunction("SearchLocationGraph")]
        [Description("Tìm kiếm thông tin về địa điểm, tiện ích xung quanh chi nhánh, bãi đậu xe, hoặc đường đi.")]
        public async Task<string> SearchLocationGraph(
            [Description("Từ khóa địa điểm hoặc chi nhánh (VD: chợ Đà Lạt, bãi xe Q1)")] string query)
        {
            _logs.AppendLine($"[SearchLocationGraph] query={query}");

            var nodes = await _context.AIGraphNodes
                .Where(n => n.IsActive && !n.IsDeleted)
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
                .Where(e => !e.IsDeleted && (nodeIds.Contains(e.FromNodeId) || nodeIds.Contains(e.ToNodeId)))
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
    }
}
