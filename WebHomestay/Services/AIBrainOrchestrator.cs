using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Services
{
    public class AIBrainOrchestrator : IAIBrainOrchestrator
    {
        private readonly ApplicationDbContext _context;
        private readonly IAvailabilityService _availabilityService;
        private readonly IAIModelClient _aiModelClient;

        public AIBrainOrchestrator(ApplicationDbContext context, IAvailabilityService availabilityService, IAIModelClient aiModelClient)
        {
            _context = context;
            _availabilityService = availabilityService;
            _aiModelClient = aiModelClient;
        }

        public async Task<AIBrainChatResponse> ChatAsync(AIBrainChatRequest request, CancellationToken cancellationToken = default)
        {
            var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId;
            var personaSummary = BuildPersonaSummary(request.Message, request.GuestCount);
            var liveSnapshot = await BuildLiveSnapshotAsync(request, cancellationToken);
            var knowledge = await RetrieveKnowledgeAsync(request.Message, cancellationToken);
            var graphReasoning = await BuildGraphReasoningAsync(request.Message, cancellationToken);
            var guardResult = BuildGuardResult(request);

            var finalConfig = await GetFinalSynthesizerConfigAsync(cancellationToken);
            var modelResponse = await _aiModelClient.CompleteAsync(new AIModelRequest
            {
                SystemPrompt = BuildSystemPrompt(personaSummary, liveSnapshot, knowledge, graphReasoning, guardResult, finalConfig.Style, finalConfig.FormSchema),
                UserMessage = request.Message,
                Temperature = 0.35m,
                MaxTokens = 900
            }, cancellationToken);

            var trace = new AIConversationTrace
            {
                SessionId = sessionId,
                CustomerMessage = request.Message,
                PersonaSummary = personaSummary,
                LiveSystemSnapshot = liveSnapshot,
                RetrievedKnowledgeJson = JsonSerializer.Serialize(knowledge),
                GraphReasoningJson = JsonSerializer.Serialize(graphReasoning),
                GuardResult = guardResult,
                FinalAnswer = modelResponse.Content,
                ModelProvider = modelResponse.Provider
            };

            _context.AIConversationTraces.Add(trace);
            await _context.SaveChangesAsync(cancellationToken);

            return new AIBrainChatResponse
            {
                TraceId = trace.Id,
                Answer = modelResponse.Content,
                PersonaSummary = personaSummary,
                GuardResult = guardResult,
                ModelProvider = modelResponse.Provider,
                IsMock = modelResponse.IsMock,
                FormSchema = finalConfig.FormSchema
            };
        }

        private string BuildPersonaSummary(string message, int guestCount)
        {
            var lowered = message.ToLowerInvariant();
            var intent = lowered.Contains("giá") || lowered.Contains("rẻ") ? "nhạy cảm về giá" : "cần tư vấn đặt phòng";
            var urgency = lowered.Contains("hôm nay") || lowered.Contains("ngay") ? "nhu cầu gấp" : "nhu cầu bình thường";
            return $"Khách có {intent}, {urgency}, số khách dự kiến: {Math.Max(guestCount, 1)}.";
        }

        private async Task<string> BuildLiveSnapshotAsync(AIBrainChatRequest request, CancellationToken cancellationToken)
        {
            if (!request.BranchId.HasValue || !request.StartTime.HasValue || !request.EndTime.HasValue)
            {
                var branches = await _context.Branches
                    .OrderBy(b => b.Id)
                    .Select(b => new { b.Id, b.Name, b.Address, b.Hotline, b.BookingLeadTimeHours })
                    .ToListAsync(cancellationToken);

                return JsonSerializer.Serialize(new
                {
                    SnapshotAt = DateTime.Now,
                    MissingAvailabilityInputs = true,
                    Branches = branches
                });
            }

            var availableRoomIds = await _availabilityService.GetAvailableRoomIds(request.BranchId.Value, request.StartTime.Value, request.EndTime.Value);
            var rooms = await _context.Rooms
                .Where(r => availableRoomIds.Contains(r.Id) && r.MaxGuests >= Math.Max(request.GuestCount, 1))
                .Include(r => r.Branch)
                .Include(r => r.Amenities)
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.Description,
                    r.PricePerHour,
                    r.PricePerDay,
                    r.ExtraGuestFee,
                    r.Capacity,
                    r.MaxGuests,
                    r.Status,
                    Branch = new
                    {
                        r.Branch!.Id,
                        r.Branch.Name,
                        r.Branch.Address,
                        r.Branch.Hotline,
                        r.Branch.BookingLeadTimeHours
                    },
                    Amenities = r.Amenities.Select(a => a.Name).ToList()
                })
                .ToListAsync(cancellationToken);

            return JsonSerializer.Serialize(new
            {
                SnapshotAt = DateTime.Now,
                request.BranchId,
                request.StartTime,
                request.EndTime,
                GuestCount = Math.Max(request.GuestCount, 1),
                AvailableRooms = rooms
            });
        }

        private async Task<List<object>> RetrieveKnowledgeAsync(string message, CancellationToken cancellationToken)
        {
            var terms = message
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length >= 3)
                .Take(8)
                .ToList();

            var units = await _context.AIKnowledgeUnits
                .Where(k => k.IsActive)
                .OrderByDescending(k => k.Priority)
                .ThenByDescending(k => k.LastUpdated)
                .Take(30)
                .Select(k => new { k.Title, k.Content, k.Tags, k.Priority })
                .ToListAsync(cancellationToken);

            return units
                .Where(k => terms.Count == 0 || terms.Any(t => k.Title.Contains(t, StringComparison.OrdinalIgnoreCase) || k.Content.Contains(t, StringComparison.OrdinalIgnoreCase) || k.Tags.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .Take(5)
                .Cast<object>()
                .ToList();
        }

        private async Task<List<object>> BuildGraphReasoningAsync(string message, CancellationToken cancellationToken)
        {
            var nodes = await _context.AIGraphNodes
                .Where(n => n.IsActive)
                .Take(30)
                .Select(n => new { n.Id, n.NodeType, n.Label, n.Summary })
                .ToListAsync(cancellationToken);

            var matchedNodes = nodes
                .Where(n => message.Contains(n.Label, StringComparison.OrdinalIgnoreCase) || n.Summary.Contains(message, StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList();

            if (matchedNodes.Count == 0)
            {
                return new List<object> { new { Step = "graph", Result = "Chưa có node graph phù hợp; chỉ dùng live data và knowledge." } };
            }

            var nodeIds = matchedNodes.Select(n => n.Id).ToList();
            var edges = await _context.AIGraphEdges
                .Where(e => nodeIds.Contains(e.FromNodeId) || nodeIds.Contains(e.ToNodeId))
                .Take(8)
                .Select(e => new { e.FromNodeId, e.ToNodeId, e.RelationshipType, e.Weight, e.Evidence })
                .ToListAsync(cancellationToken);

            return new List<object> { new { Nodes = matchedNodes, Edges = edges } };
        }

        private string BuildGuardResult(AIBrainChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message)) return "blocked: empty customer message";
            if (request.StartTime.HasValue && request.EndTime.HasValue && request.EndTime <= request.StartTime) return "warn: khoảng thời gian không hợp lệ, cần hỏi lại khách";
            return "pass: chỉ tư vấn dựa trên live snapshot, không xác nhận đặt phòng nếu chưa qua bước booking chính thức";
        }

        private async Task<(string Style, string FormSchema)> GetFinalSynthesizerConfigAsync(CancellationToken cancellationToken)
        {
            var styleSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AIFinalSynthesizerStyle", cancellationToken);
            var formSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AIFinalSynthesizerFormSchema", cancellationToken);
            var style = string.IsNullOrWhiteSpace(styleSetting?.SettingValue)
                ? "Giọng thân thiện, rõ ràng, tư vấn như lễ tân chuyên nghiệp. Trả lời ngắn gọn nhưng đủ ý. Nếu thiếu thông tin thì hỏi lại bằng các câu hỏi cụ thể."
                : styleSetting.SettingValue;
            var formSchema = string.IsNullOrWhiteSpace(formSetting?.SettingValue) ? "[]" : formSetting.SettingValue;
            return (style, formSchema);
        }

        private string BuildSystemPrompt(string personaSummary, string liveSnapshot, List<object> knowledge, List<object> graphReasoning, string guardResult, string finalStyle, string formSchema)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Bạn là Final Response Synthesizer của AI Brain Center cho homestay self check-in/self check-out.");
            builder.AppendLine("Nhiệm vụ duy nhất: viết câu trả lời cuối cùng cho khách dựa trên dữ liệu các agent cung cấp.");
            builder.AppendLine("Luôn trả lời bằng tiếng Việt, không bịa phòng trống hoặc giá.");
            builder.AppendLine("Nếu thiếu ngày/giờ/chi nhánh/số khách/ngân sách theo cấu hình form, hãy hỏi lại bằng đúng các trường cần điền.");
            builder.AppendLine("Không xác nhận đặt phòng; chỉ hướng khách sang luồng đặt phòng chính thức.");
            builder.AppendLine("QUY TẮC BẮT BUỘC CỦA FINAL RESPONSE SYNTHESIZER:");
            builder.AppendLine("- Phải ưu tiên làm theo cấu hình phong cách trả lời bên dưới hơn mọi thói quen trả lời chung.");
            builder.AppendLine("- Nếu Form Schema có trường và câu hỏi của khách thiếu dữ liệu tương ứng, hãy hỏi khách điền đúng các trường đó.");
            builder.AppendLine("- Nếu cần form, hãy trình bày thành danh sách trường rõ ràng, không hỏi lan man.");
            builder.AppendLine("- Không được bỏ qua Form Schema nếu nó liên quan đến thông tin còn thiếu.");
            builder.AppendLine($"Final Response Synthesizer Style: {finalStyle}");
            builder.AppendLine($"Final Response Form Schema JSON: {formSchema}");
            builder.AppendLine($"Persona Agent: {personaSummary}");
            builder.AppendLine($"Safety Guard: {guardResult}");
            builder.AppendLine($"Live System Agent JSON: {liveSnapshot}");
            builder.AppendLine($"Knowledge RAG Agent JSON: {JsonSerializer.Serialize(knowledge)}");
            builder.AppendLine($"Graph Reasoning Agent JSON: {JsonSerializer.Serialize(graphReasoning)}");
            return builder.ToString();
        }
    }
}
