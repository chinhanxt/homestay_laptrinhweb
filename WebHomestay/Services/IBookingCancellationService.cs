using WebHomestay.Models;

namespace WebHomestay.Services;

public record CancellationPolicyDto(int NoticeHours, int RefundPercentBeforeNotice, int RefundPercentAfterNotice, string PolicyMessage, string HandlingMode);

public record CreateCancellationRequestDto(
    string ChatSessionId,
    string? SubmittedBookingCode,
    string CustomerName,
    string CustomerPhone,
    string CustomerEmail,
    IFormFile ConfirmationEmailProof,
    IFormFile? RefundQrImage,
    string? RefundBankName,
    string? RefundBankAccountNumber,
    string? RefundBankAccountHolder);

public record ProcessCancellationDto(
    int RequestId,
    string StaffReason,
    int AppliedRefundPercent,
    string ProcessedBy,
    IFormFile? RefundBillProof,
    string? NotificationEmailSubject = null,
    string? NotificationEmailBody = null);

public record CancellationEmailPreviewDto(
    string RecipientEmail,
    string Subject,
    string Body,
    string EditableReason,
    string? AttachmentName,
    bool RequiresAttachment);

public record CreateManualCancellationDto(
    string BookingCode,
    IFormFile ManualProofImage,
    string? ProcessedBy);

public interface IBookingCancellationService
{
    Task<CancellationPolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default);
    Task<BookingCancellationRequest> CreateAsync(CreateCancellationRequestDto dto, CancellationToken cancellationToken = default);
    Task<CancellationEmailPreviewDto> BuildApprovalPreviewAsync(ProcessCancellationDto dto, CancellationToken cancellationToken = default);
    Task<CancellationEmailPreviewDto> BuildRejectionPreviewAsync(ProcessCancellationDto dto, CancellationToken cancellationToken = default);
    Task<BookingCancellationRequest> ApproveAsync(ProcessCancellationDto dto, CancellationToken cancellationToken = default);
    Task<BookingCancellationRequest> RejectAsync(ProcessCancellationDto dto, CancellationToken cancellationToken = default);
    Task<string> GetProtectedFilePathAsync(int requestId, string kind, CancellationToken cancellationToken = default);
    Task<BookingCancellationRequest> CreateManualAsync(CreateManualCancellationDto dto, CancellationToken cancellationToken = default);
}
