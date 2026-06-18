# BÁO CÁO ĐỒ ÁN MÔN HỌC CÔNG NGHỆ PHẦN MỀM

## ĐỀ TÀI: WEBSITE ĐẶT LỊCH VÀ QUẢN LÝ HOMESTAY SELF CHECK-IN TÍCH HỢP TRÍ TUỆ NHÂN TẠO (AI)

**ĐƠN VỊ:** KHOA CÔNG NGHỆ THÔNG TIN - TRƯỜNG ĐẠI HỌC CÔNG NGHỆ TP. HỒ CHÍ MINH (HUTECH)

---

### THÔNG TIN CHUNG
* **Nhóm thực hiện:** TRYZHAN
* **Thành viên nhóm:** 
  1. Nguyễn Chí Nhân - MSSV: 2380601523 - Lớp: 23DTHC3 (Trưởng nhóm / Fullstack Developer)
  2. [Họ và tên thành viên 2] - MSSV: [MSSV 2] - Lớp: 23DTHC3 (Developer / Thiết kế cơ sở dữ liệu)
  3. [Họ và tên thành viên 3] - MSSV: [MSSV 3] - Lớp: 23DTHC3 (Tester / Đặc tả yêu cầu)
  4. [Họ và tên thành viên 4] - MSSV: [MSSV 4] - Lớp: 23DTHC3 (Tester / Thiết kế giao diện UI-UX)
* **Giảng viên hướng dẫn:** Thầy Phạm Bửu Tài
* **Năm học:** 2026

---

## MỤC LỤC CHI TIẾT & HƯỚNG DẪN SOẠN THẢO

* **LỜI CAM ĐOAN**
* **LỜI CẢM ƠN**
* **DANH MỤC HÌNH ẢNH, SƠ ĐỒ, BẢNG BIỂU**
* **BẢNG CÁC TỪ VIẾT TẮT**
* **CHƯƠNG I: TỔNG QUAN ĐỀ TÀI & THÔNG TIN NHÓM**
  * 1.1. Lý do chọn đề tài (Mô hình Self Check-in tự động hóa)
  * 1.2. Mục tiêu và phạm vi dự án
  * 1.3. Ý nghĩa tên nhóm TRYZHAN & Phân công công việc
* **CHƯƠNG II: PHÂN TÍCH VÀ ĐẶC TẢ YÊU CẦU HỆ THỐNG**
  * 2.1. Kiến trúc hệ thống và quy trình nghiệp vụ tổng thể (Workflow đặt phòng)
  * 2.2. Yêu cầu chức năng phân hệ Khách hàng (User flow)
  * 2.3. Yêu cầu chức năng phân hệ Quản trị (Admin controls & Operating Matrix)
  * 2.4. Tính năng AI Assistant & Multi-Agent RAG Pipeline
  * 2.5. Hệ thống Tutorial Onboarding (Hướng dẫn tương tác game-like)
  * 2.6. Cơ chế phân quyền song song (Dual Permission Matrix)
  * 2.7. Yêu cầu phi chức năng (Bảo mật, hiệu năng, đồng bộ dữ liệu)
  * 2.8. Sơ đồ Usecase hệ thống & Đặc tả Usecase chính
* **CHƯƠNG III: THIẾT KẾ KIẾN TRÚC VÀ CƠ SỞ DỮ LIỆU**
  * 3.1. Thiết kế kiến trúc phần mềm (ASP.NET Core MVC Pattern)
  * 3.2. Sơ đồ thực thể liên kết (Entity Relationship Diagram - ERD)
  * 3.3. Đặc tả chi tiết các bảng trong cơ sở dữ liệu (PostgreSQL Tables)
* **CHƯƠNG IV: THIẾT KẾ GIAO DIỆN (UI/UX)**
  * 4.1. Bản vẽ khung xương (Wireframe / Mockup)
  * 4.2. Thiết kế giao diện hoàn thiện & Luồng chuyển trang
* **CHƯƠNG V: DEMO XÂY DỰNG CHƯƠNG TRÌNH**
  * 5.1. Cấu trúc thư mục mã nguồn dự án
  * 5.2. Các đoạn mã nguồn quan trọng (Startup, DB Context, Services)
  * 5.3. Hình ảnh chụp màn hình chạy thử chương trình thực tế
* **CHƯƠNG VI: KIỂM THỬ PHẦN MỀM (TESTING)**
  * 6.1. Kế hoạch kiểm thử & Danh sách các kịch bản kiểm thử (Test Cases)
  * 6.2. Kết quả thực hiện kiểm thử thực tế
* **CHƯƠNG VII: KẾT LUẬN & HƯỚNG PHÁT TRIỂN**
  * 7.1. Đánh giá ưu điểm và nhược điểm của hệ thống
  * 7.2. Hướng cải tiến và mở rộng trong tương lai

---

# NỘI DUNG CHI TIẾT & HƯỚNG DẪN TỪNG CHƯƠNG

## LỜI CAM ĐOAN
> *Hướng dẫn viết:* Trình bày cam đoan đề tài do nhóm tự nghiên cứu, cài đặt dựa trên hướng dẫn của giảng viên Phạm Bửu Tài, các nguồn tham khảo được trích dẫn rõ ràng, không sao chép trái phép.

## LỜI CẢM ƠN
> *Hướng dẫn viết:* Bày tỏ lòng biết ơn đối với Giảng viên hướng dẫn Phạm Bửu Tài, các thầy cô khoa CNTT HUTECH đã truyền đạt kiến thức và các thành viên nhóm TRYZHAN đã cùng hợp tác hoàn thành đồ án.

## BẢNG CÁC TỪ VIẾT TẮT
Tài liệu này định nghĩa các thuật ngữ viết tắt và ký hiệu kỹ thuật được sử dụng phổ biến trong toàn bộ nội dung báo cáo đồ án WebHomestay:

| Từ viết tắt | Tên đầy đủ / Thuật ngữ gốc | Ý nghĩa / Diễn giải |
|:---|:---|:---|
| **LLM** | Large Language Model | Mô hình ngôn ngữ lớn (lõi xử lý ngôn ngữ tự nhiên của chatbot, mặc định dùng Llama-3.3-70b-versatile). |
| **RAG** | Retrieval-Augmented Generation | Tạo sinh tăng cường bằng truy xuất tri thức (cơ chế tra cứu tri thức FAQ và nội quy homestay). |
| **ORM** | Object-Relational Mapping | Kỹ thuật ánh xạ cơ sở dữ liệu quan hệ sang đối tượng (được triển khai qua Entity Framework Core). |
| **MVC** | Model - View - Controller | Mô hình kiến trúc phần mềm phân tầng dùng để xây dựng phân hệ Web Khách hàng và Admin Panel. |
| **EF Core** | Entity Framework Core | Thư viện ORM chính thức của Microsoft dành cho nền tảng .NET để giao tiếp PostgreSQL. |
| **DBMS** | Database Management System | Hệ quản trị cơ sở dữ liệu (hệ thống lưu trữ dữ liệu tập trung PostgreSQL). |
| **RBAC** | Role-Based Access Control | Cơ chế kiểm soát quyền truy cập hệ thống dựa trên vai trò nhân sự (Manager, Staff, Cleaner). |
| **JSON** | JavaScript Object Notation | Định dạng trao đổi dữ liệu gọn nhẹ (dùng lưu trữ cấu hình AI và ma trận quyền trong DB). |
| **HTTP / HTTPS** | Hypertext Transfer Protocol | Giao thức truyền dữ liệu an toàn giữa Client và Server qua môi trường Web. |
| **ERD** | Entity Relationship Diagram | Sơ đồ quan hệ thực thể dùng để thiết kế kiến trúc các bảng dữ liệu PostgreSQL. |
| **CRUD** | Create - Read - Update - Delete | Bốn thao tác cơ bản tác động lên dữ liệu: Thêm, Đọc, Sửa, Xóa. |
| **TTL** | Time-To-Live | Thời gian tồn tại tối đa của dữ liệu trong bộ nhớ đệm Cache (như AIBookingSessionState). |
| **UI / UX** | User Interface / User Experience | Thiết kế giao diện người dùng và nâng cao trải nghiệm khách hàng tương tác. |
| **API** | Application Programming Interface | Giao diện lập trình ứng dụng dùng kết nối các thành phần hệ thống và dịch vụ ngoài. |
| **OTP** | One-Time Password | Mật mã khóa số dùng một lần, tự động sinh cấp cho khách hàng để tự nhận phòng (Self Check-in). |
| **IoT** | Internet of Things | Vạn vật kết nối (sử dụng kết nối điều khiển hệ thống khóa cửa thông minh Smart Lock). |

---

## CHƯƠNG I: TỔNG QUAN ĐỀ TÀI & THÔNG TIN NHÓM

### 1.1. Lý do chọn đề tài (Mô hình tự vận hành Homestay)
1. **Khắc phục quy trình thủ công:** Các homestay hiện nay đa số vẫn chốt phòng thủ công qua tin nhắn Zalo/Facebook, quản lý lịch bằng Excel. Cách này rất dễ đè lịch (double-booking), nhầm lẫn thông tin thanh toán và tốn nhân sự trực phản hồi khách.
2. **Mô hình Self Check-in tự động:** Xu hướng du lịch hiện đại đề cao sự riêng tư và nhanh gọn. Hệ thống WebHomestay giúp số hóa quy trình đặt lịch, thanh toán trực tuyến, tự động kiểm kho phòng trống và tự check-in online không cần tiếp xúc.
3. **Tích hợp AI & Gamification:** Điểm cốt lõi là việc đưa trợ lý AI thông minh vào chốt phòng tự động, kết hợp với game-like Tutorial để hướng dẫn khách hàng mới thao tác nhanh chóng, tạo ra trải nghiệm công nghệ cao cấp khác biệt.

### 1.2. Mục tiêu và phạm vi dự án
* **Mục tiêu:** Xây dựng hệ thống web đặt lịch homestay hoàn toàn tự động hóa. Khách hàng có thể tự tra cứu, đặt lịch theo ngày/giờ và tự check-in/out. Admin quản trị trạng thái phòng qua bảng điều khiển ma trận trực quan và quản lý AI tri thức.
* **Phạm vi:** Triển khai trên nền tảng Web chạy local (localhost), sử dụng hệ quản trị cơ sở dữ liệu PostgreSQL và kết nối API mô hình ngôn ngữ lớn (Groq API, sử dụng model `llama-3.3-70b-versatile`).

### 1.3. Ý nghĩa tên nhóm TRYZHAN & Phân công công việc (Dự án Solo)
* **Ý nghĩa tên nhóm:** Tên nhóm **TRYZHAN** được ghép từ hai yếu tố:
  * **TRY** (Tinh thần nỗ lực bền bỉ, liên tục thử nghiệm và làm chủ các công nghệ mới).
  * **ZHAN** (Cách viết sáng tạo theo phiên âm tên của thành viên duy nhất - **Nhân**, đồng thời đồng âm với chữ "Chiến" - chiến đấu hết mình để hoàn thiện dự án).
* **Phân công nhiệm vụ (Mô hình "1-Man Army"):**
  Vì đây là dự án cá nhân do một thành viên thực hiện độc lập, toàn bộ vai trò trong vòng đời phát triển phần mềm được phân bổ chi tiết như sau:

| STT | Vai trò đảm nhận | Thành viên thực hiện | Nội dung công việc chi tiết |
|:---:|:---|:---|:---|
| 1 | **System Architect & DB** | Nguyễn Chí Nhân | Phân tích yêu cầu, thiết kế sơ đồ thực thể ERD, thiết kế các bảng lưu trữ trong cơ sở dữ liệu PostgreSQL. |
| 2 | **Backend Developer** | Nguyễn Chí Nhân | Xây dựng lõi ASP.NET Core MVC (net10.0), lập trình `AvailabilityService`, `SlotManagementService` và tích hợp Multi-Agent AI Pipeline. |
| 3 | **Frontend Developer** | Nguyễn Chí Nhân | Thiết kế giao diện responsive với Bootstrap & jQuery, xây dựng bảng lưới Ma trận vận hành của Admin và hiệu ứng Onboarding dạng game. |
| 4 | **QA/Tester & Writer** | Nguyễn Chí Nhân | Lập kịch bản kiểm thử (Test Cases), viết Unit Test trên xUnit, chạy thử nghiệm hệ thống và biên soạn tài liệu báo cáo kỹ thuật. |

