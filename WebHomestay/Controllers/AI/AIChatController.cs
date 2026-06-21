using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Hubs;
using WebHomestay.Services;

namespace WebHomestay.Controllers.AI
{
    [Route("ai")]
    public class AIChatController : Controller
    {
        private readonly IAIBrainOrchestrator _orchestrator;
        private readonly IWebHostEnvironment _environment;
        private readonly IImageMaskingService _maskingService;
        private readonly ApplicationDbContext _context;
        private readonly IBookingConductor _bookingConductor;
        private readonly IBranchLeadTimeService _branchLeadTimeService;
        private readonly IAdminChatService _adminChatService;
        private readonly IHubContext<Hubs.ChatHub> _hubContext;
        private readonly ILogger<AIChatController> _logger;

        public AIChatController(
            IAIBrainOrchestrator orchestrator,
            IWebHostEnvironment environment,
            IImageMaskingService maskingService,
            ApplicationDbContext context,
            IBookingConductor bookingConductor,
            IBranchLeadTimeService branchLeadTimeService,
            IAdminChatService adminChatService,
            IHubContext<Hubs.ChatHub> hubContext,
            ILogger<AIChatController> logger)
        {
            _orchestrator = orchestrator;
            _environment = environment;
            _maskingService = maskingService;
            _context = context;
            _bookingConductor = bookingConductor;
            _branchLeadTimeService = branchLeadTimeService;
            _adminChatService = adminChatService;
            _hubContext = hubContext;
            _logger = logger;
        }

