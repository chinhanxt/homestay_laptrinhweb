# Tài liệu Thiết kế Hệ thống Web Homestay - Business Core

**Ngày tạo:** 2026-05-08
**Trạng thái:** Dự thảo (Chờ phê duyệt)

## 1. Tổng quan
Hệ thống đặt phòng Homestay tập trung vào trải nghiệm người dùng không cần đăng nhập, hỗ trợ quản lý đa chi nhánh và đảm bảo bảo mật thông tin cá nhân (CCCD) của khách hàng.

## 2. Cấu trúc Dữ liệu (Schema)

### 2.1. Branch (Chi nhánh)
- `Id`: int (PK)
- `Name`: string (100) - Tên chi nhánh
- `Address`: string (500) - Địa chỉ cụ thể
- `Description`: text - Mô tả chi nhánh
- `Hotline`: string (20)
- `MapUrl`: string (max) - Link Google Maps nhúng
- `Images`: string (max) - Danh sách URL ảnh (JSON hoặc bảng riêng)

### 2.2. Room (Phòng) - Cập nhật
- `Id`: int (PK)
- `BranchId`: int (FK -> Branch)
- `Name`: string (100)
- `Description`: text
- `BasePrice`: decimal - Giá gốc
- `ExtraGuestFee`: decimal - Phí phụ thu mỗi người thêm
- `MaxGuests`: int - Số người tối đa
- `Capacity`: int - Số người tiêu chuẩn
- `Status`: string (Available, Maintenance)
- `Amenities`: Many-to-Many với bảng `Amenity`

### 2.3. Booking (Đơn đặt phòng) - Cập nhật
- `Id`: int (PK)
- `RoomId`: int (FK -> Room)
- `CustomerName`: string (200)
- `CustomerPhone`: string (20)
- `CustomerEmail`: string (200)
- `CustomerZalo`: string (100)
- `GuestCount`: int
- `StartTime`: datetime
- `EndTime`: datetime
- `TotalPrice`: decimal
- `Status`: string (Pending, Confirmed, Cancelled, Completed)
- `PaymentStatus`: string (Unpaid, Paid)
- `IdCardFrontPath`: string - Đường dẫn tệp CCCD (Lưu bảo mật)
- `IdCardBackPath`: string - Đường dẫn tệp CCCD (Lưu bảo mật)
- `CustomerNote`: text
- `AdminNote`: text
- `CreatedAt`: datetime

### 2.4. User (Admin)
- `Id`: int (PK)
- `Username`: string
- `PasswordHash`: string
- `Role`: enum (SuperAdmin, Admin, Staff)

## 3. Kiến trúc Bảo mật (CCCD)
- **Lưu trữ:** Tệp ảnh tải lên được lưu tại `App_Data/SecureUploads/IDCards/` (nằm ngoài `wwwroot`).
- **Định danh:** Tệp được đổi tên thành GUID để tránh tấn công brute-force tên tệp.
- **Truy cập:** Chỉ Admin mới có thể xem ảnh thông qua một Action có kiểm tra `Authorize` trong Controller. Dữ liệu được trả về dưới dạng Stream nhị phân.

## 4. Logic Nghiệp vụ chính

### 4.1. Kiểm tra phòng trống (Availability)
- Một phòng được coi là trống nếu không có đơn đặt phòng nào có `Status` là 'Confirmed' hoặc 'Pending' (trong thời gian chờ thanh toán) bị trùng lặp thời gian.
- Truy vấn: `NOT (Booking.StartTime < EndTime AND Booking.EndTime > StartTime)`

### 4.2. Tính giá
- `TotalPrice = (BasePrice * Số ngày/giờ) + (GuestCount - Capacity) * ExtraGuestFee` (nếu GuestCount > Capacity).

## 5. Luồng Giao diện (UI/UX)

### 5.1. Khách hàng
- **Home:** Slide ảnh, giới thiệu chi nhánh.
- **Branch Page:** Thông tin chi nhánh và danh sách phòng.
- **Room Detail:** Bộ lịch tháng (FullCalendar) hiển thị ngày trống/bận. Cho phép chọn ngày.
- **Booking Form:** Thu thập thông tin cá nhân và ảnh CCCD (Ajax upload).
- **Payment Page:** Hiển thị mã QR ngân hàng và hướng dẫn chuyển khoản.

### 5.2. Admin
- **Login:** `/admin/login`.
- **Order Management:** Danh sách đơn hàng, xem ảnh CCCD, xác nhận thanh toán.
- **Content Management:** CRUD Chi nhánh, Phòng, Tiện nghi.

## 6. Công nghệ sử dụng
- **Backend:** ASP.NET Core MVC, Entity Framework Core.
- **Database:** PostgreSQL.
- **Frontend:** Vanilla JS/jQuery, CSS, FullCalendar.js cho phần lịch.
- **File Storage:** Local Server Storage (Secure).

## 7. Kế hoạch triển khai (Giai đoạn 1)
1. Tạo Model Branch và cập nhật DB.
2. Xây dựng trang Admin CRUD Chi nhánh/Phòng.
3. Triển khai Logic kiểm tra phòng trống.
4. Xây dựng Form đặt phòng và xử lý lưu ảnh CCCD bảo mật.
5. Hoàn thiện trang hiển thị mã QR thanh toán.
