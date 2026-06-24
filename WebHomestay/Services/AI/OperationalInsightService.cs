using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebHomestay.Data;

namespace WebHomestay.Services.AI
{
    public class OperationalInsightService : IOperationalInsightService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAIModelClient _modelClient;
        private readonly ILogger<OperationalInsightService> _logger;

        public OperationalInsightService(
            ApplicationDbContext context,
            IAIModelClient modelClient,
            ILogger<OperationalInsightService> logger)
        {
            _context = context;
            _modelClient = modelClient;
            _logger = logger;
        }

        public async Task<string> GetDailyBriefingJsonAsync(CancellationToken cancellationToken = default)
        {
            var today = DateTime.Today;

            // Gather database metrics
            var checkInsToday = await _context.Bookings
                .AsNoTracking()
                .Where(b => b.StartTime.Date == today && !b.IsDeleted && b.Status != "Cancelled")
                .Select(b => new { b.Id, b.CustomerName, b.Status, b.StartTime })
                .ToListAsync(cancellationToken);

            var checkOutsToday = await _context.Bookings
                .AsNoTracking()
                .Where(b => b.EndTime.Date == today && !b.IsDeleted && b.Status != "Cancelled")
                .Select(b => new { b.Id, b.CustomerName, b.Status, b.EndTime })
                .ToListAsync(cancellationToken);

            var pendingPayments = await _context.Bookings
                .AsNoTracking()
                .Where(b => (b.Status == "PendingPayment" || b.Status == "AwaitingApproval") && !b.IsDeleted)
                .CountAsync(cancellationToken);

            var openChats = await _context.AdminChatSessions
                .AsNoTracking()
                .Where(s => s.Status == "open" && !s.IsDeleted)
                .CountAsync(cancellationToken);

            var pendingCancellations = await _context.BookingCancellationRequests
                .AsNoTracking()
                .Where(c => c.Status == "Pending")
                .CountAsync(cancellationToken);

            var maintenanceRooms = await _context.Rooms
                .AsNoTracking()
                .Where(r => r.Status == "Maintenance")
                .Select(r => r.Name)
                .ToListAsync(cancellationToken);

            var totalRooms = await _context.Rooms.CountAsync(cancellationToken);

            // Construct data context for LLM
            var dataContext = new
            {
                TodayDate = today.ToString("dd/MM/yyyy"),
                TotalRooms = totalRooms,
                CheckInsCount = checkInsToday.Count,
                CheckInsList = checkInsToday.Select(c => $"{c.CustomerName} (Trạng thái: {c.Status}, Vào: {c.StartTime:HH:mm})"),
                CheckOutsCount = checkOutsToday.Count,
                CheckOutsList = checkOutsToday.Select(c => $"{c.CustomerName} (Trạng thái: {c.Status}, Ra: {c.EndTime:HH:mm})"),
                PendingPaymentsCount = pendingPayments,
                OpenChatsCount = openChats,
                PendingCancellationsCount = pendingCancellations,
                MaintenanceRoomsCount = maintenanceRooms.Count,
                MaintenanceRoomsList = maintenanceRooms
            };

            var dataContextJson = JsonSerializer.Serialize(dataContext);

            var systemPrompt = @"Bạn là trợ lý AI phân tích số liệu vận hành cho admin homestay.
Trả về DUY NHẤT một JSON hợp lệ, không markdown, không giải thích thêm.
JSON phải có dạng:
{""briefing"":""..."",""quickActions"":[{""label"":""..."",""action"":""VIEW_BOOKINGS|VIEW_CANCELLATIONS|VIEW_CHATS|VIEW_MAINTENANCE"",""param"":"""",""type"":""primary|success|warning|danger""}]}
Briefing gồm 2-3 câu tiếng Việt, súc tích, nêu rõ check-in/check-out, booking chờ xử lý, yêu cầu hủy, chat chờ phản hồi, bảo trì nếu có.";

            try
            {
                var response = await _modelClient.CompleteAsync(new AIModelRequest
                {
                    SystemPrompt = systemPrompt,
                    UserMessage = $"Dữ liệu vận hành thực tế hôm nay:\n{dataContextJson}",
                    Temperature = 0.2m,
                    MaxTokens = 220
                }, cancellationToken);

                var jsonStr = response.Content?.Trim() ?? string.Empty;
                
                // Strip markdown block formatting if present
                if (jsonStr.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                {
                    jsonStr = jsonStr.Substring(7);
                }
                if (jsonStr.StartsWith("```", StringComparison.OrdinalIgnoreCase))
                {
                    jsonStr = jsonStr.Substring(3);
                }
                if (jsonStr.EndsWith("```", StringComparison.OrdinalIgnoreCase))
                {
                    jsonStr = jsonStr.Substring(0, jsonStr.Length - 3);
                }
                jsonStr = jsonStr.Trim();

                if (string.IsNullOrWhiteSpace(jsonStr))
                {
                    throw new InvalidOperationException("Operational briefing AI returned empty content.");
                }

                using var doc = JsonDocument.Parse(jsonStr);
                return BuildSuccessfulBriefingJson(jsonStr, response.Provider, response.Model);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build operational briefing from AI provider.");
                return JsonSerializer.Serialize(BuildFallbackBriefing(
                    checkInsToday.Count,
                    checkOutsToday.Count,
                    pendingPayments,
                    pendingCancellations,
                    openChats,
                    maintenanceRooms.Count,
                    ExtractProviderName(ex),
                    DescribeOperationalInsightFailure(ex)));
            }
        }

        private static string BuildSuccessfulBriefingJson(string jsonStr, string provider, string model)
        {
            var root = JsonNode.Parse(jsonStr)?.AsObject() ?? new JsonObject();
            root["status"] = new JsonObject
            {
                ["tone"] = "success",
                ["label"] = "Đã kết nối AI",
                ["detail"] = "Provider phản hồi thành công và đúng định dạng phân tích.",
                ["provider"] = provider,
                ["model"] = model,
                ["connection"] = "ok",
                ["format"] = "valid",
                ["usingFallback"] = false
            };

            return root.ToJsonString();
        }

        private static object BuildFallbackBriefing(
            int checkIns,
            int checkOuts,
            int pendingPayments,
            int pendingCancellations,
            int openChats,
            int maintenanceRooms,
            string provider,
            string failureReason)
        {
            var maintenanceSentence = maintenanceRooms > 0
                ? $" Có {maintenanceRooms} phòng đang ở trạng thái bảo trì."
                : string.Empty;

            var status = BuildFailureStatus(provider, failureReason);

            return new
            {
                briefing = $"Chào Admin, hệ thống hôm nay ghi nhận {checkIns} lượt check-in, {checkOuts} lượt check-out. Hiện tại có {pendingPayments} booking đang chờ xử lý, {pendingCancellations} yêu cầu hủy chưa duyệt và {openChats} tin nhắn đang chờ phản hồi.{maintenanceSentence} {failureReason}".Trim(),
                status,
                quickActions = new[]
                {
                    new { label = "Xem danh sách đặt phòng", action = "VIEW_BOOKINGS", param = "", type = "primary" },
                    new { label = "Kiểm tra kênh chat", action = "VIEW_CHATS", param = "", type = "warning" },
                    new { label = "Duyệt yêu cầu hủy phòng", action = "VIEW_CANCELLATIONS", param = "", type = "danger" }
                }
            };
        }

        private static object BuildFailureStatus(string provider, string failureReason)
        {
            var lowered = failureReason.ToLowerInvariant();
            var tone = "warning";
            var label = "AI đang dùng fallback an toàn";
            var connection = "degraded";
            var format = "unknown";

            if (lowered.Contains("đúng định dạng"))
            {
                label = "AI phản hồi chưa đúng định dạng";
                format = "invalid";
            }
            else if (lowered.Contains("quota") || lowered.Contains("tần suất"))
            {
                label = "AI chạm giới hạn quota";
                tone = "danger";
                connection = "failed";
            }
            else if (lowered.Contains("từ chối truy cập") || lowered.Contains("api key"))
            {
                label = "AI bị từ chối truy cập";
                tone = "danger";
                connection = "failed";
            }
            else if (lowered.Contains("chưa phản hồi ổn định"))
            {
                label = "Kết nối AI chưa ổn định";
            }

            return new
            {
                tone,
                label,
                detail = failureReason,
                provider,
                model = string.Empty,
                connection,
                format,
                usingFallback = true
            };
        }

        private static string DescribeOperationalInsightFailure(Exception ex)
        {
            var message = ex.Message?.ToLowerInvariant() ?? string.Empty;

            if (message.Contains("returned 401") || message.Contains("unauthorized") || message.Contains("api key"))
            {
                return "Trợ lý AI tạm thời bị từ chối truy cập, cần kiểm tra API key hoặc quyền của provider.";
            }

            if (message.Contains("returned 429") || message.Contains("quota") || message.Contains("rate limit"))
            {
                return "Trợ lý AI đang chạm giới hạn quota hoặc tần suất gọi, vui lòng thử lại sau.";
            }

            if (ex is JsonException || message.Contains("empty content") || message.Contains("invalid json") || message.Contains("expected depth"))
            {
                return "Trợ lý AI có phản hồi nhưng chưa đúng định dạng phân tích, nên hệ thống đang dùng bản tóm tắt an toàn.";
            }

            return "Trợ lý AI tạm thời chưa phản hồi ổn định, nên hệ thống đang dùng bản tóm tắt an toàn.";
        }

        private static string ExtractProviderName(Exception ex)
        {
            var message = ex.Message ?? string.Empty;
            if (message.Contains("'groq'", StringComparison.OrdinalIgnoreCase) || message.Contains("groq", StringComparison.OrdinalIgnoreCase))
            {
                return "groq";
            }

            if (message.Contains("'gemini'", StringComparison.OrdinalIgnoreCase) || message.Contains("gemini", StringComparison.OrdinalIgnoreCase))
            {
                return "gemini";
            }

            return "ai-provider";
        }
    }
}
