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

        public AdminSettingsController(ApplicationDbContext context, ISettingService settingService)
        {
            _context = context;
            _settingService = settingService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            try 
            {
                var settings = await _context.SystemSettings.OrderBy(s => s.GroupName).ThenBy(s => s.SettingKey).ToListAsync();
                var branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
                
                ViewBag.Branches = branches;
                return View(settings);
            }
            catch (Exception ex)
            {
                return Content($"Lỗi: {ex.Message} - {ex.InnerException?.Message}");
            }
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update(string key, string value)
        {
            await _settingService.UpdateSettingAsync(key, value);
            TempData["SuccessMessage"] = "Cập nhật cấu hình hệ thống thành công.";
            return RedirectToAction(nameof(Index));
        }

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
    }
}
