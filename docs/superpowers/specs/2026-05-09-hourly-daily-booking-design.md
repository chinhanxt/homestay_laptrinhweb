# Thiết kế: Đặt phòng theo giờ + theo ngày với engine chống trùng lịch thống nhất

Tài liệu này mô tả hướng thiết kế mới cho bài toán đặt phòng khi hệ thống cần hỗ trợ đồng thời:
- **Đặt theo giờ** với các combo như `2h`, `4h`, `6h`, `ban ngày`, `ban đêm`
- **Đặt theo ngày** theo chuẩn khách sạn với `check-in 14:00` và `check-out 12:00`

Mục tiêu là tách trải nghiệm người dùng thành 2 mode rõ ràng nhưng vẫn dùng chung một engine chống trùng lịch để tránh lệch logic giữa giao diện khách, admin, và backend.

## 1. Mục tiêu

- Hỗ trợ song song 2 hình thức đặt phòng: `theo giờ` và `theo ngày`
- Cho phép khách **chọn phòng trước**, sau đó vào một không gian đặt phòng riêng của phòng đó
- Giảm nhầm lẫn khi hiển thị lịch trống bằng cách tách `mode theo giờ` và `mode theo ngày`
- Đảm bảo mọi booking đều được kiểm tra trùng lịch bằng cùng một quy tắc overlap
- Cho phép admin cấu hình lịch giờ nhanh bằng template, nhưng vẫn override từng phòng khi cần

## 2. Quy tắc nghiệp vụ cốt lõi

### 2.1. Theo ngày
- `Check-in`: `14:00`
- `Check-out`: `12:00`
- Khoảng `12:00 -> 14:00` được xem là thời gian dọn dẹp cố định giữa 2 booking theo ngày
- Ở `mode theo ngày`, khách chọn `check-in` và `check-out` trực tiếp trên lịch
- Nếu một ngày nằm trong khoảng lưu trú dự kiến đã bị chiếm bởi booking giờ hoặc booking ngày khác, ô ngày đó phải **đỏ ngay** và không cho chọn

### 2.2. Theo giờ
- Mỗi combo giờ có timeline riêng
- Ví dụ với `2h` và `30 phút dọn dẹp`, hệ thống tự sinh:
  `00:00-02:00`, `02:30-04:30`, `05:00-07:00`... đến hết ngày
- Các combo `4h`, `6h`, `ban ngày`, `ban đêm` cũng được sinh thành các ca thực tế tương tự
- Các combo **được phép chồng nhau về mặt giờ trên lịch sinh**
- Khi một slot đã được đặt, mọi slot khác có giao thời gian với slot đó đều bị khóa

### 2.3. Rule chống trùng chung
- Mọi booking cuối cùng đều quy về một khoảng chiếm phòng thực tế gồm `StartTime` và `EndTime`
- Booking bị xem là trùng nếu:
  `existing.StartTime < candidate.EndTime && existing.EndTime > candidate.StartTime`
- Rule này phải áp dụng thống nhất cho:
  - booking giờ với booking giờ
  - booking ngày với booking ngày
  - booking giờ với booking ngày

## 3. Hướng kiến trúc được chọn

Chọn hướng **hybrid**:
- `Theo giờ` dùng **slot inventory** được sinh từ template/admin override
- `Theo ngày` không slot hóa toàn bộ, mà dùng lịch ngày + kiểm tra overlap với khoảng thời gian thực
- Cả hai mode đều tạo ra `Booking` với `StartTime` và `EndTime` thật

Lý do chọn hướng này:
- `Theo giờ` cần nhìn như suất chiếu/slot cụ thể, rất hợp với inventory
- `Theo ngày` là một khoảng lưu trú nhiều ngày, không phù hợp để nhét chung vào bảng slot giờ
- Tách UI nhưng giữ cùng engine overlap giúp giảm rủi ro lệch logic giữa frontend và backend

## 4. Thiết kế trải nghiệm khách hàng

### 4.1. Luồng chính
1. Khách chọn phòng trước
2. Vào trang đặt phòng riêng của phòng đó
3. Chọn một trong hai mode:
   - `Theo giờ`
   - `Theo ngày`
4. Chọn lịch phù hợp
5. Đi tới bước xác nhận booking chung

