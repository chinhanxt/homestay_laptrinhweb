# Thiết kế: Vòng đời đơn hàng, Đồng hồ đếm ngược & Thùng rác

Tài liệu này mô tả cơ chế kiểm soát thời gian đặt phòng (giống Cinestar) và quy trình xóa/khôi phục đơn hàng chuyên nghiệp.

## 1. Cơ chế Đếm ngược 5 Phút (Cinestar-style)

### 1.1. Luồng nghiệp vụ
- Khi người dùng nhấn "Xác nhận đặt phòng" từ bảng ma trận hoặc trang chi tiết:
    - Hệ thống tạo bản ghi `Booking` với `Status = "AwaitingPayment"`.
    - Ghi nhận `CreatedAt` là thời điểm bắt đầu.
- Tại trang `Success.cshtml` (Trang thanh toán):
    - Hiển thị đồng hồ đếm ngược: `05:00` -> `00:00`.
    - Công thức: `5 phút - (Thời gian hiện tại - CreatedAt)`.
- **Hết giờ:**
    - Nếu khách chưa tải Bill: Trình duyệt tự động chuyển sang trang "Hết hạn", nút "Gửi minh chứng" bị vô hiệu hóa.
    - Server sẽ coi đơn hàng này là `Cancelled`.

### 1.2. Logic khả dụng (Availability)
- Phòng được coi là **HẾT PHÒNG** nếu:
    - Có đơn hàng `Confirmed`, `AwaitingApproval`, `CheckedIn`.
    - HOẶC có đơn hàng `AwaitingPayment` mà thời gian trôi qua chưa quá 5 phút.

## 2. Quản lý Thùng rác (Soft Delete)

### 2.1. Điều kiện xóa
- Chỉ hiển thị nút "Xóa" cho đơn hàng có trạng thái `CheckedOut` hoặc `Cancelled`.
- Khi nhấn Xóa:
    - `IsDeleted = true`.
    - `DeletedAt = DateTime.Now`.
    - Đơn hàng biến mất khỏi danh sách chính.

### 2.2. Giao diện Thùng rác (Trash Bin)
- Admin có menu riêng để xem "Đơn hàng đã xóa".
- Các thao tác trong Thùng rác:
    - **Khôi phục:** Đưa đơn hàng trở lại danh sách chính.
    - **Xóa vĩnh viễn:** Xóa hoàn toàn khỏi Database.

### 2.3. Tự động dọn dẹp (Retention Policy)
- Đơn hàng có `IsDeleted = true` và `DeletedAt` cũ hơn 7 ngày sẽ bị hệ thống tự động xóa vĩnh viễn (hoặc ẩn hoàn toàn khỏi view).

## 3. Các thay đổi về Code

### 3.1. Model `Booking.cs`
- Thêm `bool IsDeleted`.
- Thêm `DateTime? DeletedAt`.

### 3.2. Background Task
- Một tiến trình chạy ngầm (HostedService) mỗi 1 phút để:
    - Hủy các đơn hàng `AwaitingPayment` quá 5 phút.
    - Xóa vĩnh viễn các đơn trong thùng rác quá 7 ngày.

---
*(Xem chi tiết tại bản Implementation Plan tiếp theo).*
