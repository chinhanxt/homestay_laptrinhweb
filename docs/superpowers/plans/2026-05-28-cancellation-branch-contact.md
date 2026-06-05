# Cancellation Branch Contact Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a direct branch contact option to the botchat cancellation flow, using branch Zalo phone and email maintained in admin branches.

**Architecture:** Reuse `Branch.Hotline` as the stored Zalo phone number and add `Branch.Email` as a new optional public contact field. Admin branch CRUD persists both values. Botchat reads branch contact options from a small public JSON endpoint and renders Zalo/email contact details without creating a cancellation request.

**Tech Stack:** ASP.NET Core MVC, EF Core/PostgreSQL, Razor views, vanilla JavaScript in `wwwroot/js/site.js`, xUnit where backend behavior is testable.

---

## File Structure

- Modify `WebHomestay/Models/Branch.cs`
  - Add optional `Email` property.
- Modify `WebHomestay/Data/ApplicationDbContext.cs`
  - Map `Branch.Email` to `branches.email`.
- Modify `WebHomestay/Controllers/AdminBranchesController.cs`
  - Include `Email` and `MapUrl` in branch create/edit model binding.
  - Add public read endpoint for botchat branch contacts.
- Modify `WebHomestay/Views/AdminBranches/Index.cshtml`
  - Rename Hotline column to SĐT Zalo and add Email column.
- Modify `WebHomestay/Views/AdminBranches/Create.cshtml`
  - Rename Hotline label to SĐT Zalo and add Email input.
- Modify `WebHomestay/Views/AdminBranches/Edit.cshtml`
  - Rename Hotline label to SĐT Zalo and add Email input.
- Modify `WebHomestay/Views/AdminBranches/Details.cshtml`
  - Rename Hotline label to SĐT Zalo and show Email.
- Create EF migration `WebHomestay/Migrations/<timestamp>_AddBranchEmail.cs` if EF detects `branches.email` is not already in the model snapshot.
- Modify `WebHomestay/wwwroot/js/site.js`
  - Add cancellation contact option, branch selector, Zalo/email display helpers.

---

### Task 1: Add branch email model and admin persistence

