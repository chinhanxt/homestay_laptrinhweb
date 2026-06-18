# CHƯƠNG VII. KẾT LUẬN

## 7.1 Kết quả đạt được

Hệ thống **"Website đặt lịch và quản lý Homestay Self Check-in tích hợp Trí tuệ nhân tạo (AI)"** đã được phát triển hoàn thiện và đi xa hơn một ứng dụng CRUD quản lý cơ bản. Đề tài đã xây dựng thành công một giải pháp thực tiễn hỗ trợ tối đa việc tự động hóa hoạt động kinh doanh homestay. Những kết quả đạt được cụ thể bao gồm:

1. **Số hóa và tự động hóa quy trình đặt chỗ (Booking & Availability):** 
   Xây dựng thành công dịch vụ kiểm tra kho phòng trống thời gian thực `AvailabilityService` và dịch vụ tính giá động `PricingService`. Đơn đặt phòng có thể đặt linh hoạt theo ngày (Daily Booking) hoặc theo giờ (Hourly Booking) mà không xảy ra hiện tượng trùng lặp lịch. Dịch vụ nền `BookingCleanupService` chạy ngầm bất đồng bộ giúp tự động quét và giải phóng các phòng giữ chỗ quá hạn thanh toán.
2. **Quy trình tự nhận phòng trực tuyến (Self Check-in):** 
   Tích hợp tính năng cho phép khách hàng tự thực hiện check-in trực tuyến bằng cách tải lên ảnh chụp Căn cước công dân (CCCD). Sau khi thông tin được admin duyệt, khách hàng sẽ nhận được mã số phòng/mã khóa mở cửa và tự check-in không cần tiếp xúc vật lý.
3. **Trợ lý ảo AI Assistant & Multi-Agent RAG Pipeline:** 
   Triển khai thành công kiến trúc multi-agent tư vấn khách hàng tự động tại cổng Web Portal. Chatbot AI điều phối tuần tự qua 5 tác nhân (Persona, Live Snapshot, Knowledge RAG, Graph Reasoning, Safety Guard) kết nối trực tiếp với PostgreSQL để truy xuất dữ liệu phòng trống và sinh khối giao diện động (Room Cards, Hourly Slots, Booking Summary) ngay trong khung chat để người dùng click chốt phòng trực tiếp.
4. **Bảng điều khiển Ma trận vận hành trực quan (Admin Operating Matrix):** 
   Xây dựng giao diện dạng lưới cho phép quản trị viên giám sát trạng thái của tất cả các phòng vật lý theo thời gian thực (Trống, Chờ thanh toán, Chờ duyệt bill, Khách đang ở, Cần dọn dẹp). Tích hợp các nút thao tác một chạm giúp duyệt nhanh thanh toán, check-in, check-out và đổi phòng (Room Swap) nhanh chóng.
5. **Cơ chế phân quyền song song (Dual Permission Matrix):** 
   Bảo mật chặt chẽ hệ thống bằng sự kết hợp giữa mẫu quyền vai trò mặc định (Role Template) và ghi đè quyền cá nhân (Account Override). Logic phân quyền đa cấp cha-con được kiểm soát chặt chẽ ở cả Frontend (Razor Views/jQuery) và Backend (`AdminAuthorizeAttribute`).

---

## 7.2 Hạn chế của hệ thống

Mặc dù đã đạt được nhiều kết quả tích cực, hệ thống vẫn còn một số hạn chế cần khắc phục trong tương lai:

1. **Sự phụ thuộc vào bên thứ ba đối với lõi AI:** 
   Các tác nhân chatbot AI phục vụ tư vấn và đặt phòng phụ thuộc hoàn toàn vào dịch vụ API Groq/Gemini. Nếu dịch vụ API ngoài bị gián đoạn, quá tải hoặc mất kết nối Internet, tính năng chatbot AI sẽ tạm thời ngừng phản hồi.
2. **Quy trình kiểm duyệt thanh toán vẫn cần con người can thiệp:** 
   Mặc dù hệ thống đã sinh ra mã VietQR động chứa chính xác số tiền và nội dung chuyển khoản, việc xác nhận giao dịch để duyệt đơn hàng từ `AwaitingApproval` sang `Confirmed` vẫn yêu cầu quản trị viên kiểm tra thủ công hình ảnh minh chứng chuyển khoản của khách hàng tải lên.
3. **Chưa kết nối phần cứng khóa cửa thông minh (Smart Lock):** 
   Quy trình Self Check-in trực tuyến mới dừng lại ở việc hệ thống duyệt ảnh CCCD và cấp mã phòng tĩnh được thiết lập trên database. Hệ thống chưa được tích hợp với các thiết bị khóa cửa thông minh IoT thực tế qua API để tự động sinh mã mở cửa tạm thời.

---

## 7.3 Hướng phát triển

Trong tương lai, hệ thống có thể được nghiên cứu mở rộng và nâng cấp theo các hướng có giá trị thực tiễn cao:

1. **Tích hợp Webhook ngân hàng tự động (Automatic Payment Webhook):** 
   Liên kết trực tiếp với các cổng thanh toán hỗ trợ đồng bộ giao dịch thời gian thực (như PayOS, Momo, API ngân hàng). Khi khách hàng chuyển khoản thành công, hệ thống ngân hàng sẽ gọi Webhook về server để tự động chuyển đơn sang trạng thái `Confirmed` và đổi màu phòng trên Ma trận vận hành ngay lập tức.
2. **Tích hợp phần cứng khóa thông minh IoT (Smart Lock Integration):** 
   Kết nối API với các hãng khóa cửa thông minh (Tuya, Yale). Ngay khi khách hàng hoàn tất Self Check-in CCCD trực tuyến, hệ thống sẽ tự động tạo một mã mở cửa tạm thời (mã PIN/OTP) có hiệu lực đúng trong khoảng thời gian khách thuê phòng và gửi tự động qua SMS/Zalo/Email.
3. **Phát triển ứng dụng di động Housekeeping cho nhân viên dọn dẹp:** 
   Xây dựng app di động dành riêng cho nhân viên buồng phòng. Khi khách hàng bấm check-out trực tuyến, phòng đổi sang trạng thái dọn dẹp (CheckedOut/Cleaning - màu đỏ) trên Ma trận và tự động đẩy thông báo phân việc đến nhân viên dọn phòng. Sau khi nhân viên dọn xong và bấm xác nhận trên app, phòng sẽ tự động cập nhật về trống (Available - màu xanh lá).
4. **Nâng cấp chatbot AI Assistant đa ngôn ngữ và thông minh hơn:** 
   Hỗ trợ hội thoại đa ngôn ngữ (Anh, Trung, Hàn) phục vụ khách du lịch nước ngoài. Nâng cấp AI có khả năng đọc hiểu ảnh hóa đơn đặt phòng, phân tích hành vi đặt phòng để cá nhân hóa đề xuất ưu đãi cho khách hàng trung thành.

---

## 7.4 Tài liệu tham khảo

### 7.4.1. Tài liệu công nghệ và Frameworks chính thức
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

### 7.4.2. Các bài báo khoa học và bài viết nghiên cứu công nghệ
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
