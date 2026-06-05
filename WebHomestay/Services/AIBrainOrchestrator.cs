using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services
{
    public class AIBrainOrchestrator : IAIBrainOrchestrator
    {
        private readonly ApplicationDbContext _context;
        private readonly IAvailabilityService _availabilityService;
        private readonly IAIModelClient _aiModelClient;
        private readonly IMemoryCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IBookingConductor _bookingConductor;

        public AIBrainOrchestrator(
            ApplicationDbContext context,
            IAvailabilityService availabilityService,
            IAIModelClient aiModelClient,
            IMemoryCache cache,
            IServiceScopeFactory scopeFactory,
            IBookingConductor bookingConductor)
        {
            _context = context;
            _availabilityService = availabilityService;
            _aiModelClient = aiModelClient;
            _cache = cache;
            _scopeFactory = scopeFactory;
            _bookingConductor = bookingConductor;
        }

        public async Task<AIBrainChatResponse> ChatAsync(AIBrainChatRequest request, CancellationToken cancellationToken = default)
        {
            var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId;
            var conversationHistory = await BuildConversationHistoryAsync(sessionId, cancellationToken);
            var conversationAwareMessage = string.IsNullOrWhiteSpace(conversationHistory)
                ? request.Message
                : $"{conversationHistory}\nKhách vừa nhắn: {request.Message}";
            var enrichedRequest = await EnrichRequestFromConversationAsync(request, conversationAwareMessage, cancellationToken);
            var personaSummary = BuildPersonaSummary(conversationAwareMessage, enrichedRequest.GuestCount);
            var liveSnapshot = await BuildLiveSnapshotAsync(enrichedRequest, cancellationToken);
            var knowledge = await RetrieveKnowledgeAsync(conversationAwareMessage, cancellationToken);
            var graphReasoning = await BuildGraphReasoningAsync(conversationAwareMessage, cancellationToken);
            var guardResult = BuildGuardResult(enrichedRequest);

            var finalConfig = await GetFinalSynthesizerConfigAsync(cancellationToken);
            var filteredFormSchema = FilterFormSchema(finalConfig.FormSchema, enrichedRequest);

            ConductorResult? conductorResult = null;
            if (request.Mode == ChatMode.PublicBooking)
            {
                conductorResult = await _bookingConductor.DecideAsync(sessionId, request.Message, enrichedRequest, cancellationToken);
            }

            var maxTokens = 900;

            var modelResponse = await _aiModelClient.CompleteAsync(new AIModelRequest
            {
                SystemPrompt = BuildSystemPrompt(personaSummary, liveSnapshot, knowledge, graphReasoning, guardResult, finalConfig, filteredFormSchema, conversationHistory, conductorResult, request.Mode),
                UserMessage = request.Message,
                Temperature = 0.35m,
                MaxTokens = maxTokens
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
                FormSchema = filteredFormSchema,
                BookingAction = conductorResult?.Action.ToString() ?? "reply",
                BookingState = conductorResult?.State,
                UiBlocks = conductorResult?.UiBlocks ?? new List<object>()
            };
        }

        private async Task<AIBrainChatRequest> EnrichRequestFromConversationAsync(AIBrainChatRequest request, string conversationAwareMessage, CancellationToken cancellationToken)
        {
            var enriched = new AIBrainChatRequest
            {
                SessionId = request.SessionId,
                Message = request.Message,
                BranchId = request.BranchId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                GuestCount = request.GuestCount
            };

            if (!enriched.BranchId.HasValue)
            {
                var lowered = conversationAwareMessage.ToLowerInvariant();
                var branches = await _context.Branches.Select(b => new { b.Id, b.Name }).ToListAsync(cancellationToken);
                var matchedBranch = branches.FirstOrDefault(b => lowered.Contains(b.Name.ToLowerInvariant()))
                    ?? branches.FirstOrDefault(b => b.Name.Contains("Sài Gòn") && ContainsAny(lowered, "sài gòn", "sai gon", "saigon", "sg", "hcm", "tphcm", "q1", "quận 1"))
                    ?? branches.FirstOrDefault(b => b.Name.Contains("Đà Lạt") && ContainsAny(lowered, "đà lạt", "da lat", "dalat", "dl", "đl"));
                if (matchedBranch != null) enriched.BranchId = matchedBranch.Id;
            }

            if (enriched.GuestCount <= 1)
            {
                var match = Regex.Match(conversationAwareMessage, @"(\d+)\s*(ng|người|nguoi|khách|khach)", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var parsedGuests)) enriched.GuestCount = parsedGuests;
            }

            if (!enriched.StartTime.HasValue)
            {
                if (TryExtractDateRange(conversationAwareMessage, out var rangeStart, out var rangeEnd))
                {
                    enriched.StartTime = rangeStart.ToDateTime(TimeOnly.MinValue);
                    enriched.EndTime = rangeEnd.ToDateTime(TimeOnly.MaxValue);
                }
                else if (TryExtractDate(conversationAwareMessage, out var requestedDate))
                {
                    enriched.StartTime = requestedDate.ToDateTime(TimeOnly.MinValue);
                    enriched.EndTime = requestedDate.ToDateTime(TimeOnly.MaxValue);
                }
            }

            return enriched;
        }

        private bool TryExtractDateRange(string message, out DateOnly startDate, out DateOnly endDate)
        {
            var lowered = message.ToLowerInvariant();
            var today = DateTime.Today;

            var match = Regex.Match(lowered, @"(\d{1,2})\s*(?:đến|to|\-|–|—|~)\s*(\d{1,2})(?:\s*[/-]\s*(\d{1,2}))?(?:\s*[/-]\s*(\d{2,4}))?");
            if (match.Success)
            {
                var day1 = int.Parse(match.Groups[1].Value);
                var day2 = int.Parse(match.Groups[2].Value);
                var month = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : today.Month;
                var year = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : today.Year;
                if (year < 100) year += 2000;

                if (month < 1 || month > 12) month = today.Month;
                if (day1 < 1 || day1 > 31 || day2 < 1 || day2 > 31) { startDate = endDate = default; return false; }

                if (DateOnly.TryParse($"{year:D4}-{month:D2}-{day1:D2}", out startDate)
                    && DateOnly.TryParse($"{year:D4}-{month:D2}-{day2:D2}", out endDate))
                {
                    if (startDate > endDate)
                    {
                        (startDate, endDate) = (endDate, startDate);
                    }
                    return true;
                }
            }

            var dayRangeMatch = Regex.Match(lowered, @"(\d{1,2})\s*(?:đến|to|\-|–|—|~)\s*(\d{1,2})\s*(?:(?:tháng|t)\s*(\d{1,2}))?");
            if (dayRangeMatch.Success)
            {
                var day1 = int.Parse(dayRangeMatch.Groups[1].Value);
                var day2 = int.Parse(dayRangeMatch.Groups[2].Value);
                var month = dayRangeMatch.Groups[3].Success ? int.Parse(dayRangeMatch.Groups[3].Value) : today.Month;
                var year = today.Year;

                if (month < 1 || month > 12) month = today.Month;
                if (day1 < 1 || day1 > 31 || day2 < 1 || day2 > 31) { startDate = endDate = default; return false; }

                if (DateOnly.TryParse($"{year:D4}-{month:D2}-{day1:D2}", out startDate)
                    && DateOnly.TryParse($"{year:D4}-{month:D2}-{day2:D2}", out endDate))
                {
                    if (startDate > endDate)
                    {
                        (startDate, endDate) = (endDate, startDate);
                    }
                    return true;
                }
            }

            startDate = endDate = default;
            return false;
        }

        private bool TryExtractDate(string message, out DateOnly date)
        {
            var lowered = message.ToLowerInvariant();
            if (lowered.Contains("hôm nay") || lowered.Contains("hom nay"))
            {
                date = DateOnly.FromDateTime(DateTime.Today);
                return true;
            }
            if (lowered.Contains("ngày mai") || lowered.Contains("ngay mai"))
            {
                date = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
                return true;
            }

            var match = Regex.Match(lowered, @"(?:ngày\s*)?(\d{1,2})[/-](\d{1,2})(?:[/-](\d{2,4}))?");
            if (match.Success)
            {
                var day = int.Parse(match.Groups[1].Value);
                var month = int.Parse(match.Groups[2].Value);
                var year = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : DateTime.Today.Year;
                if (year < 100) year += 2000;
                return DateOnly.TryParse($"{year:D4}-{month:D2}-{day:D2}", out date);
            }

            var dayOnlyMatch = Regex.Match(lowered, @"ngày\s+(\d{1,2})(?!\s*[/-])");
            if (dayOnlyMatch.Success)
            {
                var day = int.Parse(dayOnlyMatch.Groups[1].Value);
                return DateOnly.TryParse($"{DateTime.Today.Year:D4}-{DateTime.Today.Month:D2}-{day:D2}", out date);
            }

            date = default;
            return false;
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
            if (!request.BranchId.HasValue)
            {
                var branches = await _context.Branches
                    .OrderBy(b => b.Id)
                    .Select(b => new { b.Id, b.Name, b.Address, b.Hotline, b.BookingLeadTimeHours })
                    .ToListAsync(cancellationToken);

                return JsonSerializer.Serialize(new
                {
                    SnapshotAt = DateTime.Now,
                    MissingBranchForSlotOptions = true,
                    Branches = branches
                });
            }

            if (!request.StartTime.HasValue)
            {
                var branch = await _context.Branches
                    .Where(b => b.Id == request.BranchId.Value)
                    .Select(b => new { b.Id, b.Name, b.Address, b.Hotline, b.BookingLeadTimeHours })
                    .FirstOrDefaultAsync(cancellationToken);

                return JsonSerializer.Serialize(new
                {
                    SnapshotAt = DateTime.Now,
                    request.BranchId,
                    MissingDateForSlotOptions = true,
                    Branch = branch
                });
            }

            var slotDate = DateOnly.FromDateTime(request.StartTime.Value);
            var branchRooms = await _context.Rooms
                .Where(r => r.BranchId == request.BranchId.Value && r.MaxGuests >= Math.Max(request.GuestCount, 1) && r.Status == "Available")
                .Include(r => r.Branch)
                .Include(r => r.Amenities)
                .OrderBy(r => r.PricePerHour)
                .ToListAsync(cancellationToken);

            var availableSlotOptions = new List<object>();
            var cutoffTime = DateTime.UtcNow.AddHours(branchRooms.FirstOrDefault()?.Branch?.BookingLeadTimeHours ?? 0);
            foreach (var room in branchRooms)
            {
                var slots = await _context.RoomSlotInventories
                    .Where(slot => slot.RoomId == room.Id && slot.SlotDate == slotDate && slot.Status == "Available" && slot.StartTime >= cutoffTime)
                    .OrderBy(slot => slot.StartTime)
                    .ToListAsync(cancellationToken);

                var availableLabels = new List<string>();
                foreach (var slot in slots)
                {
                    if (await _availabilityService.IsRoomAvailable(room.Id, slot.StartTime, slot.EndTime)) availableLabels.Add(slot.SlotLabel);
                }

                if (availableLabels.Any())
                {
                    availableSlotOptions.Add(new
                    {
                        RoomId = room.Id,
                        RoomName = room.Name,
                        room.Description,
                        room.PricePerHour,
                        room.PricePerDay,
                        room.Capacity,
                        room.MaxGuests,
                        Amenities = room.Amenities.Select(a => a.Name).ToList(),
                        Slots = availableLabels.Take(8).ToList()
                    });
                }
            }

            var dailyStart = slotDate.ToDateTime(new TimeOnly(14, 0));
            var dailyEnd = slotDate.AddDays(1).ToDateTime(new TimeOnly(12, 0));
            var availableDailyRooms = new List<object>();
            foreach (var room in branchRooms)
            {
                if (await _availabilityService.IsRoomAvailable(room.Id, dailyStart, dailyEnd))
                {
                    availableDailyRooms.Add(new
                    {
                        RoomId = room.Id,
                        RoomName = room.Name,
                        room.Description,
                        room.PricePerDay,
                        room.PricePerHour,
                        room.Capacity,
                        room.MaxGuests,
                        CheckIn = dailyStart,
                        CheckOut = dailyEnd,
                        Amenities = room.Amenities.Select(a => a.Name).ToList()
                    });
                }
            }

            return JsonSerializer.Serialize(new
            {
                SnapshotAt = DateTime.Now,
                request.BranchId,
                SlotDate = slotDate,
                GuestCount = Math.Max(request.GuestCount, 1),
                AvailableSlotOptions = availableSlotOptions,
                HasAvailableSlotOptions = availableSlotOptions.Any(),
                AvailableDailyRooms = availableDailyRooms,
                HasAvailableDailyRooms = availableDailyRooms.Any()
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
                "budget" => false,
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

        private async Task<string> BuildConversationHistoryAsync(string sessionId, CancellationToken cancellationToken)
        {
            var turns = await _context.AIConversationTraces
                .Where(t => t.SessionId == sessionId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(6)
                .OrderBy(t => t.CreatedAt)
                .Select(t => new { t.CustomerMessage, t.FinalAnswer })
                .ToListAsync(cancellationToken);

            if (!turns.Any()) return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("Lịch sử hội thoại gần đây trong cùng session:");
            foreach (var turn in turns)
            {
                builder.AppendLine($"Khách: {turn.CustomerMessage}");
                builder.AppendLine($"AI: {turn.FinalAnswer}");
            }

            return builder.ToString();
        }

        private async Task<FinalSynthesizerPromptConfig> GetFinalSynthesizerConfigAsync(CancellationToken cancellationToken)
        {
            var settings = await _context.SystemSettings
                .Where(s => s.GroupName == "AI")
                .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, cancellationToken);
            var formSchema = GetAISetting(settings, "AIFinalSynthesizerFormSchema", "[]");
            var optionKeywords = ExtractConditionOptionKeywords(GetAISetting(settings, "AIFinalConditionOptions", string.Empty));

            return new FinalSynthesizerPromptConfig
            {
                Style = GetAISetting(settings, "AIFinalSynthesizerStyle", "Giọng thân thiện, rõ ràng, tư vấn như lễ tân chuyên nghiệp. Trả lời ngắn gọn nhưng đủ ý. Nếu thiếu thông tin thì hỏi lại bằng các câu hỏi cụ thể."),
                FormSchema = ApplyConditionOptionKeywords(formSchema, optionKeywords),
                BasePrompt = GetAISetting(settings, "AIFinalBasePrompt", "Bạn là Final Response Synthesizer của AI Brain Center cho homestay self check-in/self check-out. Nhiệm vụ duy nhất: viết câu trả lời cuối cùng cho khách dựa trên dữ liệu các agent cung cấp."),
                LanguageRule = GetAISetting(settings, "AIFinalLanguageRule", "Luôn trả lời bằng tiếng Việt, thân thiện, tự nhiên như nhân viên tư vấn homestay."),
                DataTruthRule = GetAISetting(settings, "AIFinalDataTruthRule", "Không bịa phòng trống, giá, chính sách hoặc thông tin chi nhánh. Chỉ dùng dữ liệu từ Live System, Knowledge, Graph và cấu hình được cung cấp."),
                MissingInfoRule = GetAISetting(settings, "AIFinalMissingInfoRule", "Nếu thiếu ngày/giờ/chi nhánh/số khách theo cấu hình form đã lọc, hãy hỏi lại bằng đúng các trường cần điền. Không hỏi ngân sách vì giá phòng đã cố định trong hệ thống."),
                BookingRule = GetAISetting(settings, "AIFinalBookingRule", "Không xác nhận đặt phòng, không hứa giữ phòng, không tạo mã khóa/check-in code; chỉ hướng khách sang luồng đặt phòng chính thức."),
                FormRule = GetAISetting(settings, "AIFinalFormRule", "Form Schema JSON đã được lọc theo điều kiện của từng field cho câu hỏi hiện tại. Nếu Form Schema còn trường, hãy hỏi khách điền đúng các trường đó, không thêm trường ngoài schema. Nếu Form Schema rỗng nhưng nhu cầu chưa rõ, hãy hỏi thêm một câu ngắn để xác định intent trước khi xin thông tin."),
                PaymentRule = GetAISetting(settings, "AIFinalPaymentRule", "Nếu field type là paymentQr, được gửi đúng messageTemplate và qrImageUrl đã cấu hình như hướng dẫn chuyển khoản; không tự xác nhận booking sau khi gửi QR."),
                MemoryRule = GetAISetting(settings, "AIFinalMemoryRule", "Phải ghi nhớ các thông tin khách đã nói trong lịch sử cùng session; không hỏi lại chi nhánh, ngày giờ, số khách nếu khách đã cung cấp rồi."),
                ContextFormatRule = GetAISetting(settings, "AIFinalContextFormatRule", "Đọc Persona Agent, Safety Guard, Live System Agent JSON, Knowledge RAG Agent JSON và Graph Reasoning Agent JSON như dữ liệu nội bộ để tổng hợp câu trả lời cuối cùng; không hiển thị raw JSON cho khách.")
            };
        }

        private string GetAISetting(Dictionary<string, string> settings, string key, string fallback)
        {
            return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
        }

        private string BuildSystemPrompt(string personaSummary, string liveSnapshot, List<object> knowledge, List<object> graphReasoning, string guardResult, FinalSynthesizerPromptConfig finalConfig, string formSchema, string conversationHistory, ConductorResult? conductorResult = null, ChatMode mode = ChatMode.AdminAssistant)
        {
            var builder = new StringBuilder();
            AppendPromptSection(builder, "Base Prompt", finalConfig.BasePrompt);
            AppendPromptSection(builder, "Language Rule", finalConfig.LanguageRule);
            AppendPromptSection(builder, "Data Truth Rule", finalConfig.DataTruthRule);
            AppendPromptSection(builder, "Missing Info Rule", finalConfig.MissingInfoRule);
            AppendPromptSection(builder, "Booking Rule", finalConfig.BookingRule);
            AppendPromptSection(builder, "Form Rule", finalConfig.FormRule);
            AppendPromptSection(builder, "Payment Rule", finalConfig.PaymentRule);
            AppendPromptSection(builder, "Memory Rule", finalConfig.MemoryRule);
            AppendPromptSection(builder, "Context Format Rule", finalConfig.ContextFormatRule);

            if (mode == ChatMode.PublicBooking && conductorResult != null)
            {
                var publicPrompt = string.Empty;
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var setting = db.SystemSettings.FirstOrDefault(s => s.GroupName == "AI" && s.SettingKey == "AIPublicBookingPrompt");
                    if (setting != null && !string.IsNullOrWhiteSpace(setting.SettingValue))
                        publicPrompt = setting.SettingValue;
                }
                catch { }

                if (!string.IsNullOrWhiteSpace(publicPrompt))
                {
                    builder.AppendLine(publicPrompt
                        .Replace("{BookingAction}", conductorResult.Action.ToString())
                        .Replace("{BookingState}", JsonSerializer.Serialize(conductorResult.State)));
                }

                if (conductorResult.Action == ConductorAction.ShowRooms || conductorResult.Action == ConductorAction.ShowSlots)
                {
                    builder.AppendLine("UI blocks đã kèm theo. Hãy trả lời ngắn gọn giới thiệu các lựa chọn.");
                }
                else if (conductorResult.Action == ConductorAction.ShowForm)
                {
                    builder.AppendLine("Form đã pre-fill. Hãy hướng dẫn khách điền các field còn thiếu.");
                }
                else if (conductorResult.Action == ConductorAction.AutoBook || conductorResult.Action == ConductorAction.PaymentQr)
                {
                    builder.AppendLine("Booking đã được tạo thành công. Hãy thông báo cho khách và hướng dẫn thanh toán. KHÔNG tự bịa thông tin booking.");
                }
                else if (conductorResult.Action == ConductorAction.Reply)
                {
                    builder.AppendLine("Hãy trả lời tự nhiên, KHÔNG gợi ý phòng hay đặt phòng. Chỉ tư vấn thông tin.");
                }
                else if (conductorResult.Action == ConductorAction.AskInfo)
                {
                    builder.AppendLine("Hãy hỏi thông tin còn thiếu (chi nhánh, ngày, số khách) để tư vấn phòng phù hợp.");
                }
            }

            if (!string.IsNullOrWhiteSpace(conversationHistory)) builder.AppendLine(conversationHistory);
            builder.AppendLine($"Final Response Synthesizer Style: {finalConfig.Style}");
            builder.AppendLine($"Final Response Form Schema JSON: {formSchema}");
            builder.AppendLine($"Persona Agent: {personaSummary}");
            builder.AppendLine($"Safety Guard: {guardResult}");
            builder.AppendLine($"Live System Agent JSON: {liveSnapshot}");
            builder.AppendLine($"Knowledge RAG Agent JSON: {JsonSerializer.Serialize(knowledge)}");
            builder.AppendLine($"Graph Reasoning Agent JSON: {JsonSerializer.Serialize(graphReasoning)}");
            return builder.ToString();
        }



        private void AppendPromptSection(StringBuilder builder, string title, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;
            builder.AppendLine($"{title}: {content}");
        }
    }

    public class FinalSynthesizerPromptConfig
    {
        public string Style { get; set; } = string.Empty;
        public string FormSchema { get; set; } = "[]";
        public string BasePrompt { get; set; } = string.Empty;
        public string LanguageRule { get; set; } = string.Empty;
        public string DataTruthRule { get; set; } = string.Empty;
        public string MissingInfoRule { get; set; } = string.Empty;
        public string BookingRule { get; set; } = string.Empty;
        public string FormRule { get; set; } = string.Empty;
        public string PaymentRule { get; set; } = string.Empty;
        public string MemoryRule { get; set; } = string.Empty;
        public string ContextFormatRule { get; set; } = string.Empty;
    }
}
