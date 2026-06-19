# CẤU HÌNH MÔI TRƯỜNG PHÁT TRIỂN HỆ THỐNG (5.1 DEVELOPMENT ENVIRONMENT)

Dưới đây là nội dung bảng đặc tả cấu hình môi trường phát triển dự án **WebHomestay**, tương ứng với nội dung được xuất trong file Word [moi_truong_phat_trien.docx](file:///c:/Users/admin/Documents/VS%20T%C3%ADm/web_homestay/web_homestay/B%C3%A1o%20c%C3%A1o/moi_truong_phat_trien.docx).

| STT | Thành phần / Công cụ | Công nghệ / Phiên bản sử dụng | Mô tả vai trò trong hệ thống phát triển |
|:---:|:---|:---|:---|
| **1** | Hệ điều hành | Windows 10 / Windows 11 | Môi trường phát triển chính của lập trình viên và chạy thử nghiệm cục bộ hệ thống. |
| **2** | IDE chạy và debug chính | Microsoft Visual Studio 2022 | Môi trường phát triển tích hợp (IDE) chính thức dùng để build dự án, quản lý biên dịch C#, debug lõi Backend và quản lý di trú cơ sở dữ liệu. |
| **3** | IDE viết code bổ trợ | Visual Studio Code (VS Code) | Dùng để viết code nhanh giao diện HTML/CSS, chỉnh sửa các file Javascript tĩnh và tích hợp các công cụ hỗ trợ code bằng trí tuệ nhân tạo. |
| **4** | Hệ quản trị cơ sở dữ liệu | PostgreSQL | Hệ quản trị cơ sở dữ liệu quan hệ dùng để lưu trữ toàn bộ dữ liệu thực tế của chi nhánh, phòng, tồn kho slot, thông tin đặt phòng, tài khoản và tri thức AI RAG. |
| **5** | Công cụ quản trị database | pgAdmin 4 | Công cụ giao diện đồ họa trực quan dùng để kết nối, quản lý cấu trúc bảng, kiểm tra dữ liệu và chạy thử các truy vấn PostgreSQL. |
| **6** | Framework lập trình Backend | ASP.NET Core MVC (.NET 10.0) | Framework lõi phía máy chủ đảm nhận kiến trúc MVC, xử lý logic nghiệp vụ, định tuyến, session và kiểm soát phân quyền hệ thống. |
| **7** | Thư viện ánh xạ cơ sở dữ liệu | Entity Framework Core & Npgsql (v8.0.0) | Thư viện ORM dùng để kết nối và thao tác với cơ sở dữ liệu PostgreSQL dưới dạng hướng đối tượng thông qua ngôn ngữ C# Code-First. |
| **8** | Bộ công cụ lập trình Frontend | HTML5, CSS3, Bootstrap 5, jQuery | Dùng để xây dựng giao diện responsive thích ứng trên các thiết bị và lập trình hiệu ứng tương tác, chỉ dẫn onboarding, gửi request AJAX. |
| **9** | Cơ chế Trí tuệ nhân tạo (AI) | Groq API (llama-3.3-70b-versatile) / Gemini API | Dịch vụ mô hình ngôn ngữ lớn (LLM) bên thứ ba tích hợp để chatbot tư vấn trả lời tự nhiên và chốt phòng tự động. |
| **10** | Thư viện kiểm thử tự động | xUnit Test Framework | Thư viện dùng để viết và chạy các kịch bản kiểm thử đơn vị (Unit Tests) cho các tầng nghiệp vụ độc lập của hệ thống. |
