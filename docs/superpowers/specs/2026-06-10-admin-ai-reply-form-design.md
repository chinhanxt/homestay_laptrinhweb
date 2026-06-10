# Admin AI Reply & Form Redesign

## Mục tiêu

Thiết kế lại tab `Trả lời & Form` tại `/admin/ai` để phục vụ đúng vai trò AI tư vấn và hỗ trợ khách đặt phòng như một nhân viên sale. Phạm vi này tập trung vào:

- Cấu hình câu trả lời và hành vi hội thoại của AI.
- Cấu hình các field và block form AI dùng để thu thập thông tin.
- Runtime contract để chatbot UI sau này chỉ cần render theo chỉ thị từ backend.
- Simulator nội bộ để admin test luồng, form, block, CTA mà không gọi LLM và không tốn token.

Ngoài phạm vi:

- Thiết kế lại chatbot UI public hoàn chỉnh.
- Cấu hình thanh toán hoặc QR trong tab này.
- Biến studio thành một no-code builder kéo thả hoàn chỉnh.

## Bối cảnh và vấn đề hiện tại

Tab `Trả lời & Form` hiện tại đang dồn nhiều nhóm cấu hình rời rạc:

- `Hiển thị phòng`
- `Hiển thị khung giờ`
- `Hiển thị ngày`
- `Checklist thu thập thông tin`
- `Khối form tương tác`
- `Thanh toán / QR`
- `Điều kiện kích hoạt block`

Các vấn đề chính:

1. Dữ liệu cấu hình bị chồng nhau giữa checklist, block, display rule, trigger condition.
2. Admin phải sửa JSON thô ở các khu vực quan trọng, khó dùng và dễ sai.
3. Cấu trúc hiện tại nghĩ theo danh sách block rời thay vì nghĩ theo flow tư vấn bán phòng.
4. `Thanh toán / QR` không còn phù hợp với luồng mới vì AI không thanh toán và không chốt booking.
5. Preview hiện tại là preview tĩnh, không giúp test runtime logic theo từng trường hợp.

## Nguyên tắc thiết kế

1. AI hoạt động theo mô hình `sale-assist`, không theo wizard cứng.
2. Có 2 flow chính vào sớm:
   - `Theo giờ`
   - `Theo ngày`
3. AI vẫn trả lời tự nhiên bằng text, nhưng ở các điểm cần chọn nhanh phải show block phù hợp.
4. Hệ thống có rule lõi an toàn, nhưng admin vẫn override được điều kiện hỏi field và show block.
5. Studio phải cho admin hiểu “AI sẽ hỏi gì, hiện gì, chốt sang trang nào” mà không cần nhìn JSON.
6. Runtime phải tách được:
   - `Rule engine local` để quyết định state và UI directive
   - `LLM layer` chỉ để viết câu văn tự nhiên
7. Chatbot không xác nhận booking, không thanh toán, không giữ phòng.

## Luồng nghiệp vụ đã chốt

### Vai trò AI

AI có nhiệm vụ trả lời tư vấn, hỗ trợ khách tìm phòng, hỏi các thông tin cần thiết, hiển thị các lựa chọn phù hợp, rồi dẫn khách sang đúng trang chi tiết hoặc trang đặt phòng chính thức đã được prefill theo thông tin khách đã cung cấp.

### Điểm vào flow

- AI hỏi bằng text.
- Đồng thời show 2 lựa chọn lớn:
  - `Đặt theo giờ`
  - `Đặt theo ngày`

### Cách thu thập thông tin

- Không đi theo thứ tự cứng.
- AI bám nhu cầu của khách như một nhân viên sale.
- Field nào còn thiếu và đã đến lúc hỏi thì mới hỏi.
- Admin được cấu hình điều kiện kích hoạt cho field và block.
- Có rule lõi mặc định của hệ thống để tránh flow sai.

### Field model

- Hệ thống seed sẵn field chuẩn.
- Admin được thêm, sửa, xóa field.
- Mỗi field có thể là field hệ thống hoặc field mở rộng.
- Mỗi field có thể hiển thị kiểu:
  - `single-question`
  - `profile-form`

### Hành vi đặt theo giờ

- Khi cần biết ngày hoặc giờ, AI phải show form chọn thay vì bắt khách gõ tay nếu có thể.
- Khi đã đủ dữ liệu phù hợp, AI show các ô khung giờ trống để khách chọn.

### Hành vi đặt theo ngày

- Khi khách đi theo ngày, AI cần thu thập và hiển thị cặp ngày `check-in` và `check-out`.
- Có thể hiển thị chung trong một profile-form nếu cấu hình block yêu cầu.

