# Spec: Permission Matrix Enhancements — Account Mode Branch + Missing Permissions

## 1. Overview

Bổ sung và hoàn thiện Ma trận phân quyền (`PermissionMatrix.cshtml`) với 3 thay đổi chính:

1. **UI toggle:** Ẩn thanh tìm kiếm user và dropdown tài khoản khi ở chế độ "Theo Role"; chỉ hiện ở chế độ "Theo Tài khoản". Thêm dropdown Branch ở chế độ "Theo Tài khoản".
2. **Settings permissions:** Thêm 2 quyền còn thiếu cho tab "Cấu hình Chi nhánh" và "Cấu hình mã QR".
3. **AI Brain Center permissions:** Mở rộng từ 1 cột `manage` thành CRUD (Xem, Chi tiết, Thêm, Sửa, Xóa) + 4 quyền tab riêng ở cột "Khác" (knowledge, graph, response, trace).

## 2. UI Changes — PermissionMatrix.cshtml

### 2.1. Mode toggle behavior

| Control | Theo Role | Theo Tài khoản |
|---------|-----------|-----------------|
| Role dropdown | Hiện | Ẩn |
| Branch dropdown | Ẩn | Hiện |
| Tìm kiếm user | Ẩn | Hiện |
| Account dropdown | Ẩn | Hiện |

Chi tiết:
- **Chế độ Role:** `roleSelector` visible, `accountSelector` hidden. Form submit gửi `role` + `selectedPermissions`.
- **Chế độ Account:** `roleSelector` hidden, `accountSelector` visible. Form submit gửi `accountId` + `branchId` + `selectedPermissions`.
- Branch dropdown: là `<select>` đơn (1 branch), hiển thị branch hiện tại của user được chọn. Cho phép đổi — lưu vào `AdminUser.BranchId`.

### 2.2. Logic setMode() JS

```
setMode('role') → roleSelector display:flex, accountSelector display:none
setMode('account') → roleSelector display:none, accountSelector display:flex
```

Khi chuyển mode, clear form và load lại ma trận tương ứng.

## 3. Permission Keys — Bổ sung

### 3.1. Settings module

Thêm 2 extras trong mảng `modules`:

| Key | Label | Gắn với |
|-----|-------|---------|
| `branch.settings` | Chi nhánh | Tab "Cấu hình Chi nhánh" trong AdminSettings |
| `payment.settings` | Mã QR | Tab "Cấu hình mã QR" trong AdminSettings |

→ Tổng cộng settings extras: `update`, `holidays`, `slots`, `branch.settings`, `payment.settings` (5/5).

### 3.2. AI Brain Center module

**Cấu trúc module mới:**

| Column | Permission Key | Ghi chú |
|--------|---------------|---------|
| Xem (parent) | `ai.view` | Giữ nguyên |
| Chi tiết | `ai.detail` | Xem chi tiết 1 item (article, node, trace) |
| Thêm | `ai.create` | Tạo mới knowledge, graph node/edge, collection |
| Sửa | `ai.edit` | Chỉnh sửa knowledge, graph, config |
| Xóa | `ai.delete` | Xóa knowledge, graph node/edge, article |
| Khác: Knowledge | `ai.knowledge` | Tab "Tri thức & Graph" — phần Knowledge |
| Khác: Graph | `ai.graph` | Tab "Tri thức & Graph" — phần Graph |
| Khác: Response | `ai.response` | Tab "Trả lời & Form" — Final Synthesizer config |
| Khác: Trace | `ai.trace` | Tab "Test & Trace" |

`AdminAIController` actions gắn permission tương ứng (xem Mục 5).

## 4. Frontend — Ma trận module definitions

### 4.0. Fix pre-existing: extras key generation

Hiện tại view dùng `@(mod.id).@extra.key` để tạo permission key. Với settings extras, key `holidays` → thành `settings.holidays` nhưng backend dùng `holidays.manage`. Fix: nếu `extra.key` đã có dấu `.` thì dùng nguyên; nếu không thì prepend `mod.id`.

Sửa dòng render extras trong `PermissionMatrix.cshtml`:
```
data-key="@(extra.key.Contains('.') ? extra.key : mod.id + "." + extra.key)"
value="@(extra.key.Contains('.') ? extra.key : mod.id + "." + extra.key)"
```

### 4.1. Settings module (sửa)

Extras dùng full key (có `.` để bypass auto-prepend):

```csharp
("settings", "Cấu hình", "fa-sliders-h", false, false, false, false,
    new[] {
        ("update", "Cập nhật"),
        ("holidays.manage", "Ngày lễ"),
        ("slots.manage", "Khung giờ"),
        ("branch.settings", "Chi nhánh"),
        ("payment.settings", "Mã QR")
    })
```

→ Key cuối: `settings.update`, `holidays.manage`, `slots.manage`, `branch.settings`, `payment.settings`.

