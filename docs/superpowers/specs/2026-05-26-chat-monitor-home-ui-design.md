# Thiết kế cải thiện UI hội thoại quản trị và trang chủ

## Mục tiêu

Làm trang hội thoại quản trị dễ nhìn, dễ thao tác hơn cho nhân viên theo dõi khách; Việt hóa các nhãn tiếng Anh/không dấu; đồng thời sửa trang chủ để nút tìm phòng và nút chatbot nổi không đè nhau.

## Phạm vi

- Cập nhật giao diện `AdminChatMonitor` theo hướng kết hợp app chat và dashboard vận hành nhẹ.
- Việt hóa text trong Razor/JavaScript của màn hình hội thoại.
- Chỉnh CSS trang chủ/chatbot để vùng nút nổi không che nội dung hoặc cạnh tranh với nút tìm phòng.
- Không thay đổi backend, SignalR hub, database, hay logic AI hiện có.

## Thiết kế trang hội thoại quản trị

Giữ bố cục 2 cột hiện tại để giảm rủi ro: sidebar bên trái là danh sách hội thoại, vùng bên phải là nội dung chat. Sidebar sẽ có tiêu đề rõ “Hội thoại”, ô tìm kiếm “Tìm theo tên hoặc mã phiên…”, thẻ hội thoại có tên khách, tin nhắn gần nhất, trạng thái và thời gian tương đối bằng tiếng Việt.

Vùng chat sẽ có header như dashboard nhẹ: tên khách, mã phiên rút gọn, badge trạng thái AI, và nhóm nút thao tác rõ chữ gồm “Tạm dừng AI”, “Bật lại AI”, “Tin nhắn tự động”. Trạng thái sẽ dùng các nhãn tiếng Việt như “AI đang trả lời”, “AI đã tạm dừng”.

Tin nhắn sẽ được trình bày thoáng hơn: khách căn trái nền xanh nhạt, AI/admin căn phải, label người gửi và thời gian nhỏ hơn, nội dung có line-height dễ đọc. Khung nhập ở đáy dùng placeholder “Nhập tin nhắn cho khách…”, nút gửi nổi bật, menu gửi nhanh gồm “Gửi danh sách phòng”, “Gửi khung giờ”, “Gửi form thông tin”, “Gửi QR thanh toán”.

## Thiết kế trang chủ

Form tìm phòng giữ cấu trúc hiện tại nhưng chỉnh khoảng cách và responsive. Trên desktop, nút “TÌM PHÒNG” vẫn nằm trong form nhưng không bị đè bởi chatbot. Trên màn hình nhỏ, form sẽ xếp xuống nhiều hàng và nút tìm phòng chiếm chiều ngang phù hợp.

Khu vực cuối trang có padding dưới đủ lớn để nút chatbot nổi không che footer hoặc thẻ phòng cuối. Nút chatbot giữ vị trí góc phải dưới nhưng có khoảng cách an toàn với mép và nội dung.

## Kiểm thử

- Build project bằng `dotnet build WebHomestay/WebHomestay.csproj`.
- Chạy app và kiểm tra thủ công trang chủ: desktop/mobile, form tìm kiếm, nút chatbot không đè nút tìm phòng hoặc footer.
- Kiểm tra thủ công trang `/admin/chat-monitor`: danh sách hội thoại, chọn phiên, nút tạm dừng/bật lại AI, gửi tin nhắn, menu gửi nhanh, các nhãn đã Việt hóa.