**Files:**
- Modify: `WebHomestay/Models/Branch.cs`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`
- Modify: `WebHomestay/Controllers/AdminBranchesController.cs`
- Create/Modify: `WebHomestay/Migrations/*AddBranchEmail*.cs` and `ApplicationDbContextModelSnapshot.cs` if needed

- [ ] **Step 1: Add `Email` to `Branch`**

In `WebHomestay/Models/Branch.cs`, add this property after `Hotline`:

```csharp
[StringLength(100)]
[EmailAddress]
public string? Email { get; set; }
```

Expected nearby shape:

```csharp
[StringLength(20)]
public string? Hotline { get; set; }

[StringLength(100)]
[EmailAddress]
public string? Email { get; set; }

public string? MapUrl { get; set; }
```

- [ ] **Step 2: Map `Branch.Email`**

In `WebHomestay/Data/ApplicationDbContext.cs`, inside the `modelBuilder.Entity<Branch>` mapping, add after Hotline mapping:

```csharp
entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100);
```

Expected nearby shape:

```csharp
entity.Property(e => e.Description).HasColumnName("description");
entity.Property(e => e.Hotline).HasColumnName("hotline");
entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100);
entity.Property(e => e.MapUrl).HasColumnName("map_url");
```

- [ ] **Step 3: Include `Email` and preserve `MapUrl` in admin branch binding**

In `WebHomestay/Controllers/AdminBranchesController.cs`, change the Create POST signature from:

```csharp
public async Task<IActionResult> Create([Bind("Id,Name,Address,Hotline,Description")] Branch branch)
```

to:

```csharp
public async Task<IActionResult> Create([Bind("Id,Name,Address,Hotline,Email,Description,MapUrl")] Branch branch)
```

Change the Edit POST signature from:

```csharp
public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,Hotline,Description")] Branch branch)
```

to:

```csharp
public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,Hotline,Email,Description,MapUrl")] Branch branch)
```

- [ ] **Step 4: Generate migration if needed**

Run:

```bash
dotnet ef migrations add AddBranchEmail --project WebHomestay/WebHomestay.csproj
```

Expected outcomes:

- If `branches.email` is new to the model snapshot, EF creates a migration adding nullable `email` column with max length 100.
- If EF reports no changes because a previous migration/snapshot already contains `email`, do not create a manual duplicate migration.

A valid migration `Up` should include:

```csharp
migrationBuilder.AddColumn<string>(
    name: "email",
    table: "branches",
    type: "character varying(100)",
    maxLength: 100,
    nullable: true);
```

A valid `Down` should drop `email` from `branches`.

- [ ] **Step 5: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: PASS. Existing NU1903 warnings may remain.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Models/Branch.cs WebHomestay/Data/ApplicationDbContext.cs WebHomestay/Controllers/AdminBranchesController.cs WebHomestay/Migrations
git commit -m "feat: add branch email contact field"
```

---

### Task 2: Update admin branch UI labels and fields

**Files:**
- Modify: `WebHomestay/Views/AdminBranches/Index.cshtml`
- Modify: `WebHomestay/Views/AdminBranches/Create.cshtml`
- Modify: `WebHomestay/Views/AdminBranches/Edit.cshtml`
- Modify: `WebHomestay/Views/AdminBranches/Details.cshtml`

- [ ] **Step 1: Update branch list columns**

In `WebHomestay/Views/AdminBranches/Index.cshtml`, change the table header from:

```html
<th>Hotline</th>
<th class="text-end">Thao tác</th>
```

to:

```html
<th>SĐT Zalo</th>
<th>Email</th>
<th class="text-end">Thao tác</th>
```

Change the Hotline cell from:

```html
<td>
    <div class="small"><i class="fas fa-phone me-2 text-muted"></i>@item.Hotline</div>
</td>
<td class="text-end">
```

to:

```html
<td>
    <div class="small"><i class="fas fa-comment-dots me-2 text-muted"></i>@item.Hotline</div>
</td>
<td>
    <div class="small"><i class="fas fa-envelope me-2 text-muted"></i>@(string.IsNullOrWhiteSpace(item.Email) ? "Chưa cấu hình" : item.Email)</div>
</td>
<td class="text-end">
```

Change empty-state colspan from:

```html
<td colspan="4" class="text-center py-5">
```

to:

```html
<td colspan="5" class="text-center py-5">
```

- [ ] **Step 2: Update Create form**

In `WebHomestay/Views/AdminBranches/Create.cshtml`, change the Hotline label and placeholder from:

```html
<label asp-for="Hotline" class="admin-form-label">Hotline liên hệ</label>
<input asp-for="Hotline" class="admin-form-input" placeholder="090..." />
```

to:

```html
<label asp-for="Hotline" class="admin-form-label">SĐT Zalo</label>
<input asp-for="Hotline" class="admin-form-input" placeholder="090..." />
```

After the first row that contains Name and Hotline, add this email field before Address:

```html
<div class="admin-form-group">
    <label asp-for="Email" class="admin-form-label">Email chi nhánh</label>
    <input asp-for="Email" class="admin-form-input" placeholder="chinhanh@example.com" />
    <span asp-validation-for="Email" class="text-danger small"></span>
</div>
```

- [ ] **Step 3: Update Edit form**

In `WebHomestay/Views/AdminBranches/Edit.cshtml`, change the Hotline label from:

```html
<label asp-for="Hotline" class="admin-form-label">Hotline liên hệ</label>
```

to:

```html
<label asp-for="Hotline" class="admin-form-label">SĐT Zalo</label>
```

After the first row that contains Name and Hotline, add this email field before Address:

```html
<div class="admin-form-group">
    <label asp-for="Email" class="admin-form-label">Email chi nhánh</label>
    <input asp-for="Email" class="admin-form-input" />
    <span asp-validation-for="Email" class="text-danger small"></span>
</div>
```

- [ ] **Step 4: Update Details view**

In `WebHomestay/Views/AdminBranches/Details.cshtml`, change:

```html
<label class="admin-form-label">Hotline</label>
<div class="fw-bold fs-5 text-primary">@Model.Hotline</div>
```

to:

```html
<label class="admin-form-label">SĐT Zalo</label>
<div class="fw-bold fs-5 text-primary">@Model.Hotline</div>
```

Add this block after the Zalo block:

```html
<div class="mb-4">
    <label class="admin-form-label">Email chi nhánh</label>
    <div class="fw-bold">@(string.IsNullOrWhiteSpace(Model.Email) ? "Chưa cấu hình" : Model.Email)</div>
</div>
```

- [ ] **Step 5: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: PASS. Existing NU1903 warnings may remain.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Views/AdminBranches/Index.cshtml WebHomestay/Views/AdminBranches/Create.cshtml WebHomestay/Views/AdminBranches/Edit.cshtml WebHomestay/Views/AdminBranches/Details.cshtml
git commit -m "feat: show branch zalo and email in admin"
```

---

### Task 3: Add public branch contact endpoint

**Files:**
- Modify: `WebHomestay/Controllers/AdminBranchesController.cs`

- [ ] **Step 1: Add endpoint method**

In `WebHomestay/Controllers/AdminBranchesController.cs`, add this action before the private `BranchExists` method:

```csharp
[HttpGet("public-contacts")]
[AllowAnonymous]
public async Task<IActionResult> PublicContacts()
{
    var branches = await _context.Branches
        .OrderBy(b => b.Name)
        .Select(b => new
        {
            id = b.Id,
            name = b.Name,
            address = b.Address,
            zaloPhone = b.Hotline,
            email = b.Email
        })
        .ToListAsync();

    return Ok(branches);
}
```

Add this using at the top if it is not already present:

```csharp
using Microsoft.AspNetCore.Authorization;
```

- [ ] **Step 2: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: PASS. Existing NU1903 warnings may remain.

- [ ] **Step 3: Smoke check endpoint**

Run the app:

```bash
dotnet run --project WebHomestay/WebHomestay.csproj
```

In another shell, run:

```bash
python - <<'PY'
import json, urllib.request
with urllib.request.urlopen('http://localhost:5000/admin/branches/public-contacts', timeout=10) as r:
    data = json.loads(r.read().decode('utf-8'))
print(type(data).__name__)
print(data[0].keys() if data else 'empty')
PY
```

Expected: output is `list`; first item keys include `id`, `name`, `address`, `zaloPhone`, `email`. If database has no branches, `empty` is acceptable.

- [ ] **Step 4: Commit**

```bash
git add WebHomestay/Controllers/AdminBranchesController.cs
git commit -m "feat: expose branch contact options"
```

---

### Task 4: Add direct contact option in botchat cancellation flow

**Files:**
- Modify: `WebHomestay/wwwroot/js/site.js`

- [ ] **Step 1: Change cancellation prompt to show two choices**

In `WebHomestay/wwwroot/js/site.js`, replace the body of `renderCancellationPrompt()` with:

```javascript
function renderCancellationPrompt() {
    const wrapper = appendBlock('ai-cancellation-prompt');
    const text = document.createElement('div');
    text.className = 'ai-payment-message';
    text.textContent = 'AI không tự hủy phòng, nhưng bạn có thể gửi yêu cầu để nhân viên kiểm tra hoặc liên hệ trực tiếp chi nhánh đã đặt.';
    wrapper.appendChild(text);

    const requestButton = document.createElement('button');
    requestButton.type = 'button';
    requestButton.className = 'ai-chip-btn';
    requestButton.textContent = 'Gửi yêu cầu hủy';
    requestButton.addEventListener('click', () => renderCancellationForm());
    wrapper.appendChild(requestButton);

    const contactButton = document.createElement('button');
    contactButton.type = 'button';
    contactButton.className = 'ai-chip-btn';
    contactButton.textContent = 'Liên hệ Zalo/email';
    contactButton.addEventListener('click', () => renderCancellationBranchContactSelector());
    wrapper.appendChild(contactButton);

    scrollMessages();
}
```

- [ ] **Step 2: Add branch contact selector**

Add this function after `renderCancellationForm()`:

```javascript
async function renderCancellationBranchContactSelector() {
    setOpen(true);
    const wrapper = appendBlock('ai-cancellation-contact-block');
    wrapper.textContent = 'Đang tải danh sách chi nhánh...';

    try {
        const response = await fetch('/admin/branches/public-contacts');
        const branches = await response.json().catch(() => []);
        if (!response.ok) throw new Error('Không tải được danh sách chi nhánh.');
        if (!Array.isArray(branches) || branches.length === 0) {
            wrapper.textContent = 'Hiện chưa có thông tin chi nhánh để liên hệ.';
            scrollMessages();
            return;
        }

        wrapper.innerHTML = '';
        const message = document.createElement('div');
        message.className = 'ai-payment-message';
        message.textContent = 'Bạn chọn đúng chi nhánh đã đặt phòng để nhận Zalo/email liên hệ.';
        wrapper.appendChild(message);

        const select = document.createElement('select');
        select.className = 'ai-booking-input';
        const placeholder = document.createElement('option');
        placeholder.value = '';
        placeholder.textContent = 'Chọn chi nhánh đã đặt';
        select.appendChild(placeholder);

        branches.forEach(branch => {
            const option = document.createElement('option');
            option.value = String(branch.id || '');
            option.textContent = branch.address ? `${branch.name} - ${branch.address}` : branch.name;
            select.appendChild(option);
        });
        wrapper.appendChild(select);

        const contactBox = document.createElement('div');
        contactBox.className = 'ai-payment-message';
        contactBox.hidden = true;
        wrapper.appendChild(contactBox);

        select.addEventListener('change', () => {
            const selected = branches.find(branch => String(branch.id || '') === select.value);
            renderSelectedBranchContact(contactBox, selected);
        });
    } catch (error) {
        wrapper.textContent = error.message || 'Không tải được thông tin chi nhánh.';
    }

    scrollMessages();
}
```

- [ ] **Step 3: Add selected branch contact renderer**

Add this function after `renderCancellationBranchContactSelector()`:

```javascript
function renderSelectedBranchContact(contactBox, branch) {
    contactBox.hidden = !branch;
    contactBox.innerHTML = '';
    if (!branch) return;

    const title = document.createElement('div');
    title.className = 'fw-bold mb-2';
    title.textContent = branch.name || 'Chi nhánh';
    contactBox.appendChild(title);

    if (branch.address) {
        const address = document.createElement('div');
        address.textContent = `Địa chỉ: ${branch.address}`;
        contactBox.appendChild(address);
    }

    const zaloPhone = String(branch.zaloPhone || '').trim();
    const zaloLine = document.createElement('div');
    if (zaloPhone) {
        const zaloLink = document.createElement('a');
        zaloLink.href = buildZaloLink(zaloPhone);
        zaloLink.target = '_blank';
        zaloLink.rel = 'noopener noreferrer';
        zaloLink.textContent = zaloPhone;
        zaloLine.appendChild(document.createTextNode('Zalo: '));
        zaloLine.appendChild(zaloLink);
    } else {
        zaloLine.textContent = 'Zalo: Chi nhánh chưa cấu hình SĐT Zalo.';
    }
    contactBox.appendChild(zaloLine);

    const email = String(branch.email || '').trim();
    const emailLine = document.createElement('div');
    if (email) {
        const emailLink = document.createElement('a');
        emailLink.href = `mailto:${email}`;
        emailLink.textContent = email;
        emailLine.appendChild(document.createTextNode('Email: '));
        emailLine.appendChild(emailLink);
    } else {
        emailLine.textContent = 'Email: Chi nhánh chưa cấu hình email.';
    }
    contactBox.appendChild(emailLine);

    scrollMessages();
}
```

- [ ] **Step 4: Add Zalo link helper**

Add this function near the existing helper functions at the bottom of `site.js`, before `scrollMessages()`:

```javascript
function buildZaloLink(phone) {
    const digits = String(phone || '').replace(/\D/g, '');
    return digits ? `https://zalo.me/${digits}` : '#';
}
```

- [ ] **Step 5: JavaScript syntax check**

Run:

```bash
node --check WebHomestay/wwwroot/js/site.js
```

Expected: no syntax errors.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/wwwroot/js/site.js
git commit -m "feat: add cancellation branch contact option"
```

---

### Task 5: Full verification

**Files:**
- No planned code changes unless fixing failures.

- [ ] **Step 1: Run focused frontend syntax check**

```bash
node --check WebHomestay/wwwroot/js/site.js
```

Expected: PASS.

- [ ] **Step 2: Run tests**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj
```

Expected: PASS. Existing NU1903 warnings may remain.

- [ ] **Step 3: Build app**

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: PASS. Existing NU1903 warnings may remain.

- [ ] **Step 4: Manual admin verification**

Run:

```bash
dotnet run --project WebHomestay/WebHomestay.csproj
```

Open `/admin/branches` after logging in as an admin with branch permissions.

Verify:

- Branch list column says `SĐT Zalo`, not `Hotline`.
- Branch list has an `Email` column.
- Create branch form has `SĐT Zalo` and `Email chi nhánh` fields.
- Edit branch form persists both values.
- Details view shows `SĐT Zalo` and `Email chi nhánh`.

- [ ] **Step 5: Manual botchat verification**

On a public page with botchat loaded:

1. Type a cancellation intent such as `tôi muốn hủy phòng`.
2. Verify bot shows two options: `Gửi yêu cầu hủy` and `Liên hệ Zalo/email`.
3. Click `Gửi yêu cầu hủy`.
4. Verify the existing cancellation request form opens.
5. Trigger the cancellation prompt again and click `Liên hệ Zalo/email`.
6. Verify branch selector appears.
7. Select a branch.
8. Verify branch name, address, Zalo phone link, and email/mailto link render correctly.
9. Verify no cancellation request is created by choosing direct contact.

- [ ] **Step 6: Commit verification fixes only if needed**

If verification required code fixes:

```bash
git status --short
git add WebHomestay/Models/Branch.cs WebHomestay/Data/ApplicationDbContext.cs WebHomestay/Controllers/AdminBranchesController.cs WebHomestay/Views/AdminBranches/Index.cshtml WebHomestay/Views/AdminBranches/Create.cshtml WebHomestay/Views/AdminBranches/Edit.cshtml WebHomestay/Views/AdminBranches/Details.cshtml WebHomestay/wwwroot/js/site.js WebHomestay/Migrations
git commit -m "fix: stabilize cancellation branch contact flow"
```

If no fixes were required, do not create an empty commit.

---

## Self-Review Notes

- Spec coverage: Admin branch Zalo/email fields are covered in Tasks 1-2; public branch contact endpoint is covered in Task 3; botchat two-option cancellation flow is covered in Task 4; verification is covered in Task 5.
- Placeholder scan: No TBD/TODO placeholders remain. Migration timestamp is intentionally generated by EF at implementation time.
- Type consistency: `Branch.Email`, JSON `zaloPhone`, and JS `branch.zaloPhone` are consistent across backend and frontend tasks.
- Scope check: This plan does not create cancellation records for direct contact and does not auto-detect branch from booking code; the customer chooses the branch manually as approved.
