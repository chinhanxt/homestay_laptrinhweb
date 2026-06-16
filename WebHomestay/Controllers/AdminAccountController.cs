using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Controllers
{
    [Route("admin")]
    public class AdminAccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminAccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(string username, string password)
        {
            var user = await _context.AdminUsers
                .Include(u => u.Branch)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user != null && (user.PasswordHash == password || BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)))
            {
                HttpContext.Session.SetString("AdminUser", user.Username);
                HttpContext.Session.SetString("AdminRole", user.Role.ToString());
                HttpContext.Session.SetInt32("AdminUserId", user.Id);
                if (user.BranchId.HasValue) 
                    HttpContext.Session.SetInt32("AdminBranchId", user.BranchId.Value);

                // Store detailed permissions in session as JSON
                var permsJson = System.Text.Json.JsonSerializer.Serialize(user.Permissions);
                HttpContext.Session.SetString("AdminPermissions", permsJson);

                // Redirect based on role
                if (user.Role == AdminRole.SuperAdmin)
                {
                    return RedirectToAction("Index", "AdminBranches");
                }
                else
                {
                    return RedirectToAction("Index", "AdminMatrix");
                }
            }

            ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không chính xác.";
            return View();
        }

        [HttpGet("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("AdminUser");
            HttpContext.Session.Remove("AdminRole");
            HttpContext.Session.Remove("AdminUserId");
            HttpContext.Session.Remove("AdminBranchId");
            return RedirectToAction("Index", "Home");
        }

        [HttpGet("seed")]
        public async Task<IActionResult> SeedData()
        {
            // 1. Create Default SuperAdmin
            if (!_context.AdminUsers.Any(u => u.Username == "admin"))
            {
                var admin = new AdminUser
                {
                    Username = "admin",
                    FullName = "Hệ thống Admin",
                    Role = AdminRole.SuperAdmin,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    CreatedAt = DateTime.UtcNow
                };
                _context.AdminUsers.Add(admin);
            }

            // 2. Seed role permission templates
            if (!_context.RolePermissionTemplates.Any())
            {
                var managerPerms = new Dictionary<string, bool>
                {
                    ["matrix.view"] = true,
                    ["bookings.view"] = true, ["bookings.detail"] = true, ["bookings.create"] = true, ["bookings.edit"] = true,
                    ["branches.view"] = true, ["branches.detail"] = true, ["branches.create"] = true, ["branches.edit"] = true, ["branches.trash"] = true, ["branches.restore"] = true,
                    ["rooms.view"] = true, ["rooms.detail"] = true, ["rooms.create"] = true, ["rooms.edit"] = true, ["rooms.trash"] = true, ["rooms.restore"] = true,
                    ["images.view"] = true, ["images.detail"] = true, ["images.trash"] = true, ["images.restore"] = true,
                    ["statistics.view"] = true, ["statistics.export"] = true,
                    ["staff.view"] = true, ["staff.trash"] = true, ["staff.restore"] = true, ["logs.view"] = true,
                    ["settings.view"] = true, ["settings.update"] = true, ["holidays.manage"] = true, ["slots.manage"] = true, ["branch.settings"] = true, ["payment.settings"] = true,
                    ["ai.view"] = true, ["ai.create"] = true, ["ai.edit"] = true, ["ai.delete"] = true, ["ai.knowledge"] = true, ["ai.graph"] = true, ["ai.response"] = true
                };
                var staffPerms = new Dictionary<string, bool>
                {
                    ["matrix.view"] = true,
                    ["bookings.view"] = true,
                    ["branches.view"] = true,
                    ["rooms.view"] = true,
                    ["images.view"] = true,
                    ["staff.view"] = true
                };

                _context.RolePermissionTemplates.AddRange(
                    new RolePermissionTemplate { Role = "Manager", Permissions = managerPerms },
                    new RolePermissionTemplate { Role = "Staff", Permissions = staffPerms }
                );
            }

            // 3. Create Branches & Rooms if empty
            if (!_context.Branches.Any())
            {
                var b1 = new Branch { Name = "StayEasy Sài Gòn", Address = "123 Lê Lợi, Quận 1, TP.HCM", Hotline = "0901234567", Description = "Chi nhánh trung tâm sầm uất." };
                var b2 = new Branch { Name = "StayEasy Đà Lạt", Address = "45 Khởi Nghĩa Bắc Sơn, Đà Lạt", Hotline = "0907654321", Description = "Không gian thơ mộng giữa rừng thông." };
                _context.Branches.AddRange(b1, b2);
                await _context.SaveChangesAsync();

                var r1 = new Room { Name = "Phòng Deluxe City View", BranchId = b1.Id, PricePerDay = 850000, PricePerHour = 120000, Capacity = 2, MaxGuests = 3, Status = "Available", Description = "View toàn cảnh thành phố, đầy đủ tiện nghi cao cấp.", ImageUrl = "https://images.unsplash.com/photo-1566665797739-1674de7a421a?w=800" };
                var r2 = new Room { Name = "Phòng Studio Minimalist", BranchId = b1.Id, PricePerDay = 650000, PricePerHour = 90000, Capacity = 2, MaxGuests = 2, Status = "Available", Description = "Thiết kế tối giản, hiện đại, phù hợp cho cặp đôi.", ImageUrl = "https://images.unsplash.com/photo-1522771739844-6a9f6d5f14af?w=800" };
                var r3 = new Room { Name = "Phòng Gác Mái Rừng Thông", BranchId = b2.Id, PricePerDay = 950000, PricePerHour = 150000, Capacity = 2, MaxGuests = 4, Status = "Available", Description = "Trải nghiệm ngủ giữa ngàn thông, ban công cực chill.", ImageUrl = "https://images.unsplash.com/photo-1512918766671-ad650b9b732d?w=800" };
                
                _context.Rooms.AddRange(r1, r2, r3);
            }

            // 3. Seed AdditionalImages for Saigon rooms if missing
            var saigonRooms = _context.Branches
                .Where(b => b.Name.Contains("Sài Gòn"))
                .SelectMany(b => b.Rooms)
                .Where(r => string.IsNullOrEmpty(r.AdditionalImages))
                .ToList();
            if (saigonRooms.Any())
            {
                var unsplashBase = "https://images.unsplash.com/photo-";
                var roomImages = new Dictionary<string, List<string>>
                {
                    ["Deluxe City View"] = new()
                    {
                        unsplashBase + "1566665797739-1674de7a421a?w=800",
                        unsplashBase + "1618773928121-c08142c1e7c7?w=800",
                        unsplashBase + "1598928506311-c55ez3f1b1b1?w=800",
                        unsplashBase + "1600585154340-be6161a56a0c?w=800",
                        unsplashBase + "1560448204-603b3fc33ddc?w=800"
                    },
                    ["Studio Minimalist"] = new()
                    {
                        unsplashBase + "1522771739844-6a9f6d5f14af?w=800",
                        unsplashBase + "1586023492125-27c2c045212b?w=800",
                        unsplashBase + "1560448204-603b3fc33ddc?w=800",
                        unsplashBase + "1598928506311-c55ez3f1b1b1?w=800",
                        unsplashBase + "1600585154340-be6161a56a0c?w=800"
                    }
                };
                foreach (var room in saigonRooms)
                {
                    var key = roomImages.Keys.FirstOrDefault(k => room.Name.Contains(k));
                    if (key != null)
                    {
                        room.AdditionalImages = System.Text.Json.JsonSerializer.Serialize(roomImages[key]);
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Content("Đã cài đặt dữ liệu mẫu và tài khoản Admin thành công! (Tài khoản: admin / admin123)");
        }
    }
}
