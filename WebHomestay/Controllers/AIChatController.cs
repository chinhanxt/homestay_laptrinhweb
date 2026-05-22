using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Hubs;
using WebHomestay.Services;

namespace WebHomestay.Controllers
{
    [Route("ai")]
    public class AIChatController : Controller
    {
        private readonly IAIBrainOrchestrator _orchestrator;
        private readonly IWebHostEnvironment _environment;
        private readonly IImageMaskingService _maskingService;
        private readonly ApplicationDbContext _context;
        private readonly IBookingConductor _bookingConductor;
        private readonly IAdminChatService _adminChatService;
        private readonly IHubContext<Hubs.ChatHub> _hubContext;

        public AIChatController(
            IAIBrainOrchestrator orchestrator,
            IWebHostEnvironment environment,
            IImageMaskingService maskingService,
            ApplicationDbContext context,
            IBookingConductor bookingConductor,
            IAdminChatService adminChatService,
            IHubContext<Hubs.ChatHub> hubContext)
        {
            _orchestrator = orchestrator;
            _environment = environment;
            _maskingService = maskingService;
            _context = context;
            _bookingConductor = bookingConductor;
            _adminChatService = adminChatService;
            _hubContext = hubContext;
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
        public async Task<IActionResult> Chat([FromBody] PublicAIChatRequest request, CancellationToken cancellationToken)
        {
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
                    request.SessionId, request.CustomerName);

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

                    await _hubContext.Clients.Group("admin_monitor")
                        .SendAsync("sessionUpdate", new
                        {
                            sessionId = request.SessionId,
                            status = "paused",
                            lastMessage = msg.Content,
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

                // Broadcast session update to admin monitor
                await _hubContext.Clients.Group("admin_monitor")
                    .SendAsync("sessionUpdate", new
                    {
                        sessionId = request.SessionId,
                        status = session.Status,
                        lastMessage = brainResponse.Answer,
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
            catch
            {
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
                var result = await _bookingConductor.HandleActionAsync(actionRequest, cancellationToken);

                return Ok(new
                {
                    answer = result.Answer,
                    sessionId = actionRequest.SessionId,
                    currentStep = result.Action.ToString(),
                    uiBlocks = result.UiBlocks,
                    state = result.State
                });
            }
            catch (Exception ex)
            {
                return StatusCode(503, new
                {
                    answer = "Xin lỗi, thao tác đặt phòng đang tạm thời bận. Bạn thử lại sau nhé.",
                    sessionId = actionRequest.SessionId,
                    currentStep = "error"
                });
            }
        }

        [HttpPost("booking-id-card")]
        public async Task<IActionResult> UploadBookingIdCard(int bookingId, IFormFile? idCardFront, IFormFile? idCardBack)
        {
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null) return NotFound(new { message = "Không tìm thấy đơn đặt phòng." });

            if (idCardFront != null && idCardFront.Length > 0)
            {
                booking.IdCardFrontPath = await SaveSecureFile(idCardFront);
                booking.IdCardFrontMaskedPath = await _maskingService.MaskIdCardAsync(booking.IdCardFrontPath!, true);
            }

            if (idCardBack != null && idCardBack.Length > 0)
            {
                booking.IdCardBackPath = await SaveSecureFile(idCardBack);
                booking.IdCardBackMaskedPath = await _maskingService.MaskIdCardAsync(booking.IdCardBackPath!, false);
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpPost("payment-proof")]
        public async Task<IActionResult> UploadPaymentProof(int bookingId, IFormFile? paymentProof)
        {
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null) return NotFound(new { message = "Không tìm thấy đơn đặt phòng." });
            if (paymentProof == null || paymentProof.Length == 0) return BadRequest(new { message = "Bạn chọn ảnh bill thanh toán trước nhé." });
            if (!paymentProof.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "Bill thanh toán phải là file ảnh." });

            var uploadDir = Path.Combine(_environment.WebRootPath, "uploads", "payments");
            Directory.CreateDirectory(uploadDir);
            var extension = Path.GetExtension(paymentProof.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10) extension = ".png";
            var fileName = $"bill_{bookingId}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
            var filePath = Path.Combine(uploadDir, fileName);
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await paymentProof.CopyToAsync(stream);
            }

            booking.PaymentProofUrl = "/uploads/payments/" + fileName;
            booking.Status = "AwaitingApproval";
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpGet("branches")]
        public async Task<IActionResult> Branches(CancellationToken cancellationToken)
        {
            var branches = await _context.Branches
                .OrderBy(b => b.Id)
                .Select(b => new { b.Id, b.Name })
                .ToListAsync(cancellationToken);
            return Ok(branches);
        }

        private async Task<string?> SaveSecureFile(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;
            string secureDir = Path.Combine(_environment.ContentRootPath, "App_Data", "SecureUploads", "IDCards");
            if (!Directory.Exists(secureDir)) Directory.CreateDirectory(secureDir);
            string fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            string filePath = Path.Combine(secureDir, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return fileName;
        }
    }
}
