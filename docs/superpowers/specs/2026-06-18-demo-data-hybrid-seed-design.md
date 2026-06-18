# Demo Data Hybrid Seed Design

## Mục tiêu

Chuẩn bị một gói dữ liệu demo có thể khôi phục nhanh như backup/restore database thật, đồng thời có thể reseed lặp lại vào database hiện tại khi cần cứu hỏa trước buổi demo.

Gói dữ liệu phải làm cho:

- Ma trận vận hành có booking rải đều, nhìn bận vừa phải trong giai đoạn 15/06/2026 đến 22/06/2026.
- Tất cả phòng đều hoạt động và có đầy đủ combo giờ.
- Mỗi booking demo đều có đủ 2 ảnh CCCD và 1 ảnh bill từ thư mục `ảnh/`.
- Tất cả chi nhánh đều có dữ liệu QR thanh toán.
- Có các đơn hủy đang chờ duyệt ở tất cả chi nhánh.
- Trang thống kê có dữ liệu doanh thu dày hơn một tuần demo, với lịch sử 1-3 tháng trước đó.

## Phạm vi

Phạm vi seed bao gồm:

- `branches`
- `rooms`
- `room_slot_templates`
- `room_slot_template_assignments`
- `room_slot_inventories`
- `bookings`
- `booking_cancellation_requests`
- `system_settings` liên quan QR thanh toán
- các file upload mà booking và QR đang tham chiếu

Phạm vi không bao gồm:

- thay đổi logic nghiệp vụ booking
- thay đổi UI
- thay đổi migration schema
- làm mới dữ liệu AI nếu không liên quan trực tiếp đến demo booking, ma trận, thống kê

## Cách tiếp cận được chọn

Chọn mô hình hybrid:

1. Tạo một database demo riêng, ví dụ `web_homestay_demo`, để chạy demo an toàn.
2. Có một bộ reseed idempotent để tạo lại trạng thái demo chuẩn.
3. Có một quy trình backup từ đúng database demo sau khi reseed hoàn tất.
4. Có một quy trình restore nhanh để khôi phục database demo hoặc bơm lại vào database hiện tại khi cần.

Lý do chọn:

- An toàn hơn so với chỉ seed thẳng lên DB đang phát triển.
- Dễ reset về trạng thái đẹp trước giờ demo.
- Vẫn linh hoạt nếu cần sửa dữ liệu phút chót.
- Phù hợp yêu cầu "giống restore db, backup db".

## Deliverable

Gói bàn giao dự kiến gồm:

- một spec này
- một script PowerShell để reseed demo data
- các script SQL thành phần cho từng lớp dữ liệu
- một script backup PostgreSQL
- một script restore PostgreSQL
- tài liệu hướng dẫn chạy ngắn gọn

Tên file có thể điều chỉnh theo cấu trúc repo, nhưng tối thiểu phải có:

- `scripts/seed-demo-core.sql`
- `scripts/seed-demo-bookings.sql`
- `scripts/seed-demo-cancellations.sql`
- `scripts/seed-demo-history.sql`
- `scripts/reseed-demo-data.ps1`
- `scripts/backup-demo-db.ps1`
- `scripts/restore-demo-db.ps1`

## Kiến trúc Seed

### 1. Core normalization

Script core chịu trách nhiệm chuẩn hóa dữ liệu nền để mọi script sau có điểm tựa ổn định:

- đảm bảo tất cả chi nhánh mục tiêu tồn tại
- đảm bảo tất cả phòng mục tiêu tồn tại và `status = Available`
- đảm bảo tất cả phòng đều có đầy đủ combo giờ
- đảm bảo các setting QR cho từng chi nhánh đều được gán
- đảm bảo các file QR đã được copy vào đúng thư mục upload mà hệ thống đang dùng

Script này phải có thể chạy lặp lại mà không tạo bản ghi trùng.

### 2. Demo asset sync

Asset sync chịu trách nhiệm map file thật từ thư mục `ảnh/` vào các vị trí mà hệ thống đọc:

- `ảnh/cccd.png`
- `ảnh/chuyenkhoan.jpg`
- `ảnh/ma qr.png`

Quy tắc:

- Mỗi booking demo phải có đủ 2 ảnh CCCD và 1 bill.
- Tất cả chi nhánh còn thiếu QR phải được gán QR demo từ `ảnh/ma qr.png`.
- Nếu hệ thống yêu cầu file thật trong `wwwroot/uploads` hoặc `App_Data/SecureUploads`, script phải copy file sang đúng thư mục thay vì chỉ ghi path ảo.
- Tên file đầu ra nên có tiền tố hoặc mẫu riêng để dễ phân biệt asset demo.

