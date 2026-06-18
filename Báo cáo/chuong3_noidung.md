# CHƯƠNG III: THIẾT KẾ KIẾN TRÚC VÀ CƠ SỞ DỮ LIỆU (TIẾP THEO)

## 3.1 Công nghệ sử dụng và lý do lựa chọn

Hệ thống **WebHomestay** được xây dựng trên một stack công nghệ hiện đại, tối ưu cho hiệu năng và khả năng bảo trì:

1. **ASP.NET Core MVC (.NET 10.0)**
   - *Lý do lựa chọn:* Là framework mã nguồn mở của Microsoft có hiệu năng xử lý request cực cao. Tích hợp sẵn cơ chế Dependency Injection (DI) mạnh mẽ, bảo mật tốt thông qua Middleware/Filter (`AdminAuthorizeAttribute`) và giúp phát triển nhanh giao diện máy chủ thông qua Razor Views.
2. **PostgreSQL & Entity Framework Core (ORM)**
   - *Lý do lựa chọn:* PostgreSQL là hệ quản trị cơ sở dữ liệu quan hệ mạnh mẽ, hỗ trợ lưu trữ kiểu dữ liệu phức tạp (như JSON, UUID). EF Core làm ORM code-first giúp lập trình viên thao tác với database bằng ngôn ngữ C# an toàn, tự động đồng bộ schema database thông qua EF Migrations.
3. **HTML5, CSS3, Bootstrap 5 & jQuery**
   - *Lý do lựa chọn:* Đơn giản hóa quá trình thiết kế giao diện responsive tương thích tốt trên cả Mobile và Desktop. Tương thích trực tiếp với Razor Views mà không cần các framework SPA (như React/Angular) cồng kềnh, giúp tối ưu hóa thời gian tải trang ban đầu.
4. **Groq API / Gemini API (Mô hình ngôn ngữ lớn LLM)**
   - *Lý do lựa chọn:* Groq cung cấp tốc độ phản hồi cực nhanh (độ trễ thấp dưới 1 giây) phù hợp cho chatbot thời gian thực, trong khi Gemini API hỗ trợ khả năng suy luận ngữ cảnh sâu sắc, giúp AI Booking Conductor dễ dàng bóc tách ý định đặt phòng và tư vấn chính xác.

---

## 3.2 Kiến trúc tổng thể của hệ thống

Hệ thống được thiết kế theo mô hình **Kiến trúc phân tầng (Multi-tier Architecture)** kết hợp với mẫu kiến trúc **Model-View-Controller (MVC)**:

```mermaid
graph TD
    subgraph Tầng Trình Diễn (Presentation Layer)
        V[Razor Views: HTML/CSS/JS/Bootstrap/jQuery]
    end

    subgraph Tầng Nghiệp Vụ (Business Logic Layer)
        C[Controllers: Điều phối request]
        S[Services: AvailabilityService, AIBrainOrchestrator, Pricing...]
    end

    subgraph Tầng Truy Cập Dữ Liệu (Data Access Layer)
        ORM[Entity Framework Core - ApplicationDbContext]
    end

    subgraph Tầng Cơ Sở Dữ Liệu & Dịch Vụ Ngoài
        DB[(CSDL PostgreSQL)]
        Ext[API Ngoài: Groq/Gemini, VietQR]
    end

    V <-->|Yêu cầu / Phản hồi| C
    C <--> S
    S <--> ORM
    ORM <--> DB
    S <--> Ext
```

- **Tầng Trình diễn (Presentation Layer):** Nằm ở phía Client/Browser, chịu trách nhiệm kết xuất giao diện (View), nhận tương tác từ người dùng và hiển thị card phòng động từ AI.
- **Tầng Nghiệp vụ (Business Logic Layer):** Nơi xử lý logic nghiệp vụ chính. Các Controller tiếp nhận yêu cầu từ View, gọi các Service chuyên biệt (như kiểm tra phòng trống `AvailabilityService`, tính giá phòng `PricingService`, điều phối chatbot `AIBrainOrchestrator`) để thực thi nghiệp vụ.
- **Tầng Truy cập dữ liệu (Data Access Layer):** Sử dụng `ApplicationDbContext` để kết nối và thực hiện các câu lệnh truy vấn dữ liệu an toàn.
- **Tầng CSDL & Dịch vụ ngoài:** Lưu trữ dữ liệu thực tế tại PostgreSQL và giao tiếp với các API ngoài (VietQR, Groq, Gemini).

