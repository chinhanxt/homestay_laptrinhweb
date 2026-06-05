# Thiết kế đánh dấu đã xem và gửi nhanh form từ admin

## Mục tiêu

Khi admin mở hội thoại, các tin khách chưa xem trong hội thoại đó được đánh dấu đã xem và badge thông báo giảm ngay. Menu “Gửi nhanh” của admin phải gửi đúng các block tương tác mà chatbot AI đang gửi cho khách, để admin chủ động gửi chọn phòng, chọn giờ, form thông tin hoặc link thanh toán.

## Phạm vi

- Badge thông báo admin chỉ đếm tin nhắn khách chưa xem.
- Mở một hội thoại trong trang giám sát chat sẽ gọi server đánh dấu đã xem cho phiên đó.
- Server trả/broadcast lại số tin chưa xem để các UI admin cập nhật.
- Form gửi nhanh admin dùng lại `uiBlocks` public chatbot đang render trong `site.js`, không chỉ hiển thị tin nhắn mô tả.
- Không thay đổi luồng AI multi-agent/RAG/Graph hiện có.

## Thiết kế thông báo đã xem

Thêm API trong `AdminChatMonitorController` để đánh dấu đã xem theo `sessionId`. API này set `IsRead = true` cho các `AdminChatMessage` thuộc phiên có `Role = "user"` hoặc các bản ghi đại diện tin khách nếu có. Sau khi lưu, hub sẽ gửi lại thông tin session/badge cho nhóm `admin_monitor`.

Danh sách session và notification float sẽ nhận thêm `unreadCount`. Badge tổng hiển thị tổng số tin khách chưa xem. Khi admin chọn session, frontend gọi endpoint mark-read rồi cập nhật session hiện tại về `unreadCount = 0`, tổng badge giảm tương ứng.

## Thiết kế gửi nhanh form

Thêm endpoint tạo block gửi nhanh theo loại: `roomSelector`, `slotPicker`, `infoForm`, `paymentQr`. Endpoint đọc ngữ cảnh session hiện có và dữ liệu thật trong database để tạo JSON block tương thích `site.js`:

- `roomSelector`: tạo `roomCards` hoặc `dailyRooms` từ danh sách phòng phù hợp.
- `slotPicker`: tạo `hourlySlots` nếu có ngày/phòng; nếu thiếu ngữ cảnh thì gửi `dateSelector` trước.
- `infoForm`: tạo `bookingForm` với các field khách cần nhập.
- `paymentQr`: tạo `paymentQr` hoặc link thanh toán khi đã có booking/payment URL; nếu chưa có booking thì trả thông báo yêu cầu khách hoàn tất thông tin trước.

`ChatHub.AdminReply` tiếp tục broadcast `formBlockJson` và `formBlockType`, nhưng JSON sẽ là UI block thật. `site.js` parse JSON này và gọi lại `renderUiBlocks(...)` để khách thấy form giống AI gửi.

## Kiểm thử

- Build project.
- Mở trang chủ, gửi 2 tin khách; badge admin tăng 2.
- Admin mở đúng hội thoại; badge giảm 2 và refresh không tăng lại.
- Admin dùng từng mục gửi nhanh; khách thấy block tương tác thật trong chatbot và thao tác được như khi AI gửi.
