using Microsoft.AspNetCore.Mvc;
using WebHomestay.Services;

namespace WebHomestay.Controllers.AI;

[Route("ai/cancellations")]
public class AICancellationsController : Controller
{
    private readonly IBookingCancellationService _service;

    public AICancellationsController(IBookingCancellationService service)
    {
        _service = service;
    }

    [HttpGet("policy")]
    public async Task<IActionResult> Policy(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetPolicyAsync(cancellationToken));
    }

    [HttpPost("submit")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> Submit(
        [FromForm] string sessionId,
        [FromForm] string? bookingCode,
        [FromForm] string customerName,
        [FromForm] string customerPhone,
        [FromForm] string customerEmail,
        [FromForm] IFormFile? confirmationEmailProof,
        [FromForm] IFormFile? refundQrImage,
        [FromForm] string? refundBankName,
        [FromForm] string? refundBankAccountNumber,
        [FromForm] string? refundBankAccountHolder,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(customerPhone) || string.IsNullOrWhiteSpace(customerEmail))
        {
            return BadRequest(new { message = "Bạn nhập đầy đủ tên, SĐT và email giúp mình nhé." });
        }

        if (confirmationEmailProof == null || confirmationEmailProof.Length == 0)
        {
            return BadRequest(new { message = "Bạn cần upload ảnh email xác nhận đặt phòng." });
        }

        try
        {
            var request = await _service.CreateAsync(new CreateCancellationRequestDto(
                sessionId,
                bookingCode,
                customerName,
                customerPhone,
                customerEmail,
                confirmationEmailProof,
                refundQrImage,
                refundBankName,
                refundBankAccountNumber,
                refundBankAccountHolder), cancellationToken);

            return Ok(new
            {
                success = true,
                requestId = request.Id,
                status = request.Status,
                message = "Yêu cầu hủy đã được gửi. Nhân viên sẽ kiểm tra và phản hồi qua email."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
