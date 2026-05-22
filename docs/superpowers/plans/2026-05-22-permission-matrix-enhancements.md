# Permission Matrix Enhancements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the permission matrix with UI toggle fixes, missing settings permissions (branch + payment), and full AI Brain Center CRUD + tab permissions.

**Architecture:** Frontend changes to PermissionMatrix.cshtml (mode toggle visibility, extras key fix, module definitions), backend permission key updates on AdminSettingsController and AdminAIController, ValidateInheritance mapping update, seed data update.

**Tech Stack:** ASP.NET Core MVC, Razor, JavaScript, xUnit + EF InMemory

---

### Task 1: Fix extras key generation in PermissionMatrix.cshtml

**Files:**
- Modify: `WebHomestay/Views/AdminStaff/PermissionMatrix.cshtml:197-199`

Pre-existing bug: extras render `settings.holidays` but backend checks `holidays.manage`. Fix: use full key when extra.key contains `.`, prepend mod.id otherwise.

- [ ] **Replace data-key and value generation for extras**

In PermissionMatrix.cshtml, find the extras rendering block (around lines 197-199). Change:
```html
data-key="@(mod.id).@extra.key" value="@(mod.id).@extra.key" name="selectedPermissions"
```
to:
```html
data-key="@(extra.key.Contains('.') ? extra.key : mod.id + "." + extra.key)" value="@(extra.key.Contains('.') ? extra.key : mod.id + "." + extra.key)" name="selectedPermissions"
```

- [ ] **Build to verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Commit**

```bash
git add WebHomestay/Views/AdminStaff/PermissionMatrix.cshtml
git commit -m "fix: extras key generation respects full permission keys with dots"
```

---

### Task 2: Update module definitions — settings extras + AI CRUD + tab extras

**Files:**
- Modify: `WebHomestay/Views/AdminStaff/PermissionMatrix.cshtml:127-136`

- [ ] **Update modules array**

Replace the current `modules` array definition (lines 125-136 in the current file) with:

```csharp
var modules = new (string id, string name, string icon, bool hasDetail, bool hasCreate, bool hasEdit, bool hasDelete, (string key, string label)[] extras)[]
{
    ("matrix", "Ma trận vận hành", "fa-th", false, false, false, false, Array.Empty<(string,string)>()),
    ("bookings", "Đơn đặt phòng", "fa-calendar-check", true, true, true, true, new[] { ("trash", "Thùng rác"), ("restore", "Khôi phục") }),
    ("branches", "Chi nhánh", "fa-building", true, true, true, true, Array.Empty<(string,string)>()),
    ("rooms", "Phòng", "fa-door-open", true, true, true, true, Array.Empty<(string,string)>()),
    ("images", "Kho ảnh", "fa-images", true, false, false, false, Array.Empty<(string,string)>()),
    ("statistics", "Thống kê", "fa-chart-bar", false, false, false, false, new[] { ("export", "Xuất Excel") }),
    ("staff", "Nhân sự", "fa-users-cog", false, true, true, true, new[] { ("permissions", "Phân quyền"), ("logs", "Nhật ký") }),
    ("settings", "Cấu hình", "fa-sliders-h", false, false, false, false,
        new[] {
            ("update", "Cập nhật"),
            ("holidays.manage", "Ngày lễ"),
            ("slots.manage", "Khung giờ"),
            ("branch.settings", "Chi nhánh"),
            ("payment.settings", "Mã QR")
        }),
    ("ai", "AI Brain Center", "fa-brain", true, true, true, true,
        new[] {
            ("knowledge", "Knowledge"),
            ("graph", "Graph"),
            ("response", "Response"),
            ("trace", "Trace")
        })
};
```

- [ ] **Build to verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Commit**

```bash
git add WebHomestay/Views/AdminStaff/PermissionMatrix.cshtml
git commit -m "feat: update permission matrix modules — settings 5/5, AI CRUD + 4 tabs"
```

---

### Task 3: Update UI mode toggle — hide/show account/role controls

**Files:**
- Modify: `WebHomestay/Views/AdminStaff/PermissionMatrix.cshtml:78-102`

- [ ] **Update mode toggle selector HTML**

Replace the current role selector + account selector block (lines 78-102) with:

```html
<div id="roleSelector" class="d-flex align-items-center gap-2" style="display:none;">
  <label class="fw-bold small text-muted">Chọn vai trò:</label>
  <select id="roleSelect" class="form-select form-select-sm border-0 bg-light rounded-pill px-4" style="width:auto;">
    <option value="Manager">Manager</option>
    <option value="Staff">Staff</option>
    <option value="Admin">Admin (SuperAdmin)</option>
  </select>
</div>

<div id="accountSelector" class="d-flex align-items-center gap-2" style="display:none;">
  <label class="fw-bold small text-muted">Chọn tài khoản:</label>
  <input type="text" id="accountFilter" class="form-control form-control-sm bg-light rounded-pill px-3 account-search" placeholder="Tìm tên / username..." />
  <select id="accountSelect" class="form-select form-select-sm border-0 bg-light rounded-pill px-3" style="width:auto;min-width:250px;">
    @foreach (var acc in accounts)
    {
      <option value="@acc.Id"
              data-name="@acc.FullName"
              data-username="@acc.Username"
              data-branch="@(acc.Branch?.Name ?? "Tất cả")"
              data-role="@acc.Role"
              data-branch-id="@(acc.BranchId?.ToString() ?? "")">
        @acc.FullName (@@@acc.Username) - @(acc.Branch?.Name ?? "Tất cả CN")
      </option>
    }
  </select>
</div>
```

- [ ] **Update fetchAccounts list to include branchId + role**

Find the `if (currentMode === 'account')` block in `PermissionMatrix()` action in `AdminStaffController.cs` (line 179). Ensure `ViewBag.Accounts` includes Branch data (already done — includes Branch).

Also update the `accounts` dropdown in the view to include `data-role` and `data-branch-id` attributes (done in the HTML above).

- [ ] **Update setMode() JS function**

Find the `setMode` function (around line 260). Replace with:

```js
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
```

- [ ] **Update submit handler to include role/account hidden inputs**

Find the submit handler (around line 442). Replace with:

```js
permForm.addEventListener('submit', function(e) {
  if (currentMode === 'account' && !accountSelect.value) {
    e.preventDefault();
    showToast('Vui lòng chọn tài khoản trước khi lưu.');
    return;
  }
  var inputs = document.querySelectorAll('.parent-check:not(:checked)');
  for (var i = 0; i < inputs.length; i++) {
    var mod = inputs[i].dataset.module;
    var row = inputs[i].closest('tr');
    var children = row.querySelectorAll('.child-check:checked');
    if (children.length > 0) {
      e.preventDefault();
      showToast('Lỗi: Cần bật Xem ' + getModuleName(mod) + ' trước.');
      return;
    }
  }
  if (currentMode === 'account') {
    var inp = document.createElement('input');
    inp.type = 'hidden'; inp.name = 'accountId'; inp.value = accountSelect.value;
    permForm.appendChild(inp);
  } else {
    var inp = document.createElement('input');
    inp.type = 'hidden'; inp.name = 'role'; inp.value = roleSelect.value;
    permForm.appendChild(inp);
  }
});
```

- [ ] **Build to verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Commit**

```bash
git add WebHomestay/Views/AdminStaff/PermissionMatrix.cshtml
git commit -m "feat: hide account search/dropdown in role mode, show only in account mode"
```

---

### Task 4: Update ValidateInheritance mapping in AdminStaffController

**Files:**
- Modify: `WebHomestay/Controllers/AdminStaffController.cs:289-301`

- [ ] **Update parentChildMap and moduleNames**

Add AI children and new settings children to the `parentChildMap` and `moduleNames` dictionaries. Find `ValidateInheritance()` around line 289 and update:

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
        ["settings.view"] = new[] { "settings.update", "holidays.manage", "slots.manage", "branch.settings", "payment.settings" },
        ["ai.view"] = new[] { "ai.detail", "ai.create", "ai.edit", "ai.delete", "ai.knowledge", "ai.graph", "ai.response", "ai.trace" }
    };

    var moduleNames = new Dictionary<string, string>
    {
        ["bookings"] = "Đơn đặt phòng", ["branches"] = "Chi nhánh", ["rooms"] = "Phòng",
        ["images"] = "Kho ảnh", ["statistics"] = "Thống kê", ["staff"] = "Nhân sự",
        ["settings"] = "Cấu hình", ["ai"] = "AI Brain Center"
    };

    // Rest of the method stays the same
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

- [ ] **Build to verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Commit**

```bash
git add WebHomestay/Controllers/AdminStaffController.cs
git commit -m "feat: update ValidateInheritance with AI and settings permission children"
```

---

### Task 5: Update AdminSettingsController permission keys

**Files:**
- Modify: `WebHomestay/Controllers/AdminSettingsController.cs`

- [ ] **Change UpdateBranch permission to branch.settings**

Find line 65:
```csharp
[AdminAuthorize(Permission = "settings.update")]
[HttpPost("update-branch")]
```
Change to:
```csharp
[AdminAuthorize(Permission = "branch.settings")]
[HttpPost("update-branch")]
```

- [ ] **Change UpdatePaymentQr permission to payment.settings**

Find line 78:
```csharp
[AdminAuthorize(Permission = "settings.update")]
[HttpPost("payment-qr")]
```
Change to:
```csharp
[AdminAuthorize(Permission = "payment.settings")]
[HttpPost("payment-qr")]
```

