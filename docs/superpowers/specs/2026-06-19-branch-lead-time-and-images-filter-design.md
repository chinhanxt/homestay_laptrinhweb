# Thiết kế: Lead Time theo Giờ/Ngày cho Chi nhánh và Bộ lọc Chi nhánh cho Kho ảnh

Ngày: 2026-06-19
Trạng thái: Draft đã được người dùng duyệt bằng hội thoại, chờ user review file

## 1. Mục tiêu

Thực hiện hai thay đổi có liên quan đến vận hành theo chi nhánh:

1. Thêm dropdown lọc chi nhánh tại trang `Kho ảnh Đơn hàng` (`/chinhan/hethong/images`).
2. Mở rộng cấu hình chi nhánh từ lead time chỉ theo `giờ` sang một cấu hình thống nhất có thể chọn `giờ` hoặc `ngày`, và áp dụng xuyên suốt cho toàn bộ luồng khách, admin, và AI.

Mục tiêu nghiệp vụ là diễn đạt đúng khái niệm `đặt trước tối thiểu`.

## 2. Quy tắc nghiệp vụ đã chốt

### 2.1. Cấu hình lead time theo chi nhánh

Mỗi chi nhánh có một cấu hình thống nhất gồm:

- `BookingLeadTimeValue`: số lượng
- `BookingLeadTimeUnit`: đơn vị `Hours` hoặc `Days`

UI chỉ hiển thị một cụm nhập liệu gồm:

- một ô số
- một dropdown chọn `Giờ` hoặc `Ngày`

### 2.2. Ý nghĩa khi đơn vị là giờ

Nếu cấu hình là `N giờ`, thời điểm bắt đầu lưu trú phải lớn hơn hoặc bằng `thời điểm hiện tại + N giờ`.

Ví dụ:

- Bây giờ là `19/06/2026 10:00`
- Cấu hình `2 giờ`
- Slot bắt đầu trước `12:00` cùng ngày bị chặn

### 2.3. Ý nghĩa khi đơn vị là ngày

Nếu cấu hình là `N ngày`, booking theo ngày phải được đặt trước ít nhất `N ngày` theo nghiệp vụ người dùng đã chốt.

Ví dụ chuẩn:

- Hôm nay là `19/06/2026`
- Cấu hình `7 ngày`
- Các ngày `19/06/2026` đến `26/06/2026` bị chặn
- `27/06/2026` là ngày đầu tiên được phép đặt

Đây là quy tắc bắt buộc để tránh hiểu nhầm giữa:

- `được đặt trong tối đa N ngày tới`
- và `phải đặt trước ít nhất N ngày`

Hệ thống sẽ triển khai theo cách hiểu thứ hai.

### 2.4. Phạm vi áp dụng

Rule này áp dụng cho toàn bộ luồng có liên quan đến chọn ngày/giờ:

- khách web
- admin
- AI public booking
- AI/admin quick actions nếu có đề xuất hoặc dựng CTA đặt phòng

Frontend cần chặn sớm. Backend vẫn phải chặn cứng để bảo toàn tính đúng đắn.

## 3. Thiết kế dữ liệu

### 3.1. Entity `Branch`

Thay vì chỉ có `BookingLeadTimeHours`, branch sẽ dùng cặp cấu hình mới:

- `BookingLeadTimeValue`
- `BookingLeadTimeUnit`

Đề xuất:

- giữ tương thích dữ liệu bằng migration chuyển dữ liệu cũ sang:
  - `BookingLeadTimeValue = BookingLeadTimeHours`
  - `BookingLeadTimeUnit = Hours`
- có thể giữ cột cũ tạm thời trong giai đoạn migration nếu cần an toàn rollout, nhưng hướng đích là tất cả logic đọc qua cấu hình mới

### 3.2. Import/seed

Các nguồn dữ liệu cũ đang chỉ có một số lead time sẽ mặc định được hiểu là `giờ`.

Phần seed, import, và AI reseed text cần cập nhật để không còn hard-code chuỗi `X giờ` nếu chi nhánh chuyển sang `ngày`.

## 4. Thiết kế backend

### 4.1. Điểm resolve rule thống nhất

Tạo một điểm xử lý chung để các service không tự cộng giờ/ngày riêng lẻ. Điểm này nên trả về một model thống nhất, ví dụ:

- `Value`
- `Unit`
- `HourlyCutoffUtc`
- `EarliestAllowedDailyDate`

Trách nhiệm:

- đọc cấu hình branch
- fallback nếu dữ liệu trống hoặc không hợp lệ
- chuẩn hóa rule theo thời gian hệ thống hiện hành

### 4.2. Rule tính toán

Khi `Unit = Hours`:

- `HourlyCutoffUtc = now + N giờ`
- daily flow nếu có dùng cùng rule thời điểm, chỉ khi nghiệp vụ cần; mặc định thay đổi chính cần tập trung vào hourly slot

Khi `Unit = Days`:

- `EarliestAllowedDailyDate = today + (N + 1 ngày)` theo rule user đã chốt với ví dụ `19 -> 27` khi `N = 7`
- mọi ngày nhỏ hơn mốc này đều bị chặn