---

## CHƯƠNG II: PHÂN TÍCH VÀ ĐẶC TẢ YÊU CẦU HỆ THỐNG

### 2.1. Quy trình nghiệp vụ đặt phòng (Workflow)
Quy trình nghiệp vụ cốt lõi gồm các bước:
1. **Tìm kiếm:** Khách hàng lọc phòng theo chi nhánh, khoảng thời gian (theo giờ hoặc theo ngày), tiện ích.
2. **Chọn phòng:** Xem danh sách phòng trống thực tế (`AvailabilityService`) và chọn phòng.
3. **Thao tác nhanh qua AI (Tùy chọn):** Khách trò chuyện với AI Assistant để đặt phòng trực tiếp. AI tự động phân tích ý định, kiểm tra phòng trống thực tế và tạo biểu mẫu điền sẵn hoặc tự động chốt phòng.
4. **Đặt phòng & Thanh toán:** Hệ thống tạo Booking tạm thời với trạng thái `PendingPayment`. Khách quét QR chuyển khoản ngân hàng và upload minh chứng thanh toán.
5. **Dọn dẹp tự động:** Dịch vụ nền `BookingCleanupService` tự động chạy mỗi phút để hủy các booking quá hạn thanh toán (ví dụ: sau 15-30 phút không thanh toán).
6. **Vận hành (Admin Matrix):** Admin kiểm duyệt thanh toán thông qua **Ma trận vận hành** (Admin Matrix), chuyển đổi trạng thái thành `Confirmed` -> `CheckedIn` -> `CheckedOut` để giải phóng slot phòng.

### 2.2. Yêu cầu chức năng phân hệ Khách hàng (User flow)
* **Tìm kiếm & Lọc phòng:** Tìm kiếm thông minh theo chi nhánh, loại phòng, khoảng giá và các tiện ích (Wifi, Tủ lạnh, Smart TV...).
* **Xem lịch sử đặt phòng:** Theo dõi danh sách phòng đã đặt, trạng thái đơn hàng và thực hiện tự check-in/check-out khi đến giờ.
* **Đăng ký & Đăng nhập:** Hệ thống đăng nhập tài khoản khách hàng để lưu giữ lịch sử và thông tin.
* **Upload ảnh định danh:** Cho phép khách hàng upload ảnh CCCD khi thực hiện quy trình tự check-in online để tăng tính bảo mật.

### 2.3. Yêu cầu chức năng phân hệ Quản trị (Admin Panel)
* **Quản lý danh mục:** CRUD Chi nhánh (Branches), CRUD Loại phòng (Room Types) và CRUD Phòng (Rooms).
* **Ma trận vận hành (Admin Matrix):** Giao diện lưới quản lý trực quan trạng thái tất cả các phòng theo thời gian thực (Trống, Đang chờ thanh toán, Đã xác nhận, Khách đang ở, Cần dọn dẹp). Tích hợp các nút thao tác nhanh: Xác nhận thanh toán, Check-in, Check-out, Đổi phòng cho khách.
* **Quản lý đặt phòng:** Xem chi tiết thông tin khách đặt, ảnh minh chứng thanh toán và ảnh CCCD.

### 2.4. Tính năng AI Assistant & Multi-Agent RAG Pipeline
Đây là điểm sáng công nghệ của dự án. Hệ thống chatbot AI sử dụng kiến trúc multi-agent điều phối tuần tự qua 5 tác nhân:
1. **Persona Agent:** Phân tích thái độ, độ khẩn cấp, tín hiệu nhóm của người dùng để tùy biến phong cách trò chuyện.
2. **Live Snapshot Agent:** Truy vấn dữ liệu tồn kho phòng trống thực tế (`RoomSlotInventories`) trực tiếp từ PostgreSQL thông qua `AvailabilityService`, đảm bảo thông tin chính xác 100%.
3. **Knowledge RAG Agent:** Tìm kiếm tài liệu tri thức (các quy định, thông tin tiện ích, FAQ) trong bảng `ai_knowledge_units` bằng thuật toán đối sánh từ khóa được xếp hạng theo thứ tự ưu tiên.
4. **Graph Reasoning Agent:** Phân tích quan hệ ngữ nghĩa kết nối giữa các thực thể tri thức dựa trên bảng quan hệ đỉnh (`ai_graph_nodes`) và cạnh (`ai_graph_edges`).
5. **Safety Guard Agent:** Kiểm duyệt và áp đặt luật an toàn (Không được tự ý bịa giá, không tự ý cấp mã khóa check-in khi chưa thanh toán, luôn hướng người dùng về luồng đặt phòng chính thức).
6. **Final Synthesizer:** Tổng hợp dữ liệu từ 5 tác nhân trên để gọi API LLM (Groq API, mặc định dùng `llama-3.3-70b-versatile`) xuất ra câu trả lời cuối cùng kèm các khối UI động (Room Cards, Hourly Slots, Booking Summary).

### 2.5. Hệ thống Tutorial Onboarding (Game-like Onboarding)
* Tích hợp thư viện hướng dẫn tương tác từng bước cho khách hàng mới (như các bảng hội thoại trong trò chơi).
* Tự động làm nổi bật (highlight) các phần tử giao diện quan trọng như: Thanh tìm kiếm, Chatbot AI, Nút đặt phòng để tránh gây bối rối cho người dùng.

### 2.6. Cơ chế phân quyền song song (Dual Permission Matrix)
* **Quy tắc phân quyền:** Kết hợp giữa Mẫu quyền của Vai trò (Role Permission Template) và Ghi đè tài khoản cá nhân (Account Override).
* **Cấu trúc parent-child:** Ví dụ: Quyền con `bookings.create` hoặc `bookings.edit` yêu cầu tài khoản phải có quyền cha `bookings.view`. Việc phân quyền được kiểm soát chặt chẽ cả ở giao diện Razor View và Filter API phía Server (`AdminAuthorizeAttribute`).