### 4.2. AI module (sửa)

```csharp
("ai", "AI Brain Center", "fa-brain", true, true, true, true,
    new[] {
        ("knowledge", "Knowledge"),
        ("graph", "Graph"),
        ("response", "Response"),
        ("trace", "Trace")
    })
```

→ Key cuối: `ai.knowledge`, `ai.graph`, `ai.response`, `ai.trace` (không có dấu `.` nên prepend `ai.`).

- `hasDetail = true` → cột "Chi tiết" với key `ai.detail`
- `hasCreate = true` → cột "Thêm" với key `ai.create`
- `hasEdit = true` → cột "Sửa" với key `ai.edit`
- `hasDelete = true` → cột "Xóa" với key `ai.delete`
- Extras: 4 tab permissions ở cột "Khác"

## 5. Backend — Controller Permission Enforcement

### 5.1. AdminSettingsController

| Action | Permission hiện tại | Permission mới |
|--------|-------------------|----------------|
| `Index` | `settings.view` | `settings.view` (giữ) |
| `Update` | `settings.update` | `settings.update` (giữ) |
| `UpdateBranch` | `settings.update` | **`branch.settings`** |
| `UpdatePaymentQr` | `settings.update` | **`payment.settings`** |

Sửa `[AdminAuthorize(Permission = "settings.update")]` trên `UpdateBranch` và `UpdatePaymentQr` thành `branch.settings` và `payment.settings`.

### 5.2. AdminAIController

Không có permission key trên hầu hết action (chỉ có class-level `[AdminAuthorize]`). Thêm action-level:

| Action | Permission |
|--------|-----------|
| `Index` | `ai.view` |
| `GetFinalSynthesizerConfig` | `ai.response` |
| `SaveFinalSynthesizerConfig` | `ai.edit` |
| `GetBookingFormConfig` | `ai.response` |
| `SaveBookingFormConfig` | `ai.edit` |
| `UploadPaymentQr` | `ai.edit` |
| `TestAgent` | `ai.trace` |
| `BrainChat` | `ai.trace` |
| `BrainPreview` | `ai.trace` |
| `GetBrainKnowledge` | `ai.knowledge` |
| `SaveBrainScope` | `ai.create` |
| `SaveBrainKnowledgeUnit` | `ai.create` |
| `DeleteBrainKnowledgeUnit` | `ai.delete` |
| `GetBrainGraph` | `ai.graph` |
| `SaveBrainGraphNode` | `ai.create` |
| `DeleteBrainGraphNode` | `ai.delete` |
| `SaveBrainGraphEdge` | `ai.create` |
| `DeleteBrainGraphEdge` | `ai.delete` |
| `GetBrainTraces` | `ai.trace` |
| `GetBrainTrace` | `ai.detail` |
| `SeedBrainData` | `ai.edit` |
| `GetCollections` | `ai.knowledge` |
| `SaveCollection` | `ai.edit` |
| `DeleteCollection` | `ai.delete` |
| `GetArticles` | `ai.knowledge` |
| `GetArticle` | `ai.detail` |
| `SaveArticle` | `ai.edit` |
| `DeleteArticle` | `ai.delete` |

Thêm `using WebHomestay.Filters;` nếu chưa có.

### 5.3. ValidateInheritance() — Thêm mapping

```csharp
["ai.view"] = new[] {
    "ai.detail", "ai.create", "ai.edit", "ai.delete",
    "ai.knowledge", "ai.graph", "ai.response", "ai.trace"
},
["settings.view"] = new[] {
    "settings.update", "holidays.manage", "slots.manage",
    "branch.settings", "payment.settings"
}
```

### 5.4. Seed data — RolePermissionTemplate

Cập nhật seed trong `AdminAccountController.SeedData()` cho Manager:

```csharp
// Thêm vào managerPerms:
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
```

## 6. Tests

### 6.1. PermissionInheritanceTests

Thêm test cases cho:
- `ai.view` + children hợp lệ
- thiếu `ai.view` nhưng có `ai.create` → fail
- `settings.view` + `branch.settings` + `payment.settings` hợp lệ
- thiếu `settings.view` nhưng có `payment.settings` → fail

Cập nhật `parentChildMap` trong test file giống với `AdminStaffController.ValidateInheritance()`.

### 6.2. PermissionResolveServiceTests

Không cần thay đổi — logic resolve giữ nguyên.

## 7. Data Migration

Không cần migration mới — không thay đổi model. Chỉ update seed data trong `AdminAccountController.SeedData()`.

## 8. Non-Goals

- Không thay đổi `AdminUser.BranchId` thành many-to-many trong spec này (có thể làm sau)
- Không thay đổi `RolePermissionTemplate` entity (không thêm branch_id — template vẫn global)
- Không sửa `PermissionResolveService` (logic giữ nguyên)
