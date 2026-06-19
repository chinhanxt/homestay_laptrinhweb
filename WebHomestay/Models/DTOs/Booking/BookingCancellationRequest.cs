using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebHomestay.Models.DTOs.Booking;

public class BookingCancellationRequest
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string ChatSessionId { get; set; } = string.Empty;

    public int? BookingId { get; set; }
    [ForeignKey(nameof(BookingId))]
    public WebHomestay.Models.Entities.Core.Booking? Booking { get; set; }

    [StringLength(50)]
    public string? SubmittedBookingCode { get; set; }

    [Required, StringLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required]
    public string ConfirmationEmailProofPath { get; set; } = string.Empty;

    public string? RefundQrImagePath { get; set; }

    [StringLength(100)]
    public string? RefundBankName { get; set; }

    [StringLength(50)]
    public string? RefundBankAccountNumber { get; set; }

    [StringLength(200)]
    public string? RefundBankAccountHolder { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Pending";

    public string? SuggestedBookingIdsJson { get; set; }

    public int PolicyNoticeHoursSnapshot { get; set; }
    public int RefundPercentBeforeNoticeSnapshot { get; set; }
    public int RefundPercentAfterNoticeSnapshot { get; set; }
    public string PolicyMessageSnapshot { get; set; } = string.Empty;

    public bool IsManual { get; set; }

    public int? AppliedRefundPercent { get; set; }

    [Required, StringLength(20)]
    public string RefundStatus { get; set; } = "NotRefunded";

    public string? RefundBillProofPath { get; set; }
    public string? StaffReason { get; set; }

    [StringLength(100)]
    public string? ProcessedBy { get; set; }

    public DateTime? ProcessedAt { get; set; }

    [StringLength(200)]
    public string? NotificationEmailSubject { get; set; }

    public string? NotificationEmailBody { get; set; }

    public DateTime? NotificationEmailSentAt { get; set; }

    [StringLength(255)]
    public string? NotificationEmailAttachmentName { get; set; }

    [StringLength(20)]
    public string? NotificationEmailType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
