using System.Net;
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
        var branchScope = GetCurrentBranchScope();
        if (!CanAccessBranchScopedData())
        {
            return Ok(Array.Empty<object>());
        }

        var sessions = await _adminChatService.GetSessionsAsync(includeDeleted, 30, branchScope);
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

        var unreadCounts = await _adminChatService.GetUnreadCustomerMessageCountsAsync(sessionIds, branchScope);
        var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync(branchScope);
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
        var session = await FindAccessibleSessionAsync(sessionId);
        if (session == null || session.IsDeleted)
        {
            return NotFound(new { message = "Không tìm thấy phiên chat để tiếp quản." });
        }

        var adminUser = HttpContext.Session.GetString("AdminUser") ?? "admin";
        await _adminChatService.TakeoverSessionAsync(sessionId, adminUser);
        session = await FindAccessibleSessionAsync(sessionId, asNoTracking: true);
        var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync(GetCurrentBranchScope());

        await BroadcastSessionScopedAsync(session?.BranchId, "sessionUpdate", new
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
        var session = await FindAccessibleSessionAsync(sessionId);
        if (session == null || session.IsDeleted)
        {
            return NotFound(new { message = "Không tìm thấy phiên chat để xóa." });
        }

        var adminUser = HttpContext.Session.GetString("AdminUser") ?? "admin";
        await _adminChatService.SoftDeleteSessionAsync(sessionId, adminUser);

        await BroadcastSessionScopedAsync(session.BranchId, "sessionDeleted", new
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
        var session = await FindAccessibleSessionAsync(sessionId, includeDeleted: true);
        if (session == null || !session.IsDeleted)
        {
            return NotFound(new { message = "Không tìm thấy phiên chat cần khôi phục." });
        }

        await _adminChatService.RestoreSessionAsync(sessionId);
        session = await FindAccessibleSessionAsync(sessionId, includeDeleted: true, asNoTracking: true);

        await BroadcastSessionScopedAsync(session?.BranchId, "sessionRestored", new
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
        var session = await FindAccessibleSessionAsync(sessionId, includeDeleted: true, asNoTracking: true);
        if (session == null)
        {
            return NotFound(new { message = "Không tìm thấy phiên chat để xóa vĩnh viễn." });
        }

        await _adminChatService.PermanentlyDeleteSessionAsync(sessionId);

        await BroadcastSessionScopedAsync(session.BranchId, "sessionPurged", new
        {
            sessionId
        });

        return Ok(new { success = true, sessionId });
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("session/{sessionId}")]
    public async Task<IActionResult> GetSessionDetail(string sessionId)
    {
        var session = await FindAccessibleSessionAsync(sessionId, includeDeleted: true, asNoTracking: true);
        if (session == null)
        {
            return NotFound(new { message = "Không tìm thấy hội thoại." });
        }

        var messages = await _adminChatService.GetSessionMessagesAsync(sessionId, GetCurrentBranchScope());
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
        var session = await FindAccessibleSessionAsync(sessionId, includeDeleted: true);
        if (session == null)
        {
            return NotFound(new { message = "Không tìm thấy hội thoại." });
        }

        var readCount = await _adminChatService.MarkCustomerMessagesReadAsync(sessionId);
        var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync(GetCurrentBranchScope());
        
        // Không cập nhật lastActivityAt khi chỉ mark read để tránh card nhảy lên đầu
        await BroadcastSessionScopedAsync(session.BranchId, "sessionUpdate", new
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
        var session = await FindAccessibleSessionAsync(sessionId, cancellationToken: cancellationToken);
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
        await BroadcastSessionScopedAsync(session.BranchId, "newMessage", replyPayload, cancellationToken);

        var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync(GetCurrentBranchScope());
        await BroadcastSessionScopedAsync(session.BranchId, "sessionUpdate", new
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
        var session = await FindAccessibleSessionAsync(sessionId, asNoTracking: true, cancellationToken: cancellationToken);
        if (session == null || session.IsDeleted)
        {
            return NotFound(new { message = "Phiên chat không còn khả dụng." });
        }

        if (!await _adminChatService.IsPausedAsync(sessionId))
        {
            return BadRequest(new { message = "Cần takeover và tạm dừng AI trước khi gửi nhanh." });
        }

        var schema = await _quickSendService.GetSchemaAsync(type, GetCurrentBranchScope(), cancellationToken);
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
        var session = await FindAccessibleSessionAsync(sessionId, cancellationToken: cancellationToken);
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
            var block = await _quickSendService.BuildAsync(sessionId, type, request.Payload, GetCurrentBranchScope(), cancellationToken);
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
            await BroadcastSessionScopedAsync(session.BranchId, "newMessage", quickSendPayload, cancellationToken);

            var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync(GetCurrentBranchScope());
            await BroadcastSessionScopedAsync(session.BranchId, "sessionUpdate", new
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
        var session = await FindAccessibleSessionAsync(sessionId, asNoTracking: true, cancellationToken: cancellationToken);
        if (session == null || session.IsDeleted)
        {
            return NotFound(new { message = "Phiên chat không còn khả dụng." });
        }

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
        var session = await FindAccessibleSessionAsync(sessionId);
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
        var accessibleSessionIds = await GetAccessibleSessionIdsAsync(includeDeleted: true);
        var requests = await _context.BookingCancellationRequests
            .Where(r => accessibleSessionIds.Contains(r.ChatSessionId))
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
    [HttpPost("cancellations/lookup-booking")]
    public async Task<IActionResult> LookupBookingForCancellation([FromBody] LookupBookingRequest request)
    {
        if (!CanAccessBranchScopedData())
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.BookingCode))
            return BadRequest(new { error = "Vui lòng nhập mã booking." });

        var bookingId = ParseBookingIdInt(request.BookingCode.Trim());
        if (!bookingId.HasValue)
            return BadRequest(new { error = "Mã booking không hợp lệ." });

        var booking = await _context.Bookings
            .Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.Id == bookingId.Value);

        if (booking == null)
            return NotFound(new { error = "Không tìm thấy booking với mã này." });

        if (string.Equals(booking.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Booking này đã bị huỷ trước đó." });

        if (!CanAccessBookingBranch(booking.Room?.BranchId))
            return NotFound(new { error = "Không tìm thấy booking với mã này." });

        return Ok(new
        {
            bookingId = booking.Id,
            customerName = booking.CustomerName,
            customerPhone = booking.CustomerPhone,
            customerEmail = booking.CustomerEmail ?? "",
            roomName = booking.Room?.Name ?? "",
            checkIn = booking.StartTime,
            checkOut = booking.EndTime,
            status = booking.Status
        });
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("cancellations/create-manual")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> CreateManualCancellation([FromForm] string bookingCode, [FromForm] IFormFile? image)
    {
        if (!CanAccessBranchScopedData())
            return NotFound();

        if (string.IsNullOrWhiteSpace(bookingCode))
            return BadRequest(new { error = "Vui lòng nhập mã booking." });

        if (image == null || image.Length == 0)
            return BadRequest(new { error = "Vui lòng chọn ảnh chụp Zalo." });

        var bookingId = ParseBookingIdInt(bookingCode.Trim());
        if (!bookingId.HasValue)
            return BadRequest(new { error = "Mã booking không hợp lệ." });

        var bookingBranchId = await _context.Bookings
            .Where(booking => booking.Id == bookingId.Value)
            .Select(booking => (int?)booking.Room.BranchId)
            .FirstOrDefaultAsync();
        if (!CanAccessBookingBranch(bookingBranchId))
            return NotFound(new { error = "Không tìm thấy booking với mã này." });

        try
        {
            var processedBy = HttpContext.Session.GetString("AdminUser") ?? "admin";
            var request = await _bookingCancellationService.CreateManualAsync(
                new CreateManualCancellationDto(bookingCode.Trim(), image, processedBy));
            return Ok(new { requestId = request.Id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [AdminAuthorize(Permission = "chats.view")]
    [HttpGet("cancellations/{id:int}")]
    public async Task<IActionResult> GetCancellationDetail(int id)
    {
        var request = await _context.BookingCancellationRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        if (!await CanAccessSessionIdAsync(request.ChatSessionId, includeDeleted: true)) return NotFound();

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
        if (!await CanAccessSessionIdAsync(request.ChatSessionId, includeDeleted: true)) return NotFound();
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
        if (!await CanAccessSessionIdAsync(request.ChatSessionId, includeDeleted: true)) return NotFound();
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
            if (!await CanAccessCancellationAsync(id)) return NotFound();
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
            if (!await CanAccessCancellationAsync(id)) return NotFound();
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
            if (!await CanAccessCancellationAsync(id)) return NotFound();
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
            if (!await CanAccessCancellationAsync(id)) return NotFound();
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
            if (!await CanAccessCancellationAsync(id))
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return Content(BuildMissingCancellationFileHtml(kind, "Ban khong co quyen truy cap tep dinh kem nay."), "text/html; charset=utf-8");
            }

            var path = await _bookingCancellationService.GetProtectedFilePathAsync(id, kind);
            if (!System.IO.File.Exists(path))
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return Content(BuildMissingCancellationFileHtml(kind, "Tep da duoc tham chieu nhung hien khong con trong he thong."), "text/html; charset=utf-8");
            }

            return PhysicalFile(path, GetImageContentType(Path.GetExtension(path)));
        }
        catch (InvalidOperationException ex)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return Content(BuildMissingCancellationFileHtml(kind, ex.Message), "text/html; charset=utf-8");
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

    private static int? ParseBookingIdInt(string code)
    {
        var digits = new string(code.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var id) ? id : null;
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

    private static string BuildMissingCancellationFileHtml(string kind, string detail)
    {
        var title = GetCancellationFileLabel(kind);
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeDetail = WebUtility.HtmlEncode(detail);

        return $$"""
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Không có {{safeTitle}}</title>
    <style>
        :root {
            color-scheme: light;
            --bg: #f6f2ea;
            --card: #fffdf9;
            --border: #eadfce;
            --text: #1f2a44;
            --muted: #6f7a90;
            --accent: #c79a57;
        }

        * { box-sizing: border-box; }

        body {
            margin: 0;
            min-height: 100vh;
            display: grid;
            place-items: center;
            padding: 24px;
            font-family: "Segoe UI", Arial, sans-serif;
            background:
                radial-gradient(circle at top left, rgba(199, 154, 87, 0.16), transparent 32%),
                linear-gradient(180deg, #fffefb 0%, var(--bg) 100%);
            color: var(--text);
        }

        .card {
            width: min(560px, 100%);
            background: var(--card);
            border: 1px solid var(--border);
            border-radius: 24px;
            padding: 32px 28px;
            box-shadow: 0 18px 50px rgba(31, 42, 68, 0.08);
        }

        .eyebrow {
            margin: 0 0 10px;
            font-size: 12px;
            font-weight: 700;
            letter-spacing: 0.18em;
            text-transform: uppercase;
            color: var(--accent);
        }

        h1 {
            margin: 0 0 12px;
            font-size: clamp(28px, 5vw, 36px);
            line-height: 1.15;
        }

        p {
            margin: 0;
            font-size: 16px;
            line-height: 1.65;
            color: var(--muted);
        }

        .actions {
            display: flex;
            gap: 12px;
            flex-wrap: wrap;
            margin-top: 24px;
        }

        .button {
            appearance: none;
            border: 0;
            border-radius: 999px;
            padding: 12px 18px;
            font-size: 14px;
            font-weight: 700;
            text-decoration: none;
            cursor: pointer;
        }

        .button-primary {
            background: var(--text);
            color: #fff;
        }

        .button-secondary {
            background: #f4ede2;
            color: var(--text);
        }
    </style>
</head>
<body>
    <main class="card">
        <p class="eyebrow">Duyet yeu cau huy</p>
        <h1>Khong co {{safeTitle}}</h1>
        <p>{{safeDetail}}</p>
        <p style="margin-top: 10px;">Ban co the quay lai man hinh duyet yeu cau huy de tiep tuc xu ly, hoac bo sung tep cho yeu cau nay neu can.</p>
        <div class="actions">
            <button class="button button-primary" type="button" onclick="if (window.history.length > 1) { history.back(); } else { location.href = '/chinhan/hethong/chat-monitor'; }">Quay lai</button>
            <a class="button button-secondary" href="/chinhan/hethong/chat-monitor">Mo chat monitor</a>
        </div>
    </main>
</body>
</html>
""";
    }

    private static string GetCancellationFileLabel(string kind)
    {
        return kind?.ToLowerInvariant() switch
        {
            "confirmation" => "anh xac nhan",
            "refundqr" => "anh QR hoan tien",
            "refundbill" => "bill hoan tien",
            _ => "tep dinh kem"
        };
    }

    private async Task<object> BuildRoomSelectorBlock(CancellationToken cancellationToken)
    {
        var branchScope = GetCurrentBranchScope();
        var roomsQuery = _context.Rooms
            .Where(r => r.Status == "Available");

        if (branchScope.HasValue)
        {
            roomsQuery = roomsQuery.Where(r => r.BranchId == branchScope.Value);
        }

        var rooms = await roomsQuery
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
        var branchScope = GetCurrentBranchScope();
        var slotsQuery = _context.RoomSlotInventories
            .Include(s => s.Room)
            .Where(s => s.Status == "Available" && s.StartTime > now);

        if (branchScope.HasValue)
        {
            slotsQuery = slotsQuery.Where(s => s.Room.BranchId == branchScope.Value);
        }

        var slots = await slotsQuery
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
        var branchScope = GetCurrentBranchScope();
        var hasBranches = branchScope.HasValue
            ? await _context.Branches.AnyAsync(b => b.Id == branchScope.Value, cancellationToken)
            : await _context.Branches.AnyAsync(cancellationToken);
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

    private bool CanAccessBranchScopedData()
        => ChatMonitorScopeHelper.IsSuperAdmin(HttpContext.Session) || HttpContext.Session.GetInt32("AdminBranchId").HasValue;

    private int? GetCurrentBranchScope()
        => ChatMonitorScopeHelper.GetScopedBranchId(HttpContext.Session);

    private IQueryable<WebHomestay.Models.AdminChatSession> BuildAccessibleSessionQuery(bool includeDeleted, bool asNoTracking = false)
    {
        var query = asNoTracking
            ? _context.AdminChatSessions.AsNoTracking()
            : _context.AdminChatSessions.AsQueryable();

        query = includeDeleted
            ? query
            : query.Where(session => !session.IsDeleted);

        return ChatMonitorScopeHelper.ApplyBranchScope(
            query,
            GetCurrentBranchScope(),
            ChatMonitorScopeHelper.IsSuperAdmin(HttpContext.Session));
    }

    private Task<WebHomestay.Models.AdminChatSession?> FindAccessibleSessionAsync(
        string sessionId,
        bool includeDeleted = true,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default)
    {
        return BuildAccessibleSessionQuery(includeDeleted, asNoTracking)
            .FirstOrDefaultAsync(session => session.SessionId == sessionId, cancellationToken);
    }

    private async Task<List<string>> GetAccessibleSessionIdsAsync(bool includeDeleted)
    {
        if (!CanAccessBranchScopedData())
        {
            return new List<string>();
        }

        return await BuildAccessibleSessionQuery(includeDeleted, asNoTracking: true)
            .Select(session => session.SessionId)
            .ToListAsync();
    }

    private async Task<bool> CanAccessSessionIdAsync(string sessionId, bool includeDeleted)
    {
        if (!CanAccessBranchScopedData())
        {
            return false;
        }

        return await BuildAccessibleSessionQuery(includeDeleted, asNoTracking: true)
            .AnyAsync(session => session.SessionId == sessionId);
    }

    private async Task<bool> CanAccessCancellationAsync(int id)
    {
        var chatSessionId = await _context.BookingCancellationRequests
            .Where(request => request.Id == id)
            .Select(request => request.ChatSessionId)
            .FirstOrDefaultAsync();

        return !string.IsNullOrWhiteSpace(chatSessionId)
            && await CanAccessSessionIdAsync(chatSessionId, includeDeleted: true);
    }

    private Task BroadcastSessionScopedAsync(int? branchId, string method, object payload, CancellationToken cancellationToken = default)
    {
        var groups = ChatMonitorScopeHelper.GetMonitorGroupsForSession(branchId);
        return _hubContext.Clients.Groups(groups).SendAsync(method, payload, cancellationToken);
    }

    private bool CanAccessBookingBranch(int? branchId)
    {
        if (ChatMonitorScopeHelper.IsSuperAdmin(HttpContext.Session))
        {
            return true;
        }

        var scopedBranchId = HttpContext.Session.GetInt32("AdminBranchId");
        return branchId.HasValue && scopedBranchId.HasValue && branchId.Value == scopedBranchId.Value;
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

public class LookupBookingRequest
{
    public string BookingCode { get; set; } = string.Empty;
}
