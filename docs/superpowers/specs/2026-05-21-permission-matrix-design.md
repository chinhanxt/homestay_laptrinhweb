# Spec: Permission Matrix — Role Templates + Account Override

## 1. Overview
Replace the current per-account-only permission assignment with a dual-mode permission matrix: assign permissions by **Role template** (Manager/Staff) and optionally **override** per individual account. All admin pages are listed in the matrix with parent-child hierarchy enforced on both frontend and backend.

## 2. Goals
- Single permission matrix page with toggle: "Theo Role" / "Theo Tài khoản"
- Role mode: dropdown chọn Manager hoặc Staff, ma trận quyền mặc định
- Account mode: dropdown có filter/sort chọn 1 tài khoản, ma trận = role template + override
- Cây trang cha-con đầy đủ (tất cả controller + action)
- Inheritance validation: con yêu cầu cha được bật, chặn + thông báo nếu vi phạm
- Resolve quyền: SuperAdmin bypass, kế tiếp là role template, cuối cùng là account override
- Fix các security gap hiện tại (AdminSlots, AdminAI, AdminSettings)

## 3. Data Model

### 3.1. New table: `role_permission_templates`

| Column | Type | Notes |
|--------|------|-------|
| `role` | varchar(20) | PK. "Manager" hoặc "Staff" |
| `permissions_json` | text | JSON dictionary `{"module.action": bool, ...}` |
| `updated_at` | datetime | Auto update |

Entity mới: `RolePermissionTemplate` trong `Models/`.
- Map xuống bảng `role_permission_templates` trong `ApplicationDbContext`.
- Có navigation property ngược? Không cần — bảng độc lập, không FK.

### 3.2. AdminUser — giữ nguyên
- `permissions_json` (text): lưu override riêng cho account.
- Không thay đổi cấu trúc.

### 3.3. Resolve quyền runtime
```
if role == SuperAdmin → full quyền, bypass
lấy role template (Manager/Staff) → dict A
nếu account có override → merge: A + override (override thắng)
```

## 4. Page Tree — Permission Keys đầy đủ

```
Module                   │ Parent key      │ Action keys (con)
─────────────────────────┼─────────────────┼──────────────────────────────
1. Ma trận vận hành      │ matrix.view     │ (không có con)
2. Đơn đặt phòng         │ bookings.view   │ bookings.detail
                         │                 │ bookings.create
                         │                 │ bookings.edit
                         │                 │ bookings.delete
                         │                 │ bookings.trash
                         │                 │ bookings.restore
3. Chi nhánh             │ branches.view   │ branches.detail
                         │                 │ branches.create
                         │                 │ branches.edit
                         │                 │ branches.delete
4. Phòng                 │ rooms.view      │ rooms.detail
                         │                 │ rooms.create
                         │                 │ rooms.edit
                         │                 │ rooms.delete
5. Kho ảnh               │ images.view     │ images.detail
6. Thống kê              │ statistics.view │ statistics.export
7. Nhân sự               │ staff.view      │ staff.create
                         │                 │ staff.edit
                         │                 │ staff.delete
                         │                 │ staff.permissions
                         │                 │ staff.logs
8. Cấu hình              │ settings.view   │ settings.update
                         │                 │ holidays.manage
                         │                 │ slots.manage
9. AI Brain Center       │ ai.view         │ ai.manage
```

### 4.1. Luật inheritance (cha-con)
- Mỗi action con yêu cầu action Xem của chính module đó (parent key) phải được bật.
- UI: chặn check con khi cha chưa check + popup thông báo.
- UI: uncheck cha → tự động uncheck hết con + popup thông báo.
- Backend: server-side validate lại trước khi lưu.
- CRUD inheritance trong cùng module: edit/create/delete yêu cầu view của module đó (đã được parent key đảm bảo).

### 4.2. SuperAdmin
- Không hiển thị trong danh sách role để cấu hình.
- Cột SuperAdmin disabled/mờ, không cho chỉnh.
- Mọi check đều bypass ở runtime.

## 5. Giao diện — 1 trang duyệt

**Route:** `/admin/staff/permission-matrix` (nằm trong tab Nhân sự & Bảo mật, route mới không xung đột với `permissions/{id}` cũ)

### 5.1. Chế độ "Theo Role"
- Radio/button toggle: ◉ Theo Role
- Dropdown chọn role: Manager / Staff
- Ma trận hiển thị quyền mặc định của role đó
- Lưu → cập nhật `role_permission_templates`

