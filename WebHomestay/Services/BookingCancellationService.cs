using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Services;

public class BookingCancellationService : IBookingCancellationService
{
    public const long MaxImageBytes = 5 * 1024 * 1024;

    private readonly ApplicationDbContext _context;
    private readonly ISettingService _settingService;
    private readonly IMailService _mailService;
    private readonly IWebHostEnvironment _env;

    public BookingCancellationService(
        ApplicationDbContext context,
        ISettingService settingService,
        IMailService mailService,
        IWebHostEnvironment env)
    {
        _context = context;
        _settingService = settingService;
        _mailService = mailService;
        _env = env;
    }

    public async Task<CancellationPolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        return new CancellationPolicyDto(
            await _settingService.GetIntAsync("CancellationNoticeHours", 24),
            await _settingService.GetIntAsync("CancellationRefundPercentBeforeNotice", 50),
            await _settingService.GetIntAsync("CancellationRefundPercentAfterNotice", 0),
            await _settingService.GetStringAsync("CancellationPolicyMessage", "Yêu cầu hủy sẽ được nhân viên kiểm tra và phản hồi qua email."),
            await _settingService.GetStringAsync("CancellationHandlingMode", "Manual"));
    }

    public async Task<BookingCancellationRequest> CreateAsync(CreateCancellationRequestDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.ChatSessionId)) throw new InvalidOperationException("Vui lòng cung cấp phiên chat yêu cầu hủy.");
        if (string.IsNullOrWhiteSpace(dto.CustomerName)) throw new InvalidOperationException("Vui lòng nhập họ tên khách hàng.");
        if (string.IsNullOrWhiteSpace(dto.CustomerPhone)) throw new InvalidOperationException("Vui lòng nhập số điện thoại.");
        if (string.IsNullOrWhiteSpace(dto.CustomerEmail)) throw new InvalidOperationException("Vui lòng nhập email.");

        var confirmationPath = await SaveProtectedImageAsync(dto.ConfirmationEmailProof, "confirmation", cancellationToken);
        var refundQrPath = dto.RefundQrImage is null ? null : await SaveProtectedImageAsync(dto.RefundQrImage, "refundQr", cancellationToken);
        var policy = await GetPolicyAsync(cancellationToken);
        var parsedBookingId = ParseBookingId(dto.SubmittedBookingCode);
        var suggestedIds = await GetSuggestedBookingIdsAsync(dto, parsedBookingId, cancellationToken);
        var linkedBookingId = await GetAutoLinkedBookingIdAsync(parsedBookingId, dto.CustomerEmail, dto.CustomerPhone, cancellationToken);

        var request = new BookingCancellationRequest
        {
            ChatSessionId = dto.ChatSessionId.Trim(),
            BookingId = linkedBookingId,
            SubmittedBookingCode = string.IsNullOrWhiteSpace(dto.SubmittedBookingCode) ? null : dto.SubmittedBookingCode.Trim(),
            CustomerName = dto.CustomerName.Trim(),
            CustomerPhone = dto.CustomerPhone.Trim(),
            CustomerEmail = dto.CustomerEmail.Trim(),
            ConfirmationEmailProofPath = confirmationPath,
            RefundQrImagePath = refundQrPath,
            RefundBankName = string.IsNullOrWhiteSpace(dto.RefundBankName) ? null : dto.RefundBankName.Trim(),
            RefundBankAccountNumber = string.IsNullOrWhiteSpace(dto.RefundBankAccountNumber) ? null : dto.RefundBankAccountNumber.Trim(),
            RefundBankAccountHolder = string.IsNullOrWhiteSpace(dto.RefundBankAccountHolder) ? null : dto.RefundBankAccountHolder.Trim(),
            SuggestedBookingIdsJson = JsonSerializer.Serialize(suggestedIds),
            PolicyNoticeHoursSnapshot = policy.NoticeHours,
            RefundPercentBeforeNoticeSnapshot = policy.RefundPercentBeforeNotice,
            RefundPercentAfterNoticeSnapshot = policy.RefundPercentAfterNotice,
            PolicyMessageSnapshot = policy.PolicyMessage,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.BookingCancellationRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<BookingCancellationRequest> ApproveAsync(ProcessCancellationDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.StaffReason)) throw new InvalidOperationException("Vui lòng nhập lý do xử lý.");
        if (dto.RefundBillProof is null || dto.RefundBillProof.Length == 0) throw new InvalidOperationException("Vui lòng tải lên ảnh chứng từ hoàn tiền.");

        var request = await LoadPendingRequestAsync(dto.RequestId, cancellationToken);
        if (!request.BookingId.HasValue) throw new InvalidOperationException("Yêu cầu hủy chưa được liên kết với booking.");

        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == request.BookingId.Value, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy booking được liên kết.");

        request.RefundBillProofPath = await SaveProtectedImageAsync(dto.RefundBillProof, "refundBill", cancellationToken);
        request.Status = "Approved";
        request.RefundStatus = "Refunded";
        request.AppliedRefundPercent = Math.Clamp(dto.AppliedRefundPercent, 0, 100);
        request.StaffReason = dto.StaffReason.Trim();
        request.ProcessedBy = string.IsNullOrWhiteSpace(dto.ProcessedBy) ? null : dto.ProcessedBy.Trim();
        request.ProcessedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        var handlingMode = await _settingService.GetStringAsync("CancellationHandlingMode", "Manual");
        if (string.Equals(handlingMode, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            booking.Status = "Cancelled";
            if (booking.BookingMode == BookingMode.Hourly && booking.RoomSlotInventoryId.HasValue)
            {
                var slot = await _context.RoomSlotInventories.FirstOrDefaultAsync(s => s.Id == booking.RoomSlotInventoryId.Value, cancellationToken);
                if (slot is not null) slot.Status = "Available";
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _mailService.SendEmailAsync(request.CustomerEmail, "Yêu cầu hủy đặt phòng đã được duyệt", BuildApprovalEmail(request));
        return request;
    }

    public async Task<BookingCancellationRequest> RejectAsync(ProcessCancellationDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.StaffReason)) throw new InvalidOperationException("Vui lòng nhập lý do xử lý.");

        var request = await LoadPendingRequestAsync(dto.RequestId, cancellationToken);
        request.Status = "Rejected";
        request.StaffReason = dto.StaffReason.Trim();
        request.ProcessedBy = string.IsNullOrWhiteSpace(dto.ProcessedBy) ? null : dto.ProcessedBy.Trim();
        request.ProcessedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await _mailService.SendEmailAsync(request.CustomerEmail, "Yêu cầu hủy đặt phòng bị từ chối", BuildRejectionEmail(request));
        return request;
    }

    public async Task<string> GetProtectedFilePathAsync(int requestId, string kind, CancellationToken cancellationToken = default)
    {
        var request = await _context.BookingCancellationRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy yêu cầu hủy.");

        var path = kind switch
        {
            "confirmation" => request.ConfirmationEmailProofPath,
            "refundQr" => request.RefundQrImagePath,
            "refundBill" => request.RefundBillProofPath,
            _ => throw new InvalidOperationException("Loại tệp không hợp lệ.")
        };

        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Không tìm thấy tệp yêu cầu.");
        return path;
    }

    private async Task<BookingCancellationRequest> LoadPendingRequestAsync(int requestId, CancellationToken cancellationToken)
    {
        var request = await _context.BookingCancellationRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy yêu cầu hủy.");
        if (!string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Yêu cầu hủy đã được xử lý.");
        return request;
    }

    private async Task<string> SaveProtectedImageAsync(IFormFile file, string folder, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) throw new InvalidOperationException("Vui lòng tải lên ảnh hợp lệ.");
        if (file.Length > MaxImageBytes) throw new InvalidOperationException("Ảnh tải lên không được vượt quá 5MB.");
        if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ chấp nhận tệp hình ảnh.");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10) extension = ".bin";
        extension = string.Concat(extension.Where(c => char.IsLetterOrDigit(c) || c == '.')).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension)) extension = ".bin";

        var directory = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", "Cancellations", folder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}{extension}");

        await using var stream = new FileStream(path, FileMode.CreateNew);
        await file.CopyToAsync(stream, cancellationToken);
        return path;
    }

    private async Task<List<int>> GetSuggestedBookingIdsAsync(CreateCancellationRequestDto dto, int? parsedBookingId, CancellationToken cancellationToken)
    {
        var email = dto.CustomerEmail.Trim();
        var phone = dto.CustomerPhone.Trim();
        var name = dto.CustomerName.Trim();
        var query = _context.Bookings.AsQueryable();

        query = query.Where(b =>
            (parsedBookingId.HasValue && b.Id == parsedBookingId.Value) ||
            (b.CustomerEmail != null && b.CustomerEmail.ToLower() == email.ToLower()) ||
            b.CustomerPhone == phone ||
            b.CustomerName.ToLower().Contains(name.ToLower()));

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => b.Id)
            .Distinct()
            .Take(10)
            .ToListAsync(cancellationToken);
    }

    private async Task<int?> GetAutoLinkedBookingIdAsync(int? parsedBookingId, string email, string phone, CancellationToken cancellationToken)
    {
        if (!parsedBookingId.HasValue) return null;

        var normalizedEmail = email.Trim();
        var normalizedPhone = phone.Trim();
        var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == parsedBookingId.Value, cancellationToken);
        if (booking is null) return null;

        var emailMatches = booking.CustomerEmail != null && string.Equals(booking.CustomerEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase);
        var phoneMatches = booking.CustomerPhone == normalizedPhone;
        return emailMatches || phoneMatches ? booking.Id : null;
    }

    private static int? ParseBookingId(string? submittedBookingCode)
    {
        if (string.IsNullOrWhiteSpace(submittedBookingCode)) return null;
        var digits = new string(submittedBookingCode.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var id) ? id : null;
    }

    private static string BuildApprovalEmail(BookingCancellationRequest request)
    {
        var bookingLine = request.BookingId.HasValue ? $"Mã booking: {request.BookingId.Value}\n" : string.Empty;
        return $"Yêu cầu hủy của quý khách đã được duyệt.\n{bookingLine}Lý do/ghi chú: {request.StaffReason}\nTỷ lệ hoàn tiền: {request.AppliedRefundPercent}%\nChính sách: {request.PolicyMessageSnapshot}";
    }

    private static string BuildRejectionEmail(BookingCancellationRequest request)
    {
        var bookingLine = request.BookingId.HasValue ? $"Mã booking: {request.BookingId.Value}\n" : string.Empty;
        return $"Yêu cầu hủy của quý khách chưa được duyệt.\n{bookingLine}Lý do/ghi chú: {request.StaffReason}\nChính sách: {request.PolicyMessageSnapshot}";
    }
}
