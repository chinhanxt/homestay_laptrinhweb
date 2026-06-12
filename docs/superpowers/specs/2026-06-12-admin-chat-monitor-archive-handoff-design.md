# Admin Chat Monitor Archive And Handoff Design

## Mục tiêu

Nâng cấp màn `Giám sát Chatbot` để:

- Lưu toàn bộ hội thoại khách, AI, nhân viên, hệ thống vào cùng một timeline có thể xem lại.
- Cho phép xóa mềm từng phiên chat trong monitor, có thể khôi phục hoặc xóa vĩnh viễn.
- Đồng bộ tin nhắn nhân viên và block gửi nhanh sang widget khách theo thời gian thực.
- Biến `Gửi nhanh` thành form điều kiện nội bộ cho nhân viên, không để AI hỏi thay.
- Bắt buộc tiếp quản thủ công trước khi nhân viên nhắn hoặc gửi nhanh để tránh xung đột với AI.

## Hiện trạng cần sửa

- Monitor đang đọc `AdminChatMessages`, nhưng còn logic hiển thị tách biệt với trace AI nên khó đảm bảo timeline là nguồn sự thật duy nhất.
- Nhân viên gửi tin nhắn hoặc gửi nhanh chưa có luồng delivery thống nhất nên phía khách có thể không nhận được.
- `Gửi nhanh` đang phát block quá sớm, chưa có form điều kiện để nhân viên nhập đủ dữ liệu trước khi gửi.
- Phiên chat chưa có vòng đời archive/restore/purge theo từng phiên.
- Nhân viên có thể can thiệp khi AI vẫn đang chạy, dễ tạo phản hồi chồng chéo.

## Phạm vi

Trong phạm vi:

- `AdminChatSession` và `AdminChatMessage` cho archive, restore, purge, takeover, delivery state.
- `AdminChatMonitorController`, `IAdminChatService`, `ChatHub`, `admin-chat-monitor.js`, `site.js`.
- UI monitor để xem đầy đủ chat, xóa phiên, khôi phục phiên, xác nhận tiếp quản, gửi nhanh có form điều kiện.
- Public widget để nhận và render tin nhắn staff cùng `uiBlocks` từ monitor.

Ngoài phạm vi:

- Không đổi kiến trúc multi-agent công khai ngoài việc tạm dừng phản hồi khi staff takeover.
- Không thêm hệ quản trị phân quyền mới; tái dùng quyền monitor hiện có.
- Không thay đổi schema business của booking ngoài dữ liệu cần để compose quick-send block.

## Thiết kế dữ liệu

### AdminChatSession

Thêm các trường:

- `IsDeleted`
- `DeletedAt`
- `DeletedBy`
- `PauseReason`
- `TakenOverAt`
- `TakenOverBy`

Quy ước:

- `IsDeleted = true` là xóa mềm, không hiện ở danh sách mặc định.
- `TakenOverBy` và `PauseReason = "manual_handoff"` cho biết AI đã bị dừng để nhân viên tiếp quản.

### AdminChatMessage

Bảo đảm mỗi tin trong timeline đều được lưu thành message thật với:

- `Role`: `user`, `ai`, `admin`, `system`
- `Content`
- `CreatedAt`
- `CreatedBy`
- `FormBlockType`
- `UiBlocksJson`
- `DeliveryStatus`

Quy ước:

- Tin staff text thuần: `Role = admin`, không cần `UiBlocksJson`.
- Tin gửi nhanh: `Role = admin`, có `Content` mô tả và `UiBlocksJson` là payload để widget khách render.
- Sự kiện takeover/archive/restore/purge có thể lưu `Role = system` để audit và hiển thị rõ trong timeline khi cần.

## Kiến trúc backend

### 1. Session lifecycle service

Tách logic vòng đời phiên chat thành service riêng:

- `SoftDeleteSessionAsync`
- `RestoreSessionAsync`
- `PermanentlyDeleteSessionAsync`
- `TakeoverSessionAsync`

Trách nhiệm:

- Cập nhật session state.
- Ẩn/hiện phiên trong query monitor.
- Gửi `sessionUpdate` realtime cho admin monitor khi trạng thái đổi.

