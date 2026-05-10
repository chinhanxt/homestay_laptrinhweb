# Tài liệu thiết kế: Tái cấu trúc Quản lý Phòng và Hệ thống Giá linh hoạt

**Ngày:** 2026-05-10
**Trạng thái:** Đang chờ phê duyệt

## 1. Mục tiêu
Tái cấu trúc trang quản lý phòng (Thêm/Sửa) thành giao diện 2 bảng chuyên nghiệp, hỗ trợ cấu hình giá đa dạng theo ngày nghỉ/lễ và quản lý hình ảnh trực tiếp từ máy tính.

## 2. Thay đổi về Dữ liệu (Database)

### Cập nhật Model `Room`
- Thêm `PriceWeekend` (decimal): Giá áp dụng cho Thứ 7 và Chủ Nhật.
- Thêm `PriceHoliday` (decimal): Giá áp dụng cho các ngày Lễ.
- Cập nhật `Status`: Loại bỏ tùy chọn "Occupied" (chỉ còn Available, Maintenance).
- Chuyển `ImageUrl` thành lưu đường dẫn file nội bộ thay vì URL tuyệt đối.
- Thêm bảng `RoomImage` hoặc trường `AdditionalImages` (JSON) để lưu 5 ảnh minh họa.

### Model mới `Holiday` (Cấu hình hệ thống)
- `Id` (int)
- `Date` (DateTime): Ngày được xác định là ngày lễ.
- `Name` (string): Tên ngày lễ (Vd: Giải phóng miền Nam).

## 3. Giao diện (UI/UX) - Trang Create/Edit Room

### Bố cục: 2 Bảng (Panel)
- **Bảng trái: Thông tin cơ bản**
  - Tên phòng, Chi nhánh.
  - Trạng thái vận hành (Dropdown: Sẵn sàng, Bảo trì).
  - Mô tả tiện ích (Textarea).
  - Ảnh đại diện (File Input).
  - 5 Ảnh minh họa (File Inputs/Drag & Drop - Bắt buộc 5 ảnh).
- **Bảng phải: Cấu hình giá & Quy định**
  - Giá ngày thường.
  - Giá Thứ 7 & CN.
  - Giá ngày Lễ.
  - Giá theo giờ.
  - Phí khách thêm.
  - Sức chứa tiêu chuẩn & tối đa.

## 4. Logic Nghiệp vụ (Business Logic)

### Tính toán giá (Priority Logic)
Khi người dùng chọn ngày đặt phòng, hệ thống sẽ xác định đơn giá theo thứ tự ưu tiên:
1. **Ưu tiên 1 (Lễ):** Nếu ngày chọn nằm trong danh sách `Holiday` toàn cục.
2. **Ưu tiên 2 (Cuối tuần):** Nếu ngày chọn là Thứ 7 hoặc Chủ Nhật.
3. **Ưu tiên 3 (Ngày thường):** Các trường hợp còn lại.

### Quản lý Hình ảnh
- Sử dụng `IFormFile` trong Controller để nhận file ảnh.
- Lưu ảnh vào thư mục `wwwroot/uploads/rooms/`.
- Tự động đổi tên file để tránh trùng lặp.

## 5. Kế hoạch triển khai (Sơ bộ)
1. Cập nhật Model và chạy Migrations.
2. Tạo trang quản lý ngày Lễ (System Config).
3. Xây dựng dịch vụ tính toán giá (PricingService).
4. Refactor giao diện Create/Edit Room với CSS Grid/Flexbox để chia 2 bảng.
5. Cập nhật logic Upload file trong AdminRoomsController.