### 4.2. Mode theo giờ
- Khách **chọn ngày trước**
- Sau khi chọn ngày, hệ thống hiển thị **toàn bộ combo còn trống** trong ngày đó
- Có 2 cách sắp xếp hiển thị:
  - `Nhóm theo loại combo`
  - `Timeline từ sớm đến muộn`
- Mặc định nên hiển thị theo **loại combo**, nhưng cho phép chuyển sang **timeline**
- Sau khi chọn một slot, panel tóm tắt hiển thị:
  - tên phòng
  - ngày
  - tên combo
  - khung giờ

### 4.3. Mode theo ngày
- Khách chọn `check-in` và `check-out` theo giao diện lịch
- Giao diện phải hiển thị rõ quy tắc:
  - `Check-in 14:00`
  - `Check-out 12:00`
- Nếu có ngày bị xung đột vì booking giờ hoặc booking ngày khác, ngày đó phải báo đỏ ngay từ lúc chọn
- Không cho kéo chọn xuyên qua ngày đỏ

### 4.4. Ngôn ngữ trạng thái
- `Xanh`: còn trống
- `Vàng`: đang chọn
- `Đỏ`: không khả dụng
- `Xám`: không áp dụng / đã qua / bị tắt

## 5. Thiết kế admin

### 5.1. Template lịch giờ
Admin cần có khả năng tạo các template như:
- `Template giờ chuẩn`
- `Template cuối tuần`
- `Template combo qua đêm`

Mỗi template có thể chứa:
- tên combo
- loại combo
- thời lượng
- giờ bắt đầu seed
- thời gian dọn dẹp giữa 2 slot
- quy tắc sinh slot trong ngày

Ví dụ:
- `2h`: seed `00:00`, duration `120 phút`, cleanup `30 phút`
- Hệ thống tự sinh các slot liên tiếp đến hết ngày

### 5.2. Áp template cho phòng
- Admin có thể chọn nhiều phòng và áp template hàng loạt
- Đây là đường cấu hình nhanh để rollout toàn hệ thống

### 5.3. Override từng phòng
Sau khi áp template, admin vẫn có thể:
- tắt một combo ở phòng cụ thể
- sửa khung giờ cho phòng cụ thể
- block một số slot cụ thể theo ngày
- thêm combo riêng cho phòng đặc biệt

### 5.4. Khu quản trị đề xuất
- `Quản lý template`
- `Gán template cho phòng`
- `Lịch phòng / inventory thực tế`

## 6. Thiết kế dữ liệu

### 6.1. Booking
Giữ `Booking` là bản ghi trung tâm, nhưng bổ sung:
- `BookingMode`: `Hourly` hoặc `Daily`
- `BookingSource` hoặc `SlotInventoryId` nếu booking giờ gắn trực tiếp với slot inventory

`Booking` vẫn phải luôn lưu:
- `RoomId`
- `StartTime`
- `EndTime`
- `Status`
- `PaymentStatus`

### 6.2. RoomSlotTemplate
Định nghĩa combo gốc và quy tắc sinh slot:
- `Id`
- `Name`
- `Code`
- `Mode` hoặc `Category`
- `DurationMinutes`
- `CleanupMinutes`
- `SeedStartTime`
- `FixedStartTime` / `FixedEndTime` cho combo cố định như `ban ngày`, `ban đêm`
- `IsActive`

### 6.3. RoomSlotTemplateAssignment
Liên kết template với phòng:
- `RoomId`
- `TemplateId`
- `EffectiveFrom`
- `EffectiveTo`
- `IsActive`

### 6.4. RoomSlotInventory
Là slot thực tế đã được sinh cho từng phòng theo từng ngày:
- `Id`
- `RoomId`
- `TemplateId`
- `SlotDate`
- `SlotLabel`
- `StartTime`
- `EndTime`
- `Status`: `Available`, `Held`, `Booked`, `Blocked`
- `BookingId` nếu đã được chốt

### 6.5. RoomSlotOverride
Dùng cho ngoại lệ:
- `RoomId`
- `TargetDate`
- `TemplateId` hoặc `InventoryId`
- `OverrideType`: disable, reschedule, custom-add, block
- `Payload` hoặc các field cấu hình trực tiếp

## 7. Data flow và validate