### 2. Message delivery service

Tạo một đường gửi thống nhất cho staff/manual delivery:

- `SendStaffMessageAsync`
- `SendStaffUiBlockAsync`

Trách nhiệm:

- Validate phiên có tồn tại và chưa bị xóa mềm.
- Bảo đảm phiên đã `paused/manual handoff`.
- Lưu `AdminChatMessage`.
- Đẩy SignalR vào group session khách.
- Trả payload đầy đủ để monitor append ngay mà không cần dựng giả cục bộ.

### 3. Quick-send composer service

Tách phần dựng block gửi nhanh thành service có hai lớp:

- `GetQuickSendSchema(type, sessionId)`
- `BuildQuickSendPayload(type, formData, sessionId)`

Mỗi loại `Gửi nhanh` có schema điều kiện riêng:

- `roomSelector`
  - `branchId`
  - `bookingMode`
  - `hourlyDate` hoặc `checkInDate/checkOutDate`
  - `requestedTimeStart/requestedTimeEnd` nếu theo giờ
  - `guestCount`
- `slotPicker`
  - `branchId`
  - `roomId`
  - `hourlyDate`
  - `guestCount`
- `infoForm`
  - loại form hoặc field set tương ứng
- `bookingCta`
  - room/slot hoặc room/date-range đã đủ để dựng CTA đúng
- `handoffContact`
  - branchId hoặc contact target tối thiểu

Backend validate lại toàn bộ dữ liệu trước khi dựng block để không gửi nhầm form rỗng, sai mode, sai chi nhánh.

### 4. Session detail query

`GET /admin/chat-monitor/session/{sessionId}` sẽ trả về timeline hợp nhất từ `AdminChatMessages` làm nguồn sự thật chính.

Không render trùng từ `AIConversationTraces`.

Trace AI nếu cần debug vẫn giữ ở route/debug riêng, không chen vào timeline monitor chính.

## Kiến trúc frontend monitor

### 1. Timeline chat đầy đủ

`admin-chat-monitor.js` sẽ render tất cả message theo thứ tự thời gian:

- Khách
- AI
- Nhân viên
- Hệ thống

Nếu message có `FormBlockType`, bubble staff vẫn hiện text bình thường và badge loại block.

Preview ở session card dùng `lastMessage` thật của phiên thay vì suy luận từ nhiều nguồn.

### 2. Xóa mềm, khôi phục, xóa vĩnh viễn

UI monitor thêm:

- action `Xóa` trong phiên đang mở hoặc card phiên
- filter/tab `Đã xóa`
- action `Khôi phục`
- action `Xóa vĩnh viễn`

Luồng:

1. `Xóa` mở confirm mềm.
2. `Khôi phục` đưa phiên về list chính với timeline cũ nguyên vẹn.
3. `Xóa vĩnh viễn` mở confirm mạnh hơn và xóa sạch dữ liệu monitor của phiên.

### 3. Tiếp quản thủ công

Khi nhân viên bấm gửi tin nhắn hoặc gửi nhanh mà AI vẫn đang hoạt động:

1. Hiện modal xác nhận: `Sẽ tạm dừng AI để nhân viên tiếp quản`.
2. Nếu xác nhận, gọi endpoint takeover.
3. Sau takeover thành công mới mở form gửi nhanh hoặc thực hiện gửi tay.

Sau takeover:

- Header monitor đổi sang trạng thái AI paused.
- Widget khách không nhận thêm câu trả lời AI tự động cho phiên đó.
- Không cần AI trả lời một câu riêng; hệ thống chỉ đổi trạng thái và dừng bot.

### 4. Gửi nhanh bằng form điều kiện nội bộ

Mỗi item `Gửi nhanh` sẽ mở modal/panel form thu gọn.

Form chỉ phục vụ nhân viên, không gửi block hỏi điều kiện cho khách.

Ví dụ:

- `Gửi danh sách phòng`: chọn chi nhánh, mode, ngày, giờ, số khách rồi mới bấm gửi.
- `Gửi khung giờ`: chọn phòng, ngày, số khách.
- `Gửi CTA đặt phòng`: chọn ngữ cảnh booking đủ dữ liệu để dựng link hoặc summary đúng.