### 4.3. Các điểm cần dùng rule chung

Các vị trí đã xác định cần chuyển sang điểm resolve chung:

- `RoomBookingViewService`
  - lọc slot theo giờ
  - đánh dấu lịch ngày có cho phép đặt hay không
- `BookingCreationService`
  - chặn cứng lúc tạo booking theo giờ hoặc theo ngày
- `ContextAwareBookingConductor`
  - không show slot/ngày không hợp lệ
  - không auto-book vào ngày bị chặn
- `AdminChatQuickSendService`
  - không dựng CTA sai rule
- bất kỳ controller hoặc service admin nào tự tạo booking trực tiếp nếu có

### 4.4. Thông báo lỗi chuẩn hóa

Khi lead time theo ngày vi phạm:

- ví dụ: `Chi nhánh này yêu cầu đặt trước tối thiểu 7 ngày. Ngày gần nhất có thể đặt là 27/06/2026.`

Khi lead time theo giờ vi phạm:

- ví dụ: `Chi nhánh này yêu cầu đặt trước tối thiểu 2 giờ.`

Thông báo nên nhất quán giữa khách web, admin, và AI.

## 5. Thiết kế UI

### 5.1. Trang Cấu hình Chi nhánh

Trong mỗi card chi nhánh:

- đổi label từ `Số giờ đặt trước (Lead Time)` thành `Thời gian đặt trước tối thiểu`
- giữ một ô số
- thêm dropdown đơn vị `Giờ` / `Ngày`
- thêm helper text giải thích:
  - nếu chọn `Ngày`, ví dụ `7 ngày` nghĩa là hôm nay `19/06/2026` thì ngày đầu tiên khách được đặt là `27/06/2026`

Mục tiêu là tránh nhầm nghiệp vụ ngay trên UI.

### 5.2. Trang Kho ảnh Đơn hàng

Tại panel trái, khu vực filter sẽ có:

- ô tìm kiếm hiện có
- dropdown chi nhánh

Hành vi:

- `SuperAdmin` thấy `Tất cả chi nhánh` và toàn bộ danh sách chi nhánh
- user theo chi nhánh vẫn bị giới hạn dữ liệu theo session như hiện tại
- với user không có quyền xem đa chi nhánh, dropdown có thể bị ẩn hoặc khóa; backend mới là lớp kiểm soát chính

Filter branch phải kết hợp được với filter `search`.

## 6. Luồng trải nghiệm

### 6.1. Khách web

- Khi xem lịch ngày, các ngày chưa đủ lead time theo `Days` sẽ bị disabled hoặc coi là unavailable
- Khi xem slot theo giờ, các slot bắt đầu trước cutoff theo `Hours` sẽ bị ẩn hoặc disabled
- Nếu client vẫn submit thủ công một ngày/giờ không hợp lệ, backend trả lỗi rõ ràng

### 6.2. Admin

- Khi admin tạo hoặc chốt booking từ các màn có chọn ngày/giờ, cùng một rule được áp dụng
- Admin không được bypass âm thầm trừ khi hệ thống có yêu cầu nghiệp vụ riêng; trong scope hiện tại, admin dùng chung rule với khách và AI

### 6.3. AI

- AI conductor và quick-send không được đề xuất phòng/ngày/slot vi phạm lead time
- Nếu người dùng yêu cầu ngày bị chặn, AI phản hồi giải thích mốc ngày hợp lệ gần nhất thay vì tiếp tục flow sai

## 7. Kiểm thử

### 7.1. Test nghiệp vụ

Cần có test cho:

- branch cấu hình `Hours`
- branch cấu hình `Days`
- fallback dữ liệu cũ

Case bắt buộc:

- giả lập hôm nay là `19/06/2026`
- cấu hình `7 ngày`
- các ngày `19/06` đến `26/06` bị chặn
- `27/06/2026` hợp lệ

### 7.2. Test UI/backend filter ảnh

Cần có test hoặc verification cho:

- `SuperAdmin` lọc được theo chi nhánh
- user chi nhánh không nhìn thấy dữ liệu ngoài phạm vi session
- filter branch và search kết hợp đúng

### 7.3. Regression risk

Các rủi ro chính:

- chỉ sửa public flow nhưng sót admin/AI
- hiểu sai mốc ngày đầu tiên hợp lệ
- import/seed/AI text vẫn hiển thị `giờ` dù chi nhánh đã chuyển sang `ngày`
- UI chặn đúng nhưng backend chưa chặn cứng

## 8. Phạm vi không làm trong thay đổi này

Không mở rộng sang:

- max booking window theo ngày
- cấu hình lead time riêng cho từng loại booking trong cùng chi nhánh
- cơ chế override đặc biệt cho từng staff/admin

Nếu cần các khả năng đó, sẽ tách thành spec khác.

## 9. Kết quả mong muốn

Sau khi triển khai:

- admin cấu hình được lead time theo `giờ` hoặc `ngày` ngay tại từng chi nhánh
- rule `đặt trước tối thiểu` hoạt động đồng nhất trên khách, admin, và AI
- trang `Kho ảnh Đơn hàng` lọc được theo chi nhánh mà không làm lỏng quyền hiện có