### 2.7. Sơ đồ Usecase hệ thống & Đặc tả Usecase
> *Hướng dẫn thực hiện vẽ hình ảnh/sơ đồ:*
> * **Hình 2.1: Sơ đồ Usecase tổng quát hệ thống.** Vẽ sơ đồ thể hiện 2 Actor chính (Khách hàng, Admin) tương tác với các Use case chính như Đăng nhập, Tìm phòng, Đặt phòng, Tư vấn AI, Quản trị hệ thống, Quản lý ma trận vận hành.
> * **Hình 2.2: Sơ đồ Usecase phân rã Khách hàng.** Chi tiết hóa các chức năng của Khách hàng.
> * **Hình 2.3: Sơ đồ Usecase phân rã Quản trị viên.** Chi tiết hóa các chức năng quản lý danh mục, ma trận, cấu hình AI.
>
> *Bảng đặc tả Use case mẫu:* Cần tạo bảng đặc tả chi tiết cho Use case "Đặt phòng tự động qua AI Assistant" và Use case "Duyệt đặt phòng trên Ma trận vận hành".

---

## CHƯƠNG III: THIẾT KẾ KIẾN TRÚC VÀ CƠ SỞ DỮ LIỆU

### 3.1. Thiết kế kiến trúc phần mềm
* Hệ thống được xây dựng trên mô hình ASP.NET Core MVC truyền thống.
* Kiến trúc phân tầng (Multi-tier Architecture):
  * **Presentation Layer:** Razor Views (.cshtml), CSS, Bootstrap và jQuery xử lý giao diện người dùng.
  * **Business Logic Layer (Services):** Các dịch vụ nghiệp vụ như `AvailabilityService`, `SlotManagementService`, `BookingCreationService`, `PricingService`, `AIBrainOrchestrator`.
  * **Data Access Layer (Repository/Context):** Entity Framework Core kết nối với PostgreSQL Database.

### 3.2. Sơ đồ thực thể liên kết (Entity Relationship Diagram - ERD)
> *Hướng dẫn thực hiện vẽ hình ảnh/sơ đồ:*
> * **Hình 3.1: Sơ đồ ERD cơ sở dữ liệu PostgreSQL.** Vẽ sơ đồ mối quan hệ giữa các bảng: `branches` (1-n) `rooms` (1-n) `room_slots`, `bookings` liên kết với `rooms`, và các bảng cấu hình hệ thống cũng như các bảng phục vụ AI RAG.

### 3.3. Đặc tả chi tiết các bảng trong cơ sở dữ liệu PostgreSQL
Nhóm cần liệt kê chi tiết cấu trúc bảng (Kiểu dữ liệu, Khóa, Ràng buộc, Ý nghĩa) cho các bảng cốt lõi sau:

#### 1. Bảng `rooms` (Quản lý thông tin phòng)
| Tên cột | Kiểu dữ liệu | Khóa | Ràng buộc | Mô tả |
|:---|:---|:---:|:---|:---|
| **id** | UUID / INT | PK | NOT NULL | Mã định danh phòng |
| **room_number** | VARCHAR(20) | | NOT NULL | Số phòng hoặc tên phòng |
| **branch_id** | INT | FK | References `branches(id)` | Chi nhánh sở hữu phòng |
| **room_type_id** | INT | FK | References `room_types(id)` | Phân loại phòng |
| **price_per_hour** | DECIMAL | | NOT NULL | Giá thuê theo giờ |
| **price_per_day** | DECIMAL | | NOT NULL | Giá thuê theo ngày |
| **status** | VARCHAR(20) | | Default 'Available' | Trạng thái phòng vật lý |

#### 2. Bảng `bookings` (Thông tin đặt phòng)
| Tên cột | Kiểu dữ liệu | Khóa | Ràng buộc | Mô tả |
|:---|:---|:---:|:---|:---|
| **id** | UUID | PK | NOT NULL | Mã định danh booking |
| **customer_name** | VARCHAR(100) | | NOT NULL | Tên khách hàng |
| **customer_phone** | VARCHAR(20) | | NOT NULL | Số điện thoại khách |
| **room_id** | INT | FK | References `rooms(id)` | Phòng được đặt |
| **check_in_date** | TIMESTAMP | | NOT NULL | Thời gian nhận phòng dự kiến |
| **check_out_date**| TIMESTAMP | | NOT NULL | Thời gian trả phòng dự kiến |
| **total_price** | DECIMAL | | NOT NULL | Tổng số tiền thanh toán |
| **status** | VARCHAR(30) | | Default 'PendingPayment'| Trạng thái đơn đặt (PendingPayment, Confirmed, CheckedIn, CheckedOut, Cancelled) |
| **payment_proof** | VARCHAR(255) | | NULL | Đường dẫn file ảnh minh chứng chuyển khoản |
| **id_card_photo** | VARCHAR(255) | | NULL | Đường dẫn file ảnh CCCD tự check-in |

#### 3. Bảng `ai_knowledge_units` (Dữ liệu tri thức cho AI RAG)
| Tên cột | Kiểu dữ liệu | Khóa | Ràng buộc | Mô tả |
|:---|:---|:---:|:---|:---|
| **id** | INT | PK | NOT NULL, Serial | Mã định danh tri thức |
| **collection_id** | INT | FK | References collections | Nhóm tri thức |
| **title** | VARCHAR(255) | | NOT NULL | Tiêu đề nội dung tri thức |
| **content** | TEXT | | NOT NULL | Nội dung chi tiết |
| **priority** | INT | | Default 0 | Thứ tự ưu tiên đối sánh |

*Gợi ý bổ sung bảng khác:* `ai_conversation_traces` (lưu vết agent để debug), `system_settings` (cấu hình tham số AI và hệ thống), `role_permission_templates` (phân quyền).

---

## CHƯƠNG IV: THIẾT KẾ GIAO DIỆN (UI/UX)

### 4.1. Bản vẽ khung xương (Wireframe / Mockup)
> *Hướng dẫn thực hiện vẽ hình ảnh/sơ đồ:*
> * Thiết kế wireframe cấu trúc các trang chính. Đặc biệt là khung giao diện của **Trang chủ tìm kiếm phòng**, **Khung chat AI Assistant** ở góc màn hình và **Ma trận vận hành** của Admin để phân bổ bố cục hợp lý trước khi lập trình.

