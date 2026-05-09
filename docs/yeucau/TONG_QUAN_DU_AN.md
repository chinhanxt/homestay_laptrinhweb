# TỔNG QUAN DỰ ÁN WEB HOMESTAY SELF CHECK-IN

Dự án xây dựng hệ thống đặt phòng homestay theo mô hình vận hành hoàn toàn online, hướng tới sự tiện lợi, tự động hóa và trải nghiệm công nghệ hiện đại.

---

## 1. Concept & Tầm nhìn
- **Mô hình:** "Khách sạn mini tự vận hành" (Self check-in/self check-out).
- **Mục tiêu:** Thay thế quy trình thủ công qua Zalo/tin nhắn bằng hệ thống web tự động 100%.
- **Đối tượng:** Khách hàng tại các thành phố lớn (như TP.HCM) yêu thích sự nhanh chóng và riêng tư.

## 2. Điểm khác biệt cốt lõi (Unique Selling Points)
- **AI Assistant:** 
    - Tư vấn phòng dựa trên nhu cầu (số người, budget, tiện nghi).
    - Gợi ý phòng trống theo thời gian thực.
    - Hỗ trợ chốt phòng nhanh và hướng dẫn thao tác.
- **Tutorial Onboarding:** 
    - Giao diện hướng dẫn tương tác như trong game.
    - Highlight các tính năng quan trọng để người dùng mới không bị bối rối.

## 3. Tính năng hệ thống
### Dành cho Khách hàng (User)
- Tìm kiếm & Lọc phòng linh hoạt theo giờ/ngày, giá, tiện ích.
- Đặt phòng và thanh toán online (giả lập demo).
- Xem trạng thái phòng trống thời gian thực.
- Quản lý lịch sử đặt phòng và thực hiện check-in/out online.

### Dành cho Quản trị viên (Admin)
- Quản lý danh mục phòng (CRUD phòng).
- Quản lý danh sách đặt phòng (Booking management).
- Theo dõi trạng thái phòng và quản lý người dùng.

### AI Assistant & Trải nghiệm
- Hội thoại tự nhiên để lọc dữ liệu phòng.
- Cơ chế hướng dẫn trực quan (Tutorial).

## 4. Cấu trúc kỹ thuật (Technical Stack)
- **Backend:** ASP.NET Core MVC.
- **Database:** PostgreSQL (Sử dụng pgAdmin để quản lý).
- **Frontend:** HTML5, CSS3 (Vanilla/Bootstrap), jQuery.
- **Môi trường phát triển:**
    - **Visual Studio (Tím):** Môi trường chính để build, chạy project, debug và quản lý database.
    - **VS Code:** Dùng để chỉnh sửa UI/JS nhanh và hỗ trợ code với AI.

## 5. Lộ trình phát triển & Mục tiêu
### Giai đoạn 1: Core System (Ưu tiên)
- Hoàn thiện Login/Register.
- CRUD phòng và quản lý database.
- Quy trình Booking và quản lý trạng thái phòng.
- Kết nối dữ liệu thật và responsive giao diện.

### Giai đoạn 2: Advanced Features
- Tích hợp AI Assistant API.
- Xây dựng hệ thống Tutorial Onboarding.
- Tối ưu hóa Animation và trải nghiệm người dùng cao cấp.

---
*Tài liệu này tổng hợp từ các yêu cầu ban đầu để tạo ra một hướng dẫn duy nhất và nhất quán cho quá trình phát triển.*
