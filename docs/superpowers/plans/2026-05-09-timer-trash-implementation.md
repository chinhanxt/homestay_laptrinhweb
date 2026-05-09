# Kế hoạch triển khai: Timer 5 Phút & Thùng rác đơn hàng

---

### Task 1: Cập nhật Model & Database

**Files:**
- Modify: `WebHomestay/Models/Booking.cs`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`

- [ ] **Step 1: Thêm các trường Soft Delete**
`IsDeleted` và `DeletedAt`.
- [ ] **Step 2: Chạy Migration**
Cập nhật cấu trúc database PostgreSQL.

### Task 2: Cơ chế Đếm ngược 5 Phút (Public)

**Files:**
- Modify: `WebHomestay/Views/Bookings/Success.cshtml`
- Modify: `WebHomestay/Controllers/BookingsController.cs`

- [ ] **Step 1: Cập nhật trạng thái khởi tạo**
Chuyển trạng thái ban đầu của Booking sang `AwaitingPayment`.
- [ ] **Step 2: Nhúng đồng hồ đếm ngược JS vào trang Success**
Xử lý hiển thị đếm ngược từ 5:00. Khi về 0, vô hiệu hóa form gửi Bill và hiện thông báo lỗi.
- [ ] **Step 3: Cập nhật logic AvailabilityService**
Đảm bảo phòng bị khóa trong 5 phút đầu tiên của đơn hàng `AwaitingPayment`.

### Task 3: Quản lý Thùng rác (Admin)

**Files:**
- Modify: `WebHomestay/Controllers/AdminBookingsController.cs`
- Modify: `WebHomestay/Views/AdminBookings/Index.cshtml`
- Create: `WebHomestay/Views/AdminBookings/Trash.cshtml`

- [ ] **Step 1: Viết Action Xóa tạm (Soft Delete)**
Chỉ cho phép xóa khi trạng thái là `CheckedOut` hoặc `Cancelled`.
- [ ] **Step 2: Xây dựng trang Thùng rác**
Hiển thị danh sách `IsDeleted = true`. Thêm nút "Khôi phục" và "Xóa vĩnh viễn".

### Task 4: Tự động hóa dọn dẹp (Background Task)

**Files:**
- Create: `WebHomestay/Services/BookingCleanupService.cs`
- Modify: `WebHomestay/Program.cs`

- [ ] **Step 1: Triển khai IHostedService**
Mỗi phút quét và hủy đơn `AwaitingPayment` > 5 phút. Mỗi ngày quét và xóa vĩnh viễn đơn trong thùng rác > 7 ngày.
- [ ] **Step 2: Đăng ký Service vào DI Container**

---
*(Sử dụng subagent-driven-development để triển khai từng Task).*