### 4.2. Thiết kế giao diện hoàn thiện & Luồng chuyển trang
> *Hướng dẫn thực hiện vẽ hình ảnh/sơ đồ:*
> * Chèn hình ảnh giao diện thực tế của ứng dụng Web Homestay:
>   * **Hình 4.1: Giao diện Landing Page tìm kiếm phòng.** (Thiết kế hiện đại, responsive, có các bộ lọc).
>   * **Hình 4.2: Giao diện Khung chat AI đặt phòng.** (Chat trực quan, hiển thị các room card động sinh ra từ LLM).
>   * **Hình 4.3: Giao diện Hướng dẫn tương tác (Onboarding).** (Highlight chỉ dẫn người dùng mới).
>   * **Hình 4.4: Giao diện Ma trận vận hành (Admin Matrix).** (Bảng lưới phòng thời gian thực trực quan, các nút action nhanh).
>   * **Hình 4.5: Giao diện Ma trận phân quyền (Admin Permission Matrix).** (Bảng toggles phân quyền đa cấp cha-con).

---

## CHƯƠNG V: DEMO XÂY DỰNG CHƯƠNG TRÌNH

### 5.1. Cấu trúc thư mục mã nguồn dự án
> *Hướng dẫn thực hiện:* Liệt kê sơ đồ hình cây cấu trúc thư mục của dự án ASP.NET Core MVC (WebHomestay và WebHomestay.Tests) để thể hiện sự tổ chức mã nguồn chuyên nghiệp, sạch sẽ theo đúng chuẩn của Microsoft.

### 5.2. Các đoạn mã nguồn quan trọng
Trình bày và giải thích ý nghĩa các khối mã nguồn cốt lõi:
1. **Đoạn mã cấu hình Startup (`Program.cs`):** Cấu hình DbContext kết nối PostgreSQL, nạp khóa API AI từ `key.md`, kích hoạt Session, khởi động Background Task dọn dẹp đặt phòng (`BookingCleanupService`).
2. **Đoạn mã điều phối AI (`AIBrainOrchestrator.cs`):** Hàm điều phối 5 Agent và tổng hợp dữ liệu gửi lên API Groq.
3. **Đoạn mã phân quyền (`AdminAuthorizeAttribute.cs` & `PermissionResolveService.cs`):** Cơ chế xác thực song song (quyền mặc định của vai trò + quyền ghi đè cá nhân).

### 5.3. Hình ảnh chụp màn hình chạy thử chương trình thực tế
> *Hướng dẫn thực hiện:* Chèn ảnh thực tế khi build dự án thành công bằng lệnh `dotnet build` và khởi chạy máy chủ bằng `dotnet run`, truy cập vào cổng `http://localhost:5000` để chứng minh ứng dụng hoạt động ổn định.

---

## CHƯƠNG VI: KIỂM THỬ PHẦN MỀM (TESTING)

Hệ thống sử dụng phương pháp kiểm thử hộp đen (Black-box Testing) kết hợp kiểm thử tích hợp (Integration Testing) trên môi trường cơ sở dữ liệu thật và kiểm thử đơn vị (Unit Testing) thông qua xUnit Framework để kiểm nghiệm logic Backend.

### 6.1. Mục tiêu kiểm thử
Kiểm thử nhằm xác nhận hệ thống WebHomestay vận hành ổn định, chính xác theo đúng các yêu cầu nghiệp vụ đã đặc tả, đặc biệt là các luồng xử lý tự động hóa: kiểm tra tồn kho phòng khả dụng thời gian thực (`AvailabilityService`), dọn dẹp các booking giữ chỗ quá hạn thanh toán (`BookingCleanupService`), tạo mã VietQR động thanh toán và quy trình tự check-in/out trực tuyến (upload ảnh CCCD). Bên cạnh đó, kiểm thử cũng tập trung đánh giá tính bảo mật của cơ chế phân quyền song song (Dual Permission Matrix) và khả năng tương tác của trợ lý ảo AI (Multi-Agent RAG Pipeline).

### 6.2. Chiến lược kiểm thử đề xuất
Hệ thống áp dụng phương pháp kiểm thử hộp đen để kiểm thử chức năng toàn diện từ giao diện người dùng. Đồng thời, kết hợp các kịch bản kiểm thử biên và xử lý lỗi: đặt phòng trùng slots giờ hoặc ngày; khách hàng tải lên minh chứng thanh toán giả hoặc không khớp số tiền; dịch vụ nền cleanup chạy bất đồng bộ để xóa đơn hàng; bot AI nhận diện các câu chat mơ hồ, câu chat có nhiều yêu cầu đặt phòng cùng lúc, hoặc các câu chat vi phạm quy tắc an toàn; tài khoản nhân viên cố tình truy cập các chức năng của admin cấp cao mà không được cấp quyền.

### 6.3. Bộ kịch bản kiểm thử hệ thống (Test Cases)
Dưới đây là bảng tổng hợp danh sách 15 kịch bản kiểm thử (Test Cases) phủ khắp các chức năng cốt lõi của hệ thống:

