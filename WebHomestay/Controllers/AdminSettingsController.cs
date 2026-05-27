using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Services;
using WebHomestay.Models;
using WebHomestay.Filters;

namespace WebHomestay.Controllers
{
    [AdminAuthorize]
    [Route("admin/settings")]
    public class AdminSettingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISettingService _settingService;
        private readonly IPaymentQrSettingsService _paymentQrSettingsService;
        private readonly IWebHostEnvironment _environment;

        public AdminSettingsController(
            ApplicationDbContext context,
            ISettingService settingService,
            IPaymentQrSettingsService paymentQrSettingsService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _settingService = settingService;
            _paymentQrSettingsService = paymentQrSettingsService;
            _environment = environment;
        }

        [AdminAuthorize(Permission = "settings.view")]
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            try 
            {
                var settings = await _context.SystemSettings.OrderBy(s => s.GroupName).ThenBy(s => s.SettingKey).ToListAsync();
                var branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
                var holidays = await _context.Holidays.OrderByDescending(h => h.Date).ToListAsync();
                var templates = await _context.RoomSlotTemplates.OrderBy(t => t.Name).ToListAsync();
                
                ViewBag.Branches = branches;
                ViewBag.Holidays = holidays;
                ViewBag.Templates = templates;
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
            await _settingService.UpdateSettingAsync(key, value);
            TempData["SuccessMessage"] = "Cập nhật cấu hình hệ thống thành công.";
            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "settings.update")]
        [HttpPost("cancellation-policy")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCancellationPolicy(string handlingMode, int noticeHours, int refundBefore, int refundAfter, string policyMessage)
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

            TempData["SuccessMessage"] = "Cập nhật chính sách hủy đơn thành công.";
            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "branch.settings")]
        [HttpPost("update-branch")]
        public async Task<IActionResult> UpdateBranch(int branchId, int leadTime)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            if (branch != null)
            {
                branch.BookingLeadTimeHours = leadTime;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Cập nhật cấu hình cho chi nhánh {branch.Name} thành công.";
            }
            return RedirectToAction(nameof(Index));
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
    }
}
