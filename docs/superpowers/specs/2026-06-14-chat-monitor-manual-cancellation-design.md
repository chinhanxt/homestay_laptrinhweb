# Chat Monitor Manual Cancellation Design

## Mục tiêu

Thêm chức năng **Hủy thủ công (Zalo)** vào trang chat monitor (`/chinhan/hethong/chat-monitor`) cho phép nhân viên tạo đơn hủy booking khi khách nhắn tin hủy qua Zalo (không qua AI Chat).

## Luồng xử lý

```
Khách nhắn Zalo hủy phòng
        │
        ▼
Nhân viên bấm [Hủy thủ công (Zalo)] trên sidebar
        │
        ▼
Modal #manual-cancellation-modal hiện ra
  ├── Nhập mã booking
  ├── [Tra cứu] → hiển thị thông tin khách từ booking
  ├── Upload ảnh chụp Zalo (1 ảnh, PNG/JPG, max 5MB)
  └── [Tạo đơn hủy] → POST /cancellations/create-manual
        │
        ▼
Tạo BookingCancellationRequest (Status=Pending, IsManual=true)
        │
        ▼
Mở modal duyệt hủy hiện tại (#cancellation-modal)
  → Nhân viên nhập lý do, % hoàn tiền, bill → chấp nhận/từ chối
  → Gửi email thông báo (giống luồng hiện tại)
```

## Thay đổi cụ thể

### 1. UI — Sidebar (Index.cshtml)

Thêm nút sau 3 nút lọc cancellation:

```html
<button class="btn btn-sm btn-outline-warning w-100 mt-1"
        id="btn-manual-cancellation">
  🧾 Hủy thủ công (Zalo)
</button>
```

### 2. UI — Modal mới (#manual-cancellation-modal)

Modal nhập liệu với các trường:
- **Mã đặt phòng** — text input, required
- **[Tra cứu thông tin]** — button, gọi API lookup
- **Preview thông tin khách** — div ẩn hiện sau tra cứu (tên, SĐT, email, phòng, check-in/out)
- **Ảnh chụp Zalo** — file input, accept="image/png,image/jpeg,image/gif,image/webp"
- Tên file đã chọn + icon xoá
- Nút [Hủy] + [Tạo đơn hủy]

### 3. Model — BookingCancellationRequest

Thêm field:

```csharp
public bool IsManual { get; set; } = false;
```

Column `is_manual` trong DB.

### 4. API Endpoints mới (AdminChatMonitorController)

#### a) `POST /chinhan/hethong/chat-monitor/cancellations/lookup-booking`

**Input:** `{ bookingCode: string }`

**Logic:**
- Tìm booking theo code (chính xác, không dùng `Contains`)
- Nếu không tìm thấy → 404 `{ error: "Không tìm thấy booking với mã này" }`
- Nếu booking đã bị huỷ → 400 `{ error: "Booking này đã bị huỷ trước đó" }`
- Nếu tìm thấy → 200 `{ bookingId, customerName, customerPhone, customerEmail, roomName, checkIn, checkOut, status }`

#### b) `POST /chinhan/hethong/chat-monitor/cancellations/create-manual`

**Input:** multipart form với:
- `bookingCode` (string)
- `image` (IFormFile, 1 file)

**Logic:**
- Validate booking tồn tại, chưa bị huỷ
- Validate file ảnh (max 5MB, đúng định dạng, magic byte check)
- Gọi `BookingCancellationService.CreateManualAsync()`
- Trả về 200 `{ requestId }`

Sau đó JS gọi `openCancellationDetail(requestId)` (hàm đã có sẵn) để mở modal duyệt hủy.

### 5. Service — BookingCancellationService

Thêm method:

```csharp
Task<BookingCancellationRequest> CreateManualAsync(CreateManualCancellationDto dto);
```

**`CreateManualCancellationDto`:**
- `BookingCode` (string)
- `ManualProofImage` (IFormFile)
- `ProcessedBy` (string — tên nhân viên từ session)

**Logic `CreateManualAsync()`:**
1. Tìm booking theo code (chính xác)
2. Validate booking chưa bị huỷ
3. Validate file ảnh (magic byte, kích thước, định dạng)
4. Tạo `BookingCancellationRequest`:
   - `BookingId` = booking.Id
   - `CustomerName/Phone/Email` = từ booking
   - `SubmittedBookingCode` = bookingCode
   - `ConfirmationEmailProofPath` = lưu ảnh vào `App_Data/SecureUploads/Cancellations/Manual/{guid}.ext`
   - `Status` = "Pending"
   - `IsManual` = true
   - Policy snapshot từ SystemSettings (giống `CreateAsync`)
   - `ChatSessionId` = null (không gắn session)
5. Lưu vào DB, return entity

**Lưu ý:** Ảnh Zalo được lưu vào `ConfirmationEmailProofPath` (đóng vai trò proof của yêu cầu hủy). Tất cả ảnh manual đặt trong thư mục `Manual/` riêng để dễ phân biệt với ảnh từ AI Chat.

### 6. JS — admin-chat-monitor.js

Thêm các hàm xử lý:

- **`openManualCancellationModal()`** — hiển thị modal, reset form
- **`lookupBooking()`** — gọi API lookup, hiển thị preview
- **`submitManualCancellation()`** — gọi API create-manual → nhận `requestId` → đóng modal hiện tại → gọi `openCancellationDetail(requestId)` (hàm đã có sẵn xử lý fetch detail + mở `#cancellation-modal`)
- **`resetManualForm()`** — clear form khi đóng modal

### 7. CSS — admin-chat-monitor.css

Style cho:
- `.btn-manual-cancellation` — nổi bật, màu warning
- `.manual-preview-card` — card preview thông tin khách sau tra cứu
- `.manual-file-list` — danh sách file đã chọn

## Không thay đổi

- Không thay đổi luồng duyệt hủy hiện tại (modal #cancellation-modal giữ nguyên)
- Không thay đổi email template, policy snapshot, refund logic
- Không thêm bảng mới, chỉ thêm 1 cột `is_manual`
- Giữ nguyên SignalR hub (không cần realtime cho manual cancellation)