### 3. Matrix bookings

Lớp này chịu trách nhiệm làm đẹp ma trận vận hành trong khoảng 15/06/2026 đến 22/06/2026.

Thiết kế dữ liệu:

- Mật độ lấp đầy mục tiêu: khoảng 55-70 phần trăm slot có booking.
- Booking phải rải đều giữa các chi nhánh.
- Booking phải rải đều giữa các phòng, không dồn vài phòng rồi bỏ trống hoàn toàn các phòng khác.
- Có trộn booking theo giờ và booking theo ngày.
- Trạng thái booking phải đa dạng: `PendingPayment`, `AwaitingApproval`, `Confirmed`, `CheckedIn`, `CheckedOut`.
- Không dùng toàn bộ `Cancelled` trong ma trận chính vì sẽ làm yếu cảm giác vận hành thực.

Quy tắc consistency:

- Booking phải bám vào `room_slot_inventories` hoặc cấu trúc availability đang dùng, để ma trận hiển thị đúng bằng dữ liệu thật.
- Không được tạo overlap sai logic trên cùng một phòng và cùng một khung thời gian.
- Booking ngày hiện tại trong tuần demo nên nghiêng về `Confirmed` hoặc `CheckedIn`, còn các ngày đã qua nên có `CheckedOut` để thống kê có doanh thu thực tế.

### 4. Cancellation queue

Tạo các đơn hủy đang chờ duyệt ở tất cả chi nhánh.

Thiết kế dữ liệu:

- Mỗi chi nhánh có ít nhất một nhóm cancellation request.
- Có liên kết hợp lệ tới booking nguồn.
- Có đủ dữ liệu ảnh hoặc tài liệu liên quan nếu luồng hiện tại hiển thị chúng.
- Trạng thái phải đúng với luồng "đang chờ duyệt", không seed lẫn trạng thái đã xử lý nếu mục tiêu chính là demo queue.

### 5. Revenue history

Lớp này tạo độ dày cho báo cáo thống kê trong 1-3 tháng trước tuần demo.

Thiết kế dữ liệu:

- Có doanh thu theo ngày thường, cuối tuần và vài ngày đỉnh.
- Có mix booking giờ và booking ngày.
- Có mức giá khác nhau theo hạng phòng để biểu đồ nhìn tự nhiên.
- Có phân bố giữa các chi nhánh để biểu đồ chi nhánh không bị phẳng.
- Dữ liệu doanh thu lịch sử không cần dày 100 phần trăm occupancy; mục tiêu là giống hệ thống thật, không phải tối đa lấp đầy.

## Dữ liệu nhận diện demo

Mọi dữ liệu demo nên có marker nhận diện để reseed an toàn:

- pattern tên khách
- `CustomerNote`
- `AdminNote`
- hoặc quy ước file asset demo

Khuyến nghị dùng marker dạng rõ ràng, ví dụ một prefix thống nhất trong `CustomerNote` hoặc `AdminNote`, để script có thể:

- tìm đúng booking demo cũ
- xóa đúng cancellation demo cũ
- xóa đúng inventory hoặc asset demo liên quan
- không đụng dữ liệu thật hoặc dữ liệu dev tay

## Chiến lược idempotent và reset

Reseed phải ưu tiên reset theo phạm vi demo thay vì truncate toàn bộ bảng.

Quy trình reset dự kiến:

1. Xác định các booking demo cũ bằng marker.
2. Xóa các cancellation request phụ thuộc.
3. Xóa hoặc cập nhật các booking demo cũ.
4. Đồng bộ lại asset demo nếu thiếu.
5. Tạo lại inventory và booking demo theo chuẩn.
6. Kiểm tra lại count và độ phủ.

Quy trình này cho phép:

- reseed trên `web_homestay_demo`
- reseed trên DB hiện tại nếu người dùng cần bơm gấp
- giảm rủi ro làm bẩn dữ liệu ngoài phạm vi demo

## Backup và Restore

### Backup

Backup phải được tạo từ database demo chuẩn sau khi reseed thành công.

Yêu cầu:

- dùng công cụ PostgreSQL phù hợp như `pg_dump`
- có tên file chứa ngày giờ hoặc nhãn `demo`
- có hướng dẫn chạy ngắn gọn

### Restore

Restore phải hỗ trợ:

- khôi phục vào `web_homestay_demo`
- khôi phục vào một database khác do người dùng chỉ định

