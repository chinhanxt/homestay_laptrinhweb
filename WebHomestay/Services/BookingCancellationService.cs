using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Services;

public class BookingCancellationService : IBookingCancellationService
{
    public const long MaxImageBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp"
    };

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
        var chatSessionId = RequireTrimmed(dto.ChatSessionId, 100, "Vui lòng cung cấp phiên chat yêu cầu hủy.", "Phiên chat không được vượt quá 100 ký tự.");
        var customerName = RequireTrimmed(dto.CustomerName, 200, "Vui lòng nhập họ tên khách hàng.", "Họ tên khách hàng không được vượt quá 200 ký tự.");
        var customerPhone = RequireTrimmed(dto.CustomerPhone, 20, "Vui lòng nhập số điện thoại.", "Số điện thoại không được vượt quá 20 ký tự.");
        var customerEmail = RequireTrimmed(dto.CustomerEmail, 200, "Vui lòng nhập email.", "Email không được vượt quá 200 ký tự.");
        var submittedBookingCode = OptionalTrimmed(dto.SubmittedBookingCode, 50, "Mã booking không được vượt quá 50 ký tự.");
        var refundBankName = OptionalTrimmed(dto.RefundBankName, 100, "Tên ngân hàng không được vượt quá 100 ký tự.");
        var refundBankAccountNumber = OptionalTrimmed(dto.RefundBankAccountNumber, 50, "Số tài khoản hoàn tiền không được vượt quá 50 ký tự.");
        var refundBankAccountHolder = OptionalTrimmed(dto.RefundBankAccountHolder, 200, "Tên chủ tài khoản không được vượt quá 200 ký tự.");
        var normalizedDto = dto with
        {
            ChatSessionId = chatSessionId,
            SubmittedBookingCode = submittedBookingCode,
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            CustomerEmail = customerEmail,
            RefundBankName = refundBankName,
            RefundBankAccountNumber = refundBankAccountNumber,
            RefundBankAccountHolder = refundBankAccountHolder
        };

        var confirmationPath = await SaveProtectedImageAsync(dto.ConfirmationEmailProof, "confirmation", cancellationToken);
        var refundQrPath = dto.RefundQrImage is null ? null : await SaveProtectedImageAsync(dto.RefundQrImage, "refundQr", cancellationToken);
        var policy = await GetPolicyAsync(cancellationToken);
        var parsedBookingId = ParseBookingId(submittedBookingCode);
        var suggestedIds = await GetSuggestedBookingIdsAsync(normalizedDto, parsedBookingId, cancellationToken);
        var linkedBookingId = await GetAutoLinkedBookingIdAsync(parsedBookingId, customerEmail, customerPhone, cancellationToken);

        var request = new BookingCancellationRequest
        {
            ChatSessionId = chatSessionId,
            BookingId = linkedBookingId,
            SubmittedBookingCode = submittedBookingCode,
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            CustomerEmail = customerEmail,
            ConfirmationEmailProofPath = confirmationPath,
            RefundQrImagePath = refundQrPath,
            RefundBankName = refundBankName,
            RefundBankAccountNumber = refundBankAccountNumber,
            RefundBankAccountHolder = refundBankAccountHolder,
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

        var staffReason = RequireTrimmed(dto.StaffReason, 2000, "Vui lòng nhập lý do xử lý.", "Lý do xử lý không được vượt quá 2000 ký tự.");
        var processedBy = OptionalTrimmed(dto.ProcessedBy, 100, "Người xử lý không được vượt quá 100 ký tự.");

        request.RefundBillProofPath = await SaveProtectedImageAsync(dto.RefundBillProof, "refundBill", cancellationToken);
        request.Status = "Approved";
        request.RefundStatus = "Refunded";
        request.AppliedRefundPercent = Math.Clamp(dto.AppliedRefundPercent, 0, 100);
        request.StaffReason = staffReason;
        request.ProcessedBy = processedBy;
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

        var staffReason = RequireTrimmed(dto.StaffReason, 2000, "Vui lòng nhập lý do xử lý.", "Lý do xử lý không được vượt quá 2000 ký tự.");
        var processedBy = OptionalTrimmed(dto.ProcessedBy, 100, "Người xử lý không được vượt quá 100 ký tự.");

        var request = await LoadPendingRequestAsync(dto.RequestId, cancellationToken);
        request.Status = "Rejected";
        request.StaffReason = staffReason;
        request.ProcessedBy = processedBy;
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
        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedImageContentTypes.ContainsKey(file.ContentType))
            throw new InvalidOperationException("Chỉ chấp nhận ảnh PNG, JPEG, GIF hoặc WebP hợp lệ.");

        await using var inputStream = file.OpenReadStream();
        var detectedExtension = await DetectImageExtensionAsync(inputStream, cancellationToken);
        if (detectedExtension is null)
            throw new InvalidOperationException("Tệp tải lên không phải ảnh PNG, JPEG, GIF hoặc WebP hợp lệ.");
        if (!string.Equals(detectedExtension, AllowedImageContentTypes[file.ContentType], StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Định dạng ảnh tải lên không khớp với loại tệp đã khai báo.");

        inputStream.Position = 0;
        var directory = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", "Cancellations", folder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}{detectedExtension}");

        await using var outputStream = new FileStream(path, FileMode.CreateNew);
        await inputStream.CopyToAsync(outputStream, cancellationToken);
        return path;
    }

    private static async Task<string?> DetectImageExtensionAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[12];
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

        if (bytesRead >= 8 && buffer.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A })) return ".png";
        if (bytesRead >= 3 && buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF) return ".jpg";
        if (bytesRead >= 6 && (buffer.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || buffer.AsSpan(0, 6).SequenceEqual("GIF89a"u8))) return ".gif";
        if (bytesRead >= 12 && buffer.AsSpan(0, 4).SequenceEqual("RIFF"u8) && buffer.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return ".webp";

        return null;
    }

    private static string RequireTrimmed(string? value, int maxLength, string requiredMessage, string maxLengthMessage)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException(requiredMessage);
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength) throw new InvalidOperationException(maxLengthMessage);
        return trimmed;
    }

    private static string? OptionalTrimmed(string? value, int maxLength, string maxLengthMessage)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength) throw new InvalidOperationException(maxLengthMessage);
        return trimmed;
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