### Tư vấn phòng

- AI show danh sách phòng theo yêu cầu khách.
- Mỗi phòng có thể bấm để đi tới trang chi tiết phòng.
- Khi bấm sang trang chi tiết, session chatbot phải có contract để khôi phục trạng thái sau này.

### Chốt luồng

- Khi khách chọn được phòng, AI không thanh toán.
- AI hiển thị nút đi đến đúng trang đặt phòng chính thức.
- Trang đó phải nhận sẵn các dữ liệu ngày, giờ, chi nhánh hoặc phòng mà khách đã chọn.

### Handoff người thật

Khi AI gặp trường hợp khó, ngoài phạm vi, ngoại lệ, hoặc khách yêu cầu gặp người:

- Nếu đã biết chi nhánh:
  - show form xin số điện thoại khách
  - show số điện thoại chi nhánh đó
- Nếu chưa biết chi nhánh:
  - show form xin số điện thoại khách
  - show danh sách số điện thoại tất cả chi nhánh

Handoff được kích hoạt bởi:

- Trigger lõi hệ thống
- Trigger do admin cấu hình thêm

## Cấu trúc mới của tab `Trả lời & Form`

Tab này vẫn giữ mô hình 3 cột, nhưng đổi hoàn toàn ý nghĩa.

### Cột trái: Nhóm cấu hình

Chỉ còn 6 nhóm:

1. `Phong cách trả lời`
2. `Flow hội thoại`
3. `Kho field`
4. `Khối giao diện`
5. `Quy tắc hiển thị & chốt`
6. `Handoff người thật`

### Cột giữa: Trình chỉnh cấu hình

Đây là vùng thao tác chính của admin. Không còn textarea JSON trên màn hình chính.

### Cột phải: Runtime Simulator

Thay `Preview hội thoại` hiện tại bằng một simulator chạy local, không gọi LLM.

## Những gì phải xóa khỏi màn hiện tại

Các category cũ cần bỏ khỏi tab này:

- `Checklist thu thập thông tin`
- `Khối form tương tác`
- `Điều kiện kích hoạt block`
- `Hiển thị ngày`
- `Thanh toán / QR`

Các hành vi và thành phần cần bỏ:

- Preview giả lập QR
- `paymentQrRules` trong config mới của studio
- Block `paymentQr` trong sale-assist runtime contract mới
- Các textarea JSON thô cho checklist và interaction block trên giao diện admin

## Những gì phải thêm

### 1. Phong cách trả lời

Nội dung cấu hình nên gồm:

- Vai trò AI
- Giọng điệu
- Nguyên tắc trả lời ngắn gọn
- Cách xin thêm thông tin còn thiếu
- Điều AI không được làm

Mục này gom các rule prompt và tone theo dạng đọc được, không tách quá vụn.

### 2. Flow hội thoại

Mỗi flow là một card cấu hình rõ ràng:

- `Theo giờ`
- `Theo ngày`
- `Chuyển người thật`

Mỗi flow gồm:

- Tên flow
- Mô tả mục tiêu
- Điều kiện vào flow
- Field ưu tiên
- Điều kiện đủ để show phòng
- Điều kiện đủ để show giờ trống
- Điều kiện đủ để show CTA sang trang đặt
- Thứ tự ưu tiên block nên hiện

Flow không mô tả wizard cứng. Nó mô tả logic sale-assist và ngưỡng ra quyết định.

### 3. Kho field

Đây là phần thay thế cho checklist hiện tại.

Mỗi field có:

- `id`
- `key`
- `label`
- `inputType`
- `sourceType`
- `isSystem`
- `isRequired`
- `flowScopes`
- `captureMode`
- `questionTemplate`
- `helpText`
- `triggerRules`
- `validationRules`
- `sortOrder`

Seed sẵn các field chuẩn:

- `bookingMode`
- `branchId`
- `customerPhone`
- `hourlyDate`
- `hourlySlot`
- `checkInDate`
- `checkOutDate`
- `guestCount`
- `roomId`
- `customerNote`

Admin có thể:

- thêm field
- sửa field
- xóa field
- sắp xếp field
- nhân bản field

### 4. Khối giao diện

Đây là phần thay thế cho interaction blocks kiểu cũ.

Seed sẵn các block chính:

- `bookingModeChoice`
- `branchSelector`
- `phoneInput`
- `singleDatePicker`
- `timeSlotGrid`
- `dateRangePicker`
- `roomCards`
- `profileForm`
- `handoffContact`
- `bookingCta`

Mỗi block có:

- `id`
- `type`
- `label`
- `fieldKeys`
- `layoutMode`
- `submitBehavior`
- `messageTemplate`
- `visibilityRules`
- `styleVariant`

Mục tiêu là để chatbot UI sau này chỉ cần render block dựa trên `uiDirectives`.

### 5. Quy tắc hiển thị & chốt

Nhóm này gom toàn bộ quyết định runtime đang bị chia tách ở nhiều mục cũ.

Nội dung chính:

- Khi nào chỉ trả lời bằng text
- Khi nào show single-question
- Khi nào show profile-form
- Khi nào show giờ trống
- Khi nào show phòng
- Khi nào hiện CTA sang trang đặt
- Khi nào dừng hỏi thêm
- Khi nào chuyển sang handoff

Các rule phải được hiển thị theo dạng điều kiện đọc được, không ép admin sửa JSON.

### 6. Handoff người thật

Mục riêng để tránh trộn với flow thường.

Nội dung:

- Trigger lõi hệ thống
- Trigger do admin thêm
- Message handoff
- Rule hiển thị số chi nhánh
- Rule bắt buộc xin số điện thoại khách
- Cách chọn nguồn số điện thoại:
  - theo branch đã biết
  - hoặc danh sách toàn bộ branch nếu chưa biết

## Runtime Simulator không tốn token

### Mục tiêu

Admin phải test được đủ các case show form, show block, show CTA, show handoff mà không cần gọi mô hình thật.

### Nguyên tắc

- Simulator chỉ chạy `rule engine local`.
- Không gọi LLM.
- Không tốn token.
- Câu AI hiển thị trong simulator dùng template hoặc message fallback.

### Chức năng

#### Preset tình huống

Seed sẵn các case:

- `Khách mới mở chat`
- `Khách chọn đặt theo giờ`
- `Khách chọn đặt theo ngày`
- `Đã biết chi nhánh, thiếu ngày`
- `Đã biết ngày, thiếu giờ`
- `Đủ điều kiện show giờ trống`
- `Đủ điều kiện show phòng`
- `Khách đòi gặp người thật`
- `Khách hỏi ngoài khả năng`

#### State editor

Cho admin chỉnh state hiện tại:

- flow hiện tại
- field đã có
- field còn thiếu
- branch đã chọn
- ngày/giờ đã chọn
- số khách
- phòng đã chọn
- phone đã có
- cờ handoff
- message cuối của khách

#### Action buttons

Cho admin mô phỏng nhanh các thao tác:

- chọn `Đặt theo giờ`
- chọn `Đặt theo ngày`
- chọn chi nhánh
- chọn ngày
- chọn giờ
- chọn phòng
- bấm đi tới trang chi tiết
- bật handoff

#### Kết quả mô phỏng

Simulator hiển thị:

- flow hiện tại
- conversation state
- assistant reply template
- `uiDirectives` cần render
- `nextActions`
- CTA nếu có

### Hai chế độ test

1. `Mô phỏng miễn phí`
   - mặc định
   - không token
2. `Test với AI thật`
   - tùy chọn
   - gọi LLM
   - chỉ dùng khi cần kiểm tra văn phong thật

## Cấu trúc dữ liệu mới

Config mới nên xoay quanh 5 thực thể chính.

### 1. AssistantProfile

Chứa:

- vai trò AI
- tone
- rule an toàn
- nguyên tắc sale-assist
- nguyên tắc không thanh toán, không xác nhận booking

### 2. ConversationFlows

Danh sách flow:

- `hourly`
- `daily`
- `handoff`

Mỗi flow có:

- `id`
- `label`
- `entryConditions`
- `activeWhen`
- `completionRules`
- `priorityFields`
- `nextActionRules`

### 3. FieldDefinitions

Danh sách field seed sẵn và field custom.

Các thuộc tính chính:

- `id`
- `key`
- `label`
- `inputType`
- `sourceType`
- `isSystem`
- `isRequired`
- `flowScopes`
- `captureMode`
- `questionTemplate`
- `helpText`
- `triggerRules`
- `validationRules`
- `sortOrder`

### 4. UiBlockDefinitions

Danh sách block giao diện tái sử dụng.

Các thuộc tính chính:

- `id`
- `type`
- `label`
- `fieldKeys`
- `layoutMode`
- `submitBehavior`
- `messageTemplate`
- `visibilityRules`
- `styleVariant`

### 5. RuntimePolicies

Chứa các chính sách quyết định runtime:

- `showRoomRules`
- `showSlotRules`
- `showBookingCtaRules`
- `handoffRules`
- `fallbackReplyRules`
- `sessionPersistenceRules`

