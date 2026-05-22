# Permission Matrix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace per-account-only permission assignment with a dual-mode matrix (Role template + Account override) with parent-child hierarchy enforcement.

**Architecture:** Add `RolePermissionTemplate` entity for role defaults. `PermissionResolveService` merges role template + account override at runtime. `AdminAuthorizeAttribute` resolves from DI. New matrix view at `/admin/staff/permission-matrix` with toggle between Role and Account modes.

**Tech Stack:** ASP.NET Core MVC, EF Core + Npgsql, MemoryCache, xUnit + EF InMemory for tests.

---

### Task 1: RolePermissionTemplate entity + DbContext

**Files:**
- Create: `WebHomestay/Models/RolePermissionTemplate.cs`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`

- [ ] **Create entity**

```csharp
// WebHomestay/Models/RolePermissionTemplate.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace WebHomestay.Models;

public class RolePermissionTemplate
{
    [Key]
    [StringLength(20)]
    public string Role { get; set; } = string.Empty; // "Manager" or "Staff"

    public string PermissionsJson { get; set; } = "{}";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public Dictionary<string, bool> Permissions
    {
        get
        {
            if (string.IsNullOrEmpty(PermissionsJson)) return new();
            try { return JsonSerializer.Deserialize<Dictionary<string, bool>>(PermissionsJson) ?? new(); }
            catch { return new(); }
        }
        set
        {
            PermissionsJson = JsonSerializer.Serialize(value);
        }
    }
}
```

- [ ] **Register in ApplicationDbContext**

```csharp
// In ApplicationDbContext.cs, add after existing DbSets:
public DbSet<RolePermissionTemplate> RolePermissionTemplates { get; set; }

