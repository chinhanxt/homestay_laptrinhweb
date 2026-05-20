using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Services;

namespace WebHomestay.Controllers
{
    [Route("ai")]
    public class AIChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAIBookingFlowOrchestrator _bookingFlowOrchestrator;

        public AIChatController(ApplicationDbContext context, IAIBookingFlowOrchestrator bookingFlowOrchestrator)
        {
            _context = context;
            _bookingFlowOrchestrator = bookingFlowOrchestrator;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] PublicAIChatRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new AIBookingFlowResponse
                {
                    SessionId = request?.SessionId ?? string.Empty,
                    CurrentStep = "intent",
                    Message = "Bạn nhập giúp mình nhu cầu đặt phòng để mình tư vấn nhé.",
                    State = new AIBookingSessionState(),
                    UiBlocks = new List<AIUiBlock>()
                });
            }

            try
            {
                var response = await _bookingFlowOrchestrator.HandleChatAsync(request, cancellationToken);
                return Ok(new
                {
                    answer = response.Message,
                    message = response.Message,
                    sessionId = response.SessionId,
                    currentStep = response.CurrentStep,
                    state = response.State,
                    uiBlocks = response.UiBlocks,
                    formSchema = "[]"
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

    }}
