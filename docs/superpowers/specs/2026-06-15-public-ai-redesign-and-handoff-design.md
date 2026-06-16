# Tài liệu thiết kế: Nâng cấp Backend và Giao diện AI Khách Hàng (StayEasy Assistant)

**Ngày:** 15/06/2026  
**Trạng thái:** Bản phác thảo Thiết kế  
**Mục tiêu:** Tái cấu trúc bộ xử lý Backend (Conductor) của AI khách hàng để đồng bộ động với cấu hình từ trang Admin AI Studio; đồng thời thiết kế lại toàn bộ giao diện khung chat phía khách hàng theo phong cách Luxury Editorial (kính mờ sang trọng, vàng đồng và xanh trầm).

---

## 1. Kiến trúc Backend & Đồng bộ Cấu hình động

Để loại bỏ các logic code cứng hiện tại, `ContextAwareBookingConductor` sẽ được nâng cấp để đọc trực tiếp cấu hình từ `AdminAIStudioConfig`.

### 1.1. Luồng lọc trường còn thiếu (Dynamic Gating Flow)
*   **Trạng thái cũ:** Kiểm tra tĩnh các trường `branchId`, `bookingMode`, `hourlyDate`, `checkInDate`, `checkOutDate`, `guestCount`.
*   **Trạng thái mới:**
    1. Khi gọi `DecideAsync`, Conductor gọi `IAdminAIStudioConfigService.GetAsync()` để lấy đối tượng cấu hình hợp nhất `AdminAIStudioConfig`.
    2. Xác định luồng hội thoại thích hợp (`daily` hoặc `hourly`) dựa trên `state.BookingMode` hiện tại.
    3. Lấy danh sách các khoá trường bắt buộc (`RequiredFieldKeys`) từ luồng đó (ví dụ: `["branchId", "hourlyDate", "hourlySlot", "guestCount"]`).
    4. Xác định các trường bị thiếu bằng cách so khớp danh sách này với thông tin đã thu thập trong `BookingConfirmedState`.
    5. Với trường đầu tiên bị thiếu, tra cứu trong cấu hình `FieldDefinitions` để tìm `InputType` tương ứng và hiển thị đúng khối UI được thiết lập.

### 1.2. Chuyển giao hỗ trợ người thật (Human Handoff)
*   **Điều kiện kích hoạt:**
    *   Trong `ClassifyIntent`, nếu tin nhắn của khách hàng chứa bất kỳ từ khóa nào trong danh sách `studioConfig.Handoff.Keywords` (ví dụ: *"nhân viên", "gặp người", "khiếu nại", "hỗ trợ gấp"*), hoặc nếu hệ thống AI tự đưa ra nhãn chuyển giao.
*   **Hành động xử lý:**
    1.  Cập nhật trạng thái của `AdminChatSession` thành `Status = "paused"` và `PauseReason = "handoff_request"`.
    2.  Thêm tin nhắn hệ thống ghi lại yêu cầu handoff để hiển thị trên monitor.
    3.  Bắn thông báo realtime bằng SignalR qua `ChatHub` đến group `admin_monitor` để thông báo cho nhân viên.
    4.  Cập nhật phản hồi chatbot với câu chào Handoff của chi nhánh (`handoff.ContactInstruction`) và trả về khối UI loại `handoffContact`.

### 1.3. Ánh xạ trường ghi chú (Notes mapping)
*   Hỗ trợ đọc dữ liệu ghi chú từ cả trường `"notes"` và `"customerNote"` trong phương thức xử lý hành động `"submit-booking-form"` để tránh mất dữ liệu của khách hàng.

---

## 2. Thiết kế Giao diện Khách hàng (Luxury Editorial UI)

Toàn bộ phong cách thiết kế sẽ chuyển từ dạng phẳng cơ bản sang dạng **Kính mờ thanh lịch** để tạo cảm giác cực kỳ cao cấp.

### 2.1. Cấu trúc các khối CSS mới (`user-premium.css`)
*   **Bảng màu và các biến Luxury:**
    *   Chủ đạo: Xanh đá trầm (`--luxury-primary: #1e293b`) và Vàng đồng (`--luxury-accent: #c29b61`).
    *   Nền khung chat: Trắng kính mờ `rgba(255, 255, 255, 0.88)` kèm `backdrop-filter: blur(20px)`.
*   **Avatar & Trạng thái hoạt động (Pulse):**
    *   Tiêu đề khung chat hiển thị ảnh đại diện trợ lý ảo kèm hiệu ứng nhấp nháy xanh lá cây `online-pulse` ở cạnh tên hiển thị.
*   **Hoạt ảnh mở/đóng:**
    *   Sử dụng hoạt ảnh scale nhẹ từ góc dưới bên phải kèm trượt lên mượt mà.

### 2.2. Nâng cấp hiển thị Khối UI (UI Blocks)
*   **Khối Thẻ phòng (`roomCards`):**
    *   **Thêm Ảnh phòng:** Hiển thị thẻ ảnh phòng ở đầu thẻ phòng AI tư vấn (`room.imageUrl`) với tỉ lệ `16:9`, bo góc mượt, tự động căn tỉ lệ ảnh (`object-fit: cover`).
    *   **Thẻ phụ thu (Occupancy Badge):** Hiển thị nhãn *Chuẩn số khách* (viền vàng đồng) hoặc nhãn cảnh báo *Vượt chuẩn - có phụ thu* nếu khách vượt công suất chuẩn nhưng vẫn trong phạm vi tối đa.
    *   **Nhãn khả dụng:** Hiển thị nhãn xanh lá báo phòng còn trống đúng khung giờ khách vừa hỏi.
*   **Khối Liên hệ Handoff (`handoffContact`):**
    *   Hiển thị thông tin chi nhánh (Tên, địa chỉ, hotline).
    *   Nút **Gọi Hotline** liên kết `tel:<Hotline>`.
    *   Nút **Chat Zalo** mở link chat Zalo dựa trên số điện thoại chi nhánh.
    *   Nút **Bản đồ** mở link Google Maps chi nhánh trong tab mới.
*   **Khối Chọn giờ (`hourlySlots`):**
    *   Thiết kế lưới slot bo tròn, nền kem nhạt mịn, đổi sang viền vàng đồng khi hover và màu nền đồng đặc khi được chọn.

---

## 3. Kế hoạch xác minh (Verification Plan)

### 3.1. Kiểm tra tự động (Automated Tests)
*   Bổ sung các test case trong `WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs` để xác thực:
    *   Kiểm tra từ khóa handoff tự động chuyển trạng thái session thành `paused`.
    *   Gating động lấy đúng thông tin thiếu theo cấu hình.
*   Chạy kiểm tra: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj`

### 3.2. Kiểm tra thủ công (Manual Verification)
*   Chạy ứng dụng: `dotnet run --project WebHomestay/WebHomestay.csproj`
*   Thực hiện hội thoại trên giao diện khách hàng:
    *   Gõ yêu cầu tư vấn đặt phòng để kiểm tra giao diện kính mờ và thẻ phòng có ảnh.
    *   Gõ các từ khóa cứu trợ (*gặp nhân viên*, *hỗ trợ gấp*) để kiểm tra xem hệ thống có tự động hiện thẻ liên hệ chi nhánh và tạm dừng AI, chuyển trạng thái cho nhân viên trên trang monitor chat hay không.
