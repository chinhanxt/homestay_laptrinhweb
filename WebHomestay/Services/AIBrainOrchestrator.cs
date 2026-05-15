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
            var filteredFormSchema = FilterFormSchema(finalConfig.FormSchema, request);
            var modelResponse = await _aiModelClient.CompleteAsync(new AIModelRequest
            {
                SystemPrompt = BuildSystemPrompt(personaSummary, liveSnapshot, knowledge, graphReasoning, guardResult, finalConfig.Style, filteredFormSchema),
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
                FormSchema = filteredFormSchema
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

        private string FilterFormSchema(string formSchema, AIBrainChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(formSchema) || formSchema == "[]") return "[]";

            try
            {
                using var document = JsonDocument.Parse(formSchema);
                if (document.RootElement.ValueKind != JsonValueKind.Array) return "[]";

                var matchedFields = document.RootElement.EnumerateArray()
                    .Where(field => ShouldShowFormField(field, request))
                    .Select(field => JsonSerializer.Deserialize<object>(field.GetRawText()))
                    .Where(field => field != null)
                    .ToList();

                return JsonSerializer.Serialize(matchedFields);
            }
            catch
            {
                return "[]";
            }
        }

        private bool ShouldShowFormField(JsonElement field, AIBrainChatRequest request)
        {
            if (!field.TryGetProperty("condition", out var condition) || condition.ValueKind != JsonValueKind.Object) return true;

            var intent = GetConditionValue(condition, "intent");
            var missingData = GetConditionValue(condition, "missingData");
            var keywords = GetConditionValue(condition, "keywords");
            var advancedPrompt = GetConditionValue(condition, "advancedPrompt");
            var hasCondition = !string.IsNullOrWhiteSpace(intent) && intent != "always"
                || !string.IsNullOrWhiteSpace(missingData)
                || !string.IsNullOrWhiteSpace(keywords)
                || !string.IsNullOrWhiteSpace(advancedPrompt);

            if (!hasCondition) return true;

            if (!string.IsNullOrWhiteSpace(intent) && intent != "always" && !MatchesIntent(intent, request.Message)) return false;
            if (!string.IsNullOrWhiteSpace(missingData) && !IsMissingData(missingData, request)) return false;
            if (!string.IsNullOrWhiteSpace(keywords) && !MatchesAnyKeyword(keywords, request.Message)) return false;
            return true;
        }

        private string GetConditionValue(JsonElement condition, string propertyName)
        {
            return condition.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }

        private bool MatchesIntent(string intent, string message)
        {
            var lowered = message.ToLowerInvariant();
            return intent switch
            {
                "booking_ready" => ContainsAny(lowered, "đặt", "chốt", "book", "giữ phòng", "lấy phòng", "đặt phòng"),
                "pricing" => ContainsAny(lowered, "giá", "rẻ", "budget", "ngân sách", "bao nhiêu", "tầm tiền"),
                "availability" => ContainsAny(lowered, "còn phòng", "trống", "available", "phòng nào", "có phòng"),
                "consulting" => ContainsAny(lowered, "tư vấn", "gợi ý", "phù hợp", "nên chọn", "recommend"),
                "payment_ready" => ContainsAny(lowered, "thanh toán", "chuyển khoản", "ck", "cọc", "đặt cọc", "chốt", "giữ phòng"),
                _ => true
            };
        }

        private bool IsMissingData(string missingData, AIBrainChatRequest request)
        {
            var lowered = request.Message.ToLowerInvariant();
            return missingData switch
            {
                "branch" => !request.BranchId.HasValue,
                "datetime" => !request.StartTime.HasValue || !request.EndTime.HasValue,
                "guestCount" => request.GuestCount <= 1 && !ContainsAny(lowered, "1 người", "2 người", "3 người", "4 người", "một người", "hai người"),
                "budget" => !ContainsAny(lowered, "giá", "rẻ", "budget", "ngân sách", "k", "triệu"),
                "phone" => !lowered.Any(char.IsDigit) || lowered.Count(char.IsDigit) < 9,
                "note" => string.IsNullOrWhiteSpace(request.Message),
                _ => false
            };
        }

        private bool MatchesAnyKeyword(string keywords, string message)
        {
            var lowered = message.ToLowerInvariant();
            return keywords
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(keyword => lowered.Contains(keyword.ToLowerInvariant()));
        }

        private bool ContainsAny(string value, params string[] needles)
        {
            return needles.Any(value.Contains);
        }

        private Dictionary<string, string> ExtractConditionOptionKeywords(string? optionConfig)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(optionConfig)) return result;

            try
            {
                using var document = JsonDocument.Parse(optionConfig);
                foreach (var groupName in new[] { "intents", "missingData" })
                {
                    if (!document.RootElement.TryGetProperty(groupName, out var group) || group.ValueKind != JsonValueKind.Array) continue;
                    foreach (var option in group.EnumerateArray())
                    {
                        var value = option.TryGetProperty("value", out var valueElement) ? valueElement.GetString() : null;
                        var keywords = option.TryGetProperty("keywords", out var keywordsElement) ? keywordsElement.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(keywords)) result[value] = keywords;
                    }
                }
            }
            catch
            {
                return result;
            }

            return result;
        }

        private string ApplyConditionOptionKeywords(string formSchema, Dictionary<string, string> optionKeywords)
        {
            if (optionKeywords.Count == 0 || string.IsNullOrWhiteSpace(formSchema) || formSchema == "[]") return formSchema;

            try
            {
                var fields = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(formSchema) ?? new List<Dictionary<string, object?>>();
                foreach (var field in fields)
                {
                    if (!field.TryGetValue("condition", out var conditionValue) || conditionValue is not JsonElement conditionElement) continue;
                    var condition = JsonSerializer.Deserialize<Dictionary<string, object?>>(conditionElement.GetRawText()) ?? new Dictionary<string, object?>();
                    var inheritedKeywords = new List<string>();
                    foreach (var key in new[] { "intent", "missingData" })
                    {
                        if (!condition.TryGetValue(key, out var value) || value == null) continue;
                        var optionValue = value.ToString() ?? string.Empty;
                        if (optionKeywords.TryGetValue(optionValue, out var keywords)) inheritedKeywords.Add(keywords);
                    }
                    if (!inheritedKeywords.Any()) continue;
                    var ownKeywords = condition.TryGetValue("keywords", out var currentKeywords) ? currentKeywords?.ToString() : string.Empty;
                    condition["keywords"] = string.Join(',', inheritedKeywords.Append(ownKeywords).Where(text => !string.IsNullOrWhiteSpace(text)));
                    field["condition"] = condition;
                }
                return JsonSerializer.Serialize(fields);
            }
            catch
            {
                return formSchema;
            }
        }

        private async Task<(string Style, string FormSchema)> GetFinalSynthesizerConfigAsync(CancellationToken cancellationToken)
        {
            var styleSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AIFinalSynthesizerStyle", cancellationToken);
            var formSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AIFinalSynthesizerFormSchema", cancellationToken);
            var optionSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AIFinalConditionOptions", cancellationToken);
            var style = string.IsNullOrWhiteSpace(styleSetting?.SettingValue)
                ? "Giọng thân thiện, rõ ràng, tư vấn như lễ tân chuyên nghiệp. Trả lời ngắn gọn nhưng đủ ý. Nếu thiếu thông tin thì hỏi lại bằng các câu hỏi cụ thể."
                : styleSetting.SettingValue;
            var formSchema = string.IsNullOrWhiteSpace(formSetting?.SettingValue) ? "[]" : formSetting.SettingValue;
            var optionKeywords = ExtractConditionOptionKeywords(optionSetting?.SettingValue);
            return (style, ApplyConditionOptionKeywords(formSchema, optionKeywords));
        }

        private string BuildSystemPrompt(string personaSummary, string liveSnapshot, List<object> knowledge, List<object> graphReasoning, string guardResult, string finalStyle, string formSchema)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Bạn là Final Response Synthesizer của AI Brain Center cho homestay self check-in/self check-out.");
            builder.AppendLine("Nhiệm vụ duy nhất: viết câu trả lời cuối cùng cho khách dựa trên dữ liệu các agent cung cấp.");
            builder.AppendLine("Luôn trả lời bằng tiếng Việt, không bịa phòng trống hoặc giá.");
            builder.AppendLine("Nếu thiếu ngày/giờ/chi nhánh/số khách/ngân sách theo cấu hình form đã lọc, hãy hỏi lại bằng đúng các trường cần điền.");
            builder.AppendLine("Không xác nhận đặt phòng; chỉ hướng khách sang luồng đặt phòng chính thức.");
            builder.AppendLine("QUY TẮC BẮT BUỘC CỦA FINAL RESPONSE SYNTHESIZER:");
            builder.AppendLine("- Phải ưu tiên làm theo cấu hình phong cách trả lời bên dưới hơn mọi thói quen trả lời chung.");
            builder.AppendLine("- Form Schema JSON bên dưới đã được lọc theo điều kiện của từng field cho câu hỏi hiện tại.");
            builder.AppendLine("- Nếu Form Schema còn trường, hãy hỏi khách điền đúng các trường đó, không thêm trường ngoài schema.");
            builder.AppendLine("- Nếu Form Schema rỗng nhưng nhu cầu chưa rõ, hãy hỏi thêm một câu ngắn để xác định intent trước khi xin thông tin.");
            builder.AppendLine("- Không được hiển thị toàn bộ form cấu hình nếu điều kiện field chưa phù hợp.");
            builder.AppendLine("- Nếu field type là paymentQr, được gửi đúng messageTemplate và qrImageUrl đã cấu hình như hướng dẫn chuyển khoản; không tự xác nhận booking sau khi gửi QR.");
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