- [ ] **Build to verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Commit**

```bash
git add WebHomestay/Controllers/AdminSettingsController.cs
git commit -m "feat: add branch.settings and payment.settings permission keys"
```

---

### Task 6: Add permission keys to AdminAIController actions

**Files:**
- Modify: `WebHomestay/Controllers/AdminAIController.cs`

This task adds `[AdminAuthorize(Permission = "...")]` to every action in AdminAIController. The class already has `[AdminAuthorize]` (login check). We add action-level permission checks.

- [ ] **Add `[AdminAuthorize(Permission = "ai.view")]` to Index**

Find `Index()` around line 348. Add attribute:
```csharp
[AdminAuthorize(Permission = "ai.view")]
[HttpGet("")]
public IActionResult Index()
```

- [ ] **Add permission keys to FinalSynthesizer endpoints**

```csharp
[AdminAuthorize(Permission = "ai.response")]
[HttpGet("final-synthesizer-config")]

[AdminAuthorize(Permission = "ai.edit")]
[HttpPost("final-synthesizer-config")]
```

- [ ] **Add permission keys to BookingFormConfig endpoints**

```csharp
[AdminAuthorize(Permission = "ai.response")]
[HttpGet("booking-form-config")]

[AdminAuthorize(Permission = "ai.edit")]
[HttpPost("booking-form-config")]
```

- [ ] **Add permission key to UploadPaymentQr**

```csharp
[AdminAuthorize(Permission = "ai.edit")]
[HttpPost("upload-payment-qr")]
```

- [ ] **Add permission key to TestAgent**

```csharp
[AdminAuthorize(Permission = "ai.trace")]
[HttpPost("test-agent")]
```

- [ ] **Add permission keys to BrainChat / BrainPreview**

```csharp
[AdminAuthorize(Permission = "ai.trace")]
[HttpPost("brain-chat")]

[AdminAuthorize(Permission = "ai.trace")]
[HttpPost("brain-preview")]
```

- [ ] **Add permission keys to Knowledge endpoints**

```csharp
[AdminAuthorize(Permission = "ai.knowledge")]
[HttpGet("brain-knowledge")]

[AdminAuthorize(Permission = "ai.create")]
[HttpPost("brain-scope")]

[AdminAuthorize(Permission = "ai.create")]
[HttpPost("brain-knowledge-unit")]

[AdminAuthorize(Permission = "ai.delete")]
[HttpDelete("brain-knowledge-unit/{id}")]
```

- [ ] **Add permission keys to Graph endpoints**

```csharp
[AdminAuthorize(Permission = "ai.graph")]
[HttpGet("brain-graph")]

[AdminAuthorize(Permission = "ai.create")]
[HttpPost("brain-graph-node")]

[AdminAuthorize(Permission = "ai.delete")]
[HttpDelete("brain-graph-node/{id}")]

[AdminAuthorize(Permission = "ai.create")]
[HttpPost("brain-graph-edge")]

[AdminAuthorize(Permission = "ai.delete")]
[HttpDelete("brain-graph-edge/{id}")]
```

- [ ] **Add permission keys to Trace endpoints**

```csharp
[AdminAuthorize(Permission = "ai.trace")]
[HttpGet("brain-traces")]

[AdminAuthorize(Permission = "ai.detail")]
[HttpGet("brain-trace/{id}")]
```

- [ ] **Add permission key to SeedBrainData**

```csharp
[AdminAuthorize(Permission = "ai.edit")]
[HttpPost("seed-brain-data")]
```

- [ ] **Add permission keys to Collection/Article endpoints**

```csharp
[AdminAuthorize(Permission = "ai.knowledge")]
[HttpGet("collections")]

[AdminAuthorize(Permission = "ai.edit")]
[HttpPost("save-collection")]

[AdminAuthorize(Permission = "ai.delete")]
[HttpDelete("collection/{id}")]

[AdminAuthorize(Permission = "ai.knowledge")]
[HttpGet("articles/{collectionId}")]

[AdminAuthorize(Permission = "ai.detail")]
[HttpGet("article/{id}")]

[AdminAuthorize(Permission = "ai.edit")]
[HttpPost("save-article")]

[AdminAuthorize(Permission = "ai.delete")]
[HttpDelete("article/{id}")]
```

- [ ] **Build to verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Commit**

```bash
git add WebHomestay/Controllers/AdminAIController.cs
git commit -m "feat: add permission keys to all AdminAIController actions"
```

---

### Task 7: Update seed data in AdminAccountController

**Files:**
- Modify: `WebHomestay/Controllers/AdminAccountController.cs` (SeedData method)

- [ ] **Add new permission keys to Manager seed template**

Find the `managerPerms` dictionary in `SeedData`. Add entries for the new permissions:

```csharp
var managerPerms = new Dictionary<string, bool>
{
    // Existing...
    ["matrix.view"] = true,
    ["bookings.view"] = true, ["bookings.detail"] = true, ["bookings.create"] = true, ["bookings.edit"] = true,
    ["branches.view"] = true, ["branches.detail"] = true, ["branches.create"] = true, ["branches.edit"] = true,
    ["rooms.view"] = true, ["rooms.detail"] = true, ["rooms.create"] = true, ["rooms.edit"] = true,
    ["images.view"] = true, ["images.detail"] = true,
    ["statistics.view"] = true, ["statistics.export"] = true,
    ["staff.view"] = true, ["staff.logs"] = true,
    ["settings.view"] = true, ["settings.update"] = true, ["holidays.manage"] = true, ["slots.manage"] = true,
    ["ai.view"] = true, ["ai.manage"] = true,
    
    // New:
    ["branch.settings"] = true,
    ["payment.settings"] = true,
    ["ai.detail"] = true,
    ["ai.create"] = true,
    ["ai.edit"] = true,
    ["ai.delete"] = true,
    ["ai.knowledge"] = true,
    ["ai.graph"] = true,
    ["ai.response"] = true,
    ["ai.trace"] = true
};
```

Note: The old `["ai.manage"] = true` can be removed since it's replaced by specific tab keys. If there's concern about backward compatibility, keep it.

- [ ] **Build to verify**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds

- [ ] **Commit**

```bash
git add WebHomestay/Controllers/AdminAccountController.cs
git commit -m "feat: update seed permissions for settings and AI modules"
```

---

### Task 8: Update PermissionInheritanceTests

**Files:**
- Modify: `WebHomestay.Tests/Domain/PermissionInheritanceTests.cs`

- [ ] **Update parentChildMap in test to match controller**

Replace the `parentChildMap` in the test file (line 9-18) with:
```csharp
var parentChildMap = new Dictionary<string, string[]>
{
    ["bookings.view"] = new[] { "bookings.detail", "bookings.create", "bookings.edit", "bookings.delete", "bookings.trash", "bookings.restore" },
    ["branches.view"] = new[] { "branches.detail", "branches.create", "branches.edit", "branches.delete" },
    ["rooms.view"] = new[] { "rooms.detail", "rooms.create", "rooms.edit", "rooms.delete" },
    ["images.view"] = new[] { "images.detail" },
    ["statistics.view"] = new[] { "statistics.export" },
    ["staff.view"] = new[] { "staff.create", "staff.edit", "staff.delete", "staff.permissions", "staff.logs" },
    ["settings.view"] = new[] { "settings.update", "holidays.manage", "slots.manage", "branch.settings", "payment.settings" },
    ["ai.view"] = new[] { "ai.detail", "ai.create", "ai.edit", "ai.delete", "ai.knowledge", "ai.graph", "ai.response", "ai.trace" }
};
```

- [ ] **Add test: AI valid permissions pass**

Add after the existing tests:
```csharp
[Fact]
public void AI_Valid_Permissions_Pass()
{
    var perms = new Dictionary<string, bool>
    {
        ["ai.view"] = true,
        ["ai.detail"] = true,
        ["ai.create"] = true,
        ["ai.knowledge"] = true,
        ["ai.graph"] = true
    };
    Assert.True(ValidateInheritance(perms, out _));
}

[Fact]
public void AI_Child_Without_Parent_Fails()
{
    var perms = new Dictionary<string, bool> { ["ai.knowledge"] = true };
    Assert.False(ValidateInheritance(perms, out _));
}

[Fact]
public void Settings_Branch_Child_Without_Parent_Fails()
{
    var perms = new Dictionary<string, bool> { ["branch.settings"] = true };
    Assert.False(ValidateInheritance(perms, out _));
}

[Fact]
public void Settings_Payment_Child_Without_Parent_Fails()
{
    var perms = new Dictionary<string, bool> { ["payment.settings"] = true };
    Assert.False(ValidateInheritance(perms, out _));
}

[Fact]
public void AI_Delete_Requires_View()
{
    var perms = new Dictionary<string, bool> { ["ai.delete"] = true };
    Assert.False(ValidateInheritance(perms, out _));
}
```

- [ ] **Run tests to verify**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~PermissionInheritanceTests`
Expected: All 9+ tests pass (existing + new)

- [ ] **Commit**

```bash
git add WebHomestay.Tests/Domain/PermissionInheritanceTests.cs
git commit -m "test: add inheritance tests for AI and settings permissions"
```

---

### Task 9: Run full test suite

- [ ] **Run all tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj`
Expected: All tests pass

- [ ] **Final build check**

Run: `dotnet build WebHomestay/WebHomestay.csproj`
Expected: Build succeeds with no warnings
