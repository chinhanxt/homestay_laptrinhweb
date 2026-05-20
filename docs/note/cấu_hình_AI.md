khi  vào khung chat có các ô chọn chi nhánh giúp AI khoanh vùng tìm kiếm phòng dễ dàng hơn. và ô chọn đặt ngày hay đặt giờ
nếu user hỏi còn phòng không thì AI sẽ dựa vào ô chi nhánh/type ngày/giờ đã chọn để trả lời. 
đưa list phòng:
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
    <nút xem chi tiết>.
và nút đi tới trang của phòng http://localhost:5000/Rooms/Details/2 để xem các khung giờ/ngày đang trống
nếu chọn chế độ ngày thì: ở trang details sẽ ở chế độ đặt ngày nếu chọn giờ thì: ở trang details sẽ ở chế độ đặt giờ.
nếu khách
nhờ khách báo lại timeline khung giờ hoặc ngày 
để AI tổng hợp lại để tui tạo đơn cho khách.Sau khi xong nhờ khách điền thôn tin đã làm ở trang Final Response Synthesizer