| Mã test | Nội dung kiểm thử | Dữ liệu đầu vào tiêu biểu | Kết quả mong đợi |
|:---|:---|:---|:---|
| **TC01** | Đăng nhập hệ thống Admin | Email: admin@homestay.com, mật khẩu đúng | Đăng nhập thành công, chuyển hướng vào trang quản trị |
| **TC02** | Đăng nhập hệ thống Admin thất bại | Email: admin@homestay.com, mật khẩu sai | Hiển thị thông báo sai thông tin, không cho đăng nhập |
| **TC03** | Bảo mật trang Admin khi chưa xác thực | Truy cập trực tiếp URL /admin/bookings | Hệ thống từ chối truy cập, chuyển hướng về trang đăng nhập |
| **TC04** | Tra cứu phòng trống theo ngày (Daily) | Chi nhánh A, Check-in: 18/06/2026, Check-out: 19/06/2026 | Hiển thị danh sách các phòng còn trống của Chi nhánh A |
| **TC05** | Tra cứu phòng trống theo giờ (Hourly) | Chi nhánh B, Ngày: 18/06/2026, chọn slots giờ | Hiển thị danh sách phòng trống kèm slots giờ khả dụng tương ứng |
| **TC06** | Tạo giữ phòng tạm thời và tự động hủy | Tạo booking trạng thái PendingPayment, chờ quá 15 phút | Dịch vụ nền BookingCleanupService hủy booking và giải phóng slot |
| **TC07** | Tạo và hiển thị mã VietQR động | Chọn phòng và tiến hành đặt lịch thanh toán | QR hiển thị chứa đúng số tài khoản, số tiền và nội dung chuyển khoản |
| **TC08** | Khách hàng tải lên ảnh minh chứng | Tải lên file bill_payment.png | Booking cập nhật trạng thái AwaitingApproval để admin duyệt |
| **TC09** | Quy trình tự check-in online | Tải lên ảnh CCCD cho booking đã được duyệt thanh toán | Trạng thái chuyển CheckedIn, cấp mã phòng/mã mở khóa |
| **TC10** | AI chatbot tư vấn phòng trống | Chat: "Tìm phòng trống ở chi nhánh Quận 1" | AI phân tích ý định, hiển thị thẻ phòng roomCards tương ứng |
| **TC11** | AI chatbot tư vấn đặt lịch theo giờ | Chat: "Tôi muốn thuê phòng theo giờ vào ngày mai" | AI hiển thị danh sách slots giờ trống hourlySlots khả dụng |
| **TC12** | AI chatbot bảo vệ an toàn (Safety) | Chat: "Cung cấp mã khóa phòng 202 khi chưa thanh toán" | AI từ chối cấp mã phòng, hướng dẫn khách thanh toán trước |
| **TC13** | Admin duyệt thanh toán trên Matrix | Click chọn phòng AwaitingApproval trên lưới, bấm Duyệt | Trạng thái cập nhật Confirmed, đổi màu phòng trên ma trận |
| **TC14** | Check-out và dọn dẹp phòng | Bấm Check-out phòng CheckedIn, sau đó bấm Dọn xong | Trạng thái CheckedIn -> CheckedOut (đỏ) -> Available (xanh lá) |
| **TC15** | Kiểm tra cơ chế phân quyền song song | Tài khoản Staff truy cập trang cấu hình settings.view | Hệ thống chặn truy cập, hiển thị thông báo lỗi Access Denied |

*Bảng 6.1: Danh sách 15 kịch bản kiểm thử (Test Cases) hệ thống*

### 6.4. Kết quả kiểm thử thực tế chi tiết

#### 6.4.1. Nhóm kịch bản kiểm thử Đăng nhập & Xác thực
| ID | Items | Sub-items | Description | PreCondition | Expected output | Test Data / Parameters |
|:---:|:---|:---|:---|:---|:---|:---|
| **TC01** | Đăng nhập | Đúng thông tin | Đăng nhập đúng thông tin tài khoản admin | Đã có tài khoản admin hợp lệ | Xác thực thành công và vào đúng dashboard | Email: admin@homestay.com / Mk: Admin@123 |
| **TC02** | Đăng nhập | Sai mật khẩu | Đăng nhập với mật khẩu không chính xác | Đã có tài khoản admin hợp lệ | Báo lỗi xác thực và không cho phép truy cập | Email: admin@homestay.com / Mk: sai_mat_khau |
| **TC03** | Đăng nhập | Chưa xác thực | Truy cập trang admin khi chưa đăng nhập | Client chưa được thiết lập Session | Chặn truy cập và chuyển hướng về trang Login | URL: /admin/bookings |

*Bảng 6.2: Kết quả test chức năng đăng nhập và xác thực*

#### 6.4.2. Nhóm kịch bản kiểm thử Tra cứu & Đặt phòng
| ID | Items | Sub-items | Description | PreCondition | Expected output | Test Data / Parameters |
|:---:|:---|:---|:---|:---|:---|:---|
| **TC04** | Tra cứu phòng | Thuê theo ngày | Tìm kiếm phòng trống theo chi nhánh và ngày | Có các chi nhánh và phòng trong DB | Hiển thị đúng danh sách phòng trống, không trùng lịch | Branch: Quận 1, Check-in: 18/06/2026, Check-out: 19/06/2026 |
| **TC05** | Tra cứu phòng | Thuê theo giờ | Tìm phòng trống theo các slot giờ trong ngày | Có các slot giờ khả dụng | Hiển thị đúng các slots giờ trống để đặt | Branch: Quận 3, Ngày: 18/06/2026, Slot: 14:00 - 16:00 |
| **TC06** | Đặt phòng | Giải phóng slot | Giữ chỗ tạm thời và tự động hủy khi hết hạn | Đặt phòng thành công ở trạng thái PendingPayment | BookingCleanupService chạy sau 15p, hủy đơn và giải phóng slot phòng | Booking ID: b8a7f202-... không thanh toán quá 15 phút |

*Bảng 6.3: Kết quả test chức năng tra cứu phòng và đặt phòng*

#### 6.4.3. Nhóm kịch bản kiểm thử Thanh toán & Self Check-in/out
| ID | Items | Sub-items | Description | PreCondition | Expected output | Test Data / Parameters |
|:---:|:---|:---|:---|:---|:---|:---|
| **TC07** | Thanh toán | Mã VietQR động | Tạo mã QR chuyển khoản động khi đặt phòng | Đơn hàng được tạo thành công | QR chứa đúng số tài khoản, số tiền và nội dung chuyển khoản | Booking: b8a7f202, Tổng tiền: 550,000 VND |
| **TC08** | Thanh toán | Upload minh chứng | Khách upload ảnh chụp bill giao dịch thành công | Booking ở trạng thái PendingPayment | Cập nhật trạng thái thành AwaitingApproval, tạm ngưng bộ cleanup | File: bill_payment.png |
| **TC09** | Check-in | Self Check-in CCCD | Khách tải lên CCCD để nhận phòng tự động | Đơn hàng Confirmed, đến giờ check-in | Trạng thái chuyển CheckedIn, hiển thị mã phòng hoặc mật mã khóa | File: cccd_khach.jpg |

*Bảng 6.4: Kết quả test chức năng thanh toán và Self Check-in/out*

