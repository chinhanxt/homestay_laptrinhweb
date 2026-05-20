Cấu hình AI:
Trước khi khách chat sẽ có 1 khung form thể hiện các thông tin sau:
- Tên: để AI xưng hô 
- Chi nhánh: làm ô chọn cho User chọn (gom dữ liệu lại giúp AI tìm kiếm phòng nhanh hơn), sau này có thêm chi nhánh thì sẽ tự thêm các ô chọn khác 
- 2 ô chọn: Thuê theo giờ, Thuê theo ngày. cả 2 có 
- Số người: cho User nhập nhưng phải là số. Do phòng ta có 2 cớ chế tiêu chuẩn và tối đa, nếu user đủ điều kiện tiêu chuẩn thì hiện 1 mức giá còn ở mức trên tiêu chuẩn dưới tối đa thì báo cho user biết là giá phụ thu thêm người

Và 1 option khác điền form là tư vấn
biến AI thành 2 chế độ: Hổ trợ đặt phòng và tư vấn

- Hổ trợ đặt phòng: sau khi đã có đủ thông tin từ form 
sẽ hiển thị
- List các phòng phù hợp:
<tên phòng 1>
    <giá theo giờ nếu tích đặt giờ>
    <giá theo ngày nếu tích đặt ngày>
    <nút xem chi tiết>
    <số lượng người chuẩn>
    <số lượng người tối đa> note kèm giá phụ thu
<tên phòng 2>
    <giá theo giờ nếu tích đặt giờ>
    <giá theo ngày nếu tích đặt ngày>
    <nút xem chi tiết>
    <số lượng người chuẩn>
    <số lượng người tối đa> note kèm giá phụ thu 
    <nút xem chi tiết>
có nút chọn phòng nếu user đồng ý thì hiện: bấm vào phòng đó là nhớ đã chọn phòng đó rồi, tiếp theo cần khách chọn time đặt
- một nút dẫn tới trang đó: http://localhost:5000/Rooms/Details/2
- Nếu tích đặt giờ thì trang đó ở tab đặt giờ 
- Nếu tích đặt ngày thì trang đó ở tab đặt ngày
Mục đích trang đó đã được thiết kế để xem khung giờ, ngày hợp lý hết rồi.
AI nói với khách là :có thể xem và đặt phòng ở ngay trên đó hoặc chốt phòng khung giờ rồi báo em nhé
Sau khi khách báo chốt giờ hoặc ngày vd 05:00 -> 07:00 hoặc 1-5/5
AI sẽ gửi 1 bảng tổng hợp lại kèm form đăng ký đặt phòng
trong form hiện:
- Tên khách
- Chi nhánh (hiển thị chi nhánh đã chọn ở trên)
- Thông tin phòng đã chọn
- Khung giờ/ngày đã chọn
- Số người (hiển thị thông tin phụ thu nếu có)
- Tổng tiền
- Nút gửi/đặt phòng
bấm nút đặt phòng sẽ ra bảng thiết kế tui cấu hình ở trang Final Response Synthesizer. sau khi bấm gửi thông tin sẽ ra trường 
