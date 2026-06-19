# 🤝 Hướng Dẫn Đóng Góp (Contributing Guidelines)

Chào mừng các thành viên nhóm **23DTHC3** và các nhà phát triển tham gia đóng góp cho dự án **WebHomestay**. Để đảm bảo chất lượng mã nguồn luôn sạch sẽ, dễ bảo trì và tránh xung đột khi làm việc nhóm, vui lòng tuân thủ các quy tắc dưới đây.

---

## 🚀 Quy Trình Phát Triển Tính Năng (Workflow)

1. **Đồng bộ mã nguồn mới nhất:**
   Trước khi bắt đầu bất kỳ tính năng nào, hãy đồng bộ nhánh chính (`main` hoặc `feat/analytics-bento-pro` tùy giai đoạn) từ GitHub về máy cá nhân:
   ```bash
   git checkout main
   git pull origin main
   ```

2. **Tạo nhánh phát triển mới:**
   Tạo nhánh với tên gợi nhớ theo định dạng `feature/ten-tinh-nang` hoặc `bugfix/ten-loi`:
   ```bash
   git checkout -b feature/booking-auto-refund
   ```

3. **Viết code và kiểm thử cục bộ:**
   * Hãy chắc chắn rằng dự án của bạn biên dịch thành công (`dotnet build`).
   * Chạy kiểm thử tự động (`dotnet test`) để đảm bảo không phá vỡ logic cũ.

4. **Commit và Push lên Remote:**
   Commit code theo tiêu chuẩn thông điệp commit (xem bên dưới) và push nhánh lên GitHub:
   ```bash
   git push origin feature/booking-auto-refund
   ```

5. **Tạo Pull Request (PR):**
   * Lập Pull Request trên GitHub để các thành viên khác xem xét và duyệt trước khi merge vào nhánh chính.

---

## 🎨 Quy Chuẩn Đặt Tên & Code Style

### 1. Backend (C# / .NET)
* **PascalCase:** Dành cho Class, Interface, Method, Public Property (Ví dụ: `BookingCreationService`, `IGetRoomsQuery`, `TotalAmount`).
* **camelCase:** Dành cho biến cục bộ, tham số phương thức (Ví dụ: `checkInDate`, `roomId`).
* **_camelCase (có dấu gạch dưới):** Dành cho các trường private readonly được injection qua Constructor (Ví dụ: `_availabilityService`, `_dbContext`).
* **Quy tắc an toàn null:** Sử dụng các toán tử an toàn null (`?`, `??`) và kiểm tra giá trị null để tránh lỗi `NullReferenceException`.

### 2. Database (EF Core / PostgreSQL)
* **Tên bảng & Cột dạng viết thường (snake_case):** Tất cả các thực thể cấu hình trong `ApplicationDbContext` phải được chuyển sang chữ thường (Ví dụ: `bookings`, `room_slots`, `ai_knowledge_units`) để tương thích tốt nhất với PostgreSQL.
* **Migration:** Đặt tên migration rõ ràng, thể hiện đúng thay đổi (Ví dụ: `dotnet ef migrations add AddRefundFieldsToBooking`).

### 3. Frontend (HTML / CSS / JS)
* **Bootstrap 5 & CSS tùy chỉnh:** Sử dụng lưới Bootstrap (`row`, `col-md-6`) để thiết kế giao diện thích ứng. Viết CSS tùy chỉnh trong file `index.css` hoặc các file style riêng biệt thay vì viết CSS nội dòng (inline style).
* **jQuery & Vanilla JS:** Các sự kiện click, AJAX, hiển thị modal nên được tổ chức rõ ràng trong các file JS riêng biệt thuộc `/wwwroot/js`.

---

## 📝 Quy Chuẩn Thông Điệp Commit (Commit Messages)

Chúng tôi áp dụng chuẩn **Conventional Commits** để lịch sử Git dễ đọc và có thể tra cứu nhanh:

Định dạng: `<type>(<scope>): <description>`

| Loại Commit (Type) | Ý Nghĩa | Ví Dụ |
| :--- | :--- | :--- |
| `feat` | Thêm một tính năng mới | `feat(auth): thêm cơ chế đăng nhập bằng OTP` |
| `fix` | Sửa một lỗi kỹ thuật (bug) | `fix(booking): sửa lỗi tính sai giá phòng khi qua đêm` |
| `docs` | Thay đổi, cập nhật tài liệu hoặc comment | `docs(readme): bổ sung tài liệu hướng dẫn setup DB` |
| `style` | Thay đổi định dạng code (khoảng trắng, dấu chấm phẩy) | `style(css): chỉnh lại căn lề của bento grid` |
| `refactor` | Tái cấu trúc mã nguồn, không làm đổi tính năng | `refactor(ai): tách nhỏ các agent trong Orchestrator` |
| `test` | Thêm hoặc sửa mã nguồn kiểm thử (unit tests) | `test(pricing): thêm test case kiểm tra giá mùa lễ` |
| `chore` | Các thay đổi phụ trợ cho hệ thống build, tool... | `chore(project): cập nhật phiên bản thư viện Npgsql` |

---

## ⚠️ Lưu Ý Quan Trọng Về Bảo Mật
* **Không bao giờ commit API Keys:** Mọi API keys (Groq, Gemini, database password) phải được lưu trữ trong file cấu hình cá nhân hoặc biến môi trường.
* File `key.md` và `appsettings.Development.json` đã được đưa vào danh sách chặn của `.gitignore` để tránh đẩy các thông tin nhạy cảm lên máy chủ chung.

---
*Cảm ơn sự đóng góp nhiệt tình của bạn để sản phẩm hoàn thiện hơn! 🚀*
