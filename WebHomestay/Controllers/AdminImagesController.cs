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
        private readonly IWebHostEnvironment _env;

        public AdminImagesController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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

            var role = HttpContext.Session.GetString("AdminRole");
            var branchId = HttpContext.Session.GetInt32("AdminBranchId");
            if (role != "SuperAdmin" && branchId.HasValue && booking.Room.BranchId != branchId.Value)
            {
                return Forbid();
            }

            return PartialView("_ImageDetails", booking);
        }

        [AdminAuthorize(Permission = "images.detail")]
        [HttpPost("soft-delete")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null) return NotFound();

            booking.IsDeleted = true;
            booking.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã chuyển ảnh vào thùng rác.";
            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "images.view")]
        [HttpGet("trash")]
        public async Task<IActionResult> Trash()
        {
            var role = HttpContext.Session.GetString("AdminRole");
            var branchId = HttpContext.Session.GetInt32("AdminBranchId");

            var query = _context.Bookings
                .Include(b => b.Room)
                .Include(b => b.RoomSlotInventory)
                    .ThenInclude(i => i.Template)
                .Where(b => b.IsDeleted && (b.PaymentProofUrl != null || b.IdCardFrontPath != null));

            if (role != "SuperAdmin" && branchId.HasValue)
                query = query.Where(b => b.Room.BranchId == branchId.Value);

            var bookings = await query
                .OrderByDescending(b => b.DeletedAt)
                .ToListAsync();

            return View(bookings);
        }

        [AdminAuthorize(Permission = "images.detail")]
        [HttpPost("restore")]
        public async Task<IActionResult> Restore(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null) return NotFound();

            booking.IsDeleted = false;
            booking.DeletedAt = null;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Trash));
        }

        [AdminAuthorize(Permission = "images.detail")]
        [HttpPost("permanent-delete")]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null) return NotFound();

            // Delete physical image files from disk
            DeleteImageFile(booking.PaymentProofUrl);
            DeleteSecureFile(booking.IdCardFrontPath);
            DeleteSecureFile(booking.IdCardBackPath);
            DeleteSecureFile(booking.IdCardFrontMaskedPath);
            DeleteSecureFile(booking.IdCardBackMaskedPath);

            _context.Bookings.Remove(booking);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Trash));
        }

        private void DeleteImageFile(string? url)
        {
            if (string.IsNullOrEmpty(url)) return;

            try
            {
                // If it's a local path (not absolute URL), resolve to physical path
                string filePath;
                if (url.StartsWith("http"))
                {
                    // Remote URL — try to extract local wwwroot relative path
                    var uri = new Uri(url);
                    string localPart = uri.AbsolutePath.TrimStart('/');
                    filePath = Path.Combine(_env.WebRootPath, localPart);
                }
                else
                {
                    filePath = Path.Combine(_env.WebRootPath, url.TrimStart('/'));
                }

                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }
            catch { /* Ignore file deletion errors */ }
        }

        private void DeleteSecureFile(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;

            try
            {
                string secureDir = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", "IDCards");
                string filePath = Path.Combine(secureDir, fileName);
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }
            catch { /* Ignore file deletion errors */ }
        }
    }
}
