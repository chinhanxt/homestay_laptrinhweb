using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Filters;
using System.IO;
using System.Text.Json;

namespace WebHomestay.Controllers
{
    [AdminAuthorize]
    [Route("admin/rooms")]
    public class AdminRoomsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public AdminRoomsController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        [AdminAuthorize(Permission = "rooms.view")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString("AdminRole");
            var branchId = HttpContext.Session.GetInt32("AdminBranchId");

            var query = _context.Rooms.Include(r => r.Branch).Where(r => !r.IsDeleted).AsQueryable();

            if (role != "SuperAdmin" && branchId.HasValue)
            {
                query = query.Where(r => r.BranchId == branchId.Value);
            }

            ViewBag.Branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
            return View(await query.ToListAsync());
        }

        [AdminAuthorize(Permission = "rooms.view")]
        [HttpGet("details/{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            
            var role = HttpContext.Session.GetString("AdminRole");
            var branchId = HttpContext.Session.GetInt32("AdminBranchId");

            var room = await _context.Rooms.Include(r => r.Branch).FirstOrDefaultAsync(m => m.Id == id);
            
            if (room == null) return NotFound();

            // Bảo vệ: Nếu là nhân viên, không được xem phòng của chi nhánh khác qua link trực tiếp
            if (role != "SuperAdmin" && branchId.HasValue && room.BranchId != branchId.Value)
            {
                return Forbid("Bạn không có quyền truy cập thông tin phòng của chi nhánh khác.");
            }

            return View(room);
        }

        [AdminAuthorize(Permission = "rooms.create")]
        [HttpGet("create")]
        public IActionResult Create()
        {
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name");
            return View();
        }

        [AdminAuthorize(Permission = "rooms.create")]
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,PricePerHour,PricePerDay,PriceWeekendPerHour,PriceWeekendPerDay,PriceHolidayPerHour,PriceHolidayPerDay,ExtraGuestFee,Capacity,MaxGuests,Status,BranchId")] Room room, IFormFile? mainImageFile, List<IFormFile> illustrationFiles)
        {
            if (ModelState.IsValid)
            {
                // Handle Main Image
                if (mainImageFile != null)
                {
                    room.ImageUrl = await SaveFile(mainImageFile);
                }

                // Handle Illustration Images
                if (illustrationFiles != null && illustrationFiles.Count > 0)
                {
                    var imagePaths = new List<string>();
                    foreach (var file in illustrationFiles)
                    {
                        if (file.Length > 0)
                            imagePaths.Add(await SaveFile(file));
                    }
                    if (imagePaths.Count > 0)
                        room.AdditionalImages = System.Text.Json.JsonSerializer.Serialize(imagePaths);
                }

                _context.Add(room);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", room.BranchId);
            return View(room);
        }

        [AdminAuthorize(Permission = "rooms.edit")]
        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", room.BranchId);
            return View(room);
        }

        [AdminAuthorize(Permission = "rooms.edit")]
        [HttpPost("edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,PricePerHour,PricePerDay,PriceWeekendPerHour,PriceWeekendPerDay,PriceHolidayPerHour,PriceHolidayPerDay,ExtraGuestFee,Capacity,MaxGuests,Status,BranchId")] Room room, IFormFile? mainImageFile, List<IFormFile> illustrationFiles)
        {
            if (id != room.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingRoom = await _context.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
                    if (existingRoom == null) return NotFound();

                    // Keep existing images if no new files uploaded
                    room.ImageUrl = existingRoom.ImageUrl;
                    room.AdditionalImages = existingRoom.AdditionalImages;

                    // Update Main Image if new file uploaded
                    if (mainImageFile != null)
                    {
                        room.ImageUrl = await SaveFile(mainImageFile);
                    }

                    // Update Illustration Images if new files uploaded
                    if (illustrationFiles != null && illustrationFiles.Count > 0)
                    {
                        var existingPaths = !string.IsNullOrEmpty(existingRoom.AdditionalImages)
                            ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(existingRoom.AdditionalImages) ?? new List<string>()
                            : new List<string>();
                        var newPaths = new List<string>();
                        foreach (var file in illustrationFiles)
                        {
                            if (file.Length > 0)
                                newPaths.Add(await SaveFile(file));
                        }
                        existingPaths.AddRange(newPaths);
                        room.AdditionalImages = System.Text.Json.JsonSerializer.Serialize(existingPaths);
                    }

                    room.CreatedAt = existingRoom.CreatedAt;

                    _context.Update(room);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RoomExists(room.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", room.BranchId);
            return View(room);
        }

        private async Task<string> SaveFile(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads", "rooms");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return "/uploads/rooms/" + uniqueFileName;
        }

        [AdminAuthorize(Permission = "rooms.edit")]
        [HttpPost("toggle-status/{id}")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var room = await _context.Rooms.Include(r => r.Bookings).FirstOrDefaultAsync(r => r.Id == id);
            if (room == null) return NotFound();

            string message = "";
            bool success = true;

            if (room.Status == "Maintenance")
            {
                room.Status = "Available";
                message = $"Phòng {room.Name} đã hoạt động trở lại.";
            }
            else
            {
                // Kiểm tra đơn đặt phòng trong tương lai (Confirmed, AwaitingApproval, CheckedIn)
                var activeBookings = room.Bookings.Any(b => 
                    b.EndTime > DateTime.Now && 
                    (b.Status == "Confirmed" || b.Status == "AwaitingApproval" || b.Status == "CheckedIn")
                );

                if (activeBookings)
                {
                    success = false;
                    message = $"Không thể tắt hoạt động phòng {room.Name} vì đang có đơn đặt phòng hoặc khách đang ở.";
                }
                else
                {
                    room.Status = "Maintenance";
                    message = $"Đã tạm ngưng hoạt động phòng {room.Name}.";
                }
            }

            if (success) await _context.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success, message, newStatus = room.Status });
            }

            if (success) TempData["SuccessMessage"] = message;
            else TempData["ErrorMessage"] = message;

            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "rooms.delete")]
        [HttpPost("delete/{id}"), ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room != null) _context.Rooms.Remove(room);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "rooms.delete")]
        [HttpPost("soft-delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();

            room.IsDeleted = true;
            room.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Phòng '{room.Name}' đã được chuyển vào thùng rác.";
            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "rooms.view")]
        [HttpGet("trash")]
        public async Task<IActionResult> Trash()
        {
            var role = HttpContext.Session.GetString("AdminRole");
            var branchId = HttpContext.Session.GetInt32("AdminBranchId");

            var query = _context.Rooms
                .Include(r => r.Branch)
                .Where(r => r.IsDeleted);

            if (role != "SuperAdmin" && branchId.HasValue)
            {
                query = query.Where(r => r.BranchId == branchId.Value);
            }

            var rooms = await query
                .OrderByDescending(r => r.DeletedAt)
                .ToListAsync();
            return View(rooms);
        }

        [AdminAuthorize(Permission = "rooms.edit")]
        [HttpPost("restore/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();

            room.IsDeleted = false;
            room.DeletedAt = null;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Phòng '{room.Name}' đã được khôi phục.";
            return RedirectToAction(nameof(Trash));
        }

        [AdminAuthorize(Permission = "rooms.delete")]
        [HttpPost("permanent-delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();

            _context.Rooms.Remove(room);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Phòng '{room.Name}' đã bị xóa vĩnh viễn.";
            return RedirectToAction(nameof(Trash));
        }

        private bool RoomExists(int id)
        {
            return _context.Rooms.Any(e => e.Id == id);
        }
    }
}
