using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Filters;

namespace WebHomestay.Controllers
{
    [AdminAuthorize]
    [Route("admin/images")]
    public class AdminImagesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminImagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AdminAuthorize(Permission = "images.view")]
        public async Task<IActionResult> Index(string? search)
        {
            var role = HttpContext.Session.GetString("AdminRole");
            var branchId = HttpContext.Session.GetInt32("AdminBranchId");

            var query = _context.Bookings
                .Include(b => b.Room)
                .ThenInclude(r => r.Branch)
                .Where(b => !b.IsDeleted && (b.PaymentProofUrl != null || b.IdCardFrontPath != null));

            if (role != "SuperAdmin" && branchId.HasValue)
            {
                query = query.Where(b => b.Room.BranchId == branchId.Value);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(b => b.CustomerName.Contains(search) || b.CustomerPhone.Contains(search) || b.Id.ToString().Contains(search));
            }

            var bookings = await query
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(bookings);
        }

        [AdminAuthorize(Permission = "images.view")]
        [HttpGet("get-details/{id}")]
        public async Task<IActionResult> GetDetails(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.Room)
                .ThenInclude(r => r.Branch)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();

            // Only allow viewing bookings from same branch if not SuperAdmin
            var role = HttpContext.Session.GetString("AdminRole");
            var branchId = HttpContext.Session.GetInt32("AdminBranchId");
            if (role != "SuperAdmin" && branchId.HasValue && booking.Room.BranchId != branchId.Value)
            {
                return Forbid();
            }

            return PartialView("_ImageDetails", booking);
        }
    }
}