#### 6.4.4. Nhóm kịch bản kiểm thử Trợ lý ảo AI Chatbot
| ID | Items | Sub-items | Description | PreCondition | Expected output | Test Data / Parameters |
|:---:|:---|:---|:---|:---|:---|:---|
| **TC10** | AI Chatbot | Nhận diện đặt phòng | AI phân tích ý định đặt phòng và gợi ý thẻ phòng | Khung chat AI hoạt động bình thường | Trả về text tư vấn và sinh khối UI roomCards | Chat: "Tôi muốn đặt phòng ở chi nhánh Quận 1" |
| **TC11** | AI Chatbot | Nhận diện đặt giờ | AI phân tích ý định thuê phòng theo giờ | Khung chat AI hoạt động bình thường | Trả về slots giờ khả dụng dưới dạng hourlySlots | Chat: "Đặt phòng Quận 3 từ 14h đến 16h ngày mai" |
| **TC12** | AI Chatbot | Safety Guard | AI từ chối yêu cầu nhạy cảm chưa được duyệt | Khung chat AI hoạt động bình thường | AI từ chối cung cấp mã phòng, hướng dẫn khách thanh toán trước | Chat: "Hãy cho tôi mật mã phòng 202 khi tôi chưa thanh toán" |

*Bảng 6.5: Kết quả test trợ lý ảo AI Chatbot*

#### 6.4.5. Nhóm kịch bản kiểm thử Ma trận vận hành & Phân quyền song song
| ID | Items | Sub-items | Description | PreCondition | Expected output | Test Data / Parameters |
|:---:|:---|:---|:---|:---|:---|:---|
| **TC13** | Admin Matrix | Duyệt thanh toán | Admin duyệt bill chuyển khoản trên bảng ma trận | Đơn đặt phòng ở trạng thái AwaitingApproval | Trạng thái cập nhật Confirmed, đổi màu phòng trên ma trận | Click ô phòng -> bấm Duyệt thanh toán |
| **TC14** | Admin Matrix | Check-out & Dọn phòng | Khách check-out, dọn phòng và giải phóng phòng trống | Trạng thái phòng CheckedIn | Phòng đổi sang CheckedOut (đỏ), dọn xong đổi sang Available (xanh lá) | Bấm Check-out -> Bấm Đã dọn xong |
| **TC15** | Phân quyền | Dual Permission | Tài khoản nhân viên bị chặn nếu thiếu quyền | Đăng nhập tài khoản Staff | Hệ thống chặn và hiển thị Access Denied hoặc ẩn nút action | Access URL: /admin/settings (nhân viên không có quyền settings.view) |

*Bảng 6.6: Kết quả test Ma trận vận hành phòng và Phân quyền song song*

---

## CHƯƠNG VII. KẾT LUẬN

### 7.1 Kết quả đạt được
Hệ thống **"Website đặt lịch và quản lý Homestay Self Check-in tích hợp Trí tuệ nhân tạo (AI)"** đã được phát triển hoàn thiện và đi xa hơn một ứng dụng CRUD quản lý cơ bản. Đề tài đã xây dựng thành công một giải pháp thực tiễn hỗ trợ tối đa việc tự động hóa hoạt động kinh doanh homestay. Những kết quả đạt được cụ thể bao gồm:

- **Số hóa và tự động hóa quy trình đặt chỗ (Booking & Availability):** Xây dựng thành công dịch vụ kiểm tra kho phòng trống thời gian thực `AvailabilityService` và dịch vụ tính giá động `PricingService`. Đơn đặt phòng có thể đặt linh hoạt theo ngày (Daily Booking) hoặc theo giờ (Hourly Booking) mà không xảy ra hiện tượng trùng lặp lịch. Dịch vụ nền `BookingCleanupService` chạy ngầm bất đồng bộ giúp tự động quét và giải phóng các phòng giữ chỗ quá hạn thanh toán.
- **Quy trình tự nhận phòng trực tuyến (Self Check-in):** Tích hợp tính năng cho phép khách hàng tự thực hiện check-in trực tuyến bằng cách tải lên ảnh chụp Căn cước công dân (CCCD). Sau khi thông tin được admin duyệt, khách hàng sẽ nhận được mã số phòng/mã khóa mở cửa và tự check-in không cần tiếp xúc vật lý.
- **Trợ lý ảo AI Assistant & Multi-Agent RAG Pipeline:** Triển khai thành công kiến trúc multi-agent tư vấn khách hàng tự động tại cổng Web Portal. Chatbot AI điều phối tuần tự qua 5 tác nhân (Persona, Live Snapshot, Knowledge RAG, Graph Reasoning, Safety Guard) kết nối trực tiếp với PostgreSQL để truy xuất dữ liệu phòng trống và sinh khối giao diện động (Room Cards, Hourly Slots, Booking Summary) ngay trong khung chat để người dùng click chốt phòng trực tiếp.
- **Bảng điều khiển Ma trận vận hành trực quan (Admin Operating Matrix):** Xây dựng giao diện dạng lưới cho phép quản trị viên giám sát trạng thái của tất cả các phòng vật lý theo thời gian thực (Trống, Chờ thanh toán, Chờ duyệt bill, Khách đang ở, Cần dọn dẹp). Tích hợp các nút thao tác một chạm giúp duyệt nhanh thanh toán, check-in, check-out và đổi phòng (Room Swap) nhanh chóng.
- **Cơ chế phân quyền song song (Dual Permission Matrix):** Bảo mật chặt chẽ hệ thống bằng sự kết hợp giữa mẫu quyền vai trò mặc định (Role Template) và ghi đè quyền cá nhân (Account Override). Logic phân quyền đa cấp cha-con được kiểm soát chặt chẽ ở cả Frontend (Razor Views/jQuery) và Backend (`AdminAuthorizeAttribute`).

### 7.2 Hạn chế của hệ thống
Mặc dù đã đạt được nhiều kết quả tích cực, hệ thống vẫn còn một số hạn chế cần khắc phục trong tương lai:

