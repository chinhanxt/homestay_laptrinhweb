using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Filters;

namespace WebHomestay.Controllers
{
    [AdminAuthorize]
    [Route("admin/holidays")]
    public class AdminHolidaysController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminHolidaysController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return Redirect("/admin/settings?tab=holiday");
        }


        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DateTime date, string description)
        {
            if (date == default)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn ngày hợp lệ.";
                return Redirect("/admin/settings?tab=holiday");
            }

            var exists = await _context.Holidays.AnyAsync(h => h.Date.Date == date.Date);
            if (exists)
            {
                TempData["ErrorMessage"] = "Ngày này đã được cấu hình là ngày Lễ.";
                return Redirect("/admin/settings?tab=holiday");
            }

            var holiday = new Holiday
            {
                Date = date.Date,
                Description = description
            };

            _context.Holidays.Add(holiday);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã thêm ngày Lễ: {date:dd/MM/yyyy}";
            return Redirect("/admin/settings?tab=holiday");
        }

        [HttpPost("delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var holiday = await _context.Holidays.FindAsync(id);
            if (holiday != null)
            {
                _context.Holidays.Remove(holiday);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = " Đã xóa ngày Lễ.";
            }
            return Redirect("/admin/settings?tab=holiday");
        }

        [HttpPost("create-range")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRange(DateTime startDate, DateTime endDate, string description)
        {
            if (startDate == default || endDate == default || startDate > endDate)
            {
                TempData["ErrorMessage"] = "Khoảng ngày không hợp lệ.";
                return Redirect("/admin/settings?tab=holiday");
            }

            int addedCount = 0;
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var exists = await _context.Holidays.AnyAsync(h => h.Date.Date == date);
                if (!exists)
                {
                    _context.Holidays.Add(new Holiday
                    {
                        Date = date,
                        Description = description
                    });
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã thêm {addedCount} ngày lễ vào hệ thống.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không có ngày mới nào được thêm (có thể đã tồn tại).";
            }

            return Redirect("/admin/settings?tab=holiday");
        }

        [HttpPost("delete-by-desc")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteByDescription(string description)
        {
            var holidays = await _context.Holidays.Where(h => h.Description == description).ToListAsync();
            if (holidays.Any())
            {
                _context.Holidays.RemoveRange(holidays);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa dịp lễ: {description}";
            }
            return Redirect("/admin/settings?tab=holiday");
        }
    }
}
