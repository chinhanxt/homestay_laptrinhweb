# Thiết kế: Thanh chọn ngày Premium & Chọn ngày xa (Mode Giờ)

Tài liệu này mô tả việc nâng cấp giao diện và tính năng chọn ngày cho chế độ đặt phòng theo giờ, nhằm mang lại trải nghiệm Luxury và linh hoạt hơn.

## 1. Mục tiêu
- **Thẩm mỹ cao cấp**: Nâng cấp thanh chọn ngày (Date Chips) với hiệu ứng nổi khối, đổ bóng và typography sang trọng.
- **Linh hoạt thời gian**: Cho phép khách hàng đặt phòng theo giờ vào các ngày xa hơn trong tương lai thông qua ô chọn ngày thủ công.
- **Tương tác mượt mà**: Đồng bộ hóa dữ liệu khung giờ ngay khi ngày thay đổi.

## 2. Thành phần giao diện (UI Components)

### A. Thanh chọn ngày ngang (Horizontal Date Bar)
- **Cấu trúc**: Danh sách các "Chip" ngày có thể cuộn ngang.
- **Style**:
  - `lux-date-chip`: Bo góc 15px-18px, border mỏng (#eee).
  - `active`: Scale 1.1, đổ bóng `0 10px 30px rgba(184, 146, 94, 0.2)`, màu chủ đạo vàng đồng.
  - Biên thanh: Sử dụng dải màu gradient mờ để chỉ dẫn cuộn ngang.

### B. Ô chọn ngày thủ công (Manual Date Picker)
- **Vị trí**: Phía trên thanh chọn ngày ngang.
- **Chức năng**: Mở lịch hệ thống/custom để chọn ngày bất kỳ.
- **Đồng bộ**: Khi ngày thay đổi, thanh ngang bên dưới sẽ được vẽ lại bắt đầu từ ngày được chọn.

## 3. Logic xử lý (Interaction Logic)

1. **Khởi tạo**: Mặc định hiển thị 14 ngày kể từ ngày hiện tại trên thanh ngang.
2. **Chọn ngày thủ công**: 
   - Người dùng chọn ngày X.
   - JavaScript cập nhật trạng thái `currentHourlyDate = X`.
   - Vẽ lại thanh ngang với ngày X làm trung tâm/điểm bắt đầu.
   - Gọi API/Hàm `loadHourlySlots(X)` để tải lại lưới giờ.
3. **Chọn trên thanh ngang**:
   - Cập nhật trạng thái và tải lại lưới giờ tương ứng.

## 4. Kế hoạch triển khai (Sơ bộ)
- [ ] Cập nhật HTML cấu trúc mới cho `WebHomestay/Views/Rooms/Details.cshtml`.
- [ ] Bổ sung CSS cho thanh Premium trong `WebHomestay/wwwroot/css/user-premium.css`.
- [ ] Viết hàm `renderHourlyDateBar(startDate)` và listener đồng bộ trong `WebHomestay/wwwroot/js/room-booking.js`.

---
**Ghi chú**: Đảm bảo hiệu ứng chuyển động khi vẽ lại thanh ngang không gây giật lag (Layout shift).
