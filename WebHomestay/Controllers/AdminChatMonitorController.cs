using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Filters;
using WebHomestay.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using WebHomestay.Hubs;

namespace WebHomestay.Controllers;

[AdminAuthorize]
[Route("admin/chat-monitor")]
public class AdminChatMonitorController : Controller
{
    private readonly IAdminChatService _adminChatService;
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IBookingCancellationService _bookingCancellationService;

    public AdminChatMonitorController(
        IAdminChatService adminChatService,
        ApplicationDbContext context,
        IHubContext<ChatHub> hubContext,
        IBookingCancellationService bookingCancellationService)
    {
        _adminChatService = adminChatService;
        _context = context;
        _hubContext = hubContext;
        _bookingCancellationService = bookingCancellationService;
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await _adminChatService.GetActiveSessionsAsync(30);
        var sessionIds = sessions.Select(s => s.SessionId).ToList();

        var lastMessages = await _context.AdminChatMessages
            .Where(m => sessionIds.Contains(m.SessionId))
            .GroupBy(m => m.SessionId)
            .Select(g => new
            {
                SessionId = g.Key,
                LastContent = g.OrderByDescending(m => m.CreatedAt).Select(m => m.Content).FirstOrDefault(),
                LastTime = g.Max(m => m.CreatedAt)
            })
            .ToListAsync();

        var unreadCounts = await _adminChatService.GetUnreadCustomerMessageCountsAsync(sessionIds);
        var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync();
        var pendingCancellationCounts = await _context.BookingCancellationRequests
            .Where(r => sessionIds.Contains(r.ChatSessionId) && r.Status == "Pending")
            .GroupBy(r => r.ChatSessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count);

        var result = sessions.Select(s => new
        {
            sessionId = s.SessionId,
            customerName = s.CustomerName,
            status = s.Status,
            pausedBy = s.PausedBy,
            lastActivityAt = s.LastActivityAt,
            lastMessage = lastMessages.FirstOrDefault(lm => lm.SessionId == s.SessionId)?.LastContent ?? "",
            unreadCount = unreadCounts.GetValueOrDefault(s.SessionId),
            pendingCancellationCount = pendingCancellationCounts.GetValueOrDefault(s.SessionId),
            totalUnreadCount
        });

        return Ok(result);
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("session/{sessionId}")]
    public async Task<IActionResult> GetSessionDetail(string sessionId)
    {
        var messages = await _adminChatService.GetSessionMessagesAsync(sessionId);
        var traces = await _context.AIConversationTraces
            .Where(t => t.SessionId == sessionId)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new
            {
                role = "user",
                content = t.CustomerMessage,
                aiReply = t.FinalAnswer,
                createdAt = t.CreatedAt
            })
            .ToListAsync();

