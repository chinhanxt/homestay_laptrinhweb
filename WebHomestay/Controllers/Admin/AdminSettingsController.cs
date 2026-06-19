using WebHomestay.Services.Slots;
using WebHomestay.Services.Room;
using WebHomestay.Services.Chat;
using WebHomestay.Services.Settings;
using WebHomestay.Services.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Services;
using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;
using WebHomestay.Filters;

namespace WebHomestay.Controllers.Admin
{
    [AdminAuthorize]
    [Route("admin/settings")]
    public class AdminSettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISettingService _settingService;
        private readonly IPaymentQrSettingsService _paymentQrSettingsService;
        private readonly IWebHostEnvironment _environment;
        private readonly IExcelTemplateService _excelService;
        private readonly IBulkImportService _importService;

        public AdminSettingsController(
            ApplicationDbContext context,
            ISettingService settingService,
            IPaymentQrSettingsService paymentQrSettingsService,
            IWebHostEnvironment environment,
            IExcelTemplateService excelService,
            IBulkImportService importService)
        {
            _context = context;
            _settingService = settingService;
            _paymentQrSettingsService = paymentQrSettingsService;
            _environment = environment;
            _excelService = excelService;
            _importService = importService;
        }

        [AdminAuthorize(Permission = "settings.view")]
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            try 
            {
                var settings = await _context.SystemSettings.OrderBy(s => s.GroupName).ThenBy(s => s.SettingKey).ToListAsync();
                var branches = await _context.Branches.Where(b => !b.IsDeleted).OrderBy(b => b.Name).ToListAsync();
                var holidays = await _context.Holidays.Where(h => !h.IsDeleted).OrderByDescending(h => h.Date).ToListAsync();
                var templates = await _context.RoomSlotTemplates.Where(t => !t.IsDeleted).OrderBy(t => t.Name).ToListAsync();
                var trashedHolidays = await _context.Holidays.Where(h => h.IsDeleted).OrderByDescending(h => h.DeletedAt).ToListAsync();
                var trashedTemplates = await _context.RoomSlotTemplates.Where(t => t.IsDeleted).OrderByDescending(t => t.DeletedAt).ToListAsync();
                
                ViewBag.Branches = branches;
                ViewBag.Holidays = holidays;
                ViewBag.Templates = templates;
                ViewBag.TrashedHolidays = trashedHolidays;
                ViewBag.TrashedTemplates = trashedTemplates;
                ViewBag.PaymentQrSettings = await _paymentQrSettingsService.GetSettingsForBranchesAsync(branches);

                return View(settings);
            }
            catch (Exception ex)
            {
                return Content($"Lỗi: {ex.Message} - {ex.InnerException?.Message}");
            }
        }

        [AdminAuthorize(Permission = "settings.update")]
        [HttpPost("update")]
        public async Task<IActionResult> Update(string key, string value)
        {
            if (key == "MaxZipUploadSizeMB")
            {
                if (!int.TryParse(value, out var sizeMB) || sizeMB < 5 || sizeMB > 100)
                {
                    TempData["ErrorMessage"] = "Kích thước file ZIP tối đa cho phép phải từ 5MB đến 100MB.";
                    return RedirectToAction(nameof(Index));
                }
            }

            await _settingService.UpdateSettingAsync(key, value);
            TempData["SuccessMessage"] = "Cập nhật cấu hình hệ thống thành công.";
            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "settings.update")]
        [HttpPost("cancellation-policy")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCancellationPolicy(string handlingMode, int noticeHours, int refundBefore, int refundAfter, string policyMessage, string approvalEmailSubject, string approvalEmailBody, string rejectionEmailSubject, string rejectionEmailBody)
        {
            handlingMode = handlingMode == "Auto" ? "Auto" : "Manual";
            noticeHours = Math.Max(0, noticeHours);
            refundBefore = Math.Clamp(refundBefore, 0, 100);
            refundAfter = Math.Clamp(refundAfter, 0, 100);
            policyMessage = string.IsNullOrWhiteSpace(policyMessage)
                ? "Yêu cầu hủy sẽ được nhân viên kiểm tra và phản hồi qua email."
                : policyMessage.Trim();

            await _settingService.UpdateSettingAsync("CancellationHandlingMode", handlingMode);
            await _settingService.UpdateSettingAsync("CancellationNoticeHours", noticeHours.ToString());
            await _settingService.UpdateSettingAsync("CancellationRefundPercentBeforeNotice", refundBefore.ToString());
            await _settingService.UpdateSettingAsync("CancellationRefundPercentAfterNotice", refundAfter.ToString());
            await _settingService.UpdateSettingAsync("CancellationPolicyMessage", policyMessage);
            await _settingService.UpdateSettingAsync("CancellationApprovalEmailSubject", string.IsNullOrWhiteSpace(approvalEmailSubject) ? BookingCancellationService.DefaultApprovalSubject : approvalEmailSubject.Trim());
            await _settingService.UpdateSettingAsync("CancellationApprovalEmailBody", string.IsNullOrWhiteSpace(approvalEmailBody) ? BookingCancellationService.DefaultApprovalBody : approvalEmailBody.Trim());
            await _settingService.UpdateSettingAsync("CancellationRejectionEmailSubject", string.IsNullOrWhiteSpace(rejectionEmailSubject) ? BookingCancellationService.DefaultRejectionSubject : rejectionEmailSubject.Trim());
            await _settingService.UpdateSettingAsync("CancellationRejectionEmailBody", string.IsNullOrWhiteSpace(rejectionEmailBody) ? BookingCancellationService.DefaultRejectionBody : rejectionEmailBody.Trim());

            TempData["SuccessMessage"] = "Cập nhật chính sách hủy đơn thành công.";
            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "settings.update")]
        [HttpPost("update-branch")]
        public async Task<IActionResult> UpdateBranch(int branchId, int leadTimeHours, int leadTimeDays)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            if (branch != null)
            {
                branch.BookingLeadTimeHours = Math.Max(0, leadTimeHours);
                branch.BookingLeadTimeDays = Math.Max(0, leadTimeDays);
                branch.BookingLeadTimeValue = leadTimeHours;
                branch.BookingLeadTimeUnit = BranchLeadTimeUnit.Hours;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Cập nhật cấu hình cho chi nhánh {branch.Name} thành công.";
            }
            return RedirectToAction(nameof(Index), new { tab = "branch" });
        }

        [AdminAuthorize(Permission = "payment.settings")]
        [HttpPost("payment-qr")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePaymentQr(WebHomestay.Models.ViewModels.PaymentQrSettingsViewModel model, IFormFile? qrImage)
        {
            var result = await _paymentQrSettingsService.SaveSettingsAsync(model, qrImage, _environment.WebRootPath);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "settings.view")]
        [HttpGet("download-template")]
        public IActionResult DownloadTemplate(string type)
        {
            try
            {
                var fileBytes = _excelService.GenerateTemplate(type);
                string fileName = $"template_{type}.xlsx";
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tạo file mẫu: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [AdminAuthorize(Permission = "settings.update")]
        [HttpPost("import-slot-templates")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportSlotTemplates(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn file Excel để import.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _importService.ImportRoomSlotTemplatesAsync(file);
            if (result.Errors.Any())
            {
                TempData["ErrorMessage"] = $"Import hoàn tất. Thành công: {result.SuccessCount}, Thất bại: {result.FailureCount}. Chi tiết lỗi: {string.Join(" | ", result.Errors.Take(5))}";
            }
            else
            {
                TempData["SuccessMessage"] = $"Đã import thành công {result.SuccessCount} mẫu khung giờ.";
            }

            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "settings.update")]
        [HttpPost("import-holidays")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportHolidays(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn file Excel để import.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _importService.ImportHolidaysAsync(file);
            if (result.Errors.Any())
            {
                TempData["ErrorMessage"] = $"Import hoàn tất. Thành công: {result.SuccessCount}, Thất bại: {result.FailureCount}. Chi tiết lỗi: {string.Join(" | ", result.Errors.Take(5))}";
            }
            else
            {
                TempData["SuccessMessage"] = $"Đã import thành công {result.SuccessCount} ngày lễ.";
            }

            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "settings.update")]
        [HttpPost("import-slot-templates-preview")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportSlotTemplatesPreview(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn file để import." });
            }

            try
            {
                var result = await _importService.PreviewRoomSlotTemplatesAsync(file);
                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize(Permission = "settings.update")]
        [HttpPost("import-slot-templates-confirm")]
        public async Task<IActionResult> ImportSlotTemplatesConfirm([FromBody] ConfirmImportRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.CacheKey))
            {
                return Json(new { success = false, message = "Yêu cầu không hợp lệ." });
            }

            try
            {
                var result = await _importService.ConfirmRoomSlotTemplatesAsync(request.CacheKey);
                return Json(new { success = true, successCount = result.SuccessCount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize(Permission = "settings.update")]
        [HttpPost("import-holidays-preview")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportHolidaysPreview(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn file để import." });
            }

            try
            {
                var result = await _importService.PreviewHolidaysAsync(file);
                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize(Permission = "settings.update")]
        [HttpPost("import-holidays-confirm")]
        public async Task<IActionResult> ImportHolidaysConfirm([FromBody] ConfirmImportRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.CacheKey))
            {
                return Json(new { success = false, message = "Yêu cầu không hợp lệ." });
            }

            try
            {
                var result = await _importService.ConfirmHolidaysAsync(request.CacheKey);
                return Json(new { success = true, successCount = result.SuccessCount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
