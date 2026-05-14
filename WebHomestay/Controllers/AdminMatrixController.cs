using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Filters;
using WebHomestay.Services;

namespace WebHomestay.Controllers
{
    [AdminAuthorize]
    [Route("admin/matrix")]
    public class AdminMatrixController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISettingService _settingService;

        public AdminMatrixController(ApplicationDbContext context, ISettingService settingService)
        {
            _context = context;
            _settingService = settingService;
        }

        [AdminAuthorize(Permission = "matrix.view")]
        [HttpGet]
        public async Task<IActionResult> Index(DateTime? startDate)
        {
            try 
            {
                DateTime start = startDate ?? DateTime.Today;
                DateTime end = start.AddDays(10);

                var role = HttpContext.Session.GetString("AdminRole");
                var branchId = HttpContext.Session.GetInt32("AdminBranchId");

                var branchQuery = _context.Branches.AsQueryable();
                if (role != "SuperAdmin" && branchId.HasValue)
                {
                    branchQuery = branchQuery.Where(b => b.Id == branchId.Value);
                }

                var branches = await branchQuery
                    .Include(b => b.Rooms)
                    .OrderBy(b => b.Name)
                    .ToListAsync() ?? new List<Branch>();

                // Auto-complete logic for past bookings
                var checkoutMode = await _settingService.GetStringAsync("CheckoutMode", "Auto");
                if (checkoutMode == "Auto")
                {
                    var nowTime = DateTime.Now;
                    bool changed = false;

                    // 1. Auto Check-In: Confirmed -> CheckedIn when StartTime reached
                    var bookingsToCheckIn = await _context.Bookings
                        .Where(b => !b.IsDeleted && b.Status == "Confirmed" && b.StartTime <= nowTime)
                        .ToListAsync();

                    if (bookingsToCheckIn.Any())
                    {
                        foreach (var b in bookingsToCheckIn) b.Status = "CheckedIn";
                        changed = true;
                    }

                    // 2. Auto Check-Out: Confirmed/CheckedIn -> CheckedOut when EndTime passed
                    var pastActiveBookings = await _context.Bookings
                        .Where(b => !b.IsDeleted && 
                                   (b.Status == "Confirmed" || b.Status == "CheckedIn") && 
                                   b.EndTime < nowTime)
                        .ToListAsync();

                    if (pastActiveBookings.Any())
                    {
                        foreach (var b in pastActiveBookings) b.Status = "CheckedOut";
                        changed = true;
                    }

                    if (changed)
                    {
                        await _context.SaveChangesAsync();
                    }
                }

                var bookingsQuery = _context.Bookings
                    .Include(b => b.Room)
                    .Where(b =>
                        !b.IsDeleted &&
                        b.Status != "Cancelled" &&
                        !((b.Status == "AwaitingPayment" || b.Status == "PendingPayment") && b.CreatedAt < DateTime.UtcNow.AddMinutes(-5)) &&
                        b.StartTime < end &&
                        b.EndTime >= start);

                if (role != "SuperAdmin" && branchId.HasValue)
                {
                    bookingsQuery = bookingsQuery.Where(b => b.Room.BranchId == branchId.Value);
                }

                var bookings = await bookingsQuery
                    .OrderBy(b => b.StartTime)
                    .ToListAsync() ?? new List<Booking>();

                ViewBag.StartDate = start;
                ViewBag.EndDate = end;
                ViewBag.Bookings = bookings;

                return View(branches);
            }
            catch (Exception ex)
            {
                return Content("Lỗi hệ thống khi tải ma trận: " + ex.Message);
            }
        }
    }
}
