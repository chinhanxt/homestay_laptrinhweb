# Thiết kế: Đồng bộ & Bảo toàn trạng thái "Đã đặt" trên Lịch

Hệ thống hiện tại gặp lỗi khi người dùng chọn ngày thủ công hoặc chuyển tháng trên lịch động, dẫn đến việc mất dấu các ngày đã có khách đặt. Tài liệu này mô tả giải pháp sử dụng Quản lý Trạng thái (State Management) tập trung để khắc phục triệt để vấn đề.

## 1. Mục tiêu
- **Bảo toàn dữ liệu**: Đảm bảo các ngày "Đã đặt" (Blocked) luôn được hiển thị đúng trạng thái dù người dùng chuyển tháng hay chọn tay.
- **Ngăn chặn sai sót**: Không cho phép chọn ngày đã đặt thông qua cả lưới lịch và ô nhập thủ công.
- **Trải nghiệm cao cấp**: Sử dụng Toast Message để thông báo lỗi thay vì Alert hệ thống.

## 2. Kiến trúc giải pháp

### A. Quản lý trạng thái (Global State)
Tạo một đối tượng JavaScript toàn cục để lưu trữ:
- `blockedDates`: Mảng chứa các chuỗi ngày (YYYY-MM-DD) đã bị khóa.
- `selection`: Đối tượng lưu `checkIn` và `checkOut`.

### B. Luồng dữ liệu
1. **Khởi tạo (On Load)**: 
   - Quét toàn bộ phần tử `.lux-cal-day.blocked` được Server render ban đầu.
   - Trích xuất `data-date` và lưu vào mảng `blockedDates`.
2. **Xác thực (Validation)**:
   - Khi người dùng thay đổi ngày (Manual Input hoặc Click Grid), hệ thống sẽ đối chiếu với `blockedDates`.
   - Nếu ngày thuộc danh sách bị khóa: Hiển thị **Toast Message** và reset giá trị ô nhập.
3. **Hiển thị lịch động (Rendering)**:
   - Hàm `renderDynamicGrid` khi vẽ từng ô ngày sẽ kiểm tra xem ngày đó có nằm trong `blockedDates` hay không.
   - Nếu có: Áp dụng class `.blocked`, thêm nhãn "Đã đặt" và vô hiệu hóa click.

## 3. Giao diện & Tương tác (UI/UX)
- **Toast Message**: Thiết kế một component thông báo nhỏ, nổi lên ở giữa/góc màn hình với màu sắc hài hòa (Luxury style).
- **Trạng thái ô ngày**: 
  - Ô bị khóa: Độ mờ (opacity) giảm, không có hiệu ứng hover, nhãn "Đã đặt" nằm giữa.
  - Ô đang chọn: Giữ nguyên logic màu vàng/xanh hiện tại.

## 4. Kế hoạch triển khai (Sơ bộ)
- [ ] Cập nhật `room-booking.js` để thêm bộ quản lý trạng thái.
- [ ] Triển khai hàm `showToast` tùy biến.
- [ ] Chỉnh sửa `renderDynamicGrid` để tích hợp kiểm tra trạng thái khóa.
- [ ] Kiểm tra hồi quy (Regression test) trên cả chế độ Theo ngày và Theo giờ.

---
**Ghi chú**: Thiết kế này ưu tiên tính ổn định của dữ liệu và cảm giác mượt mà khi tương tác.
