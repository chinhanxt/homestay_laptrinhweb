using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Filters;

namespace WebHomestay.Controllers
{
    [AdminAuthorize]
    [Route("admin/matrix")]
    public class AdminMatrixController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminMatrixController(ApplicationDbContext context)
        {
            _context = context;
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

                var bookingsQuery = _context.Bookings
                    .Include(b => b.Room)
                    .Where(b =>
                        !b.IsDeleted &&
                        b.Status != "Cancelled" &&
                        !((b.Status == "AwaitingPayment" || b.Status == "PendingPayment") && b.CreatedAt < DateTime.UtcNow.AddMinutes(-5)) &&
                        b.StartTime < end &&
                        b.EndTime > DateTime.Now);

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