        var cancellations = await _context.BookingCancellationRequests
            .Where(r => r.ChatSessionId == sessionId)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Status,
                r.CustomerName,
                r.CustomerEmail,
                r.SubmittedBookingCode,
                r.BookingId,
                r.AppliedRefundPercent,
                r.CreatedAt,
                r.StaffReason
            })
            .ToListAsync();

        return Ok(new { messages, traces, cancellations });
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("session/{sessionId}/mark-read")]
    public async Task<IActionResult> MarkRead(string sessionId)
    {
        var readCount = await _adminChatService.MarkCustomerMessagesReadAsync(sessionId);
        var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync();
        await _hubContext.Clients.Group("admin_monitor").SendAsync("sessionUpdate", new
        {
            sessionId,
            unreadCount = 0,
            totalUnreadCount,
            lastActivityAt = DateTime.Now
        });
        return Ok(new { sessionId, readCount, unreadCount = 0, totalUnreadCount });
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("session/{sessionId}/quick-block/{type}")]
    public async Task<IActionResult> GetQuickBlock(string sessionId, string type, CancellationToken cancellationToken)
    {
        var block = type switch
        {
            "roomSelector" => await BuildRoomSelectorBlock(cancellationToken),
            "slotPicker" => await BuildSlotPickerBlock(cancellationToken),
            "infoForm" => await BuildInfoFormBlock(cancellationToken),
            "paymentQr" => await BuildPaymentBlock(sessionId, cancellationToken),
            _ => null
        };

        if (block == null) return BadRequest(new { message = "Loại form gửi nhanh không hợp lệ." });
        return Ok(block);
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("session/{sessionId}/auto-reply")]
    public async Task<IActionResult> SetAutoReply(string sessionId, [FromBody] AutoReplyRequest request)
    {
        var session = await _context.AdminChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session == null) return NotFound();

        session.AutoReplyMessage = request.AutoReplyMessage;
        session.LastActivityAt = DateTime.Now;

        if (request.Paused)
        {
            var adminUser = HttpContext.Session.GetString("AdminUser") ?? "admin";
            await _adminChatService.PauseAsync(sessionId, adminUser);
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("cancellations")]
    public async Task<IActionResult> GetCancellations(string status = "Pending")
    {
        var requests = await _context.BookingCancellationRequests
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new
            {
                r.Id,
                r.Status,
                r.ChatSessionId,
                r.CustomerName,
                r.CustomerEmail,
                r.SubmittedBookingCode,
                r.BookingId,
                r.CreatedAt,
                r.ProcessedAt,
                r.StaffReason
            })
            .ToListAsync();

        return Ok(requests);
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("cancellations/{id:int}")]
    public async Task<IActionResult> GetCancellationDetail(int id)
    {
        var request = await _context.BookingCancellationRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();

        var booking = request.BookingId.HasValue
            ? await BuildBookingSummary(request.BookingId.Value)
            : null;

        var suggestedIds = ParseSuggestedBookingIds(request.SuggestedBookingIdsJson);
        var suggestions = suggestedIds.Count == 0
            ? new List<object>()
            : await _context.Bookings
                .Include(b => b.Room)
                .Where(b => suggestedIds.Contains(b.Id))
                .Select(b => new object[] { new { b.Id, b.CustomerName, b.CustomerPhone, b.CustomerEmail, roomName = b.Room.Name, b.StartTime, b.EndTime, b.Status, b.TotalPrice } })
                .SelectMany(x => x)
                .ToListAsync();

        return Ok(new { request, booking, suggestions });
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("cancellations/{id:int}/link-booking")]
    public async Task<IActionResult> LinkCancellationBooking(int id, [FromForm] int bookingId)
    {
        var request = await _context.BookingCancellationRequests.FindAsync(id);
        if (request == null) return NotFound();
        var bookingExists = await _context.Bookings.AnyAsync(b => b.Id == bookingId);
        if (!bookingExists) return NotFound(new { message = "Không tìm thấy đơn đặt phòng." });

        request.BookingId = bookingId;
        request.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("cancellations/{id:int}/approve")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ApproveCancellation(int id, [FromForm] string staffReason, [FromForm] int appliedRefundPercent, [FromForm] IFormFile? refundBillProof)
    {
        try
        {
            var processedBy = HttpContext.Session.GetString("AdminUser") ?? "admin";
            var request = await _bookingCancellationService.ApproveAsync(new ProcessCancellationDto(id, staffReason, appliedRefundPercent, processedBy, refundBillProof));
            return Ok(new { success = true, request.Id, request.Status });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("cancellations/{id:int}/reject")]
    public async Task<IActionResult> RejectCancellation(int id, [FromForm] string staffReason)
    {
        try
        {
            var processedBy = HttpContext.Session.GetString("AdminUser") ?? "admin";
            var request = await _bookingCancellationService.RejectAsync(new ProcessCancellationDto(id, staffReason, 0, processedBy, null));
            return Ok(new { success = true, request.Id, request.Status });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("cancellations/{id:int}/file/{kind}")]
    public async Task<IActionResult> CancellationFile(int id, string kind)
    {
        try
        {
            var path = await _bookingCancellationService.GetProtectedFilePathAsync(id, kind);
            if (!System.IO.File.Exists(path)) return NotFound();
            return PhysicalFile(path, GetImageContentType(Path.GetExtension(path)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<object?> BuildBookingSummary(int bookingId)
    {
        return await _context.Bookings
            .Include(b => b.Room)
            .Where(b => b.Id == bookingId)
            .Select(b => new { b.Id, b.CustomerName, b.CustomerPhone, b.CustomerEmail, roomName = b.Room.Name, b.StartTime, b.EndTime, b.Status, b.TotalPrice })
            .FirstOrDefaultAsync();
    }

    private static List<int> ParseSuggestedBookingIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<int>();
        try
        {
            return JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
        }
        catch
        {
            return new List<int>();
        }
    }

    private static string GetImageContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/png"
        };
    }

    private async Task<object> BuildRoomSelectorBlock(CancellationToken cancellationToken)
    {
        var rooms = await _context.Rooms
            .Where(r => r.Status == "Available")
            .OrderBy(r => r.BranchId)
            .ThenBy(r => r.Name)
            .Take(12)
            .Select(r => new
            {
                roomId = r.Id,
                name = r.Name,
                description = r.Description,
                pricePerHour = r.PricePerHour,
                pricePerDay = r.PricePerDay,
                capacity = r.Capacity,
                maxGuests = r.MaxGuests,
                extraGuestFee = r.ExtraGuestFee,
                imageUrl = r.ImageUrl,
                detailsUrl = "/Rooms/Details/" + r.Id,
                amenities = r.Amenities.Select(a => a.Name).ToList()
            })
            .ToListAsync(cancellationToken);

        return new
        {
            message = "Mình gửi bạn danh sách phòng đang có thể chọn nhé.",
            formBlockType = "uiBlocks",
            uiBlocks = new object[]
            {
                new { type = "roomCards", data = new { rooms } }
            }
        };
    }

    private async Task<object> BuildSlotPickerBlock(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var slots = await _context.RoomSlotInventories
            .Include(s => s.Room)
            .Where(s => s.Status == "Available" && s.StartTime > now)
            .OrderBy(s => s.StartTime)
            .Take(24)
            .Select(s => new
            {
                slotId = s.Id,
                roomId = s.RoomId,
                roomName = s.Room.Name,
                label = s.SlotLabel,
                startTime = s.StartTime,
                endTime = s.EndTime,
                totalPrice = s.Room.PricePerHour > 0
                    ? Math.Round((decimal)(s.EndTime - s.StartTime).TotalHours * s.Room.PricePerHour, 0)
                    : 0m
            })
            .ToListAsync(cancellationToken);

        return new
        {
            message = slots.Count > 0 ? "Mình gửi bạn các khung giờ còn trống nhé." : "Bạn chọn ngày để mình kiểm tra khung giờ trống nhé.",
            formBlockType = "uiBlocks",
            uiBlocks = slots.Count > 0
                ? new object[] { new { type = "hourlySlots", data = new { slots } } }
                : new object[] { new { type = "dateSelector", data = new { } } }
        };
    }

    private async Task<object> BuildInfoFormBlock(CancellationToken cancellationToken)
    {
        var fields = await LoadBookingFormFields(cancellationToken);

        return new
        {
            message = "Bạn điền thông tin đặt phòng giúp mình nhé.",
            formBlockType = "uiBlocks",
            uiBlocks = new object[]
            {
                new { type = "bookingForm", data = new { fields } }
            }
        };
    }

    private async Task<object> BuildPaymentBlock(string sessionId, CancellationToken cancellationToken)
    {
        var bookingId = await FindLatestPendingBookingId(sessionId, cancellationToken);
        var paymentUrl = bookingId.HasValue ? $"/Bookings/Success/{bookingId.Value}" : "/Bookings";

        return new
        {
            message = bookingId.HasValue
                ? "Mình gửi bạn nút đi tới trang thanh toán nhé."
                : "Mình gửi bạn đường dẫn tới trang đặt phòng/thanh toán nhé.",
            formBlockType = "uiBlocks",
            uiBlocks = new object[]
            {
                new
                {
                    type = "paymentQr",
                    data = new
                    {
                        paymentUrl,
                        successUrl = paymentUrl
                    }
                }
            }
        };
    }

    private async Task<List<object>> LoadBookingFormFields(CancellationToken cancellationToken)
    {
        var schemaJson = await _context.SystemSettings
            .Where(s => s.GroupName == "AI" && s.SettingKey == "AIBookingFormSchema")
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(schemaJson) || schemaJson == "[]")
            return new List<object>();

        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var fields = new List<object>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var name = el.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : id;
                var type = el.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
                var label = el.TryGetProperty("label", out var labelEl) ? labelEl.GetString() : null;
                var required = el.TryGetProperty("required", out var reqEl) && reqEl.ValueKind == JsonValueKind.True;
                var helpText = el.TryGetProperty("helpText", out var helpEl) ? helpEl.GetString() : null;

                if (string.IsNullOrWhiteSpace(name)) continue;

                fields.Add(new
                {
                    name,
                    type = type == "image" ? "file" : (type ?? "text"),
                    label = label ?? name,
                    required,
                    value = (string?)null,
                    placeholder = helpText ?? ""
                });
            }
            return fields;
        }
        catch
        {
            return new List<object>();
        }
    }

    private async Task<int?> FindLatestPendingBookingId(string sessionId, CancellationToken cancellationToken)
    {
        var customerName = await _context.AdminChatSessions
            .Where(s => s.SessionId == sessionId)
            .Select(s => s.CustomerName)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(customerName)) return null;

        return await _context.Bookings
            .Where(b => b.CustomerName == customerName
                && (b.Status == "PendingPayment" || b.Status == "AwaitingPayment"))
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public class AutoReplyRequest
{
    public string AutoReplyMessage { get; set; } = string.Empty;
    public bool Paused { get; set; }
}