// In OnModelCreating, add after existing entity configs:
modelBuilder.Entity<RolePermissionTemplate>(entity =>
{
    entity.ToTable("role_permission_templates");
    entity.Property(e => e.Role).HasColumnName("role");
    entity.Property(e => e.PermissionsJson).HasColumnName("permissions_json");
    entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
});
```

- [ ] **Build, create migration, update database**

Run: `dotnet build WebHomestay/WebHomestay.csproj`

```bash
dotnet ef migrations add AddRolePermissionTemplates --project WebHomestay/WebHomestay.csproj
dotnet ef database update --project WebHomestay/WebHomestay.csproj
```

- [ ] **Commit**

```bash
git add WebHomestay/Models/RolePermissionTemplate.cs WebHomestay/Data/ApplicationDbContext.cs
git add WebHomestay/Migrations/
git commit -m "feat: add RolePermissionTemplate entity and migration"
```

---

### Task 2: Data seed for role templates

**Files:**
- Modify: `WebHomestay/Controllers/AdminAccountController.cs` (SeedData method)

- [ ] **Add seed for Manager and Staff templates in SeedData**

After the existing admin user creation block (around line 83), add:

```csharp
// Seed role permission templates
if (!_context.RolePermissionTemplates.Any())
{
    var managerPerms = new Dictionary<string, bool>
    {
        ["matrix.view"] = true,
        ["bookings.view"] = true, ["bookings.detail"] = true, ["bookings.create"] = true, ["bookings.edit"] = true,
        ["branches.view"] = true, ["branches.detail"] = true, ["branches.create"] = true, ["branches.edit"] = true,
        ["rooms.view"] = true, ["rooms.detail"] = true, ["rooms.create"] = true, ["rooms.edit"] = true,
        ["images.view"] = true, ["images.detail"] = true,
        ["statistics.view"] = true, ["statistics.export"] = true,
        ["staff.view"] = true, ["staff.logs"] = true,
        ["settings.view"] = true, ["settings.update"] = true, ["holidays.manage"] = true, ["slots.manage"] = true,
        ["ai.view"] = true, ["ai.manage"] = true
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
```

Add using: `using WebHomestay.Models;` (already present).

- [ ] **Commit**

```bash
git add WebHomestay/Controllers/AdminAccountController.cs
git commit -m "feat: seed Manager and Staff role permission templates"
```

---

### Task 3: PermissionResolveService + PermissionCacheService

**Files:**
- Create: `WebHomestay/Services/IPermissionResolveService.cs`
- Create: `WebHomestay/Services/PermissionResolveService.cs`
- Modify: `WebHomestay/Program.cs`

- [ ] **Create IPermissionResolveService interface**

```csharp
// WebHomestay/Services/IPermissionResolveService.cs
namespace WebHomestay.Services;

public interface IPermissionResolveService
{
    bool HasPermission(HttpContext httpContext, string permissionKey);
    Dictionary<string, bool> GetEffectivePermissions(HttpContext httpContext);
}
```

- [ ] **Create PermissionResolveService**

```csharp
// WebHomestay/Services/PermissionResolveService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Services;

public class PermissionResolveService : IPermissionResolveService
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    private const string CacheKeyPrefix = "role_perms_";

    public PermissionResolveService(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public bool HasPermission(HttpContext httpContext, string permissionKey)
    {
        var role = httpContext.Session.GetString("AdminRole");
        if (role == "SuperAdmin") return true;
        if (string.IsNullOrEmpty(role)) return false;

        var effective = GetEffectivePermissions(httpContext);
        return effective.TryGetValue(permissionKey, out var has) && has;
    }

    public Dictionary<string, bool> GetEffectivePermissions(HttpContext httpContext)
    {
        var role = httpContext.Session.GetString("AdminRole");
        if (role == "SuperAdmin" || string.IsNullOrEmpty(role))
            return new Dictionary<string, bool>();

        var template = GetRoleTemplate(role);

        // Merge with override from session
        var overrideJson = httpContext.Session.GetString("AdminPermissions");
        Dictionary<string, bool>? overrideDict = null;
        if (!string.IsNullOrEmpty(overrideJson))
        {
            try { overrideDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(overrideJson); }
            catch { }
        }

        if (overrideDict == null || overrideDict.Count == 0)
            return template;

        var merged = new Dictionary<string, bool>(template);
        foreach (var kvp in overrideDict)
        {
            merged[kvp.Key] = kvp.Value;
        }
        return merged;
    }

    private Dictionary<string, bool> GetRoleTemplate(string role)
    {
        var cacheKey = CacheKeyPrefix + role;
        if (_cache.TryGetValue(cacheKey, out Dictionary<string, bool>? cached) && cached != null)
            return cached;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var template = context.RolePermissionTemplates.AsNoTracking()
            .FirstOrDefault(t => t.Role == role);

        var result = template?.Permissions ?? new Dictionary<string, bool>();

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    public void InvalidateCache(string role)
    {
        _cache.Remove(CacheKeyPrefix + role);
    }
}
```

- [ ] **Register services in Program.cs**

```csharp
// Add alongside other service registrations:
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IPermissionResolveService, PermissionResolveService>();
```

- [ ] **Commit**

```bash
git add WebHomestay/Services/IPermissionResolveService.cs WebHomestay/Services/PermissionResolveService.cs WebHomestay/Program.cs
git commit -m "feat: add PermissionResolveService with role template cache"
```

---

### Task 4: Update AdminAuthorizeAttribute

**Files:**
- Modify: `WebHomestay/Filters/AdminAuthorizeAttribute.cs`

- [ ] **Replace session-only check with PermissionResolveService**

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebHomestay.Services;

namespace WebHomestay.Filters
{
    public class AdminAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _roles;
        public string? Permission { get; set; }

        public AdminAuthorizeAttribute(params string[] roles)
        {
            _roles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var session = context.HttpContext.Session;
            var user = session.GetString("AdminUser");
            var role = session.GetString("AdminRole");

            if (string.IsNullOrEmpty(user))
            {
                context.Result = new RedirectToActionResult("Login", "AdminAccount", null);
                return;
            }

            if (role == "SuperAdmin") return;

            if (!string.IsNullOrEmpty(Permission))
            {
                var resolveService = context.HttpContext.RequestServices
                    .GetRequiredService<IPermissionResolveService>();
                if (resolveService.HasPermission(context.HttpContext, Permission))
                    return;
            }

            if (_roles.Length > 0 && !_roles.Contains(role))
            {
                context.Result = new ContentResult
                {
                    Content = $"Bạn không có quyền thực hiện hành động này. (Yêu cầu: {Permission ?? string.Join("/", _roles)})",
                    StatusCode = 403
                };
                return;
            }

            if (!string.IsNullOrEmpty(Permission))
            {
                context.Result = new ContentResult
                {
                    Content = $"Bạn không có quyền truy cập tính năng: {Permission}",
                    StatusCode = 403
                };
            }
        }
    }
}
```

- [ ] **Commit**

```bash
git add WebHomestay/Filters/AdminAuthorizeAttribute.cs
git commit -m "feat: AdminAuthorizeAttribute uses PermissionResolveService"
```

---

### Task 5: Update PermissionHelper

**Files:**
- Modify: `WebHomestay/Helpers/PermissionHelper.cs`

- [ ] **Replace static session check with PermissionResolveService**

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WebHomestay.Services;

namespace WebHomestay.Helpers
{
    public static class PermissionHelper
    {
        public static bool HasPermission(HttpContext context, string permission)
        {
            var role = context.Session.GetString("AdminRole");
            if (role == "SuperAdmin") return true;
            if (string.IsNullOrEmpty(role)) return false;

            var resolveService = context.RequestServices.GetRequiredService<IPermissionResolveService>();
            return resolveService.HasPermission(context, permission);
        }
    }
}
```

- [ ] **Commit**

```bash
git add WebHomestay/Helpers/PermissionHelper.cs
git commit -m "feat: PermissionHelper uses PermissionResolveService"
```

---

### Task 6: Fix security gaps — add missing [AdminAuthorize]

**Files:**
- Modify: `WebHomestay/Controllers/AdminSlotsController.cs`
- Modify: `WebHomestay/Controllers/AdminAIController.cs`
- Modify: `WebHomestay/Controllers/AdminSettingsController.cs`
- Modify: `WebHomestay/Controllers/AdminStatisticsController.cs`

- [ ] **Add [AdminAuthorize] class-level to AdminSlotsController**

```csharp
// Add using at top if needed:
using WebHomestay.Filters;

// Add attribute to class:
[AdminAuthorize]
public class AdminSlotsController : Controller
```

- [ ] **Add [AdminAuthorize] class-level to AdminAIController**

```csharp
[AdminAuthorize]
[Route("admin/ai")]
public class AdminAIController : Controller
```

Also add: `using WebHomestay.Filters;`

- [ ] **Add permission keys to AdminSettingsController actions**

```csharp
[AdminAuthorize(Permission = "settings.view")]
[HttpGet("")]
public async Task<IActionResult> Index()

[AdminAuthorize(Permission = "settings.update")]
[HttpPost("update")]
public async Task<IActionResult> Update(string key, string value)

[AdminAuthorize(Permission = "settings.update")]
[HttpPost("update-branch")]
public async Task<IActionResult> UpdateBranch(int branchId, int leadTime)

[AdminAuthorize(Permission = "settings.update")]
[HttpPost("payment-qr")]
public async Task<IActionResult> UpdatePaymentQr(...)
```

- [ ] **Add permission keys to AdminStatisticsController**

```csharp
[AdminAuthorize(Permission = "statistics.view")]
[Route("")]
public IActionResult Index()

[AdminAuthorize(Permission = "statistics.export")]
[HttpGet("Export")]
public async Task<IActionResult> Export(...)
```

Note: `GetStats` and `GetBranches` are JSON API endpoints called from the statistics page — they should use `statistics.view` as well (or just rely on class-level check since Index already requires it). Add permission to them too:

```csharp
[AdminAuthorize(Permission = "statistics.view")]
[HttpGet("GetStats")]
[AdminAuthorize(Permission = "statistics.view")]
[HttpGet("GetBranches")]
```

Wait — multiple `[AdminAuthorize]` attributes on one action won't stack correctly. Instead, make the class-level `[AdminAuthorize]` sufficient. But if we want to be explicit, the class-level already handles it since all these actions require admin login. The action-level `[AdminAuthorize(Permission)]` should only be on actions that need specific permission beyond login. For Index and Export, add them. For GetStats/GetBranches, rely on class-level (they're called from Index via JS, so if user can see Index, they can call these).

Actually, let me keep it simple: only add `[AdminAuthorize(Permission)]` to `Index` and `Export`.

- [ ] **Add `holidays.manage` permission to AdminHolidaysController**

```csharp
// Already has [AdminAuthorize] class-level.
// Change Index to require holidays.manage:
[AdminAuthorize(Permission = "holidays.manage")]
[HttpGet]
public IActionResult Index()
```

Also add to Create, Delete, CreateRange, DeleteByDescription:
```csharp
[AdminAuthorize(Permission = "holidays.manage")]
[HttpPost("create")]
```

(Add this to all POST actions in the controller.)

- [ ] **Commit**

```bash
git add WebHomestay/Controllers/AdminSlotsController.cs WebHomestay/Controllers/AdminAIController.cs WebHomestay/Controllers/AdminSettingsController.cs WebHomestay/Controllers/AdminStatisticsController.cs WebHomestay/Controllers/AdminHolidaysController.cs
git commit -m "fix: add missing [AdminAuthorize] and permission keys to controllers"
```

---

### Task 7: Permission matrix page — Controller (GET/POST)

**Files:**
- Modify: `WebHomestay/Controllers/AdminStaffController.cs`

- [ ] **Add PermissionMatrix GET action — serves the new matrix page**

```csharp
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
    return View();
}
```

- [ ] **Add PermissionMatrix POST — save role template**

```csharp
[AdminAuthorize(Permission = "staff.edit")]
[HttpPost("permission-matrix/role")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SaveRoleTemplate(string role, List<string> selectedPermissions)
{
    if (role != "Manager" && role != "Staff")
        return BadRequest("Role không hợp lệ.");

    var permDict = new Dictionary<string, bool>();
    foreach (var p in selectedPermissions) permDict[p] = true;

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

    var resolveService = HttpContext.RequestServices.GetRequiredService<IPermissionResolveService>();
    resolveService.InvalidateCache(role);

    await LogAction("Cập nhật quyền Role", $"Role: {role}");
    TempData["SuccessMessage"] = $"Đã cập nhật quyền cho role {role}.";
    return RedirectToAction(nameof(PermissionMatrix));
}
```

- [ ] **Add PermissionMatrix POST — save account override**

```csharp
[AdminAuthorize(Permission = "staff.edit")]
[HttpPost("permission-matrix/account")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SaveAccountPermissions(int accountId, List<string> selectedPermissions)
{
    var user = await _context.AdminUsers.FindAsync(accountId);
    if (user == null) return NotFound();

    var permDict = new Dictionary<string, bool>();
    foreach (var p in selectedPermissions) permDict[p] = true;
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
```

Add using: `using WebHomestay.Services;` (for IPermissionResolveService).

- [ ] **Commit**

```bash
git add WebHomestay/Controllers/AdminStaffController.cs
git commit -m "feat: add PermissionMatrix GET/POST actions to AdminStaffController"
```

---

### Task 8: Permission matrix View

**Files:**
- Create: `WebHomestay/Views/AdminStaff/PermissionMatrix.cshtml`

This is the big one. The view has:
- Toggle between "Theo Role" and "Theo Tài khoản"
- Dropdown for role or account selection
- Full permission matrix with parent-child hierarchy
- JS validation for inheritance rules
- Theme consistent with existing admin UI

- [ ] **Create PermissionMatrix.cshtml**

```html
@using WebHomestay.Helpers
@using WebHomestay.Models
@using System.Text.Json
@{
    ViewData["Title"] = "Ma trận phân quyền";
    var roles = (List<string>)ViewBag.Roles;
    var accounts = (List<AdminUser>)ViewBag.Accounts;
    var roleTemplates = (List<RolePermissionTemplate>)ViewBag.RoleTemplates;

    var managerPerms = roleTemplates.FirstOrDefault(t => t.Role == "Manager")?.Permissions ?? new();
    var staffPerms = roleTemplates.FirstOrDefault(t => t.Role == "Staff")?.Permissions ?? new();
}

<link rel="stylesheet" href="~/css/admin-premium.css" asp-append-version="true" />

<style>
  .perm-table th, .perm-table td { vertical-align: middle; text-align: center; }
  .perm-table td:first-child { text-align: left; }
  .perm-child td:first-child { padding-left: 2.5rem !important; }
  .perm-child .module-icon { opacity: 0.5; }
  .perm-child label { font-size: 0.9em; }
  .perm-override { outline: 2px solid var(--luxury-accent, #c8a97e); outline-offset: 2px; }
  .perm-inherited { opacity: 0.45; }
  .badge-mode { font-size: 0.7rem; padding: 0.25rem 0.6rem; }
  .account-search { max-width: 400px; }
</style>

<div class="premium-admin-body">
  <div class="admin-shell">
    <header class="admin-hero">
      <div class="admin-hero-title">
        <span class="admin-kicker">Quản trị bảo mật</span>
        <h1><i class="fas fa-lock me-2"></i>Ma trận phân quyền</h1>
        <p class="text-muted small mt-1">Cấu hình quyền theo vai trò hoặc theo từng tài khoản</p>
      </div>
      <div class="admin-hero-actions">
        <a asp-action="Index" class="btn-premium-outline">
          <i class="fas fa-arrow-left"></i><span>Quay lại</span>
        </a>
      </div>
    </header>

    @if (TempData["SuccessMessage"] != null)
    {
      <div class="alert alert-success alert-dismissible fade show">@TempData["SuccessMessage"]
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
      </div>
    }

    <!-- Mode toggle -->
    <div class="admin-card mb-4 p-4">
      <div class="d-flex align-items-center gap-4 flex-wrap">
        <div class="btn-group" role="group" id="modeToggle">
          <input type="radio" class="btn-check" name="mode" id="modeRole" value="role" checked autocomplete="off">
          <label class="btn btn-outline-primary rounded-pill px-4" for="modeRole">
            <i class="fas fa-users me-1"></i>Theo Role
          </label>
          <input type="radio" class="btn-check" name="mode" id="modeAccount" value="account" autocomplete="off">
          <label class="btn btn-outline-primary rounded-pill px-4" for="modeAccount">
            <i class="fas fa-user me-1"></i>Theo Tài khoản
          </label>
        </div>

        <!-- Role selector (shown in role mode) -->
        <div id="roleSelector" class="d-flex align-items-center gap-2">
          <label class="fw-bold small text-muted">Chọn vai trò:</label>
          <select id="roleSelect" class="form-select form-select-sm border-0 bg-light rounded-pill px-4" style="width:auto;">
            <option value="Manager">Manager</option>
            <option value="Staff">Staff</option>
          </select>
        </div>

        <!-- Account selector (shown in account mode) -->
        <div id="accountSelector" class="d-flex align-items-center gap-2" style="display:none !important;">
          <label class="fw-bold small text-muted">Chọn tài khoản:</label>
          <input type="text" id="accountFilter" class="form-control form-control-sm bg-light rounded-pill px-3 account-search" placeholder="Tìm tên / username..." />
          <select id="accountSelect" class="form-select form-select-sm border-0 bg-light rounded-pill px-3" style="width:auto;min-width:250px;">
            @foreach (var acc in accounts)
            {
              <option value="@acc.Id"
                      data-name="@acc.FullName"
                      data-username="@acc.Username"
                      data-branch="@(acc.Branch?.Name ?? "Tất cả")">
                @acc.FullName (@@@acc.Username) - @(acc.Branch?.Name ?? "Tất cả CN")
              </option>
            }
          </select>
        </div>
      </div>
    </div>

    <!-- Permission Matrix Table -->
    <form id="permForm" method="post">
      <div class="admin-card">
        <div class="admin-table-responsive">
          <table class="admin-table perm-table" id="permMatrix">
            <thead>
              <tr>
                <th class="ps-4" style="width:320px;">Module / Tính năng</th>
                <th class="text-center" title="Xem">Xem</th>
                <th class="text-center" title="Xem chi tiết">Xem CT</th>
                <th class="text-center" title="Thêm">Thêm</th>
                <th class="text-center" title="Sửa">Sửa</th>
                <th class="text-center" title="Xóa">Xóa</th>
                <th class="text-center" title="Khác">Khác</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @{
                var modules = new (string id, string name, string icon, bool hasDetail, bool hasCreate, bool hasEdit, bool hasDelete, string[] extras)[]
                {
                  ("matrix", "Ma trận vận hành", "fa-th", false, false, false, false, Array.Empty<string>()),
                  ("bookings", "Đơn đặt phòng", "fa-calendar-check", true, true, true, true, new[]{"trash","restore"}),
                  ("branches", "Chi nhánh", "fa-building", true, true, true, true, Array.Empty<string>()),
                  ("rooms", "Phòng", "fa-door-open", true, true, true, true, Array.Empty<string>()),
                  ("images", "Kho ảnh", "fa-images", true, false, false, false, Array.Empty<string>()),
                  ("statistics", "Thống kê", "fa-chart-bar", false, false, false, false, new[]{"export"}),
                  ("staff", "Nhân sự", "fa-users-cog", false, true, true, true, new[]{"permissions","logs"}),
                  ("settings", "Cấu hình", "fa-sliders-h", false, false, false, false, new[]{"update","holidays","slots"}),
                  ("ai", "AI Brain Center", "fa-brain", false, false, false, false, new[]{"manage"})
                };

                var permOrder = new (string id, string label)[]
                {
                  ("view", "Xem"),
                  ("detail", "CT"),
                  ("create", "Thêm"),
                  ("edit", "Sửa"),
                  ("delete", "Xóa"),
                };
              }

              @foreach (var mod in modules)
              {
                var parentKey = mod.id + ".view";
                var rowClass = "perm-parent";
                var colspan = 5 + (mod.extras.Length > 0 ? 1 : 0);

                <tr class="@rowClass" data-module="@mod.id">
                  <td class="ps-4 py-3">
                    <div class="d-flex align-items-center">
                      <div class="bg-primary-soft text-primary rounded d-flex align-items-center justify-content-center me-3" style="width:32px;height:32px;">
                        <i class="fas @mod.icon"></i>
                      </div>
                      <span class="fw-bold">@mod.id.ToUpper() - @mod.name</span>
                    </div>
                  </td>
                  <!-- View (parent) -->
                  <td><input type="checkbox" class="perm-check parent-check" data-module="@mod.id" data-key="@parentKey" value="@parentKey" name="selectedPermissions"></td>
                  <!-- Detail -->
                  @if (mod.hasDetail)
                  {
                    <td><input type="checkbox" class="perm-check child-check" data-module="@mod.id" data-key="@(mod.id).detail" value="@(mod.id).detail" name="selectedPermissions"></td>
                  }
                  else
                  {
                    <td><span class="text-muted small">—</span></td>
                  }
                  <!-- Create -->
                  @if (mod.hasCreate)
                  {
                    <td><input type="checkbox" class="perm-check child-check" data-module="@mod.id" data-key="@(mod.id).create" value="@(mod.id).create" name="selectedPermissions"></td>
                  }
                  else
                  {
                    <td><span class="text-muted small">—</span></td>
                  }
                  <!-- Edit -->
                  @if (mod.hasEdit)
                  {
                    <td><input type="checkbox" class="perm-check child-check" data-module="@mod.id" data-key="@(mod.id).edit" value="@(mod.id).edit" name="selectedPermissions"></td>
                  }
                  else
                  {
                    <td><span class="text-muted small">—</span></td>
                  }
                  <!-- Delete -->
                  @if (mod.hasDelete)
                  {
                    <td><input type="checkbox" class="perm-check child-check" data-module="@mod.id" data-key="@(mod.id).delete" value="@(mod.id).delete" name="selectedPermissions"></td>
                  }
                  else
                  {
                    <td><span class="text-muted small">—</span></td>
                  }
                  <!-- Extras -->
                  <td>
                    @if (mod.extras.Length > 0)
                    {
                      <div class="d-flex gap-1 flex-wrap justify-content-center">
                        @foreach (var extra in mod.extras)
                        {
                          <span class="badge bg-light text-dark border small d-flex align-items-center gap-1">
                            @extra
                            <input type="checkbox" class="perm-check child-check" data-module="@mod.id" data-key="@(mod.id).@extra" value="@(mod.id).@extra" name="selectedPermissions" style="width:14px;height:14px;">
                          </span>
                        }
                      </div>
                    }
                    else
                    {
                      <span class="text-muted small">—</span>
                    }
                  </td>
                  <td class="text-end pe-3">
                    <button type="button" class="btn btn-sm btn-outline-secondary border-0 module-toggle-all" data-module="@mod.id" title="Bật tất cả module này"><i class="fas fa-check-double"></i></button>
                    <button type="button" class="btn btn-sm btn-outline-secondary border-0 module-toggle-none" data-module="@mod.id" title="Tắt tất cả module này"><i class="fas fa-times"></i></button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>

      <div class="mt-4 d-flex gap-3 justify-content-center align-items-center">
        <button type="submit" class="btn-premium px-5 py-2 fw-bold shadow-sm" id="saveBtn">
          <i class="fas fa-save"></i><span>LƯU THAY ĐỔI</span>
        </button>
      </div>
    </form>
  </div>
</div>

@section Scripts {
<script>
  (function() {
    // ---- State ----
    var currentMode = 'role'; // 'role' or 'account'
    var currentRole = 'Manager';
    var currentAccountId = null;

    // Permission data per role (loaded from server dicts)
    var roleData = {
      Manager: @Html.Raw(JsonSerializer.Serialize(managerPerms)),
      Staff: @Html.Raw(JsonSerializer.Serialize(staffPerms))
    };

    // Account override data (loaded from server side into data attributes)
    var accountOverrides = {};
    @foreach (var acc in accounts)
    {
      var overrideDict = acc.Permissions ?? new Dictionary<string, bool>();
      <text>accountOverrides[@acc.Id] = @Html.Raw(JsonSerializer.Serialize(overrideDict));</text>
    }

    // ---- DOM refs ----
    var $ = function(id) { return document.getElementById(id); };
    var modeRole = $('modeRole');
    var modeAccount = $('modeAccount');
    var roleSelector = $('roleSelector');
    var accountSelector = $('accountSelector');
    var roleSelect = $('roleSelect');
    var accountSelect = $('accountSelect');
    var accountFilter = $('accountFilter');
    var permForm = $('permForm');

    // ---- Mode toggle ----
    function setMode(mode) {
      currentMode = mode;
      roleSelector.style.display = mode === 'role' ? 'flex' : 'none';
      accountSelector.style.display = mode === 'account' ? 'flex' : 'none';

      if (mode === 'role') {
        permForm.action = '@Url.Action("SaveRoleTemplate", "AdminStaff")';
        renderMatrixForRole(currentRole);
      } else {
        permForm.action = '@Url.Action("SaveAccountPermissions", "AdminStaff")';
        if (accountSelect.value) {
          renderMatrixForAccount(parseInt(accountSelect.value));
        }
      }
    }

    modeRole.addEventListener('change', function() { if (this.checked) setMode('role'); });
    modeAccount.addEventListener('change', function() { if (this.checked) setMode('account'); });

    // ---- Role selection ----
    roleSelect.addEventListener('change', function() {
      currentRole = this.value;
      renderMatrixForRole(currentRole);
    });

    // ---- Account filter ----
    accountFilter.addEventListener('input', function() {
      var q = this.value.toLowerCase();
      var opts = accountSelect.options;
      for (var i = 0; i < opts.length; i++) {
        var text = opts[i].text.toLowerCase();
        opts[i].style.display = text.includes(q) ? '' : 'none';
      }
      if (opts.length > 0 && opts[0].style.display !== 'none') {
        accountSelect.selectedIndex = 0;
        accountSelect.dispatchEvent(new Event('change'));
      }
    });

    // ---- Account selection ----
    accountSelect.addEventListener('change', function() {
      if (this.value) {
        currentAccountId = parseInt(this.value);
        renderMatrixForAccount(currentAccountId);
      }
    });

    // ---- Render matrix for a role ----
    function renderMatrixForRole(role) {
      var perms = roleData[role] || {};
      applyPermsToMatrix(perms, false);
    }

    // ---- Render matrix for an account (template + override) ----
    function renderMatrixForAccount(accountId) {
      var role = getAccountRole(accountId);
      var templatePerms = roleData[role] || {};
      var overridePerms = accountOverrides[accountId] || {};
      applyPermsWithOverride(templatePerms, overridePerms);
    }

    function getAccountRole(accountId) {
      @foreach (var acc in accounts)
      {
        <text>if (accountId === @acc.Id) return '@acc.Role';</text>
      }
      return 'Staff';
    }

    // ---- Apply permissions (flat) ----
    function applyPermsToMatrix(perms, isOverride) {
      var checks = document.querySelectorAll('.perm-check');
      for (var i = 0; i < checks.length; i++) {
        var key = checks[i].value;
        if (perms[key]) {
          checks[i].checked = true;
          checks[i].classList.remove('perm-inherited');
          if (isOverride) checks[i].classList.add('perm-override');
          else checks[i].classList.remove('perm-override');
        } else {
          checks[i].checked = false;
          checks[i].classList.remove('perm-override', 'perm-inherited');
        }
      }
      validateInheritance();
    }

    // ---- Apply with override visual distinction ----
    function applyPermsWithOverride(template, override) {
      var checks = document.querySelectorAll('.perm-check');
      for (var i = 0; i < checks.length; i++) {
        var key = checks[i].value;
        var inTemplate = !!template[key];
        var inOverride = !!override[key];

        if (inOverride) {
          checks[i].checked = true;
          checks[i].classList.add('perm-override');
          checks[i].classList.remove('perm-inherited');
        } else if (inTemplate) {
          checks[i].checked = true;
          checks[i].classList.remove('perm-override');
          checks[i].classList.add('perm-inherited');
        } else {
          checks[i].checked = false;
          checks[i].classList.remove('perm-override', 'perm-inherited');
        }
      }
      validateInheritance();
    }

    // ---- Inheritance validation ----
    function validateInheritance() {
      var modules = document.querySelectorAll('.perm-parent');
      for (var i = 0; i < modules.length; i++) {
        var mod = modules[i].dataset.module;
        var parentCheck = document.querySelector('.parent-check[data-module="' + mod + '"]');
        var childChecks = document.querySelectorAll('.child-check[data-module="' + mod + '"]');

        if (parentCheck && !parentCheck.checked) {
          for (var j = 0; j < childChecks.length; j++) {
            if (childChecks[j].checked) {
              // Auto-uncheck child if parent unchecked
              childChecks[j].checked = false;
            }
          }
        }
      }
    }

    // ---- Intercept check changes for inheritance ----
    document.addEventListener('change', function(e) {
      if (e.target.classList.contains('perm-check')) {
        var mod = e.target.dataset.module;
        var key = e.target.dataset.key;
        var isParent = e.target.classList.contains('parent-check');

        if (isParent) {
          // If parent unchecked, uncheck all children
          if (!e.target.checked) {
            var children = document.querySelectorAll('.child-check[data-module="' + mod + '"]');
            for (var i = 0; i < children.length; i++) {
              children[i].checked = false;
            }
            showToast('Đã tắt quyền ' + getModuleName(mod) + ' — các quyền con cũng bị tắt theo.');
          }
        } else {
          // If child checked but parent unchecked, block
          var parentCheck = document.querySelector('.parent-check[data-module="' + mod + '"]');
          if (e.target.checked && parentCheck && !parentCheck.checked) {
            e.target.checked = false;
            showToast('Cần bật Xem ' + getModuleName(mod) + ' trước khi bật quyền này.');
          }
        }
      }
    });

    function getModuleName(mod) {
      var names = {
        'matrix': 'Ma trận vận hành',
        'bookings': 'Đơn đặt phòng',
        'branches': 'Chi nhánh',
        'rooms': 'Phòng',
        'images': 'Kho ảnh',
        'statistics': 'Thống kê',
        'staff': 'Nhân sự',
        'settings': 'Cấu hình',
        'ai': 'AI Brain Center'
      };
      return names[mod] || mod;
    }

    // ---- Module toggle all/none ----
    document.addEventListener('click', function(e) {
      if (e.target.closest('.module-toggle-all')) {
        var btn = e.target.closest('.module-toggle-all');
        var mod = btn.dataset.module;
        var checks = document.querySelectorAll('.perm-check[data-module="' + mod + '"]');
        for (var i = 0; i < checks.length; i++) checks[i].checked = true;
        validateInheritance();
      }
      if (e.target.closest('.module-toggle-none')) {
        var btn = e.target.closest('.module-toggle-none');
        var mod = btn.dataset.module;
        var checks = document.querySelectorAll('.perm-check[data-module="' + mod + '"]');
        for (var i = 0; i < checks.length; i++) checks[i].checked = false;
      }
    });

    // ---- Toast notification ----
    function showToast(msg) {
      var container = document.querySelector('.admin-hero') || document.querySelector('.admin-shell');
      var toast = document.createElement('div');
      toast.className = 'alert alert-warning alert-dismissible fade show mt-2';
      toast.innerHTML = '<i class="fas fa-exclamation-triangle me-2"></i>' + msg +
        '<button type="button" class="btn-close" data-bs-dismiss="alert"></button>';
      container.after(toast);
      setTimeout(function() { toast.remove(); }, 4000);
    }

    // ---- Server-side validation before submit ----
    permForm.addEventListener('submit', function(e) {
      if (currentMode === 'account' && !accountSelect.value) {
        e.preventDefault();
        showToast('Vui lòng chọn tài khoản trước khi lưu.');
        return;
      }

      // Check inheritance on all modules
      var errors = [];
      var modules = document.querySelectorAll('.perm-parent');
      for (var i = 0; i < modules.length; i++) {
        var mod = modules[i].dataset.module;
        var parentCheck = document.querySelector('.parent-check[data-module="' + mod + '"]');
        var childChecks = document.querySelectorAll('.child-check[data-module="' + mod + '"]:checked');
        if (childChecks.length > 0 && parentCheck && !parentCheck.checked) {
          errors.push(getModuleName(mod));
        }
      }
      if (errors.length > 0) {
        e.preventDefault();
        showToast('Lỗi: Cần bật Xem cho các module: ' + errors.join(', '));
        return;
      }

      // For account mode, ensure only checked items are submitted as override
      if (currentMode === 'account') {
        // No additional processing needed — the form submits selectedPermissions
      }

      // Append mode identifier for server
      var input = document.createElement('input');
      input.type = 'hidden';
      input.name = '__mode';
      input.value = currentMode;
      permForm.appendChild(input);

      if (currentMode === 'account') {
        var accInput = document.createElement('input');
        accInput.type = 'hidden';
        accInput.name = 'accountId';
        accInput.value = accountSelect.value;
        permForm.appendChild(accInput);
      } else {
        var roleInput = document.createElement('input');
        roleInput.type = 'hidden';
        roleInput.name = 'role';
        roleInput.value = roleSelect.value;
        permForm.appendChild(roleInput);
      }
    });

    // ---- Init ----
    setMode('role');
  })();
</script>
}
```

- [ ] **Commit**

```bash
git add WebHomestay/Views/AdminStaff/PermissionMatrix.cshtml
git commit -m "feat: add permission matrix view with inheritance validation"
```

---

### Task 9: Add server-side inheritance validation + refactor POST actions

**Files:**
- Modify: `WebHomestay/Controllers/AdminStaffController.cs`

Note: The current POST actions already save selectedPermissions as flat list. We need to add server-side inheritance validation.

- [ ] **Add validation helper and inject into both POST actions**

Add this private method to AdminStaffController:

```csharp
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
```

- [ ] **Update SaveRoleTemplate to validate before saving**

```csharp
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

    var resolveService = HttpContext.RequestServices.GetRequiredService<IPermissionResolveService>();
    resolveService.InvalidateCache(role);

    await LogAction("Cập nhật quyền Role", $"Role: {role}");
    TempData["SuccessMessage"] = $"Đã cập nhật quyền cho role {role}.";
    return RedirectToAction(nameof(PermissionMatrix));
}
```

- [ ] **Update SaveAccountPermissions to validate before saving**

```csharp
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
```

- [ ] **Commit**

```bash
git add WebHomestay/Controllers/AdminStaffController.cs
git commit -m "feat: add server-side inheritance validation to permission matrix"
```

---

### Task 10: Update menu + staff index page

**Files:**
- Modify: `WebHomestay/Views/Shared/_Layout.cshtml`
- Modify: `WebHomestay/Views/AdminStaff/Index.cshtml`

- [ ] **Update _Layout.cshtml — replace SuperAdmin hardcodes with permission keys**

```html
@* AI Brain Center — change from role check to permission check *@
@if (WebHomestay.Helpers.PermissionHelper.HasPermission(Context, "ai.view"))
{
    <li><a class="dropdown-item py-2" asp-controller="AdminAI" asp-action="Index"><i class="fas fa-brain me-2 text-muted"></i>AI Brain Center</a></li>
}

@* Cấu hình hệ thống *@
@if (WebHomestay.Helpers.PermissionHelper.HasPermission(Context, "settings.view"))
{
    <li><a class="dropdown-item py-2" asp-controller="AdminSettings" asp-action="Index"><i class="fas fa-sliders-h me-2 text-muted"></i>Cấu hình hệ thống</a></li>
}
```

- [ ] **Update AdminStaff/Index.cshtml — add link to PermissionMatrix**

Find where other action buttons are and add:
```html
@if (WebHomestay.Helpers.PermissionHelper.HasPermission(Context, "staff.edit"))
{
    <a asp-action="PermissionMatrix" class="btn-premium-outline">
        <i class="fas fa-lock"></i><span>Ma trận phân quyền</span>
    </a>
}
```

- [ ] **Commit**

```bash
git add WebHomestay/Views/Shared/_Layout.cshtml WebHomestay/Views/AdminStaff/Index.cshtml
git commit -m "feat: update menu permissions and add matrix link to staff page"
```

---

### Task 11: Update login to load role template defaults into session

**Files:**
- Modify: `WebHomestay/Controllers/AdminAccountController.cs`

Currently after login, we store only the account override in session as `AdminPermissions`. We should keep this (it's still needed for the override merge). No change needed to login — the PermissionResolveService handles merging at runtime.

But we should also store the user's role string in session (already done: `AdminRole`).

- [ ] **Verify login session setup is sufficient**

The current login code already stores:
- `AdminUser` — username
- `AdminRole` — "SuperAdmin"/"Manager"/"Staff"
- `AdminUserId` — id
- `AdminBranchId` — branch id
- `AdminPermissions` — JSON of account's override

These are sufficient. PermissionResolveService uses `AdminRole` to load template and merges with `AdminPermissions`.

No code changes needed for this task. Just verify.

- [ ] **Commit (just a note — no actual change)**

No commit needed — verified login flow is compatible.

---

### Task 12: Tests

**Files:**
- Create: `WebHomestay.Tests/Services/PermissionResolveServiceTests.cs`
- Create: `WebHomestay.Tests/Domain/PermissionInheritanceTests.cs`

- [ ] **Test permission resolve: SuperAdmin bypass**

```csharp
// WebHomestay.Tests/Services/PermissionResolveServiceTests.cs
using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WebHomestay.Services;
using WebHomestay.Data;
using WebHomestay.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace WebHomestay.Tests.Services;

public class PermissionResolveServiceTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var ctx = new ApplicationDbContext(options);
        ctx.Database.EnsureCreated();

        ctx.RolePermissionTemplates.AddRange(
            new RolePermissionTemplate
            {
                Role = "Manager",
                Permissions = new Dictionary<string, bool> { ["rooms.view"] = true, ["rooms.edit"] = true }
            },
            new RolePermissionTemplate
            {
                Role = "Staff",
                Permissions = new Dictionary<string, bool> { ["rooms.view"] = true }
            }
        );
        ctx.SaveChanges();
        return ctx;
    }

    private (PermissionResolveService, DefaultHttpContext) CreateService(ApplicationDbContext ctx, string role, Dictionary<string, bool>? overridePerms = null)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var scopeFactory = new Mock<IServiceScopeFactory>();

        var scope = new Mock<IServiceScope>();
        var sp = new Mock<IServiceProvider>();
        sp.Setup(x => x.GetService(typeof(ApplicationDbContext))).Returns(ctx);
        scope.Setup(x => x.ServiceProvider).Returns(sp.Object);
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Session = new TestSession();
        httpContext.Session.SetString("AdminRole", role);

        if (overridePerms != null)
        {
            httpContext.Session.SetString("AdminPermissions", JsonSerializer.Serialize(overridePerms));
        }

        var service = new PermissionResolveService(cache, scopeFactory.Object);
        return (service, httpContext);
    }

    [Fact]
    public void SuperAdmin_Always_HasPermission()
    {
        var ctx = CreateDbContext();
        var (service, http) = CreateService(ctx, "SuperAdmin");
        Assert.True(service.HasPermission(http, "anything.at.all"));
    }

    [Fact]
    public void Manager_Uses_RoleTemplate()
    {
        var ctx = CreateDbContext();
        var (service, http) = CreateService(ctx, "Manager");
        Assert.True(service.HasPermission(http, "rooms.view"));
        Assert.True(service.HasPermission(http, "rooms.edit"));
        Assert.False(service.HasPermission(http, "rooms.delete"));
    }

    [Fact]
    public void Staff_Uses_RoleTemplate()
    {
        var ctx = CreateDbContext();
        var (service, http) = CreateService(ctx, "Staff");
        Assert.True(service.HasPermission(http, "rooms.view"));
        Assert.False(service.HasPermission(http, "rooms.edit"));
    }

    [Fact]
    public void Override_Wins_Over_Template()
    {
        var ctx = CreateDbContext();
        var overrides = new Dictionary<string, bool> { ["rooms.edit"] = true };
        var (service, http) = CreateService(ctx, "Staff", overrides);
        Assert.True(service.HasPermission(http, "rooms.edit"));
    }

    [Fact]
    public void Override_Can_Remove_Template_Permission()
    {
        var ctx = CreateDbContext();
        var overrides = new Dictionary<string, bool> { ["rooms.view"] = false };
        var (service, http) = CreateService(ctx, "Manager", overrides);
        Assert.False(service.HasPermission(http, "rooms.view"));
    }
}
```

- [ ] **Run tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~PermissionResolveServiceTests`
Expected: All 5 tests pass.

- [ ] **Add TestSession helper if not already in project**

Check if `WebHomestay.Tests` has a `TestSession` class:

```csharp
// If not present, add WebHomestay.Tests/TestSession.cs
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace WebHomestay.Tests;

public class TestSession : ISession
{
    private Dictionary<string, byte[]> _data = new();

    public string Id => "test";
    public bool IsAvailable => true;
    public IEnumerable<string> Keys => _data.Keys;

    public void Clear() => _data.Clear();
    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Remove(string key) => _data.Remove(key);
    public void Set(string key, byte[] value) => _data[key] = value;
    public bool TryGetValue(string key, out byte[]? value) => _data.TryGetValue(key, out value);

    public void SetString(string key, string value) => Set(key, System.Text.Encoding.UTF8.GetBytes(value));
    public string? GetString(string key) => TryGetValue(key, out var bytes) ? System.Text.Encoding.UTF8.GetString(bytes) : null;
    public void SetInt32(string key, int value) => Set(key, BitConverter.GetBytes(value));
    public int? GetInt32(string key) => TryGetValue(key, out var bytes) && bytes.Length >= 4 ? BitConverter.ToInt32(bytes) : null;
}
```

- [ ] **Test inheritance validation**

```csharp
// WebHomestay.Tests/Domain/PermissionInheritanceTests.cs
using Xunit;
using System.Collections.Generic;

namespace WebHomestay.Tests.Domain;

public class PermissionInheritanceTests
{
    private static bool ValidateInheritance(Dictionary<string, bool> perms, out string errorMessage)
    {
        var parentChildMap = new Dictionary<string, string[]>
        {
            ["bookings.view"] = new[] { "bookings.detail", "bookings.create", "bookings.edit", "bookings.delete", "bookings.trash", "bookings.restore" },
            ["branches.view"] = new[] { "branches.detail", "branches.create", "branches.edit", "branches.delete" },
            ["rooms.view"] = new[] { "rooms.detail", "rooms.create", "rooms.edit", "rooms.delete" },
            ["images.view"] = new[] { "images.detail" },
            ["staff.view"] = new[] { "staff.create", "staff.edit", "staff.delete", "staff.permissions", "staff.logs" },
            ["settings.view"] = new[] { "settings.update", "holidays.manage", "slots.manage" },
            ["ai.view"] = new[] { "ai.manage" }
        };

        foreach (var kvp in parentChildMap)
        {
            var parentHas = perms.TryGetValue(kvp.Key, out var pv) && pv;
            foreach (var child in kvp.Value)
            {
                var childHas = perms.TryGetValue(child, out var cv) && cv;
                if (childHas && !parentHas)
                {
                    errorMessage = "Inheritance violation";
                    return false;
                }
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    [Fact]
    public void Valid_Permissions_Pass()
    {
        var perms = new Dictionary<string, bool>
        {
            ["rooms.view"] = true,
            ["rooms.edit"] = true,
            ["bookings.view"] = true,
            ["bookings.detail"] = true
        };
        Assert.True(ValidateInheritance(perms, out _));
    }

    [Fact]
    public void Child_Without_Parent_Fails()
    {
        var perms = new Dictionary<string, bool>
        {
            ["rooms.edit"] = true
        };
        Assert.False(ValidateInheritance(perms, out _));
    }

    [Fact]
    public void Parent_Alone_Is_Valid()
    {
        var perms = new Dictionary<string, bool>
        {
            ["rooms.view"] = true
        };
        Assert.True(ValidateInheritance(perms, out _));
    }

    [Fact]
    public void Multiple_Children_All_Need_Parent()
    {
        var perms = new Dictionary<string, bool>
        {
            ["rooms.view"] = true,
            ["rooms.detail"] = true,
            ["rooms.edit"] = true,
            ["bookings.detail"] = true
        };
        Assert.False(ValidateInheritance(perms, out _));
    }
}
```

- [ ] **Run tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj`
Expected: All tests pass (existing + new).

- [ ] **Commit**

```bash
git add WebHomestay.Tests/Services/PermissionResolveServiceTests.cs WebHomestay.Tests/Domain/PermissionInheritanceTests.cs
git commit -m "test: add permissions resolve and inheritance tests"
```
