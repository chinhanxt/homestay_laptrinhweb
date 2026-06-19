using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Filters;
using WebHomestay.Services;

namespace WebHomestay.Controllers
{
    [AdminAuthorize] // Yêu cầu đăng nhập Admin
    [Route("admin/branches")]
    public class AdminBranchesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBulkImportService _importService;

        public AdminBranchesController(ApplicationDbContext context, IBulkImportService importService)
        {
            _context = context;
            _importService = importService;
        }

        // GET: admin/branches
        [AdminAuthorize(Permission = "branches.view")]
        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString("AdminRole");
            var branchId = HttpContext.Session.GetInt32("AdminBranchId");

            var query = _context.Branches.AsQueryable();
            if (role != "SuperAdmin" && branchId.HasValue)
            {
                query = query.Where(b => b.Id == branchId.Value);
            }
            return View(await query.ToListAsync());
        }

        // GET: admin/branches/details/5
        [AdminAuthorize(Permission = "branches.view")]
        [HttpGet("details/{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var branch = await _context.Branches
                .Include(b => b.Rooms)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (branch == null) return NotFound();

            var role = HttpContext.Session.GetString("AdminRole");
            var userBranchId = HttpContext.Session.GetInt32("AdminBranchId");
            if (role != "SuperAdmin" && userBranchId.HasValue && branch.Id != userBranchId.Value)
            {
                return Forbid();
            }

            return View(branch);
        }

        // GET: admin/branches/create
        [AdminAuthorize(Permission = "branches.create")]
        [HttpGet("create")]
        public IActionResult Create()
        {
            return View();
        }

        [AdminAuthorize(Permission = "branches.create")]
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Address,Hotline,Email,Description,MapUrl")] Branch branch)
        {
            if (ModelState.IsValid)
            {
                _context.Add(branch);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(branch);
        }

        // GET: admin/branches/edit/5
        [AdminAuthorize(Permission = "branches.edit")]
        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var role = HttpContext.Session.GetString("AdminRole");
            var userBranchId = HttpContext.Session.GetInt32("AdminBranchId");

            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();
            if (role != "SuperAdmin" && userBranchId.HasValue && branch.Id != userBranchId.Value)
            {
                return Forbid();
            }
            return View(branch);
        }

        [AdminAuthorize(Permission = "branches.edit")]
        [HttpPost("edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,Hotline,Email,Description,MapUrl")] Branch branch)
        {
            if (id != branch.Id) return NotFound();

            var role = HttpContext.Session.GetString("AdminRole");
            var userBranchId = HttpContext.Session.GetInt32("AdminBranchId");
            if (role != "SuperAdmin" && userBranchId.HasValue && branch.Id != userBranchId.Value)
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(branch);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BranchExists(branch.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(branch);
        }

        [AdminAuthorize(Permission = "branches.delete")]
        [HttpPost("delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var role = HttpContext.Session.GetString("AdminRole");
            var userBranchId = HttpContext.Session.GetInt32("AdminBranchId");

            var branch = await _context.Branches
                .Include(b => b.Rooms)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (branch == null) return NotFound();
            if (role != "SuperAdmin" && userBranchId.HasValue && branch.Id != userBranchId.Value)
            {
                return Forbid();
            }

            // Kiểm tra xem chi nhánh có phòng nào không
            if (branch.Rooms != null && branch.Rooms.Any())
            {
                TempData["ErrorMessage"] = $"Không thể xóa chi nhánh '{branch.Name}' vì vẫn còn phòng thuộc chi nhánh này. Hãy xóa hết phòng của chi nhánh trước khi thực hiện.";
                return RedirectToAction(nameof(Index));
            }

            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã xóa chi nhánh '{branch.Name}' thành công.";
            return RedirectToAction(nameof(Index));
        }

        // POST: admin/branches/soft-delete/5
        [AdminAuthorize(Permission = "branches.delete")]
        [HttpPost("soft-delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var role = HttpContext.Session.GetString("AdminRole");
            var userBranchId = HttpContext.Session.GetInt32("AdminBranchId");

            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();
            if (role != "SuperAdmin" && userBranchId.HasValue && branch.Id != userBranchId.Value)
            {
                return Forbid();
            }

            branch.IsDeleted = true;
            branch.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã chuyển chi nhánh '{branch.Name}' vào thùng rác.";
            return RedirectToAction(nameof(Index));
        }

        // GET: admin/branches/trash
        [AdminAuthorize(Permission = "branches.trash")]
        [HttpGet("trash")]
        public async Task<IActionResult> Trash()
        {
            var trash = await _context.Branches
                .Where(b => b.IsDeleted)
                .OrderByDescending(b => b.DeletedAt)
                .ToListAsync();
            return View(trash);
        }

        // POST: admin/branches/restore/5
        [AdminAuthorize(Permission = "branches.restore")]
        [HttpPost("restore/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();

            branch.IsDeleted = false;
            branch.DeletedAt = null;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã khôi phục chi nhánh '{branch.Name}'.";
            return RedirectToAction(nameof(Trash));
        }

        // POST: admin/branches/permanent-delete/5
        [AdminAuthorize(Permission = "branches.delete")]
        [HttpPost("permanent-delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();

            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã xóa vĩnh viễn chi nhánh '{branch.Name}'.";
            return RedirectToAction(nameof(Trash));
        }

        [HttpGet("public-contacts")]
        [AllowAnonymous]
        public async Task<IActionResult> PublicContacts()
        {
            var branches = await _context.Branches
                .OrderBy(b => b.Name)
                .Select(b => new { id = b.Id, name = b.Name, address = b.Address, zaloPhone = b.Hotline, email = b.Email })
                .ToListAsync();
            return Ok(branches);
        }

        private bool BranchExists(int id)
        {
            return _context.Branches.Any(e => e.Id == id);
        }

        [AdminAuthorize(Permission = "branches.create")]
        [HttpPost("import-zip")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportZip(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn file ZIP hoặc Excel để import.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _importService.ImportBranchesAsync(file);
            if (result.Errors.Any())
            {
                TempData["ErrorMessage"] = $"Import hoàn tất. Thành công: {result.SuccessCount}, Thất bại: {result.FailureCount}. Chi tiết lỗi: {string.Join(" | ", result.Errors.Take(5))}";
            }
            else
            {
                TempData["SuccessMessage"] = $"Đã import thành công {result.SuccessCount} chi nhánh.";
            }

            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "branches.create")]
        [HttpPost("import-preview")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportPreview(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn file để import." });
            }

            try
            {
                var result = await _importService.PreviewBranchesAsync(file);
                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize(Permission = "branches.create")]
        [HttpPost("import-confirm")]
        public async Task<IActionResult> ImportConfirm([FromBody] ConfirmImportRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.CacheKey))
            {
                return Json(new { success = false, message = "Yêu cầu không hợp lệ." });
            }

            try
            {
                var result = await _importService.ConfirmBranchesAsync(request.CacheKey);
                return Json(new { success = true, successCount = result.SuccessCount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}

