# AI Chatbot Refactoring (Đại phẫu thuật Chatbot)

## Tiêu chí thành công
- Chatbot đóng vai trò sale/tư vấn, chèo kéo khách, định hướng khách đến việc đặt phòng hoặc để lại thông tin liên lạc (Zalo/SĐT) để nhân viên thật tư vấn.
- Giao diện chat được đơn giản hóa: Không còn nhúng Form điền thông tin và Form tải CCCD/Bill trong khung chat.
- Trạng thái chat được bảo lưu qua `sessionStorage` giúp khách chuyển trang hay F5 không bị mất tin nhắn.
- UI Chatbot: Thay vì hiện nút "Đặt phòng này" (như cũ), chatbot sẽ đưa ra nút "Xem phòng". Khi gom đủ ngữ cảnh (Ngày nhận, ngày trả, phòng), AI tạo ra một UI Card "Xác nhận nhu cầu" chứa link liên kết chuyển thẳng sang trang Checkout (`/Bookings/CheckoutDaily` hoặc `CheckoutHourly`) với các tham số tương ứng đã điền sẵn.
- Trình quản trị (Admin): Xóa bỏ tab "Booking Form Designer". Các cấu hình liên quan đến Prompt, quy tắc Sale sẽ được gộp vào "Final Response Synthesizer".

## Các thay đổi chính (Kiến trúc)

### 1. Frontend - `site.js` (Chat Widget UI)
- **Lưu trữ Session & Tin nhắn:** 
  - Khởi tạo `sessionId` bằng cách lưu vào `sessionStorage`. Nếu có sẵn, sử dụng lại.
  - Lưu danh sách tin nhắn hiện tại (`messages` HTML) hoặc trạng thái phiên vào `sessionStorage` mỗi khi có thay đổi. Khi tải lại trang, phục hồi lại giao diện chat từ `sessionStorage`.
- **UI Blocks mới:**
  - Hỗ trợ block loại `checkoutLink` thay vì `bookingForm`.
  - Đổi các nút "Chọn phòng này" trong block `roomCards` thành nút "Xem phòng" (chuyển hướng sang trang chi tiết phòng dựa theo link). Nút chọn phòng để AI giữ bối cảnh vẫn có thể giữ nhưng cần điều hướng text thành "Tư vấn phòng này".
- **Gỡ bỏ code thừa:**
  - Bỏ toàn bộ code sinh form trong JS (`renderBookingForm`, `submitCancellationForm` nếu không dùng nữa, logic xử lý upload ảnh, v.v...).

### 2. Backend - API `AIChatController.cs`
- Loại bỏ các endpoint không còn cần thiết cho chat public:
  - `[HttpPost("booking-id-card")]`
  - `[HttpPost("payment-proof")]`
- Sửa lại endpoint logic cho Booking Conductor để không yêu cầu Submit form nữa.

### 3. Backend - `EnhancedBookingConductor.cs` (hoặc `ContextAwareBookingConductor.cs`)
- Thay đổi logic `CheckFormReadiness`: Trạng thái cuối không phải là `bookingForm` nữa mà là `checkoutLink`.
- Trả về đối tượng JSON dạng UI Block cho link:
  - Ví dụ: `{ "type": "checkoutLink", "data": { "roomId": 4, "checkInDate": "2026-06-08", "checkOutDate": "2026-06-09", "url": "/Bookings/CheckoutDaily?roomId=4&checkInDate=..." } }`
- Prompt cho AI: Điều chỉnh System Prompt để AI hiểu rằng nó cần chèo kéo, thu thập SĐT hoặc xin khách click vào link đặt phòng để tự điền. Nó không được hứa hẹn việc tự đặt phòng thành công ngay lập tức.

### 4. Admin UI - `AdminAIController.cs` & `Views/AdminAI/Index.cshtml`
- **Xóa giao diện & Logic:** Xóa bỏ phần giao diện "Booking Form Designer". 
- **Cấu hình:** Đưa toàn bộ cấu hình System Prompt, Style, Exit Keywords, Proactive Mode lên phần "Final Response Synthesizer" (hoặc thiết kế lại trang này thành AI Persona & Sales Setup).
- Điều chỉnh Database / SystemSettings (nếu cần thiết) để xóa các key liên quan đến form schema.

## Kế hoạch kiểm thử (Testing)
- Truy cập vào trang web, nhắn tin với chatbot.
- Tải lại trang (F5) xem cửa sổ chat và lịch sử có giữ nguyên không.
- Đi theo luồng chat đến lúc AI hiểu được Ngày, Giờ và Phòng. Xác minh AI hiển thị Thẻ chứa Link Checkout.
- Bấm vào link để chắc chắn nó chuyển đúng sang trang thông tin điền (CheckoutDaily/Hourly).
- Kiểm tra lại giao diện Admin AI xem "Booking Form Designer" đã bị xóa và các cấu hình Prompt vẫn hoạt động tốt.

---
*Tài liệu này đóng vai trò Spec cho quá trình thực thi tiếp theo.*
