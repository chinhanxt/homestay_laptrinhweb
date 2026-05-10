using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Filters;

namespace WebHomestay.Controllers
{
    [AdminAuthorize]
    [Route("admin/rooms")]
    public class AdminRoomsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminRoomsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AdminAuthorize(Permission = "rooms.view")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString("AdminRole");
            var branchId = HttpContext.Session.GetInt32("AdminBranchId");

            var query = _context.Rooms.Include(r => r.Branch).AsQueryable();

            if (role != "SuperAdmin" && branchId.HasValue)
            {
                query = query.Where(r => r.BranchId == branchId.Value);
            }

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
        public async Task<IActionResult> Create([Bind("Id,Name,Description,PricePerHour,PricePerDay,ExtraGuestFee,Capacity,MaxGuests,Status,ImageUrl,BranchId")] Room room)
        {
            if (ModelState.IsValid)
            {
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,PricePerHour,PricePerDay,ExtraGuestFee,Capacity,MaxGuests,Status,ImageUrl,BranchId")] Room room)
        {
            if (id != room.Id) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
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

        private bool RoomExists(int id)
        {
            return _context.Rooms.Any(e => e.Id == id);
        }
    }
}
