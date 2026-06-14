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
    private readonly ISettingService _settingService;
    private readonly IAdminChatQuickSendService _quickSendService;

    public AdminChatMonitorController(
        IAdminChatService adminChatService,
        ApplicationDbContext context,
        IHubContext<ChatHub> hubContext,
        IBookingCancellationService bookingCancellationService,
        ISettingService settingService,
        IAdminChatQuickSendService quickSendService)
    {
        _adminChatService = adminChatService;
        _context = context;
        _hubContext = hubContext;
        _bookingCancellationService = bookingCancellationService;
        _settingService = settingService;
        _quickSendService = quickSendService;
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(string scope = "active")
    {
        if (!string.Equals(scope, "active", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(scope, "deleted", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "scope chỉ hỗ trợ active hoặc deleted." });
        }

        var includeDeleted = string.Equals(scope, "deleted", StringComparison.OrdinalIgnoreCase);
        var sessions = await _adminChatService.GetSessionsAsync(includeDeleted, 30);
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
            pauseReason = s.PauseReason,
            takenOverBy = s.TakenOverBy,
            takenOverAt = s.TakenOverAt,
            isDeleted = s.IsDeleted,
            deletedAt = s.DeletedAt,
            deletedBy = s.DeletedBy,
            lastActivityAt = s.LastActivityAt,
            lastMessage = lastMessages.FirstOrDefault(lm => lm.SessionId == s.SessionId)?.LastContent ?? "",
            unreadCount = unreadCounts.GetValueOrDefault(s.SessionId),
            pendingCancellationCount = pendingCancellationCounts.GetValueOrDefault(s.SessionId),
            totalUnreadCount
        });

        return Ok(result);
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("session/{sessionId}/takeover")]
    public async Task<IActionResult> Takeover(string sessionId)
    {
        var session = await _context.AdminChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session == null || session.IsDeleted)
        {
            return NotFound(new { message = "Không tìm thấy phiên chat để tiếp quản." });
        }

        var adminUser = HttpContext.Session.GetString("AdminUser") ?? "admin";
        await _adminChatService.TakeoverSessionAsync(sessionId, adminUser);
        session = await _context.AdminChatSessions.AsNoTracking().FirstAsync(s => s.SessionId == sessionId);
        var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync();

        await _hubContext.Clients.Group("admin_monitor").SendAsync("sessionUpdate", new
        {
            sessionId,
            status = session.Status,
            pausedBy = session.PausedBy,
            pauseReason = session.PauseReason,
            takenOverBy = session.TakenOverBy,
            takenOverAt = session.TakenOverAt,
            lastActivityAt = session.LastActivityAt,
            totalUnreadCount
        });

        return Ok(new
        {
            success = true,
            sessionId,
            status = session.Status,
            pausedBy = session.PausedBy,
            pauseReason = session.PauseReason,
            takenOverBy = session.TakenOverBy,
            takenOverAt = session.TakenOverAt
        });
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("session/{sessionId}/delete")]
    public async Task<IActionResult> SoftDelete(string sessionId)
    {
        var session = await _context.AdminChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session == null || session.IsDeleted)
        {
            return NotFound(new { message = "Không tìm thấy phiên chat để xóa." });
        }

        var adminUser = HttpContext.Session.GetString("AdminUser") ?? "admin";
        await _adminChatService.SoftDeleteSessionAsync(sessionId, adminUser);

        await _hubContext.Clients.Group("admin_monitor").SendAsync("sessionDeleted", new
        {
            sessionId,
            deletedBy = adminUser,
            deletedAt = DateTime.Now
        });

        return Ok(new { success = true, sessionId });
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("session/{sessionId}/restore")]
    public async Task<IActionResult> Restore(string sessionId)
    {
        var session = await _context.AdminChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session == null || !session.IsDeleted)
        {
            return NotFound(new { message = "Không tìm thấy phiên chat cần khôi phục." });
        }

        await _adminChatService.RestoreSessionAsync(sessionId);
        session = await _context.AdminChatSessions.AsNoTracking().FirstAsync(s => s.SessionId == sessionId);

        await _hubContext.Clients.Group("admin_monitor").SendAsync("sessionRestored", new
        {
            sessionId,
            status = session.Status,
            pausedBy = session.PausedBy,
            pauseReason = session.PauseReason,
            takenOverBy = session.TakenOverBy,
            takenOverAt = session.TakenOverAt,
            isDeleted = session.IsDeleted,
            deletedAt = session.DeletedAt,
            deletedBy = session.DeletedBy,
            lastActivityAt = session.LastActivityAt
        });

        return Ok(new { success = true, sessionId });
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("session/{sessionId}/delete-permanent")]
    public async Task<IActionResult> PermanentlyDelete(string sessionId)
    {
        var session = await _context.AdminChatSessions.AsNoTracking().FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session == null)
        {
            return NotFound(new { message = "Không tìm thấy phiên chat để xóa vĩnh viễn." });
        }

        await _adminChatService.PermanentlyDeleteSessionAsync(sessionId);

        await _hubContext.Clients.Group("admin_monitor").SendAsync("sessionPurged", new
        {
            sessionId
        });

        return Ok(new { success = true, sessionId });
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
        
        // Không cập nhật lastActivityAt khi chỉ mark read để tránh card nhảy lên đầu
        await _hubContext.Clients.Group("admin_monitor").SendAsync("sessionUpdate", new
        {
            sessionId,
            unreadCount = 0,
            totalUnreadCount
            // Bỏ lastActivityAt để giữ nguyên vị trí card
        });
        return Ok(new { sessionId, readCount, unreadCount = 0, totalUnreadCount });
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("session/{sessionId}/reply")]
    public async Task<IActionResult> SendReply(string sessionId, [FromBody] AdminChatReplyRequest request, CancellationToken cancellationToken = default)
    {
        var session = await _context.AdminChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
        if (session == null || session.IsDeleted)
        {
            return BadRequest(new { message = "Phiên chat không còn khả dụng để gửi tin." });
        }

        if (!await _adminChatService.IsPausedAsync(sessionId))
        {
            return BadRequest(new { message = "Cần takeover và tạm dừng AI trước khi nhân viên gửi tin nhắn." });
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { message = "Nội dung tin nhắn không được để trống." });
        }

        var adminUser = HttpContext.Session.GetString("AdminUser") ?? "admin";
        var message = await _adminChatService.AddAdminReplyAsync(
            sessionId,
            request.Content.Trim(),
            adminUser,
            request.UiBlocksJson,
            request.FormBlockType);

        object? uiBlocks = null;
        if (!string.IsNullOrWhiteSpace(message.FormBlockJson))
        {
            try
            {
                uiBlocks = JsonSerializer.Deserialize<object>(message.FormBlockJson);
            }
            catch
            {
                uiBlocks = null;
            }
        }

        var replyPayload = new
        {
            sessionId,
            role = "admin",
            content = message.Content,
            formBlockJson = message.FormBlockJson,
            formBlockType = message.FormBlockType,
            uiBlocks,
            createdAt = message.CreatedAt
        };
        await _hubContext.Clients.Group($"user_{sessionId}").SendAsync("newMessage", replyPayload);
        await _hubContext.Clients.Group("admin_monitor").SendAsync("newMessage", replyPayload);

        var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync();
        await _hubContext.Clients.Group("admin_monitor").SendAsync("sessionUpdate", new
        {
            sessionId,
            status = session.Status,
            pausedBy = session.PausedBy,
            pauseReason = session.PauseReason,
            takenOverBy = session.TakenOverBy,
            takenOverAt = session.TakenOverAt,
            lastMessage = message.Content,
            lastActivityAt = message.CreatedAt,
            totalUnreadCount
        });

        return Ok(new
        {
            success = true,
            message = new
            {
                message.Id,
                message.Content,
                message.FormBlockType,
                message.CreatedAt
            }
        });
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("session/{sessionId}/quick-send/{type}/schema")]
    public async Task<IActionResult> GetQuickSendSchema(string sessionId, string type, CancellationToken cancellationToken)
    {
        var session = await _context.AdminChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
        if (session == null || session.IsDeleted)
        {
            return NotFound(new { message = "Phiên chat không còn khả dụng." });
        }

        if (!await _adminChatService.IsPausedAsync(sessionId))
        {
            return BadRequest(new { message = "Cần takeover và tạm dừng AI trước khi gửi nhanh." });
        }

        var schema = await _quickSendService.GetSchemaAsync(type, cancellationToken);
        if (schema == null)
        {
            return BadRequest(new { message = "Loại form gửi nhanh không hợp lệ." });
        }

        return Ok(schema);
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("session/{sessionId}/quick-send/{type}")]
    public async Task<IActionResult> SendQuickSendBlock(
        string sessionId,
        string type,
        [FromBody] AdminChatQuickSendRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _context.AdminChatSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
        if (session == null || session.IsDeleted)
        {
            return BadRequest(new { message = "Phiên chat không còn khả dụng để gửi nhanh." });
        }

        if (!await _adminChatService.IsPausedAsync(sessionId))
        {
            return BadRequest(new { message = "Cần takeover và tạm dừng AI trước khi gửi nhanh." });
        }

        try
        {
            var block = await _quickSendService.BuildAsync(sessionId, type, request.Payload, cancellationToken);
            if (block == null)
            {
                return BadRequest(new { message = "Loại form gửi nhanh không hợp lệ." });
            }

            var adminUser = HttpContext.Session.GetString("AdminUser") ?? "admin";
            var uiBlocksJson = JsonSerializer.Serialize(new { uiBlocks = block.UiBlocks });
            var message = await _adminChatService.AddAdminReplyAsync(
                sessionId,
                block.Message,
                adminUser,
                uiBlocksJson,
                block.FormBlockType);

            var quickSendPayload = new
            {
                sessionId,
                role = "admin",
                content = message.Content,
                formBlockJson = message.FormBlockJson,
                formBlockType = message.FormBlockType,
                uiBlocks = block.UiBlocks,
                createdAt = message.CreatedAt
            };
            await _hubContext.Clients.Group($"user_{sessionId}").SendAsync("newMessage", quickSendPayload, cancellationToken);
            await _hubContext.Clients.Group("admin_monitor").SendAsync("newMessage", quickSendPayload, cancellationToken);

            var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync();
            await _hubContext.Clients.Group("admin_monitor").SendAsync("sessionUpdate", new
            {
                sessionId,
                status = session.Status,
                pausedBy = session.PausedBy,
                pauseReason = session.PauseReason,
                takenOverBy = session.TakenOverBy,
                takenOverAt = session.TakenOverAt,
                lastMessage = message.Content,
                lastActivityAt = message.CreatedAt,
                totalUnreadCount
            }, cancellationToken);

            return Ok(new
            {
                success = true,
                message = new
                {
                    message.Id,
                    message.Content,
                    message.FormBlockType,
                    message.CreatedAt
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Đã xảy ra lỗi hệ thống khi xử lý yêu cầu gửi nhanh: " + ex.Message });
        }
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
            "bookingCta" => await BuildBookingCtaBlock(sessionId, cancellationToken),
            "handoffContact" => await BuildHandoffContactBlock(cancellationToken),
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
            ? []
            : await _context.Bookings
                .Include(b => b.Room)
                .Where(b => suggestedIds.Contains(b.Id))
                .Select(b => new { b.Id, b.CustomerName, b.CustomerPhone, b.CustomerEmail, roomName = b.Room.Name, b.StartTime, b.EndTime, b.Status, b.TotalPrice })
                .ToListAsync();

        var handlingMode = await _settingService.GetStringAsync("CancellationHandlingMode", "Manual");
        return Ok(new { request, booking, suggestions, handlingMode });
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("cancellations/{id:int}/cancel-booking")]
    public async Task<IActionResult> CancelApprovedBooking(int id)
    {
        var request = await _context.BookingCancellationRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        if (!string.Equals(request.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Chỉ có thể hủy booking sau khi yêu cầu đã được chấp nhận." });
        if (!request.BookingId.HasValue)
            return BadRequest(new { message = "Yêu cầu hủy chưa được liên kết với booking." });

        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == request.BookingId.Value);
        if (booking == null) return NotFound(new { message = "Không tìm thấy booking được liên kết." });

        booking.Status = "Cancelled";
        if (booking.BookingMode == Models.BookingMode.Hourly && booking.RoomSlotInventoryId.HasValue)
        {
            var slot = await _context.RoomSlotInventories.FirstOrDefaultAsync(s => s.Id == booking.RoomSlotInventoryId.Value);
            if (slot is not null) slot.Status = "Available";
        }

        request.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { success = true, booking.Id, booking.Status });
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
    [HttpPost("cancellations/{id:int}/approval-preview")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> PreviewApprovalCancellation(int id, [FromForm] string staffReason, [FromForm] int appliedRefundPercent, [FromForm] IFormFile? refundBillProof)
    {
        try
        {
            var processedBy = HttpContext.Session.GetString("AdminUser") ?? "admin";
            var preview = await _bookingCancellationService.BuildApprovalPreviewAsync(new ProcessCancellationDto(id, staffReason, appliedRefundPercent, processedBy, refundBillProof));
            return Ok(preview);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("cancellations/{id:int}/approve")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ApproveCancellation(int id, [FromForm] string staffReason, [FromForm] int appliedRefundPercent, [FromForm] IFormFile? refundBillProof, [FromForm] string? notificationEmailSubject, [FromForm] string? notificationEmailBody)
    {
        try
        {
            var processedBy = HttpContext.Session.GetString("AdminUser") ?? "admin";
            var request = await _bookingCancellationService.ApproveAsync(new ProcessCancellationDto(id, staffReason, appliedRefundPercent, processedBy, refundBillProof, notificationEmailSubject, notificationEmailBody));
            return Ok(new { success = true, request.Id, request.Status });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("cancellations/{id:int}/rejection-preview")]
    public async Task<IActionResult> PreviewRejectionCancellation(int id, [FromForm] string staffReason)
    {
        try
        {
            var processedBy = HttpContext.Session.GetString("AdminUser") ?? "admin";
            var preview = await _bookingCancellationService.BuildRejectionPreviewAsync(new ProcessCancellationDto(id, staffReason, 0, processedBy, null));
            return Ok(preview);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("cancellations/{id:int}/reject")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> RejectCancellation(int id, [FromForm] string staffReason, [FromForm] string? notificationEmailSubject, [FromForm] string? notificationEmailBody)
    {
        try
        {
            var processedBy = HttpContext.Session.GetString("AdminUser") ?? "admin";
            var request = await _bookingCancellationService.RejectAsync(new ProcessCancellationDto(id, staffReason, 0, processedBy, null, notificationEmailSubject, notificationEmailBody));
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

    private async Task<object> BuildBookingCtaBlock(string sessionId, CancellationToken cancellationToken)
    {
        var bookingId = await FindLatestPendingBookingId(sessionId, cancellationToken);
        var targetUrl = bookingId.HasValue ? $"/Bookings/Success/{bookingId.Value}" : "/Bookings";

        return new
        {
            message = bookingId.HasValue
                ? "Mình gửi bạn nút đi tới trang thanh toán/hoàn tất đặt phòng nhé."
                : "Mình gửi bạn nút qua luồng đặt phòng chính thức nhé.",
            formBlockType = "uiBlocks",
            uiBlocks = new object[]
            {
                new
                {
                    type = "bookingCta",
                    data = new
                    {
                        target = targetUrl,
                        message = "Bạn bấm nút bên dưới để tiếp tục trên trang đặt phòng chính thức."
                    }
                }
            }
        };
    }

    private async Task<object> BuildHandoffContactBlock(CancellationToken cancellationToken)
    {
        var hasBranches = await _context.Branches.AnyAsync(cancellationToken);
        return new
        {
            message = hasBranches
                ? "Mình gửi bạn thông tin liên hệ chi nhánh để nhân viên hỗ trợ trực tiếp nhé."
                : "Mình sẽ chuyển bạn sang nhân viên hỗ trợ trực tiếp.",
            formBlockType = "uiBlocks",
            uiBlocks = new object[]
            {
                new
                {
                    type = "handoffContact",
                    data = new
                    {
                        message = "Bạn chọn chi nhánh phù hợp để lấy Zalo/email liên hệ trực tiếp."
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

public class AdminChatReplyRequest
{
    public string Content { get; set; } = string.Empty;
    public string? UiBlocksJson { get; set; }
    public string? FormBlockType { get; set; }
}

public class AdminChatQuickSendRequest
{
    public JsonElement Payload { get; set; }
}