Chỉ khi form hợp lệ mới gọi backend compose và gửi cho khách.

## Public widget

`site.js` cần hỗ trợ:

- nhận realtime staff text và `uiBlocks` từ SignalR bằng cùng cấu trúc payload monitor gửi
- append ngay vào khung chat
- lưu local session state để không mất hiển thị tạm thời
- có API hydrate lịch sử từ DB khi reload trang để không phụ thuộc hoàn toàn vào `sessionStorage`

Hydrate ưu tiên lấy từ server, `sessionStorage` chỉ là cache hiển thị ngắn hạn.

## API đề xuất

- `GET /admin/chat-monitor/sessions?scope=active|deleted`
- `GET /admin/chat-monitor/session/{sessionId}`
- `POST /admin/chat-monitor/session/{sessionId}/takeover`
- `POST /admin/chat-monitor/session/{sessionId}/reply`
- `GET /admin/chat-monitor/session/{sessionId}/quick-send/{type}/schema`
- `POST /admin/chat-monitor/session/{sessionId}/quick-send/{type}`
- `POST /admin/chat-monitor/session/{sessionId}/delete`
- `POST /admin/chat-monitor/session/{sessionId}/restore`
- `DELETE /admin/chat-monitor/session/{sessionId}`
- `GET /ai/chat-history/{sessionId}`

Các route cũ như `quick-block` có thể được giữ tạm để tương thích, nhưng UI mới sẽ chuyển sang schema + submit payload thay vì lấy block sẵn rồi bắn ngay.

## Điều kiện và validation

- Staff không được gửi tay hoặc gửi nhanh nếu chưa takeover.
- Session đã xóa mềm không được gửi tin.
- Session đã xóa vĩnh viễn không thể khôi phục.
- Quick-send chỉ gửi được khi đủ trường bắt buộc cho block tương ứng.
- Payload block phải được backend dựng từ dữ liệu đã validate, không tin hoàn toàn dữ liệu client.

## Xử lý lỗi

- Takeover thất bại: giữ nguyên modal, hiện lỗi, chưa gửi tin.
- Compose quick-send thất bại: không append bubble giả; chỉ hiện lỗi cho nhân viên.
- Gửi realtime thất bại sau khi lưu DB: đánh dấu `DeliveryStatus = Failed` để monitor biết cần gửi lại.
- Widget khách không kết nối SignalR: khi reload sẽ hydrate lại lịch sử từ server.

## Testing

### Backend

- test soft delete / restore / purge theo session
- test session deleted không xuất hiện trong list active
- test takeover là điều kiện bắt buộc trước khi staff send
- test quick-send validation theo từng loại block
- test delivery service lưu message và tạo payload đúng

### Frontend

- monitor render đủ `user/ai/admin/system`
- gửi tay khi AI đang chạy phải mở confirm takeover
- quick-send mở form điều kiện thay vì gửi ngay
- sau gửi thành công, khách thấy text + block tương ứng
- tab `Đã xóa` có thể khôi phục và xóa vĩnh viễn

## Triển khai theo lát cắt

1. Chuẩn hóa dữ liệu session/message cho archive và delivery state.
2. Hợp nhất timeline monitor chỉ dùng `AdminChatMessages`.
3. Tạo takeover + unified delivery cho staff text.
4. Đồng bộ widget khách nhận message staff.
5. Nâng cấp quick-send thành schema form + composer backend.
6. Thêm soft delete / restore / purge UI và filter monitor.

## Rủi ro chính

- `admin-chat-monitor.js` hiện đang ôm quá nhiều trách nhiệm; khi triển khai nên tách module theo `sessions`, `timeline`, `quick-send`, `cancellations`.
- Nếu giữ cả trace AI và chat message trong cùng màn hình sẽ dễ lặp dữ liệu; monitor chính phải chỉ có một nguồn sự thật.
- Xóa vĩnh viễn cần rõ phạm vi chỉ áp vào dữ liệu monitor/chat session, không vô tình xóa booking business.
