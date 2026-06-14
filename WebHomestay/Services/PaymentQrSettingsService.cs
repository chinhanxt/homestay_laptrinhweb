using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services
{
    public interface IPaymentQrSettingsService
    {
        Task<PaymentQrSettingsViewModel> GetSettingsAsync(int branchId, string branchName);
        Task<Dictionary<int, PaymentQrSettingsViewModel>> GetSettingsForBranchesAsync(IEnumerable<Branch> branches);
        Task<(bool Success, string Message)> SaveSettingsAsync(PaymentQrSettingsViewModel model, IFormFile? qrImage, string webRootPath);
        Task<PaymentQrDisplayViewModel> BuildDisplayAsync(Booking booking);
    }

    public class PaymentQrSettingsService : IPaymentQrSettingsService
    {
        private readonly ApplicationDbContext _context;
        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        public PaymentQrSettingsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaymentQrSettingsViewModel> GetSettingsAsync(int branchId, string branchName)
        {
            var prefix = $"PaymentQr:{branchId}:";
            var settings = await _context.SystemSettings
                .Where(s => s.SettingKey.StartsWith(prefix))
                .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue ?? string.Empty);

            string Get(string name, string defaultValue = "")
            {
                return settings.TryGetValue(Key(branchId, name), out var value) && !string.IsNullOrWhiteSpace(value)
                    ? value
                    : defaultValue;
            }

            var countdownValue = Get("CountdownMinutes", "5");
            var countdown = int.TryParse(countdownValue, out var parsedCountdown) && parsedCountdown > 0 ? parsedCountdown : 5;

            return new PaymentQrSettingsViewModel
            {
                BranchId = branchId,
                BranchName = branchName,
                QrImagePath = Get("QrImagePath"),
                QrImageUrl = Get("QrImageUrl"),
                BankCode = Get("BankCode"),
                BankAccountNumber = Get("BankAccountNumber"),
                BankAccountName = Get("BankAccountName"),
                TransferContentTemplate = Get("TransferContentTemplate", "chinhan {BookingId}"),
                CountdownMinutes = countdown,
                BeforeBillMessage = Get("BeforeBillMessage", "Vui lòng tải lên ảnh chụp màn hình bill thanh toán thành công để chúng tôi xác nhận nhanh nhất."),
                AfterBillMessage = Get("AfterBillMessage", "Chúng tôi đã nhận được Bill của bạn. Nhân viên sẽ đối soát và gửi mã phòng qua Email sớm nhất."),
                ExpiredMessage = Get("ExpiredMessage", "Rất tiếc, thời gian giữ chỗ của bạn đã kết thúc. Vui lòng quay lại trang chủ để chọn lại.")
            };
        }

        public async Task<Dictionary<int, PaymentQrSettingsViewModel>> GetSettingsForBranchesAsync(IEnumerable<Branch> branches)
        {
            var result = new Dictionary<int, PaymentQrSettingsViewModel>();
            foreach (var branch in branches)
            {
                result[branch.Id] = await GetSettingsAsync(branch.Id, branch.Name);
            }
            return result;
        }

        public async Task<(bool Success, string Message)> SaveSettingsAsync(PaymentQrSettingsViewModel model, IFormFile? qrImage, string webRootPath)
        {
            var branchExists = await _context.Branches.AnyAsync(b => b.Id == model.BranchId);
            if (!branchExists)
            {
                return (false, "Không tìm thấy chi nhánh cần cấu hình.");
            }

            if (model.CountdownMinutes < 1)
            {
                return (false, "Thời gian đếm ngược phải từ 1 phút trở lên.");
            }

            var current = await GetSettingsAsync(model.BranchId, model.BranchName);
            var qrImagePath = current.QrImagePath;

            if (qrImage != null && qrImage.Length > 0)
            {
                var extension = Path.GetExtension(qrImage.FileName);
                if (!AllowedImageExtensions.Contains(extension))
                {
                    return (false, "Ảnh QR chỉ hỗ trợ định dạng JPG, PNG hoặc WEBP.");
                }

                var uploadDir = Path.Combine(webRootPath, "uploads", "payment-qr");
                Directory.CreateDirectory(uploadDir);
                var fileName = $"branch-{model.BranchId}-qr{extension.ToLowerInvariant()}";
                var filePath = Path.Combine(uploadDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await qrImage.CopyToAsync(stream);
                }

                qrImagePath = $"/uploads/payment-qr/{fileName}";
            }

            await Update(model.BranchId, "QrImagePath", qrImagePath);
            await Update(model.BranchId, "QrImageUrl", model.QrImageUrl?.Trim() ?? string.Empty);
            await Update(model.BranchId, "BankCode", model.BankCode?.Trim() ?? string.Empty);
            await Update(model.BranchId, "BankAccountNumber", model.BankAccountNumber?.Trim() ?? string.Empty);
            await Update(model.BranchId, "BankAccountName", model.BankAccountName?.Trim() ?? string.Empty);
            await Update(model.BranchId, "TransferContentTemplate", string.IsNullOrWhiteSpace(model.TransferContentTemplate) ? "chinhan {BookingId}" : model.TransferContentTemplate.Trim());
            await Update(model.BranchId, "CountdownMinutes", model.CountdownMinutes.ToString());
            await Update(model.BranchId, "BeforeBillMessage", model.BeforeBillMessage?.Trim() ?? string.Empty);
            await Update(model.BranchId, "AfterBillMessage", model.AfterBillMessage?.Trim() ?? string.Empty);
            await Update(model.BranchId, "ExpiredMessage", model.ExpiredMessage?.Trim() ?? string.Empty);

            return (true, "Cập nhật cấu hình mã QR thành công.");
        }

        public async Task<PaymentQrDisplayViewModel> BuildDisplayAsync(Booking booking)
        {
            var branch = booking.Room?.Branch;
            if (branch == null)
            {
                return BuildMissingDisplay();
            }

            var settings = await GetSettingsAsync(branch.Id, branch.Name);
            var transferContent = BuildTransferContent(settings.TransferContentTemplate, booking, branch.Name);
            var generatedQr = BuildGeneratedQrUrl(settings, booking.TotalPrice, transferContent);
            var qrSrc = FirstNonEmpty(settings.QrImagePath, settings.QrImageUrl, generatedQr);

            return new PaymentQrDisplayViewModel
            {
                QrImageSrc = qrSrc,
                HasQrImage = !string.IsNullOrWhiteSpace(qrSrc),
                MissingQrMessage = string.IsNullOrWhiteSpace(qrSrc) ? "Chi nhánh này chưa cấu hình mã QR thanh toán." : string.Empty,
                BankCode = settings.BankCode,
                BankAccountNumber = settings.BankAccountNumber,
                BankAccountName = settings.BankAccountName,
                TransferContent = transferContent,
                CountdownMinutes = settings.CountdownMinutes > 0 ? settings.CountdownMinutes : 5,
                BeforeBillMessage = settings.BeforeBillMessage,
                AfterBillMessage = settings.AfterBillMessage,
                ExpiredMessage = settings.ExpiredMessage
            };
        }

        private async Task Update(int branchId, string name, string value)
        {
            var key = Key(branchId, name);
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
            if (setting == null)
            {
                _context.SystemSettings.Add(new SystemSetting
                {
                    SettingKey = key,
                    SettingValue = value,
                    GroupName = "PaymentQr",
                    Description = $"Payment QR setting {name} for branch {branchId}",
                    LastUpdated = DateTime.UtcNow
                });
            }
            else
            {
                setting.SettingValue = value;
                setting.LastUpdated = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        private static string Key(int branchId, string name) => $"PaymentQr:{branchId}:{name}";

        private static string BuildTransferContent(string template, Booking booking, string branchName)
        {
            return (string.IsNullOrWhiteSpace(template) ? "chinhan {BookingId}" : template)
                .Replace("{BookingId}", booking.Id.ToString())
                .Replace("{BranchName}", branchName);
        }

        private static string BuildGeneratedQrUrl(PaymentQrSettingsViewModel settings, decimal amount, string transferContent)
        {
            if (string.IsNullOrWhiteSpace(settings.BankCode) || string.IsNullOrWhiteSpace(settings.BankAccountNumber))
            {
                return string.Empty;
            }

            return $"https://img.vietqr.io/image/{Uri.EscapeDataString(settings.BankCode)}-{Uri.EscapeDataString(settings.BankAccountNumber)}-compact.png?amount={amount:0}&addInfo={Uri.EscapeDataString(transferContent)}";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }

        private static PaymentQrDisplayViewModel BuildMissingDisplay()
        {
            return new PaymentQrDisplayViewModel
            {
                HasQrImage = false,
                MissingQrMessage = "Không tìm thấy chi nhánh để lấy cấu hình mã QR thanh toán.",
                CountdownMinutes = 5,
                BeforeBillMessage = "Vui lòng tải lên ảnh chụp màn hình bill thanh toán thành công để chúng tôi xác nhận nhanh nhất.",
                AfterBillMessage = "Chúng tôi đã nhận được Bill của bạn. Nhân viên sẽ đối soát và gửi mã phòng qua Email sớm nhất.",
                ExpiredMessage = "Rất tiếc, thời gian giữ chỗ của bạn đã kết thúc. Vui lòng quay lại trang chủ để chọn lại."
            };
        }
    }
}