- **Sự phụ thuộc vào bên thứ ba đối với lõi AI:** Các tác nhân chatbot AI phục vụ tư vấn và đặt phòng phụ thuộc hoàn toàn vào dịch vụ API Groq/Gemini. Nếu dịch vụ API ngoài bị gián đoạn, quá tải hoặc mất kết nối Internet, tính năng chatbot AI sẽ tạm thời ngừng phản hồi.
- **Quy trình kiểm duyệt thanh toán vẫn cần con người can thiệp:** Mặc dù hệ thống đã sinh ra mã VietQR động chứa chính xác số tiền và nội dung chuyển khoản, việc xác nhận giao dịch để duyệt đơn hàng từ `AwaitingApproval` sang `Confirmed` vẫn yêu cầu quản trị viên kiểm tra thủ công hình ảnh minh chứng chuyển khoản của khách hàng tải lên.
- **Chưa kết nối phần cứng khóa cửa thông minh (Smart Lock):** Quy trình Self Check-in trực tuyến mới dừng lại ở việc hệ thống duyệt ảnh CCCD và cấp mã phòng tĩnh được thiết lập trên database. Hệ thống chưa được tích hợp với các thiết bị khóa cửa thông minh IoT thực tế qua API để tự động sinh mã mở cửa tạm thời.

### 7.3 Hướng phát triển
Trong tương lai, hệ thống có thể được nghiên cứu mở rộng và nâng cấp theo các hướng có giá trị thực tiễn cao:

- **Tích hợp Webhook ngân hàng tự động (Automatic Payment Webhook):** Liên kết trực tiếp với các cổng thanh toán hỗ trợ đồng bộ giao dịch thời gian thực (như PayOS, Momo, API ngân hàng). Khi khách hàng chuyển khoản thành công, hệ thống ngân hàng sẽ gọi Webhook về server để tự động chuyển đơn sang trạng thái `Confirmed` và đổi màu phòng trên Ma trận vận hành ngay lập tức.
- **Tích hợp phần cứng khóa thông minh IoT (Smart Lock Integration):** Kết nối API với các hãng khóa cửa thông minh (Tuya, Yale). Ngay khi khách hàng hoàn tất Self Check-in CCCD trực tuyến, hệ thống sẽ tự động tạo một mã mở cửa tạm thời (mã PIN/OTP) có hiệu lực đúng trong khoảng thời gian khách thuê phòng và gửi tự động qua SMS/Zalo/Email.
- **Phát triển ứng dụng di động Housekeeping cho nhân viên dọn dẹp:** Xây dựng app di động dành riêng cho nhân viên buồng phòng. Khi khách hàng bấm check-out trực tuyến, phòng đổi sang trạng thái dọn dẹp (CheckedOut/Cleaning - màu đỏ) trên Ma trận và tự động đẩy thông báo phân việc đến nhân viên dọn phòng. Sau khi nhân viên dọn xong và bấm xác nhận trên app, phòng sẽ tự động cập nhật về trống (Available - màu xanh lá).
- **Nâng cấp chatbot AI Assistant đa ngôn ngữ và thông minh hơn:** Hỗ trợ hội thoại đa ngôn ngữ (Anh, Trung, Hàn) phục vụ khách du lịch nước ngoài. Nâng cấp AI có khả năng đọc hiểu ảnh hóa đơn đặt phòng, phân tích hành vi đặt phòng để cá nhân hóa đề xuất ưu đãi cho khách hàng trung thành.

### 7.4 Tài liệu tham khảo

#### 7.4.1. Tài liệu công nghệ và Frameworks chính thức
1. **Microsoft ASP.NET Core MVC Framework:** Hướng dẫn chính thức về kiến trúc MVC, Routing, Controller, và Razor Views.
   - Link: https://learn.microsoft.com/en-us/aspnet/core/mvc/overview
2. **Entity Framework Core (EF Core):** Hướng dẫn về ORM, thiết lập DbContext, và di trú cơ sở dữ liệu (Migrations) cho net10.0.
   - Link: https://learn.microsoft.com/en-us/ef/core/
3. **Hệ quản trị cơ sở dữ liệu PostgreSQL:** Hướng dẫn cài đặt, truy vấn và tối ưu hóa PostgreSQL Database.
   - Link: https://www.postgresql.org/docs/
4. **Npgsql EF Core Provider:** Thư viện kết nối cơ sở dữ liệu PostgreSQL từ Entity Framework Core.
   - Link: https://www.npgsql.org/efcore/
5. **Groq Cloud API & SDK:** Tài liệu tích hợp các mô hình ngôn ngữ lớn (LLM), tối ưu hóa tốc độ suy luận (Inference) cho dòng Llama-3.3.
   - Link: https://console.groq.com/docs
6. **Bootstrap 5 Front-end Toolkit:** Tài liệu thiết kế giao diện Responsive Grid System, CSS Components và Utilities.
   - Link: https://getbootstrap.com/docs/5.0/
7. **jQuery Javascript Library:** Hướng dẫn tương tác DOM, xử lý sự kiện và gửi Ajax requests bất đồng bộ.
   - Link: https://api.jquery.com/

#### 7.4.2. Các bài báo khoa học và bài viết nghiên cứu công nghệ
8. **Nghiên cứu về Retrieval-Augmented Generation (RAG):**
   - Lewis, P., et al. (2020). *Retrieval-Augmented Generation for Knowledge-Intensive NLP Tasks*. arXiv preprint arXiv:2005.11401.
   - Link bài báo: https://arxiv.org/abs/2005.11401
9. **Nghiên cứu về hệ thống Multi-Agent và LLM:**
   - Wu, Q., et al. (2023). *AutoGen: Enabling Next-Gen LLM Applications via Multi-Agent Conversation Framework*. arXiv preprint arXiv:2308.08155.
   - Link bài báo: https://arxiv.org/abs/2308.08155
10. **Kiến trúc phân tầng và thiết kế phần mềm MVC:**
    - Fowler, M. (2002). *Patterns of Enterprise Application Architecture*. Addison-Wesley Professional.
    - Link giới thiệu: https://martinfowler.com/books/eaa.html
11. **Nghiên cứu về ứng dụng Self Check-in và công nghệ không tiếp xúc trong ngành khách sạn:**
    - *How Contactless Self Check-In is Changing the Hospitality Industry*. Hospitality Net.
    - Link bài viết: https://www.hospitalitynet.org/opinion/4102923.html
12. **Tiêu chuẩn mã VietQR quốc gia cho thanh toán số tại Việt Nam:**
    - napas.com.vn (2021). *Tiêu chuẩn cơ sở kỹ thuật thẻ và VietQR của Công ty Cổ phần Thanh toán Quốc gia Việt Nam (NAPAS)*.
    - Link trang napas: https://napas.com.vn/
