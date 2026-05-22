using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Filters;
using WebHomestay.Services;

namespace WebHomestay.Controllers
{
    [AdminAuthorize]
    [Route("admin/staff")]
    public class AdminStaffController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionResolveService _permissionResolve;

        public AdminStaffController(ApplicationDbContext context, IPermissionResolveService permissionResolve)
        {
            _context = context;
            _permissionResolve = permissionResolve;
        }

        [AdminAuthorize(Permission = "staff.view")]
        public async Task<IActionResult> Index()
        {
            var staff = await _context.AdminUsers.Include(u => u.Branch).ToListAsync();
            return View(staff);
        }

        [AdminAuthorize(Permission = "staff.create")]
        [HttpGet("create")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Branches = new SelectList(await _context.Branches.ToListAsync(), "Id", "Name");
            return View();
        }

        [AdminAuthorize(Permission = "staff.create")]
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminUser user, string password)
        {
            // Kiểm tra trùng tên đăng nhập
            if (await _context.AdminUsers.AnyAsync(u => u.Username == user.Username))
            {
                ModelState.AddModelError("Username", "Tên đăng nhập này đã tồn tại trên hệ thống.");
            }

            ModelState.Remove("PasswordHash");
            ModelState.Remove("ActivityLogs");
            
            if (ModelState.IsValid)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                user.CreatedAt = DateTime.UtcNow;
                _context.Add(user);
                await _context.SaveChangesAsync();

                await LogAction("Tạo nhân sự", $"Tài khoản: {user.Username}");
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Branches = new SelectList(await _context.Branches.ToListAsync(), "Id", "Name");
            return View(user);
        }

        [AdminAuthorize(Permission = "staff.edit")]
        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var user = await _context.AdminUsers.FindAsync(id);
            if (user == null) return NotFound();
            ViewBag.Branches = new SelectList(await _context.Branches.ToListAsync(), "Id", "Name", user.BranchId);
            return View(user);
        }

        [AdminAuthorize(Permission = "staff.edit")]
        [HttpPost("edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AdminUser user, string? newPassword)
        {
            // Kiểm tra trùng tên đăng nhập (trừ chính nó)
            if (await _context.AdminUsers.AnyAsync(u => u.Username == user.Username && u.Id != id))
            {
                ModelState.AddModelError("Username", "Tên đăng nhập này đã được sử dụng bởi tài khoản khác.");
            }

            ModelState.Remove("PasswordHash");
            ModelState.Remove("ActivityLogs");

            if (ModelState.IsValid)
            {
                var existingUser = await _context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
                if (existingUser == null) return NotFound();

                // Kiểm tra quyền Reset mật khẩu (Chỉ SuperAdmin mới được đổi pass cho người khác)
                var currentRole = HttpContext.Session.GetString("AdminRole");
                if (!string.IsNullOrEmpty(newPassword))
                {
                    if (currentRole == "SuperAdmin" || HttpContext.Session.GetInt32("AdminUserId") == id)
                    {
                        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                    }
                    else
                    {
                        return Forbid("Bạn không có quyền thay đổi mật khẩu của nhân viên này.");
                    }
                }
                else
                {
                    user.PasswordHash = existingUser.PasswordHash;
                }

                user.CreatedAt = existingUser.CreatedAt;
                _context.Update(user);
                await _context.SaveChangesAsync();

                await LogAction("Cập nhật nhân sự", $"Tài khoản: {user.Username}");
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Branches = new SelectList(await _context.Branches.ToListAsync(), "Id", "Name", user.BranchId);
            return View(user);
        }

        [AdminAuthorize(Permission = "staff.delete")]
        [HttpPost("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.AdminUsers.FindAsync(id);
            if (user == null) return NotFound();
            if (user.Username == "admin") return BadRequest("Không thể xóa tài khoản hệ thống.");

            _context.AdminUsers.Remove(user);
            await _context.SaveChangesAsync();

            await LogAction("Xóa nhân sự", $"Tài khoản: {user.Username}");
            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "staff.edit")]
        [HttpGet("permissions/{id}")]
        public async Task<IActionResult> Permissions(int? id)
        {
            if (id == null) return NotFound();
            var user = await _context.AdminUsers.FindAsync(id);
            if (user == null) return NotFound();
            
            ViewBag.Branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
            return View(user);
        }

        [AdminAuthorize(Permission = "staff.edit")]
        [HttpPost("permissions/{id}")]
        public async Task<IActionResult> Permissions(int id, List<string> selectedPermissions, int? branchId)
        {
            var user = await _context.AdminUsers.FindAsync(id);
            if (user == null) return NotFound();

            // Cập nhật quyền hạn
            var permDict = new Dictionary<string, bool>();
            foreach (var p in selectedPermissions) permDict[p] = true;
            user.Permissions = permDict;

            // Cập nhật chi nhánh công tác
            user.BranchId = branchId;

            await _context.SaveChangesAsync();

            await LogAction("Cập nhật quyền & Chi nhánh", $"Tài khoản: {user.Username}, Chi nhánh ID: {branchId}");
            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "staff.edit")]
        [HttpGet("permission-matrix")]
        public async Task<IActionResult> PermissionMatrix()
        {
            ViewBag.Roles = new List<string> { "Manager", "Staff" };
            ViewBag.Accounts = await _context.AdminUsers
                .Where(u => u.Role != AdminRole.SuperAdmin)
                .Include(u => u.Branch)
                .OrderBy(u => u.FullName)
                .ToListAsync();
            ViewBag.RoleTemplates = await _context.RolePermissionTemplates.ToListAsync();
            ViewBag.Branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
            return View();
        }

        [AdminAuthorize(Permission = "staff.edit")]
        [HttpPost("permission-matrix/role")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRoleTemplate(string role, List<string> selectedPermissions)
        {
            if (role != "Manager" && role != "Staff")
                return BadRequest("Role không hợp lệ.");

            var permDict = new Dictionary<string, bool>();
            foreach (var p in selectedPermissions ?? new List<string>()) permDict[p] = true;

            if (!ValidateInheritance(permDict, out var error))
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction(nameof(PermissionMatrix));
            }

            var template = await _context.RolePermissionTemplates.FirstOrDefaultAsync(t => t.Role == role);
            if (template == null)
            {
                template = new RolePermissionTemplate { Role = role, Permissions = permDict };
                _context.RolePermissionTemplates.Add(template);
            }
            else
            {
                template.Permissions = permDict;
                template.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _permissionResolve.InvalidateCache(role);

            await LogAction("Cập nhật quyền Role", $"Role: {role}");
            TempData["SuccessMessage"] = $"Đã cập nhật quyền cho role {role}.";
            return RedirectToAction(nameof(PermissionMatrix));
        }

        [AdminAuthorize(Permission = "staff.edit")]
        [HttpPost("permission-matrix/account")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAccountPermissions(int accountId, List<string> selectedPermissions)
        {
            var user = await _context.AdminUsers.FindAsync(accountId);
            if (user == null) return NotFound();

            var permDict = new Dictionary<string, bool>();
            foreach (var p in selectedPermissions ?? new List<string>()) permDict[p] = true;

            if (!ValidateInheritance(permDict, out var error))
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction(nameof(PermissionMatrix));
            }

            user.Permissions = permDict;
            await _context.SaveChangesAsync();

            // Update session if this user is currently logged in
            var currentUserId = HttpContext.Session.GetInt32("AdminUserId");
            if (currentUserId == accountId)
            {
                var permsJson = System.Text.Json.JsonSerializer.Serialize(permDict);
                HttpContext.Session.SetString("AdminPermissions", permsJson);
            }

            await LogAction("Cập nhật quyền Tài khoản", $"Tài khoản: {user.Username}");
            TempData["SuccessMessage"] = $"Đã cập nhật quyền cho {user.FullName}.";
            return RedirectToAction(nameof(PermissionMatrix));
        }

        [AdminAuthorize(Permission = "logs.view")]
        [HttpGet("logs")]
        public async Task<IActionResult> Logs()
        {
            var logs = await _context.ActivityLogs
                .Include(l => l.AdminUser)
                .OrderByDescending(l => l.Timestamp)
                .Take(100)
                .ToListAsync();
            return View(logs);
        }

        private async Task LogAction(string action, string details)
        {
            int? adminId = HttpContext.Session.GetInt32("AdminUserId");
            if (adminId.HasValue)
            {
                var log = new ActivityLog
                {
                    AdminUserId = adminId.Value,
                    Action = action,
                    Target = "Nhân sự",
                    Details = details,
                    Timestamp = DateTime.UtcNow
                };
                _context.ActivityLogs.Add(log);
                await _context.SaveChangesAsync();
            }
        }

        private bool ValidateInheritance(Dictionary<string, bool> perms, out string errorMessage)
        {
            var parentChildMap = new Dictionary<string, string[]>
            {
                ["bookings.view"] = new[] { "bookings.detail", "bookings.create", "bookings.edit", "bookings.delete", "bookings.trash", "bookings.restore" },
                ["branches.view"] = new[] { "branches.detail", "branches.create", "branches.edit", "branches.delete" },
                ["rooms.view"] = new[] { "rooms.detail", "rooms.create", "rooms.edit", "rooms.delete" },
                ["images.view"] = new[] { "images.detail" },
                ["statistics.view"] = new[] { "statistics.export" },
                ["staff.view"] = new[] { "staff.create", "staff.edit", "staff.delete", "staff.permissions", "staff.logs" },
                ["settings.view"] = new[] { "settings.update", "holidays.manage", "slots.manage" },
                ["ai.view"] = new[] { "ai.manage" }
            };

            var moduleNames = new Dictionary<string, string>
            {
                ["bookings"] = "Đơn đặt phòng", ["branches"] = "Chi nhánh", ["rooms"] = "Phòng",
                ["images"] = "Kho ảnh", ["statistics"] = "Thống kê", ["staff"] = "Nhân sự",
                ["settings"] = "Cấu hình", ["ai"] = "AI Brain Center"
            };

            foreach (var kvp in parentChildMap)
            {
                var parentHas = perms.TryGetValue(kvp.Key, out var pv) && pv;
                foreach (var child in kvp.Value)
                {
                    var childHas = perms.TryGetValue(child, out var cv) && cv;
                    if (childHas && !parentHas)
                    {
                        var modName = child.Split('.')[0];
                        var display = moduleNames.TryGetValue(modName, out var dn) ? dn : modName;
                        errorMessage = $"Cần bật Xem {display} trước khi bật quyền này.";
                        return false;
                    }
                }
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