## Runtime contract cho simulator và chatbot UI

Simulator admin và chatbot UI public sau này phải dùng chung một contract runtime.

### conversationState

Chứa:

- flow hiện tại
- field đã thu thập
- field còn thiếu
- branch/phòng/ngày/giờ đã chọn
- cờ handoff
- session persistence key

### assistantReply

Chứa nội dung text AI sẽ hiển thị.

- Trong simulator miễn phí: dùng template fallback.
- Trong AI thật: dùng LLM viết lại theo tone.

### uiDirectives

Danh sách block cần render ở bước hiện tại, ví dụ:

- `bookingModeChoice`
- `branchSelector`
- `profileForm`
- `timeSlotGrid`
- `roomCards`
- `handoffContact`
- `bookingCta`

### nextActions

Chứa quyết định kế tiếp:

- field nào cần hỏi tiếp
- có đủ điều kiện show phòng chưa
- có đủ điều kiện show giờ trống chưa
- có nên handoff không
- có được hiện CTA sang trang đặt phòng không

## Hành vi điều hướng và giữ trạng thái chatbot

Khi khách bấm vào phòng để sang trang chi tiết:

- UI có thể mở cùng tab hoặc tab mới.
- Runtime contract phải luôn hỗ trợ khôi phục state đầy đủ.

State tối thiểu cần lưu:

- lịch sử tóm tắt hội thoại
- flow hiện tại
- selected branch
- selected room
- selected slot hoặc date range
- guest count
- customer phone nếu có

Mục tiêu là không để chatbot “mất trí nhớ” khi người dùng xem phòng rồi quay lại.

## Migration từ config cũ sang config mới

### Giữ lại

- `responseStyle`
- `safetyRules`
- `memoryRules`
- một phần field/block cơ bản nếu map được rõ ràng

### Bỏ hẳn

- `paymentQrRules`
- `dateDisplayRules`
- cấu trúc tách rời cũ giữa `ChecklistFields`, `InteractionBlocks`, `TriggerConditions` nếu đã được hấp thụ vào model mới

### Cách migrate

1. Đọc `AIStudioConfig` cũ.
2. Tạo config mới.
3. Map các phần còn giá trị.
4. Seed flow, field, block mặc định còn thiếu.
5. Nếu config cũ lỗi hoặc quá thiếu, fallback về default config mới.

## Testing

### 1. Migration tests

- Config cũ map được sang config mới.
- Config rỗng hoặc lỗi fallback về default config mới.
- `paymentQrRules` không còn xuất hiện trong config mới.

### 2. Rule engine local tests

- Khách mới vào chat thì hiện `bookingModeChoice`.
- Flow `theo giờ` thiếu branch thì hiện branch selector.
- Đủ dữ liệu thì hiện slot grid hoặc room cards đúng ngữ cảnh.
- Chọn phòng xong thì hiện booking CTA.
- Handoff thì hiện form xin số điện thoại và số branch đúng trường hợp.

### 3. Simulator tests

- Preset nạp đúng state.
- Action button cập nhật state đúng.
- Simulator không gọi LLM.
- `uiDirectives` hiển thị đúng với state đã dựng.

### 4. Safety regression tests

- Sale-assist runtime mới không trả `paymentQr`.
- AI không xác nhận booking.
- AI không tự giữ phòng.
- AI không bịa dữ liệu availability hoặc giá.

## Rủi ro và cách kiểm soát

### Rủi ro

1. Config cũ có thể bẩn hoặc thiếu cấu trúc.
2. Runtime conductor hiện tại đang gắn với block cũ.
3. JS admin hiện tại render theo category text và JSON editor, nên refactor phải đi cùng service/model.

### Kiểm soát

1. Tách local rule engine trước khi nối simulator.
2. Giữ backward compatibility ở backend trong giai đoạn chuyển đổi nếu cần, nhưng không còn expose UI cũ.
3. Dùng default config mới làm baseline an toàn khi migration thất bại.

## Phạm vi triển khai khuyến nghị

1. Đổi model config và service migration.
2. Làm lại tab `Trả lời & Form`.
3. Tách `rule engine local`.
4. Gắn `Runtime Simulator`.
5. Chốt runtime contract cho chatbot UI sau này.

## Kết quả mong muốn

Sau redesign:

- Admin hiểu rõ AI đang được cấu hình theo flow nào.
- Admin không cần sửa JSON để cấu hình các hành vi phổ biến.
- Các cấu hình dư thừa bị loại bỏ.
- Có simulator test đủ case mà không tốn token.
- Runtime contract rõ ràng cho chatbot UI public ở giai đoạn sau.
