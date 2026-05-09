# Thiết kế: Xác nhận thanh toán thủ công & Gửi vé điện tử qua Email

Tài liệu này mô tả chi tiết tính năng tải minh chứng thanh toán (Bill), quy trình duyệt của nhân viên và tự động gửi vé điện tử (Digital Ticket) cho khách hàng qua Email.

## 1. Mục tiêu
- Cung cấp phương thức thanh toán "cổ điển" nhưng chuyên nghiệp.
- Cho phép khách hàng tải ảnh bill để nhân viên đối soát.
- Tự động hóa việc gửi thông tin nhận phòng (Mã số, PIN, Hướng dẫn) sau khi nhân viên xác nhận.

## 2. Cấu trúc dữ liệu (Data Changes)

Cập nhật model `Booking.cs`:
- `PaymentProofUrl` (string?): Đường dẫn lưu ảnh bill (lưu trong `wwwroot/uploads/payments/`).
- `Status` (string): Thêm các trạng thái `PendingPayment`, `AwaitingApproval`, `Confirmed`, `Rejected`.
- `SmartLockCode` (string?): Mật khẩu khóa điện tử (cấp khi duyệt).
- `WifiPassword` (string?): Mật khẩu Wifi của phòng/chi nhánh.
- `CheckInInstructions` (string?): Hướng dẫn check-in chi tiết.

## 3. Quy trình nghiệp vụ (Workflow)

### Bước 1: Khách hàng thanh toán
- Sau khi nhấn "Đặt phòng", khách được dẫn tới trang `Checkout.cshtml`.
- Trang này hiển thị mã QR VietQR và thông tin chuyển khoản.
- Khách hàng chọn file hoặc chụp ảnh bill, nhấn "Gửi minh chứng".
- Hệ thống lưu file, cập nhật trạng thái đơn hàng thành `AwaitingApproval`.

### Bước 2: Nhân viên duyệt (Admin Dashboard)
- Trang `AdminBookings/Index` hiển thị danh sách đơn hàng.
- Những đơn hàng ở trạng thái `AwaitingApproval` sẽ có icon cảnh báo và nút "Xem Bill".
- Nhân viên bấm "Xem Bill" -> Hiện modal phóng to ảnh bill.
- Nếu hợp lệ, nhấn "Duyệt & Gửi vé". Một form popup hiện ra cho phép Admin điền:
    - **Smart Lock PIN** (Mã mở cửa).
    - **Ghi chú thêm** (nếu có).
- Nhấn "Xác nhận gửi":
    - Cập nhật trạng thái thành `Confirmed`.
    - Gọi `MailService` để gửi email cho khách.

### Bước 3: Gửi vé điện tử (Email Delivery)
- Email được gửi từ tài khoản SMTP của hệ thống.
- Nội dung Email (HTML Template):
    - Lời chào & Xác nhận thanh toán thành công.
    - **Thông tin nhận phòng:** Mã phòng, Mật khẩu cửa, Thời gian Check-in/out.
    - **Hướng dẫn:** Link Google Maps, hướng dẫn các bước mở cửa.
    - **Quy định:** Nội quy homestay, quy định phụ thu.

## 4. Giao diện (UI Design)

### 4.1. Trang Thanh toán (Khách hàng)
- Thiết kế dạng Glassmorphism đồng nhất với trang chủ.
- Vùng tải ảnh bill: Hỗ trợ kéo thả, xem trước ảnh (Preview) trước khi gửi.

### 4.2. Trang Quản trị (Admin)
- Thêm thông báo thời gian thực (Toast) khi có bill mới.
- Cột "Minh chứng" trong bảng: Hiển thị thumbnail ảnh, bấm vào để phóng to bằng thư viện Lightbox hoặc Modal Bootstrap.

## 5. Kỹ thuật triển khai (Technical Specs)
- **File Upload:** Sử dụng `IFormFile`, lưu trữ cục bộ trong `wwwroot`. Tự động đổi tên file theo định dạng `booking_{id}_{timestamp}.jpg` để tránh trùng lặp.
- **Email Service:** Triển khai `IMailService` sử dụng `MailKit` (khuyên dùng) hoặc `System.Net.Mail`.
- **Template:** Sử dụng `RazorViewToString` hoặc String Template để tạo nội dung Email HTML động.

## 6. Kế hoạch kiểm thử (Testing)
- Kiểm tra tải file ảnh kích thước lớn (>5MB).
- Kiểm tra gửi email với các nhà cung cấp khác nhau (Gmail, Mailtrap).
- Kiểm tra logic bảo mật: Chỉ Admin mới xem được ảnh bill.

---
**Ghi chú:** Admin có quyền reset mật khẩu và cập nhật thông tin nhân viên (đã triển khai ở task trước).
