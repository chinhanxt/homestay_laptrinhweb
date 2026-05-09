Mục tiêu xây dựng một website đặt phòng homestay theo giờ hoặc theo ngày với mô hình vận hành hoàn toàn online, hướng đến xu hướng “self check-in/self check-out” đang phát triển tại Việt Nam, đặc biệt ở TP.HCM.

Khách hàng có thể tự tìm kiếm, đặt phòng, thanh toán, nhận hướng dẫn check-in/check-out mà không cần gặp nhân viên hay thực hiện thủ tục trực tiếp. Hệ thống hướng đến việc thay thế quy trình đặt phòng thủ công hiện đang phổ biến qua Zalo hoặc tin nhắn.

## Concept chính

Website hoạt động như một “khách sạn mini tự vận hành”, tập trung vào:

* tự động hóa quy trình đặt phòng,
* trải nghiệm người dùng hiện đại,
* giảm thao tác thủ công,
* tối ưu cho người dùng mới.

Người dùng có thể:

* tìm phòng theo giờ/ngày linh hoạt,
* lọc phòng theo giá, tiện nghi hoặc số người,
* xem trạng thái phòng trống theo thời gian thực,
* đặt phòng online,
* xem lịch sử đặt phòng,
* thực hiện check-in/check-out online.

## Điểm khác biệt của dự án

Điểm nổi bật chính là tích hợp AI Assistant hỗ trợ khách hàng trong suốt quá trình sử dụng website.

AI có vai trò:

* trò chuyện với khách hàng,
* tư vấn phòng phù hợp,
* tự động suggest phòng còn trống,
* hỗ trợ chốt phòng nhanh,
* hướng dẫn thao tác trực tiếp trên giao diện web.

Ví dụ:

* “Còn phòng nào cho 2 người tối nay?”
* “Phòng nào có máy chiếu?”
* “Budget 500k có phòng nào đẹp?”

AI sẽ:

* phân tích nhu cầu,
* lọc dữ liệu phòng,
* gợi ý phù hợp,
* hướng dẫn người dùng đặt phòng.

## Hệ thống hướng dẫn thông minh (Tutorial Onboarding)

Website tích hợp cơ chế hướng dẫn thao tác cho người dùng mới tương tự tutorial trong game.

Ví dụ:

* highlight các khu vực trên giao diện,
* hiển thị hướng dẫn:

  * “Bấm vào đây để xem phòng”
  * “Kéo để chọn thời gian nhận phòng”
* hỗ trợ người dùng làm quen với hệ thống nhanh chóng.

Mục tiêu:

* tăng trải nghiệm người dùng,
* giảm bối rối cho khách mới,
* tạo cảm giác hiện đại và thân thiện.

## Chức năng chính

### User

* Đăng ký / đăng nhập
* Xem danh sách phòng
* Lọc phòng theo:

  * giờ/ngày
  * giá
  * tiện nghi
* Đặt phòng online
* Thanh toán giả lập (demo)
* Xem lịch sử đặt phòng

### AI Assistant


* tích hợp AI API,
* hỗ trợ hội thoại tự nhiên,
* suggest phòng thông minh theo nhu cầu người dùng.

### Admin

* CRUD phòng
* Quản lý booking
* Quản lý trạng thái phòng
* Quản lý người dùng

## Chiến lược phát triển

Ưu tiên hoàn thành phần core trước:

* login/register,
* CRUD phòng,
* booking,
* database,
* quản lý trạng thái phòng.

Sau khi hệ thống hoạt động ổn định mới tiếp tục phát triển:

* AI Assistant,
* tutorial onboarding,
* animation/trải nghiệm nâng cao.

Mục tiêu là xây dựng một đồ án thực tế, vừa đủ mạnh để tạo điểm khác biệt nhưng vẫn phù hợp phạm vi môn học, tránh phát triển quá lớn như một startup hoàn chỉnh.

## Công nghệ sử dụng

* ASP.NET Core MVC
* SQL Server
* HTML/CSS
* Bootstrap
* jQuery
* Microsoft Visual Studio