        private static BookingActionRequest MapBookingActionRequest(JsonElement raw)
        {
            var req = new BookingActionRequest
            {
                SessionId = raw.TryGetProperty("sessionId", out var sid) ? sid.GetString() ?? "" : "",
                Action = raw.TryGetProperty("action", out var act) ? act.GetString() ?? "" : "",
            };

            if (raw.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.Object)
            {
                if (state.TryGetProperty("selectedRoomId", out var roomEl) && roomEl.ValueKind == JsonValueKind.Number)
                    req.RoomId = roomEl.GetInt32();
                if (state.TryGetProperty("selectedSlotId", out var slotEl) && slotEl.ValueKind == JsonValueKind.Number)
                    req.SlotId = slotEl.GetInt32();
                if (state.TryGetProperty("branchId", out var branchEl) && branchEl.ValueKind == JsonValueKind.Number)
                    req.BranchId = branchEl.GetInt32();
                if (state.TryGetProperty("guestCount", out var guestEl) && guestEl.ValueKind == JsonValueKind.Number)
                    req.GuestCount = guestEl.GetInt32();
                if (state.TryGetProperty("bookingMode", out var modeEl) && modeEl.ValueKind == JsonValueKind.String)
                    req.BookingMode = modeEl.GetString();
                if (state.TryGetProperty("checkInDate", out var ciEl) && ciEl.ValueKind == JsonValueKind.String)
                    req.CheckInDate = ciEl.GetString();
                if (state.TryGetProperty("checkOutDate", out var coEl) && coEl.ValueKind == JsonValueKind.String)
                    req.CheckOutDate = coEl.GetString();
                if (state.TryGetProperty("hourlyDate", out var hdEl) && hdEl.ValueKind == JsonValueKind.String)
                    req.HourlyDate = hdEl.GetString();
            }

            if (raw.TryGetProperty("formSubmission", out var formEl) && formEl.ValueKind == JsonValueKind.Object)
            {
                var rawDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(formEl.GetRawText());
                if (rawDict != null)
                {
                    req.FormData = rawDict
                        .Where(kv => kv.Value.ValueKind == JsonValueKind.String)
                        .ToDictionary(kv => kv.Key, kv => kv.Value.GetString() ?? "");
                }
            }

            return req;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] JsonElement raw, CancellationToken cancellationToken)
        {
            var request = new PublicAIChatRequest
            {
                SessionId = raw.TryGetProperty("sessionId", out var sidProp) ? sidProp.GetString() ?? "" : "",
                Message = raw.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "",
                CustomerName = raw.TryGetProperty("customerName", out var nameProp) ? nameProp.GetString() : null,
                BranchId = raw.TryGetProperty("branchId", out var bidProp) && bidProp.ValueKind == JsonValueKind.Number ? bidProp.GetInt32() : null,
                BookingMode = raw.TryGetProperty("bookingMode", out var modeProp) ? modeProp.GetString() ?? "hourly" : "hourly",
                GuestCount = raw.TryGetProperty("guestCount", out var gcProp) && gcProp.ValueKind == JsonValueKind.Number ? gcProp.GetInt32() : 1
            };
            if (raw.TryGetProperty("startTime", out var stProp) && stProp.ValueKind == JsonValueKind.String && DateTime.TryParse(stProp.GetString(), out var stDate)) request.StartTime = stDate;
            if (raw.TryGetProperty("endTime", out var etProp) && etProp.ValueKind == JsonValueKind.String && DateTime.TryParse(etProp.GetString(), out var etDate)) request.EndTime = etDate;

            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new
                {
                    answer = "Bạn nhập giúp mình nhu cầu đặt phòng để mình tư vấn nhé.",
                    message = "Bạn nhập giúp mình nhu cầu đặt phòng để mình tư vấn nhé.",
                    sessionId = request?.SessionId ?? string.Empty,
                    currentStep = "reply",
                    uiBlocks = Array.Empty<object>(),
                    state = new AIBookingSessionState()
                });
            }

            try
            {
                // Upsert session + update activity
                var session = await _adminChatService.UpsertSessionAsync(
                    request.SessionId, request.CustomerName, request.BranchId);
                await _adminChatService.AddCustomerMessageAsync(
                    request.SessionId, request.Message, request.CustomerName);
                var contactPhone = ExtractPhoneLikeContact(request.Message);
                if (!string.IsNullOrWhiteSpace(contactPhone))
                {
                    await _adminChatService.UpsertSessionAsync(request.SessionId, contactPhone, request.BranchId);
                    await _adminChatService.AddSystemMessageAsync(
                        request.SessionId,
                        $"Khách vừa để lại SĐT/Zalo trong chat: {contactPhone}. Nhân viên nên liên hệ hỗ trợ nếu khách cần chốt đặt phòng.");
                }

                var unreadCount = (await _adminChatService.GetUnreadCustomerMessageCountsAsync(new[] { request.SessionId }, session.BranchId))
                    .GetValueOrDefault(request.SessionId);
                var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync(session.BranchId);

                // Check if paused
                if (session.Status == "paused")
                {
                    await _adminChatService.AddSystemAutoReplyAsync(request.SessionId);

                    var msg = await _context.AdminChatMessages
                        .Where(m => m.SessionId == request.SessionId)
                        .OrderByDescending(m => m.CreatedAt)
                        .FirstAsync(cancellationToken);

                    await _hubContext.Clients.Group($"user_{request.SessionId}")
                        .SendAsync("newMessage", new
                        {
                            role = "system",
                            content = msg.Content,
                            createdAt = msg.CreatedAt
                        }, cancellationToken);

                    // Broadcast customer message to admin monitor in realtime
                    await BroadcastToAdminGroupsAsync(session.BranchId, "newMessage", new
                        {
                            sessionId = request.SessionId,
                            role = "user",
                            content = request.Message,
                            createdAt = DateTime.Now
                        }, cancellationToken);

                    // Broadcast system auto-reply to admin monitor in realtime
                    await BroadcastToAdminGroupsAsync(session.BranchId, "newMessage", new
                        {
                            sessionId = request.SessionId,
                            role = "system",
                            content = msg.Content,
                            createdAt = msg.CreatedAt
                        }, cancellationToken);

                    await BroadcastToAdminGroupsAsync(session.BranchId, "sessionUpdate", new
                        {
                            sessionId = request.SessionId,
                            status = "paused",
                            lastMessage = request.Message,
                            unreadCount,
                            totalUnreadCount,
                            lastActivityAt = DateTime.Now
                        }, cancellationToken);

                    return Ok(new
                    {
                        answer = msg.Content,
                        message = msg.Content,
                        sessionId = request.SessionId,
                        currentStep = "paused",
                        isPaused = true,
                        uiBlocks = Array.Empty<object>(),
                        state = new AIBookingSessionState()
                    });
                }

                // Broadcast customer message to admin monitor in realtime (normal flow)
                await BroadcastToAdminGroupsAsync(session.BranchId, "newMessage", new
                    {
                        sessionId = request.SessionId,
                        role = "user",
                        content = request.Message,
                        createdAt = DateTime.Now
                    }, cancellationToken);

                // Normal AI flow
                var brainRequest = new AIBrainChatRequest
                {
                    SessionId = request.SessionId,
                    Message = request.Message,
                    Mode = ChatMode.PublicBooking,
                    BranchId = request.BranchId,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    GuestCount = request.GuestCount
                };

                var brainResponse = await _orchestrator.ChatAsync(brainRequest, cancellationToken);
                var resolvedBranchId = (brainResponse.BookingState as AIBookingSessionState)?.BranchId ?? request.BranchId ?? session.BranchId;
                await _adminChatService.AssignBranchAsync(request.SessionId, resolvedBranchId);
                session = await _adminChatService.UpsertSessionAsync(request.SessionId, request.CustomerName, resolvedBranchId);

                if (await ShouldAppendInitialBranchSelectorAsync(request.SessionId, resolvedBranchId, brainResponse.UiBlocks))
                {
                    brainResponse.UiBlocks ??= new List<object>();
                    brainResponse.UiBlocks.Add(await BuildBranchSelectorBlockAsync(cancellationToken));
                    await _adminChatService.MarkBranchPromptShownAsync(request.SessionId);
                }

                // Save AI reply to database
                string? uiBlocksJson = null;
                if (brainResponse.UiBlocks != null && brainResponse.UiBlocks.Any())
                {
                    uiBlocksJson = JsonSerializer.Serialize(new { uiBlocks = brainResponse.UiBlocks });
                }
                var aiMsg = await _adminChatService.AddAiReplyAsync(
                    request.SessionId,
                    brainResponse.Answer,
                    uiBlocksJson,
                    brainResponse.BookingAction);

                // Broadcast AI message to admin monitor in realtime
                await BroadcastToAdminGroupsAsync(session.BranchId, "newMessage", new
                    {
                        sessionId = request.SessionId,
                        role = "ai",
                        content = brainResponse.Answer,
                        formBlockJson = uiBlocksJson,
                        formBlockType = brainResponse.BookingAction,
                        createdAt = aiMsg.CreatedAt
                    }, cancellationToken);

                // Broadcast session update to admin monitor
                await BroadcastToAdminGroupsAsync(session.BranchId, "sessionUpdate", new
                    {
                        sessionId = request.SessionId,
                        status = session.Status,
                        lastMessage = brainResponse.Answer,
                        unreadCount,
                        totalUnreadCount,
                        lastActivityAt = DateTime.Now
                    }, cancellationToken);

                return Ok(new
                {
                    answer = brainResponse.Answer,
                    message = brainResponse.Answer,
                    sessionId = request.SessionId,
                    currentStep = brainResponse.BookingAction,
                    uiBlocks = brainResponse.UiBlocks,
                    state = brainResponse.BookingState ?? new AIBookingSessionState()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing AI chat for Session {SessionId}: {ErrorType} - {ErrorMessage}", request.SessionId, ex.GetType().Name, ex.Message);
                return StatusCode(503, new
                {
                    answer = "Xin lỗi, mình chưa kiểm tra được tình trạng phòng lúc này. Bạn thử lại sau ít phút nhé.",
                    message = "Xin lỗi, mình chưa kiểm tra được tình trạng phòng lúc này. Bạn thử lại sau ít phút nhé.",
                    sessionId = request.SessionId,
                    currentStep = "error",
                    uiBlocks = Array.Empty<object>(),
                    state = new AIBookingSessionState()
                });
            }
        }

        [HttpPost("booking-action")]
        public async Task<IActionResult> BookingAction([FromBody] JsonElement raw, CancellationToken cancellationToken)
        {
            var actionRequest = MapBookingActionRequest(raw);

            if (string.IsNullOrWhiteSpace(actionRequest.Action) || string.IsNullOrWhiteSpace(actionRequest.SessionId))
            {
                return BadRequest(new
                {
                    answer = "Thao tác không hợp lệ.",
                    sessionId = actionRequest.SessionId
                });
            }

            try
            {
                // Save user action to database
                string userActionDesc = await GetActionDescriptionAsync(actionRequest);
                if (!string.IsNullOrEmpty(userActionDesc))
                {
                    await _adminChatService.AddCustomerMessageAsync(actionRequest.SessionId, userActionDesc, null);

                    // Broadcast customer message to admin monitor
                    var actionBranchId = await ResolveBranchIdForActionAsync(actionRequest, cancellationToken);
                    await _adminChatService.AssignBranchAsync(actionRequest.SessionId, actionBranchId);

                    await BroadcastToAdminGroupsAsync(actionBranchId, "newMessage", new
                        {
                            sessionId = actionRequest.SessionId,
                            role = "user",
                            content = userActionDesc,
                            createdAt = DateTime.Now
                        }, cancellationToken);
                }

                // Handle the action
                var result = await _bookingConductor.HandleActionAsync(actionRequest, cancellationToken);
                var resolvedBranchId = result.State?.Confirmed?.BranchId
                    ?? actionRequest.BranchId
                    ?? await ResolveBranchIdForActionAsync(actionRequest, cancellationToken);
                await _adminChatService.AssignBranchAsync(actionRequest.SessionId, resolvedBranchId);

                // Save AI reply to database
                string? uiBlocksJson = null;
                if (result.UiBlocks != null && result.UiBlocks.Any())
                {
                    uiBlocksJson = JsonSerializer.Serialize(new { uiBlocks = result.UiBlocks });
                }
                var aiMsg = await _adminChatService.AddAiReplyAsync(
                    actionRequest.SessionId,
                    result.Answer,
                    uiBlocksJson,
                    result.Action.ToString());

                // Broadcast AI message to admin monitor in realtime
                await BroadcastToAdminGroupsAsync(resolvedBranchId, "newMessage", new
                    {
                        sessionId = actionRequest.SessionId,
                        role = "ai",
                        content = result.Answer,
                        formBlockJson = uiBlocksJson,
                        formBlockType = result.Action.ToString(),
                        createdAt = aiMsg.CreatedAt
                    }, cancellationToken);

                // Broadcast session update to admin monitor
                var unreadCount = (await _adminChatService.GetUnreadCustomerMessageCountsAsync(new[] { actionRequest.SessionId }, resolvedBranchId))
                    .GetValueOrDefault(actionRequest.SessionId);
                var totalUnreadCount = await _adminChatService.GetUnreadCustomerMessageCountAsync(resolvedBranchId);

                await BroadcastToAdminGroupsAsync(resolvedBranchId, "sessionUpdate", new
                    {
                        sessionId = actionRequest.SessionId,
                        status = "auto",
                        lastMessage = result.Answer,
                        unreadCount,
                        totalUnreadCount,
                        lastActivityAt = DateTime.Now
                    }, cancellationToken);

                return Ok(new
                {
                    answer = result.Answer,
                    message = result.Answer,
                    sessionId = actionRequest.SessionId,
                    currentStep = result.Action.ToString(),
                    uiBlocks = result.UiBlocks,
                    state = result.State
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing booking action {Action} for Session {SessionId}: {ErrorType} - {ErrorMessage}", actionRequest.Action, actionRequest.SessionId, ex.GetType().Name, ex.Message);
                return StatusCode(503, new
                {
                    answer = "Xin lỗi, thao tác đặt phòng đang tạm thời bận. Bạn thử lại sau nhé.",
                    sessionId = actionRequest.SessionId,
                    currentStep = "error"
                });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] string sessionId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return BadRequest(new { message = "Session ID is required." });
            }

            var messages = await _adminChatService.GetSessionMessagesAsync(sessionId);
            return Ok(messages.Select(m => new
            {
                role = m.Role,
                content = m.Content,
                formBlockJson = m.FormBlockJson,
                formBlockType = m.FormBlockType,
                createdAt = m.CreatedAt
            }));
        }

        private async Task<string> GetActionDescriptionAsync(BookingActionRequest req)
        {
            switch (req.Action)
            {
                case "select-booking-mode":
                    return req.BookingMode == "daily" ? "Chọn hình thức: Theo ngày" : "Chọn hình thức: Theo giờ";
                
                case "select-branch":
                    var branchName = req.BranchId.HasValue 
                        ? await _context.Branches.Where(b => b.Id == req.BranchId).Select(b => b.Name).FirstOrDefaultAsync()
                        : null;
                    return $"Chọn chi nhánh: {branchName ?? req.BranchId?.ToString() ?? "Chưa chọn"}";
                
                case "select-room":
                    var roomName = req.RoomId.HasValue
                        ? await _context.Rooms.Where(r => r.Id == req.RoomId).Select(r => r.Name).FirstOrDefaultAsync()
                        : null;
                    return $"Chọn phòng: {roomName ?? req.RoomId?.ToString() ?? "Chưa chọn"}";
                
                case "commit-room":
                    var cRoomName = req.RoomId.HasValue
                        ? await _context.Rooms.Where(r => r.Id == req.RoomId).Select(r => r.Name).FirstOrDefaultAsync()
                        : null;
                    return $"Xác nhận chọn phòng: {cRoomName ?? req.RoomId?.ToString() ?? "Chưa chọn"}";
                
                case "confirm-dates":
                    var modeText = req.BookingMode == "daily" ? "Theo ngày" : "Theo giờ";
                    var datesText = req.BookingMode == "daily"
                        ? $"Từ {req.CheckInDate} đến {req.CheckOutDate}"
                        : $"Ngày {req.HourlyDate}";
                    return $"Xác nhận thời gian ({modeText}): {datesText}";
                
                case "select-slot":
                    var slotLabel = req.SlotId.HasValue
                        ? await _context.RoomSlotInventories.Where(s => s.Id == req.SlotId).Select(s => s.SlotLabel).FirstOrDefaultAsync()
                        : null;
                    return $"Chọn khung giờ: {slotLabel ?? req.SlotId?.ToString() ?? "Chưa chọn"}";
                
                case "submit-booking-form":
                case "submit-form":
                    var custName = req.FormData?.GetValueOrDefault("customerName") ?? "";
                    var custPhone = req.FormData?.GetValueOrDefault("phoneNumber") ?? req.FormData?.GetValueOrDefault("customerPhone") ?? "";
                    return $"Gửi thông tin đặt phòng (Họ tên: {custName}, SĐT: {custPhone})";
                
                case "submit-contact-phone":
                    var phone = req.FormData?.GetValueOrDefault("phone") ?? req.FormData?.GetValueOrDefault("customerPhone") ?? "";
                    return $"Gửi thông tin liên hệ: {phone}";
                
                default:
                    return $"Thực hiện hành động: {req.Action}";
            }
        }

        [HttpGet("branches")]
        public async Task<IActionResult> Branches(CancellationToken cancellationToken)
        {
            var branches = await _context.Branches
                .OrderBy(b => b.Id)
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.BookingLeadTimeDays,
                    b.BookingLeadTimeHours
                })
                .ToListAsync(cancellationToken);

            var payload = new List<object>(branches.Count);
            foreach (var branch in branches)
            {
                var rule = await _branchLeadTimeService.ResolveAsync(branch.Id, cancellationToken);
                payload.Add(new
                {
                    id = branch.Id,
                    name = branch.Name,
                    bookingLeadTimeDays = branch.BookingLeadTimeDays,
                    bookingLeadTimeHours = branch.BookingLeadTimeHours,
                    earliestAllowedDailyDate = rule.EarliestAllowedDailyDate?.ToString("yyyy-MM-dd"),
                    earliestAllowedHourlyDate = rule.EarliestAllowedHourlyDate?.ToString("yyyy-MM-dd")
                });
            }

            return Ok(payload);
        }

        [HttpGet("debug-last-trace")]
        public async Task<IActionResult> DebugLastTrace(CancellationToken cancellationToken)
        {
            var trace = await _context.AIConversationTraces
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            return Ok(trace);
        }

        [HttpGet("debug-settings")]
        public async Task<IActionResult> DebugSettings(CancellationToken cancellationToken)
        {
            var settings = await _context.SystemSettings
                .Where(s => s.GroupName == "AI")
                .ToListAsync(cancellationToken);
            return Ok(settings);
        }

        private static string? ExtractPhoneLikeContact(string? message)
        {
            var digits = new string((message ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digits.Length is < 8 or > 15) return null;
            return digits;
        }

        private Task BroadcastToAdminGroupsAsync(int? branchId, string method, object payload, CancellationToken cancellationToken)
        {
            var groups = ChatMonitorScopeHelper.GetMonitorGroupsForSession(branchId);
            return _hubContext.Clients.Groups(groups).SendAsync(method, payload, cancellationToken);
        }

        private async Task<int?> ResolveBranchIdForActionAsync(BookingActionRequest actionRequest, CancellationToken cancellationToken)
        {
            if (actionRequest.BranchId.HasValue)
            {
                return actionRequest.BranchId.Value;
            }

            if (actionRequest.RoomId.HasValue)
            {
                return await _context.Rooms
                    .Where(room => room.Id == actionRequest.RoomId.Value)
                    .Select(room => (int?)room.BranchId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (actionRequest.SlotId.HasValue)
            {
                return await _context.RoomSlotInventories
                    .Where(slot => slot.Id == actionRequest.SlotId.Value)
                    .Select(slot => (int?)slot.Room.BranchId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return await _context.AdminChatSessions
                .Where(session => session.SessionId == actionRequest.SessionId)
                .Select(session => session.BranchId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<bool> ShouldAppendInitialBranchSelectorAsync(string sessionId, int? branchId, IEnumerable<object>? uiBlocks)
        {
            if (branchId.HasValue)
            {
                return false;
            }

            if (uiBlocks != null && uiBlocks.Any(block => JsonSerializer.Serialize(block).Contains("\"branchSelector\"", StringComparison.OrdinalIgnoreCase)))
            {
                await _adminChatService.MarkBranchPromptShownAsync(sessionId);
                return false;
            }

            if (await _adminChatService.HasBranchPromptBeenShownAsync(sessionId))
            {
                return false;
            }

            var userMessageCount = await _context.AdminChatMessages
                .CountAsync(message => message.SessionId == sessionId && message.Role == "user");

            return userMessageCount == 1;
        }

        private async Task<object> BuildBranchSelectorBlockAsync(CancellationToken cancellationToken)
        {
            var branches = await _context.Branches
                .OrderBy(branch => branch.Id)
                .Select(branch => new
                {
                    id = branch.Id,
                    name = branch.Name
                })
                .ToListAsync(cancellationToken);

            return new
            {
                type = "branchSelector",
                data = new
                {
                    label = "Bạn chọn chi nhánh giúp mình để mình lọc phòng chính xác hơn nhé",
                    branches
                }
            };
        }


    }
}