---

## 3.3 Ý nghĩa của việc phân tầng và lý do sắp xếp

### 3.3.1 Ý nghĩa của việc phân tầng (Đơn giản)
- **Dễ chỉnh sửa, nâng cấp:** Tách biệt Giao diện và Logic xử lý. Khi bạn sửa đổi giao diện (HTML/CSS) thì không lo làm ảnh hưởng hay gây lỗi cho logic tính toán giá phòng bên dưới.
- **Tái sử dụng code:** Một hàm xử lý (như kiểm tra phòng trống) chỉ cần viết một lần nhưng dùng được cho cả trang web đặt phòng truyền thống và chatbot AI tư vấn.
- **Dễ kiểm tra lỗi:** Lập trình viên có thể viết các bài test độc lập cho từng phần mà không cần chạy toàn bộ hệ thống.

### 3.3.2 Lý do sắp xếp các tầng (Đơn giản)
- **Đi theo một chiều duy nhất:** Luồng dữ liệu đi thẳng từ **Giao diện ➔ Logic xử lý ➔ Database**. Tầng trên gọi tầng dưới, không đi ngược lại để tránh xung đột hệ thống.
- **Bảo mật tuyệt đối:** Không cho giao diện đụng trực tiếp vào database. Mọi yêu cầu dữ liệu bắt buộc phải qua bộ lọc kiểm tra quyền hạn ở tầng giữa (Logic) để tránh bị tin tặc tấn công.

---

## 3.4 PROMPT VẼ SƠ ĐỒ PHÂN TẦNG KIẾN TRÚC (3-TIER LAYER)

Bạn copy prompt dưới đây gửi cho ChatGPT (DALL-E) hoặc các AI vẽ hình để tạo sơ đồ phân tầng trực quan:

```text
Tạo một sơ đồ thiết kế dạng khối xếp chồng (3-Tier Layered Architecture Diagram) cho hệ thống phần mềm WebHomestay.
- Style thiết kế: Bản vẽ phẳng (flat sketch/draw style), tối giản, nét vẽ tay gọn gàng, viền đen dày, nền trắng tinh, màu sắc nhẹ nhàng (xanh dương nhạt, xanh lá nhạt, cam nhạt) cho các khối chồng lên nhau.
- Bố cục gồm 3 khối hộp chữ nhật lớn xếp chồng lên nhau từ trên xuống dưới:

1. Khối trên cùng: ghi nhãn to "TẦNG TRÌNH DIỄN (Presentation Layer)"
   - Bên trong chứa các nhãn nhỏ: "Giao diện Razor View", "Bootstrap 5", "jQuery CSS".

2. Khối ở giữa: ghi nhãn to "TẦNG NGHIỆP VỤ (Business Logic Layer)"
   - Bên trong chứa các nhãn nhỏ: "Controllers điều hướng", "Services nghiệp vụ đặt phòng", "Bộ điều phối AI Brain Orchestrator".

3. Khối dưới cùng: ghi nhãn to "TẦNG TRUY CẬP DỮ LIỆU & DỊCH VỤ NGOÀI (Data & External Layer)"
   - Bên trong chứa các nhãn nhỏ: "PostgreSQL Database", "EF Core ORM", "LLM APIs (Groq/Gemini)".

- Phía bên cạnh sơ đồ: Vẽ một mũi tên đứng chỉ hướng từ trên xuống dưới xuyên qua cả 3 khối, trên mũi tên ghi nhãn chữ: "Luồng xử lý một chiều & Đảm bảo bảo mật".
- Yêu cầu: Chữ viết hiển thị rõ ràng bằng tiếng Việt, bố cục cân đối và dễ nhìn để chèn vào báo cáo đồ án.
```



---

## 3.4 Cấu trúc thư mục và tổ chức mã nguồn (Sạch & Gọn)