Yêu cầu:

- script nhận tên database đích
- có bước cảnh báo rõ nếu database đích đã có dữ liệu
- thao tác không tương tác nếu có thể, để chạy nhanh trước demo

## Luồng chạy đề xuất

Luồng chuẩn để chuẩn bị demo:

1. Tạo hoặc làm sạch `web_homestay_demo`.
2. Chạy migrate nếu cần.
3. Chạy reseed demo.
4. Verify dữ liệu demo.
5. Tạo backup snapshot từ DB demo.
6. Trước giờ demo, nếu dữ liệu bị lệch, restore snapshot hoặc reseed lại.

Luồng cứu hỏa:

1. Trỏ script vào DB hiện tại.
2. Chỉ reset dữ liệu có marker demo.
3. Reseed lại nhanh.

## Kiểm thử và xác minh

Tối thiểu phải verify các điểm sau:

- tất cả chi nhánh đều có QR thanh toán hợp lệ
- tất cả phòng mục tiêu đều `Available`
- tất cả phòng đều có đủ combo giờ
- tuần 15/06/2026 đến 22/06/2026 có booking rải đều, mật độ gần mức mục tiêu
- mỗi booking demo đều có đủ 2 ảnh CCCD và 1 bill
- tất cả chi nhánh đều có đơn hủy chờ duyệt
- trang thống kê có dữ liệu doanh thu trong 1-3 tháng trước tuần demo

Nếu có thể, nên bổ sung ít nhất một lệnh kiểm tra nhanh sau reseed để in ra:

- số chi nhánh có QR
- số phòng active
- số phòng có đủ combo
- số booking demo trong tuần
- số cancellation pending
- khoảng ngày doanh thu hiện có

## Rủi ro và cách xử lý

### Rủi ro 1: lệch logic availability

Nếu seed booking trực tiếp mà không khớp `room_slot_inventories` hoặc logic availability, ma trận sẽ hiển thị sai.

Giải pháp:

- đọc kỹ schema inventory và cách `AvailabilityService` cùng `SlotManagementService` dùng dữ liệu
- seed bám đúng bảng và quan hệ hiện có

### Rủi ro 2: path ảnh không trỏ tới file thật

Nếu chỉ cập nhật path trong DB mà không có file thật ở thư mục upload, view chi tiết booking hoặc QR sẽ vỡ ảnh.

Giải pháp:

- seed phải bao gồm bước copy asset thật
- dùng path đúng theo convention hiện tại của hệ thống

### Rủi ro 3: đụng dữ liệu dev hiện có

Nếu reseed trên DB đang dùng mà không có marker rõ, script có thể xóa nhầm dữ liệu tay.

Giải pháp:

- tất cả dữ liệu demo phải có marker thống nhất
- mọi thao tác reset chỉ dựa trên marker đó

### Rủi ro 4: backup không tái lập đúng môi trường demo

Nếu backup được tạo trước khi verify, snapshot có thể mang dữ liệu lỗi.

Giải pháp:

- chỉ cho phép backup sau bước verify thành công
- tài liệu hóa rõ thứ tự chạy

## Quyết định cụ thể đã chốt với người dùng

- Deliverable theo hướng hybrid: vừa reseed vừa backup/restore.
- Có cả DB demo riêng và khả năng bơm lại vào DB hiện tại.
- Ma trận vận hành đẹp trong khoảng 15/06/2026 đến 22/06/2026.
- Báo cáo có thêm lịch sử 1-3 tháng trước đó.
- Mỗi booking demo có đúng 2 ảnh CCCD và 1 bill từ thư mục `ảnh/`.
- Đơn hủy đang chờ duyệt xuất hiện ở tất cả chi nhánh.
- Mức độ kín của ma trận ở mức khá bận, khoảng 55-70 phần trăm.

## Ngoài phạm vi cho vòng này

- tối ưu hiệu năng lâu dài cho reseed khối lượng rất lớn
- tạo dữ liệu ngẫu nhiên vô hạn
- thay đổi business rule của booking hoặc cancellation
- làm lại báo cáo thống kê

## Tiêu chí hoàn thành

Thiết kế này được xem là hoàn thành khi implementation sau đó có thể:

- dựng một DB demo riêng có dữ liệu đẹp và ổn định cho buổi demo
- reset lại trạng thái demo trong thời gian ngắn
- restore lại snapshot nếu cần
- hiển thị ma trận, danh sách booking, danh sách hủy và thống kê với dữ liệu nhìn giống hệ thống thật
