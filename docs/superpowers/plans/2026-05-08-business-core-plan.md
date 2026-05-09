# WebHomestay Business Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Triển khai hệ thống đặt phòng đa chi nhánh, quản lý phòng trống và bảo mật thông tin CCCD.

**Architecture:** Sử dụng ASP.NET Core MVC với Entity Framework Core. Bảo mật file CCCD bằng cách lưu ngoài thư mục web công khai và kiểm soát truy cập qua Controller.

**Tech Stack:** .NET 8, EF Core, PostgreSQL, FullCalendar.js.

---

### Task 1: Cập nhật Model và Database Schema

**Files:**
- Create: `WebHomestay/Models/Branch.cs`
- Modify: `WebHomestay/Models/Room.cs`
- Modify: `WebHomestay/Models/Booking.cs`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`

- [ ] **Bước 1: Tạo Model Branch**
    - Tạo file `WebHomestay/Models/Branch.cs` với các trường: `Id`, `Name`, `Address`, `Description`, `Hotline`, `MapUrl`.
- [ ] **Bước 2: Cập nhật Room Model**
    - Thêm `BranchId` và `Branch` navigation property.
    - Thêm `MaxGuests`, `BasePrice`, `ExtraGuestFee`.
- [ ] **Bước 3: Cập nhật Booking Model**
    - Thêm: `CustomerName`, `CustomerPhone`, `CustomerEmail`, `CustomerZalo`, `GuestCount`.
    - Thêm: `IdCardFrontPath`, `IdCardBackPath`, `CustomerNote`, `AdminNote`.
- [ ] **Bước 4: Cập nhật ApplicationDbContext**
    - Đăng ký `DbSet<Branch>`.
    - Cấu hình quan hệ 1-N giữa Branch và Room.
- [ ] **Bước 5: Tạo và chạy Migration**
    - Chạy `dotnet ef migrations add AddBranchAndBookingUpdates`.
    - Chạy `dotnet ef database update`.
- [ ] **Bước 6: Commit**

### Task 2: Quản trị Chi nhánh (Admin Branch CRUD)

**Files:**
- Create: `WebHomestay/Controllers/AdminBranchesController.cs`
- Create: `WebHomestay/Views/AdminBranches/Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Delete.cshtml`

- [ ] **Bước 1: Tạo AdminBranchesController**
    - Sử dụng Scaffolding hoặc viết tay để hỗ trợ CRUD.
- [ ] **Bước 2: Tạo giao diện Razor Views cho Chi nhánh**
- [ ] **Bước 3: Kiểm tra chức năng thêm/sửa chi nhánh trên trình duyệt**
- [ ] **Bước 4: Commit**

### Task 3: Cập nhật Quản trị Phòng (Admin Room CRUD with Branch)

**Files:**
- Modify: `WebHomestay/Controllers/AdminRoomsController.cs`
- Modify: `WebHomestay/Views/AdminRooms/Create.cshtml`, `Edit.cshtml`

- [ ] **Bước 1: Cập nhật Controller để hỗ trợ chọn Chi nhánh khi tạo phòng**
- [ ] **Bước 2: Cập nhật Views để có Dropdown chọn Branch**
- [ ] **Bước 3: Commit**

### Task 4: Logic Kiểm tra Phòng trống (Availability Service)

**Files:**
- Create: `WebHomestay/Services/IAvailabilityService.cs`
- Create: `WebHomestay/Services/AvailabilityService.cs`

- [ ] **Bước 1: Triển khai IAvailabilityService**
    - Phương thức: `Task<bool> IsRoomAvailable(int roomId, DateTime start, DateTime end)`.
- [ ] **Bước 2: Đăng ký Service trong Program.cs**
- [ ] **Bước 3: Commit**

### Task 5: Form Đặt phòng và Xử lý Bảo mật CCCD

**Files:**
- Create: `WebHomestay/Controllers/BookingsController.cs`
- Create: `WebHomestay/Views/Bookings/Checkout.cshtml`
- Create: `WebHomestay/Views/Bookings/Success.cshtml`

- [ ] **Bước 1: Xây dựng Form Checkout**
    - Cho phép nhập thông tin khách hàng và upload 2 file ảnh CCCD.
- [ ] **Bước 2: Logic xử lý Upload ảnh bảo mật**
    - Lưu vào thư mục `App_Data/IDCards/`.
    - Đổi tên file thành GUID.
- [ ] **Bước 3: Logic tính tổng tiền (TotalPrice)**
- [ ] **Bước 4: Hiển thị trang thành công với Mã QR chuyển khoản**
- [ ] **Bước 5: Commit**

### Task 6: Quản lý Đơn hàng và Xem CCCD (Dành cho Admin)

**Files:**
- Create: `WebHomestay/Controllers/AdminBookingsController.cs`
- Create: `WebHomestay/Views/AdminBookings/Index.cshtml`, `Details.cshtml`

- [ ] **Bước 1: Action ViewIDCard trong AdminBookingsController**
    - Kiểm tra Authorize.
    - Trả về `FileStream` từ thư mục bảo mật.
- [ ] **Bước 2: Giao diện chi tiết đơn hàng cho Admin**
    - Hiển thị thông tin khách và link xem ảnh CCCD.
    - Nút xác nhận thanh toán/Hủy đơn.
- [ ] **Bước 3: Commit**
