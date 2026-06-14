using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;

namespace WebHomestay.Services.AI
{
    public class OperationalInsightService : IOperationalInsightService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAIModelClient _modelClient;

        public OperationalInsightService(ApplicationDbContext context, IAIModelClient modelClient)
        {
            _context = context;
            _modelClient = modelClient;
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

            var systemPrompt = @"Bạn là trợ lý AI phân tích số liệu vận hành cho Admin của hệ thống Homestay Tự Vận Hành.
Nhiệm vụ của bạn là đọc dữ liệu vận hành hôm nay và tạo ra một báo cáo thông minh ngắn gọn (Daily Briefing) cùng với danh sách các hành động nhanh (Quick Actions) tương ứng.

Yêu cầu định dạng đầu ra:
Bạn phải trả về DUY NHẤT một chuỗi JSON hợp lệ, không chứa bất kỳ thẻ markdown code block (như ```json) hay lời giải thích nào bên ngoài.

Cấu trúc JSON mong muốn:
{
  ""briefing"": ""Một đoạn văn tóm tắt ngắn gọn bằng tiếng Việt, thân thiện, súc tích (khoảng 3-4 câu) về tình hình vận hành hôm nay. Hãy nêu bật các cảnh báo quan trọng như: nhiều check-in/out cùng lúc, có phòng đang bảo trì, có yêu cầu hủy đơn chưa duyệt hoặc có nhiều chat khách hàng đang chờ trả lời."",
  ""quickActions"": [
    {
      ""label"": ""Tên nút hành động ngắn gọn bằng tiếng Việt (VD: Duyệt booking chờ thanh toán, Xem yêu cầu hủy, Chat với khách)"",
      ""action"": ""Mã hành động: CHOOSE_ONE_OF [VIEW_BOOKINGS, VIEW_CANCELLATIONS, VIEW_CHATS, VIEW_MAINTENANCE]"",
      ""param"": ""Tham số phụ nếu cần (ví dụ id hoặc để trống)"",
      ""type"": ""Màu sắc hiển thị: CHOOSE_ONE_OF [primary, success, warning, danger]""
    }
  ]
}

Ví dụ JSON trả về:
{
  ""briefing"": ""Chào buổi sáng! Hệ thống hôm nay ghi nhận có 3 lượt check-in và 2 lượt check-out. Đáng chú ý là có 1 yêu cầu hủy phòng đang chờ duyệt và 2 phiên chat của khách hàng chưa được trả lời. Ngoài ra, phòng Suite 102 vẫn đang trong quá trình bảo trì."",
  ""quickActions"": [
    {
      ""label"": ""Xem yêu cầu hủy phòng"",
      ""action"": ""VIEW_CANCELLATIONS"",
      ""param"": """",
      ""type"": ""danger""
    },
    {
      ""label"": ""Trả lời chat khách hàng"",
      ""action"": ""VIEW_CHATS"",
      ""type"": ""warning"",
      ""param"": """"
    }
  ]
}";

            try
            {
                var response = await _modelClient.CompleteAsync(new AIModelRequest
                {
                    SystemPrompt = systemPrompt,
                    UserMessage = $"Dữ liệu vận hành thực tế hôm nay:\n{dataContextJson}",
                    Temperature = 0.3m,
                    MaxTokens = 800
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

                // Simple validation by parsing
                using var doc = JsonDocument.Parse(jsonStr);
                return jsonStr;
            }
            catch (Exception)
            {
                // Return a default structured fallback in case of AI errors
                var fallback = new
                {
                    briefing = $"Chào Admin, hệ thống hôm nay ghi nhận {checkInsToday.Count} lượt check-in, {checkOutsToday.Count} lượt check-out. Hiện tại có {pendingPayments} booking đang chờ xử lý, {pendingCancellations} yêu cầu hủy chưa duyệt và {openChats} tin nhắn đang chờ phản hồi. Trợ lý AI tạm thời không thể phân tích sâu hơn do lỗi kết nối.",
                    quickActions = new[]
                    {
                        new { label = "Xem danh sách đặt phòng", action = "VIEW_BOOKINGS", param = "", type = "primary" },
                        new { label = "Kiểm tra kênh chat", action = "VIEW_CHATS", param = "", type = "warning" },
                        new { label = "Duyệt yêu cầu hủy phòng", action = "VIEW_CANCELLATIONS", param = "", type = "danger" }
                    }
                };
                return JsonSerializer.Serialize(fallback);
            }
        }
    }
}
