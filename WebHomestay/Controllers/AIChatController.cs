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
        private readonly IWebHostEnvironment _environment;
        private readonly IImageMaskingService _maskingService;

        public AIChatController(
            ApplicationDbContext context,
            IAIBookingFlowOrchestrator bookingFlowOrchestrator,
            IWebHostEnvironment environment,
            IImageMaskingService maskingService)
        {
            _context = context;
            _bookingFlowOrchestrator = bookingFlowOrchestrator;
            _environment = environment;
            _maskingService = maskingService;
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
            catch (InvalidOperationException ex)
            {
                return BadRequest(new AIBookingFlowResponse
                {
                    SessionId = request.SessionId,
                    CurrentStep = "error",
                    Message = ex.Message,
                    State = request.State,
                    UiBlocks = new List<AIUiBlock>()
                });
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

    }}
