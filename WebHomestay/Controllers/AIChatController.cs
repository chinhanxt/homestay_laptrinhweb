using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Services;

namespace WebHomestay.Controllers
{
    [Route("ai")]
    public class AIChatController : Controller
    {
        private readonly IAIBrainOrchestrator _aiBrainOrchestrator;
        private readonly ApplicationDbContext _context;
        private readonly IAIBookingFlowOrchestrator _bookingFlowOrchestrator;

        public AIChatController(IAIBrainOrchestrator aiBrainOrchestrator, ApplicationDbContext context, IAIBookingFlowOrchestrator bookingFlowOrchestrator)
        {
            _aiBrainOrchestrator = aiBrainOrchestrator;
            _context = context;
            _bookingFlowOrchestrator = bookingFlowOrchestrator;
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
                    currentStep = "intent",
                    state = new AIBookingSessionState(),
                    uiBlocks = Array.Empty<AIUiBlock>(),
                    formSchema = "[]"
                });
            }

            var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId.Trim();

            try
            {
                var message = request.Message.Trim();
                var guestCount = ResolveGuestCount(message, request.GuestCount);
                var branchId = request.BranchId ?? await ResolveBranchIdAsync(message, cancellationToken);
                var hasDate = TryExtractDate(message, out var parsedDate);
                var response = await _aiBrainOrchestrator.ChatAsync(new AIBrainChatRequest
                {
                    SessionId = sessionId,
                    Message = message,
                    BranchId = branchId,
                    StartTime = hasDate ? parsedDate.ToDateTime(TimeOnly.MinValue) : request.StartTime,
                    EndTime = hasDate ? parsedDate.ToDateTime(TimeOnly.MaxValue) : request.EndTime,
                    GuestCount = guestCount
                }, cancellationToken);

                var bookingMode = string.Equals(request.BookingMode, "daily", StringComparison.OrdinalIgnoreCase) ? "daily" : "hourly";
                var state = new AIBookingSessionState
                {
                    CustomerName = request.CustomerName?.Trim() ?? string.Empty,
                    BranchId = branchId,
                    BookingMode = bookingMode,
                    HourlyDate = bookingMode == "hourly" && hasDate ? parsedDate : null,
                    CheckInDate = bookingMode == "daily" && hasDate ? parsedDate : null,
                    CheckOutDate = bookingMode == "daily" && hasDate ? parsedDate.AddDays(1) : null,
                    GuestCount = guestCount
                };

                var flowResponse = branchId.HasValue && hasDate
                    ? await _bookingFlowOrchestrator.BuildRoomCardsAsync(state)
                    : new AIBookingFlowResponse { CurrentStep = "chat", Message = response.Answer, State = state };

                return Ok(new
                {
                    answer = response.Answer,
                    message = response.Answer,
                    sessionId,
                    currentStep = flowResponse.CurrentStep,
                    state = flowResponse.State,
                    uiBlocks = flowResponse.UiBlocks,
                    formSchema = response.FormSchema
                });
            }
            catch
            {
                return StatusCode(503, new
                {
                    answer = "Xin lỗi, trợ lý AI đang tạm thời bận. Bạn thử lại sau ít phút nhé.",
                    message = "Xin lỗi, trợ lý AI đang tạm thời bận. Bạn thử lại sau ít phút nhé.",
                    sessionId,
                    currentStep = "error",
                    state = new AIBookingSessionState(),
                    uiBlocks = Array.Empty<AIUiBlock>(),
                    formSchema = "[]"
                });
            }
        }

        [HttpPost("booking-action")]
        public async Task<IActionResult> BookingAction([FromBody] AIBookingActionRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Action))
            {
                return BadRequest(new AIBookingFlowResponse
                {
                    CurrentStep = "error",
                    Message = "Thao tác không hợp lệ.",
                    State = new AIBookingSessionState()
                });
            }

            try
            {
                var response = await _bookingFlowOrchestrator.HandleActionAsync(request);
                response.SessionId = request.SessionId;
                return Ok(response);
            }
            catch
            {
                return StatusCode(503, new AIBookingFlowResponse
                {
                    SessionId = request.SessionId,
                    CurrentStep = "error",
                    Message = "Xin lỗi, thao tác đặt phòng đang tạm thời bận. Bạn thử lại sau ít phút nhé.",
                    State = request.State,
                    UiBlocks = new List<AIUiBlock>()
                });
            }
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

        private async Task<int?> ResolveBranchIdAsync(string text, CancellationToken cancellationToken)
        {
            var lowered = text.ToLowerInvariant();
            var branches = await _context.Branches.ToListAsync(cancellationToken);
            return branches.FirstOrDefault(b => lowered.Contains(b.Name.ToLowerInvariant()))?.Id
                ?? branches.FirstOrDefault(b => b.Name.Contains("Sài Gòn") && (lowered.Contains("sài gòn") || lowered.Contains("sai gon") || lowered.Contains("saigon") || lowered.Contains("sg") || lowered.Contains("hcm") || lowered.Contains("tphcm")))?.Id
                ?? branches.FirstOrDefault(b => b.Name.Contains("Đà Lạt") && (lowered.Contains("đà lạt") || lowered.Contains("da lat") || lowered.Contains("dalat") || lowered.Contains("dl")))?.Id;
        }

        private int ResolveGuestCount(string text, int requestGuestCount)
        {
            var match = Regex.Match(text, @"(\d+)\s*(ng|người|nguoi|khách|khach)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var parsedGuests) && parsedGuests > 0) return parsedGuests;
            return requestGuestCount <= 0 ? 1 : requestGuestCount;
        }

        private bool TryExtractDate(string text, out DateOnly date)
        {
            var lowered = text.ToLowerInvariant();
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
    }}