### 7.1. Booking theo giờ
1. Khách chọn phòng
2. Chọn ngày
3. Hệ thống lấy inventory còn trống của ngày đó
4. Khách chọn slot
5. Backend kiểm tra overlap lại bằng interval thật trước khi tạo booking
6. Nếu hợp lệ, tạo booking giờ và cập nhật slot inventory thành `Booked` hoặc `Held` tùy lifecycle

### 7.2. Booking theo ngày
1. Khách chọn `check-in` và `check-out`
2. Hệ thống đổi thành interval thật:
   - `check-in 14:00`
   - `check-out 12:00`
3. Hệ thống kiểm tra overlap với mọi booking active và slot blocked liên quan
4. Nếu có xung đột, ngày liên quan báo đỏ và không được phép xác nhận

## 8. Xử lý lỗi và edge case

- Nếu slot giờ vừa bị người khác đặt:
  `Khung giờ này vừa được người khác đặt, vui lòng chọn khung khác.`
- Nếu khoảng ngày đi qua một ngày đã có booking giờ:
  `Ngày 10-05-2026 không còn trống vì đã có lịch đặt theo giờ.`
- Nếu admin vừa đổi cấu hình khiến inventory thay đổi:
  `Lịch phòng vừa được cập nhật, vui lòng tải lại để xem khung giờ mới.`
- Nếu booking `ban đêm` kéo qua ngày hôm sau:
  interval phải được lưu đúng sang ngày kế tiếp, không cắt tại 23:59

## 9. Kế hoạch kiểm thử

### 9.1. Unit test
- Sinh slot từ template `2h`, `4h`, `6h`
- Sinh slot với cleanup `30 phút`
- Tính interval thật của booking ngày `14:00 -> 12:00`
- Overlap giữa 2 interval bất kỳ

### 9.2. Service test
- `2h` bị khóa khi `6h` đã chiếm khung liên quan
- `ban đêm` ăn sang ngày hôm sau
- booking ngày bị chặn vì có slot giờ nằm giữa kỳ
- ngày đỏ không được chọn khi đã có xung đột

### 9.3. UI test
- Chuyển mode `theo giờ` / `theo ngày`
- Đổi sort `theo combo` / `theo timeline`
- Chọn ngày trước rồi xem toàn bộ combo còn trống
- Không cho chọn khoảng ngày xuyên qua ô đỏ

### 9.4. Regression test
- Flow booking hiện tại theo ngày không bị vỡ hoàn toàn trong giai đoạn chuyển đổi
- Các màn admin booking cũ vẫn đọc được booking mới

## 10. Rủi ro và phạm vi refactor

- `Booking` hiện tại đang phục vụ logic interval đơn giản, nên khi thêm mode giờ sẽ cần refactor service availability
- Giao diện hiện tại của `Rooms/Details`, `Bookings/Checkout`, `AdminMatrix`, và các màn admin booking sẽ bị ảnh hưởng đáng kể
- Không nên cố vá trực tiếp vào form cũ; nên thiết kế lại `không gian đặt phòng` theo 2 mode rõ ràng
- Cần một lớp migrate trung gian để dữ liệu cũ vẫn hoạt động khi tính năng mới đang rollout

### 10.1. Phạm vi triển khai nên chia pha
- `Pha 1`: refactor domain và availability engine để hỗ trợ `BookingMode` + overlap thống nhất
- `Pha 2`: xây `mode theo ngày` mới trên flow khách hiện tại
- `Pha 3`: thêm template, inventory, và `mode theo giờ`
- `Pha 4`: hoàn thiện admin cấu hình template, gán phòng, và override

Chia pha theo thứ tự này giúp giữ hệ thống luôn chạy được trong lúc chuyển đổi, đồng thời giảm rủi ro phải thay toàn bộ UI và dữ liệu trong một lần.

## 11. Kết luận

Hướng thiết kế phù hợp nhất cho dự án là:
- Tách **2 mode UI**: `theo giờ` và `theo ngày`
- Dùng **slot inventory** cho `mode theo giờ`
- Dùng **calendar interval** cho `mode theo ngày`
- Hợp nhất tất cả về một **engine overlap chung** dựa trên `StartTime` và `EndTime`

Thiết kế này cân bằng được ba mục tiêu:
- khách dễ hiểu
- admin dễ cấu hình
- backend dễ kiểm soát trùng lịch