### 5.2. Chế độ "Theo Tài khoản"
- Radio/button toggle: ○ Theo Tài khoản
- Dropdown + filter/sort tìm kiếm tài khoản theo tên/branch
- Ma trận hiển thị: role template (xám/mờ) + override (tích đậm)
- Click checkbox → toggle override cho account đó
- Lưu → cập nhật `AdminUser.permissions_json`

### 5.3. Ma trận (chung cho cả 2 chế độ)
- Hàng: các module theo thứ tự, con thụt vào dưới cha
- Cột: các action key
- Cuối hàng: các nút BẬT TẤT CẢ / TẮT TẤT CẢ cho từng module
- Nút LƯU THAY ĐỔI
- Hiển thị tất cả 9 module (người dùng vào trang này đã có quyền staff.edit, nên cần thấy đủ để cấu hình)

## 6. Authorization enforcement

### 6.1. Backend: `AdminAuthorizeAttribute`
- Sửa `OnAuthorization`: resolve quyền từ role template (cache nhẹ MemoryCache 5 phút) + merge override từ session.
- SuperAdmin vẫn bypass.
- `Permission` string parameter giữ nguyên.

### 6.2. Frontend: `PermissionHelper`
- Cập nhật `HasPermission` dùng cùng logic resolve.
- Session vẫn chứa `AdminPermissions` (override). Nếu không có override cho key đó → fallback về role template.

### 6.3. Fix security gaps
| Controller | Vấn đề | Fix |
|---|---|---|
| `AdminSlotsController` | Không có `[AdminAuthorize]` class-level | Thêm `[AdminAuthorize]` |
| `AdminAIController` | Không có `[AdminAuthorize]` class-level | Thêm `[AdminAuthorize]` |
| `AdminSettingsController` | Có class-level nhưng không có permission key | Thêm permission check `settings.view` / `settings.update` |
| `AdminStatisticsController` | Không có permission key trên action | Thêm `statistics.view`, `statistics.export` |
| `AdminHolidaysController` | Không có `[AdminAuthorize]` class-level | Thêm `[AdminAuthorize]` + `holidays.manage` |

### 6.4. Menu _Layout.cshtml
- AI Brain Center: chuyển từ hardcode `"SuperAdmin"` sang `ai.view`
- Cấu hình hệ thống: chuyển từ hardcode `"SuperAdmin"` sang `settings.view`
- Ẩn nút khi không có quyền; nếu cố vào URL → 403 + toast message

## 7. Implementation Plan (High Level)

1. **Data layer**
   - Tạo entity `RolePermissionTemplate`, migration mới
   - Seed dữ liệu mặc định (Manager: view all + create/edit room/branch/booking; Staff: chỉ view)
   - Cập nhật `ApplicationDbContext`

2. **Backend logic**
   - Sửa `AdminAuthorizeAttribute` resolve từ role template + override
   - Cập nhật `PermissionHelper` tương tự
   - Thêm `PermissionCacheService` (MemoryCache, TTL 5 phút)
   - Thêm `[AdminAuthorize]` cho các controller thiếu

3. **Permission matrix page**
   - Sửa `AdminStaffController.Permissions` (GET/POST) — hỗ trợ role mode + account mode
   - Viết view mới hoặc sửa `Permissions.cshtml`

4. **UI validation inheritance**
   - JavaScript validation cha-con
   - Server-side validation
   - Thông báo tiếng Việt rõ ràng

5. **Menu + frontend gating**
   - Cập nhật `_Layout.cshtml` với permission keys mới
   - Thêm `[AdminAuthorize(Permission)]` cho các action còn thiếu

6. **Tests**
   - Thêm test cho role template resolve
   - Thêm test cho inheritance validation
   - Thêm test cho `AdminAuthorizeAttribute` với role template

## 8. Data Migration
- Tạo migration `AddRolePermissionTemplates`
- Seed Manager: matrix.view, bookings.view+create+edit, branches.view+create+edit, rooms.view+create+edit, images.view, statistics.view, staff.view+logs, settings.view+holidays.manage+slots.manage, ai.view — tất cả action con tương ứng cũng được bật
- Seed Staff: matrix.view, bookings.view, branches.view, rooms.view, images.view — chỉ parent view, không có action con nào
- Copy `permissions_json` từ các `AdminUser` hiện tại (giữ nguyên)

## 9. Security Considerations
- Role template lưu trong DB, không hardcode
- Permission cache có TTL ngắn (5 phút) — không stale lâu
- Backend luôn re-validate, frontend chỉ là UX
- SuperAdmin bypass ở filter layer, không thể bypass từ frontend