Hệ thống được tổ chức mã nguồn chuẩn xác theo mô hình MVC phân lớp của ASP.NET Core, tập trung vào các mục liên quan và lược bỏ các thư mục rác/tạm thời (như `bin`, `obj`, `.vs`, `.git`):

```text
web_homestay/ (Thư mục gốc giải pháp)
│
├── WebHomestay/ (Dự án mã nguồn Web chính)
│   ├── Controllers/             # Chứa bộ điều hướng và xử lý request
│   │   ├── Admin*Controller.cs  # Các Controller quản trị (AI, Matrix, Staff...)
│   │   └── *Controller.cs       # Các Controller công khai (Home, Rooms, Booking...)
│   │
│   ├── Data/                    # Tầng truy cập dữ liệu
│   │   └── ApplicationDbContext.cs # Cấu hình ánh xạ bảng PostgreSQL (EF Core)
│   │
│   ├── Migrations/              # Lịch sử và file tạo di trú cơ sở dữ liệu
│   │
│   ├── Models/                  # Thực thể (Entities) và ViewModels
│   │
│   ├── Services/                # Tầng nghiệp vụ lõi (Business Logic Layer)
│   │   ├── AvailabilityService.cs   # Dịch vụ tìm phòng trống thời gian thực
│   │   ├── SlotManagementService.cs # Quản lý kho slot đặt theo ngày/giờ
│   │   ├── BookingCreationService.cs# Logic tạo đơn hàng và kiểm tra thời gian
│   │   ├── PricingService.cs        # Tính giá phòng và phụ thu lễ tết
│   │   ├── AIBrainOrchestrator.cs   # Lõi điều phối AI chatbot Multi-Agent
│   │   └── BookingCleanupService.cs # Background service tự động hủy đơn quá hạn
│   │
│   ├── Filters/                 # Các bộ lọc phân quyền và bảo mật
│   │   └── AdminAuthorizeAttribute.cs # Xác thực Session và phân quyền Dual Matrix
│   │
│   ├── Helpers/                 # Các tiện ích bổ trợ cho Views/Controller
│   │
│   ├── Views/                   # Giao diện hiển thị Razor Views (.cshtml)
│   │   ├── Admin*/              # Giao diện dành riêng cho Admin (Matrix, AI...)
│   │   ├── Home/ & Rooms/       # Giao diện đặt phòng của Khách hàng
│   │   └── Shared/              # Layout chung và Widgets (Chatbot, Tutorial)
│   │
│   ├── wwwroot/                 # Tài nguyên tĩnh của website (CSS, JS, Images)
│   │   └── js/                  # JS tùy biến (Onboarding, Ma trận, Admin AI)
│   │
│   ├── Program.cs               # File cấu hình khởi động, nạp DI, nạp API Key
│   └── appsettings.json         # File cấu hình database và hệ thống
│
└── WebHomestay.Tests/ (Dự án kiểm thử tự động)
    ├── Services/                # Kiểm thử các dịch vụ Availability, Pricing...
    ├── Domain/                  # Kiểm thử logic nghiệp vụ thuần
    ├── FakeSession.cs           # Lớp giả lập Session phục vụ test phân quyền
    └── FakeServiceScopeFactory.cs # Lớp giả lập Scope phục vụ kiểm thử song song
```

### Ý nghĩa tổ chức mã nguồn:
- **Tách biệt Controller & Services:** Mọi logic xử lý phức tạp (kiểm tra tồn kho slot phòng, tính giá, gọi AI) đều được đưa vào thư mục `Services/`. Thư mục `Controllers/` chỉ đóng vai trò nhận request, gọi service tương ứng và trả về Views.
- **Phân tách phân hệ rõ ràng trong Views & Controllers:** Các file phục vụ quản trị Admin đều được đặt tiền tố `Admin*` để dễ quản lý, bảo mật độc lập với phân hệ công khai dành cho Khách hàng.
- **Dự án Tests độc lập:** Thư mục `WebHomestay.Tests/` chứa toàn bộ các kịch bản kiểm thử độc lập cho Services và Domain sử dụng cơ sở dữ liệu giả lập (InMemory), giúp đảm bảo chất lượng phần mềm mà không gây ảnh hưởng đến dữ liệu chạy thật.
