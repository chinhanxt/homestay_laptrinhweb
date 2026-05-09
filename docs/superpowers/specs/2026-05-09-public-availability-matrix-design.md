# Thiết kế: Ma trận phòng tương tác (Public Availability Matrix)

Tài liệu này mô tả tính năng hiển thị lịch trống của tất cả các phòng trên giao diện khách hàng, cho phép người dùng xem nhanh và đặt phòng trực tiếp từ bảng ma trận.

## 1. Mục tiêu
- Giúp khách hàng có cái nhìn tổng quan về tình trạng phòng của toàn bộ hệ thống.
- Tăng tỷ lệ chuyển đổi bằng cách cho phép chọn ngày và đặt phòng nhanh (Click-to-Book).
- Minh bạch hóa thông tin về các chi nhánh để khách không bị nhầm lẫn.

## 2. Giao diện (UI/UX)

### 2.1. Cấu trúc bảng Ma trận
- **Đầu trang:** Bộ lọc thời gian (Chọn tháng/năm) và Chú thích màu sắc (Trống, Đã đặt, Chờ duyệt).
- **Trục dọc (Y-axis):** Danh sách các phòng.
    - Các phòng được nhóm theo **Chi nhánh** (Branch).
    - Mỗi nhóm chi nhánh có tiêu đề lớn, icon địa điểm và màu sắc nhận diện riêng.
- **Trục ngang (X-axis):** Các ngày trong tháng (hiển thị 30 ngày từ ngày hiện tại).
- **Ô dữ liệu (Cells):**
    - **Màu Xanh (Trống):** Có thể đặt. Khi di chuột vào sẽ hiện hiệu ứng highlight.
    - **Màu Đỏ (Đã đặt):** Không thể chọn.
    - **Màu Vàng (Chờ duyệt):** Đang có khách giữ chỗ nhưng chưa thanh toán.

### 2.2. Tính năng tương tác (Click-to-Book)
1. **Bước 1:** Khách nhấn vào một ô "Trống".
2. **Bước 2:** Một Modal (cửa sổ nhỏ) hiện lên ngay tại vị trí đó hiển thị:
    - Tên phòng & Chi nhánh.
    - Giá tiền/đêm.
    - Ô chọn ngày kết thúc (End Date) - mặc định là ngày tiếp theo.
3. **Bước 3:** Nhấn "Đặt ngay" -> Chuyển hướng thẳng tới trang `Checkout` với các thông tin ngày đã chọn sẵn.

## 3. Kỹ thuật triển khai (Technical Specs)

### 3.1. Backend
- **Controller:** Tạo `MatrixController` (hoặc tích hợp vào `HomeController`).
- **Logic:** 
    - Lấy danh sách tất cả Chi nhánh và Phòng.
    - Lấy tất cả các Đơn đặt phòng (Booking) có trạng thái khác `Cancelled` trong khoảng thời gian 30 ngày tới.
    - Sử dụng `AvailabilityService` để tính toán trạng thái cho từng ô (Room, Date).

### 3.2. Frontend
- **HTML/CSS:** Sử dụng CSS Grid/Table với `sticky` header và `sticky` column (để khi cuộn ngang vẫn thấy tên phòng, cuộn dọc vẫn thấy ngày).
- **JavaScript:** 
    - Xử lý việc cuộn ngang mượt mà trên mobile.
    - Xử lý Modal chọn ngày nhanh.

## 4. Bảo mật & Hiệu năng
- **Caching:** Vì dữ liệu ma trận ít thay đổi liên tục, có thể cache kết quả trong 1-2 phút để giảm tải cho DB.
- **Lazy Loading:** Chỉ tải dữ liệu cho 30 ngày hiện tại, khi khách chuyển tháng mới tải tiếp.

---
*(Xem mockup hình ảnh đính kèm để biết thêm chi tiết về thẩm mỹ thiết kế).*
