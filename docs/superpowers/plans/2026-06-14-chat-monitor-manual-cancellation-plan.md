# Manual Cancellation (Zalo) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add "Hủy thủ công (Zalo)" button + modal to chat monitor for staff to manually create cancellation requests when customers cancel via Zalo.

**Architecture:** New modal on existing chat monitor page → lookup booking by code → upload Zalo screenshot → create `BookingCancellationRequest` via existing service pattern → open existing approval modal. Adds 1 bool column `is_manual` to `booking_cancellation_requests`.

**Tech Stack:** ASP.NET Core MVC, EF Core + Npgsql, vanilla JS, Bootstrap 5

---

### Task 1: Add `IsManual` to Model + DbContext

**Files:**
- Modify: `WebHomestay/Models/BookingCancellationRequest.cs:53`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs:395`

- [ ] **Step 1: Add `IsManual` property to model**

Add after line 52 (`public string PolicyMessageSnapshot`):

```csharp
    public bool IsManual { get; set; }
```

File: `WebHomestay/Models/BookingCancellationRequest.cs`

- [ ] **Step 2: Add column mapping in DbContext**

Add inside the `BookingCancellationRequest` config block, before the closing `});` at line 395:

```csharp
                entity.Property(e => e.IsManual).HasColumnName("is_manual");
```

File: `WebHomestay/Data/ApplicationDbContext.cs`

- [ ] **Step 3: Build and verify**

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add IsManual field to BookingCancellationRequest"
```

---

### Task 2: Add `CreateManualAsync` to Service

**Files:**
- Modify: `WebHomestay/Services/IBookingCancellationService.cs:41`
- Modify: `WebHomestay/Services/BookingCancellationService.cs:272` (before `GetProtectedFilePathAsync`)

- [ ] **Step 1: Add DTO + method to interface**

Add after line 34 (`CancellationEmailPreviewDto`):

```csharp
public record CreateManualCancellationDto(
    string BookingCode,
    IFormFile ManualProofImage,
    string ProcessedBy);
```

Add before closing `}` of interface (after `GetProtectedFilePathAsync`):

```csharp
    Task<BookingCancellationRequest> CreateManualAsync(CreateManualCancellationDto dto, CancellationToken cancellationToken = default);
```

File: `WebHomestay/Services/IBookingCancellationService.cs`

- [ ] **Step 2: Implement `CreateManualAsync` in service**

Add before `GetProtectedFilePathAsync` (before line 223):

```csharp
    public async Task<BookingCancellationRequest> CreateManualAsync(CreateManualCancellationDto dto, CancellationToken cancellationToken = default)
    {
        var bookingCode = OptionalTrimmed(dto.BookingCode, 50, "Mã booking không được vượt quá 50 ký tự.");
        if (string.IsNullOrWhiteSpace(bookingCode))
            throw new InvalidOperationException("Vui lòng nhập mã booking.");

        var bookingId = ParseBookingId(bookingCode);
        if (!bookingId.HasValue)
            throw new InvalidOperationException("Mã booking không hợp lệ.");

        var booking = await _context.Bookings.Include(b => b.Room).FirstOrDefaultAsync(b => b.Id == bookingId.Value, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy booking với mã này.");

        if (string.Equals(booking.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Booking này đã bị huỷ trước đó.");

        var imagePath = await SaveProtectedImageAsync(dto.ManualProofImage, "Manual", cancellationToken);
        var processedBy = OptionalTrimmed(dto.ProcessedBy, 100, "Người xử lý không được vượt quá 100 ký tự.");
        var policy = await GetPolicyAsync(cancellationToken);

        var request = new BookingCancellationRequest
        {
            BookingId = booking.Id,
            SubmittedBookingCode = bookingCode,
            CustomerName = booking.CustomerName,
            CustomerPhone = booking.CustomerPhone,
            CustomerEmail = booking.CustomerEmail ?? string.Empty,
            ConfirmationEmailProofPath = imagePath,
            Status = "Pending",
            IsManual = true,
            PolicyNoticeHoursSnapshot = policy.NoticeHours,
            RefundPercentBeforeNoticeSnapshot = policy.RefundPercentBeforeNotice,
            RefundPercentAfterNoticeSnapshot = policy.RefundPercentAfterNotice,
            PolicyMessageSnapshot = policy.PolicyMessage,
            ProcessedBy = processedBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.BookingCancellationRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);
        return request;
    }
```

File: `WebHomestay/Services/BookingCancellationService.cs`

- [ ] **Step 3: Build and verify**

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add CreateManualAsync to BookingCancellationService"
```

---

### Task 3: Add API Endpoints to Controller

**Files:**
- Modify: `WebHomestay/Controllers/AdminChatMonitorController.cs` (after line 524, before `GetCancellationDetail`)

- [ ] **Step 1: Add `LookupBookingForCancellation` endpoint**

Add after `GetCancellations` method (after line 524):

```csharp
    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("cancellations/lookup-booking")]
    public async Task<IActionResult> LookupBookingForCancellation([FromBody] LookupBookingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BookingCode))
            return BadRequest(new { error = "Vui lòng nhập mã booking." });

        var bookingId = ParseBookingIdInt(request.BookingCode.Trim());
        if (!bookingId.HasValue)
            return BadRequest(new { error = "Mã booking không hợp lệ." });

        var booking = await _context.Bookings
            .Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.Id == bookingId.Value);

        if (booking == null)
            return NotFound(new { error = "Không tìm thấy booking với mã này." });

        if (string.Equals(booking.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Booking này đã bị huỷ trước đó." });

        return Ok(new
        {
            bookingId = booking.Id,
            customerName = booking.CustomerName,
            customerPhone = booking.CustomerPhone,
            customerEmail = booking.CustomerEmail ?? "",
            roomName = booking.Room?.Name ?? "",
            checkIn = booking.StartTime,
            checkOut = booking.EndTime,
            status = booking.Status
        });
    }
```

- [ ] **Step 2: Add `CreateManualCancellation` endpoint**

Add after `LookupBookingForCancellation`:

```csharp
    [AdminAuthorize(Permission = "chats.view")]
    [HttpPost("cancellations/create-manual")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> CreateManualCancellation([FromForm] string bookingCode, [FromForm] IFormFile? image)
    {
        if (string.IsNullOrWhiteSpace(bookingCode))
            return BadRequest(new { error = "Vui lòng nhập mã booking." });

        if (image == null || image.Length == 0)
            return BadRequest(new { error = "Vui lòng chọn ảnh chụp Zalo." });

        try
        {
            var processedBy = HttpContext.Session.GetString("AdminUser") ?? "admin";
            var request = await _bookingCancellationService.CreateManualAsync(
                new CreateManualCancellationDto(bookingCode.Trim(), image, processedBy));
            return Ok(new { requestId = request.Id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
```

- [ ] **Step 3: Add helper `ParseBookingIdInt` + `LookupBookingRequest` DTO**

Add before the closing `}` of the controller class (before line 898), or after the existing `ParseSuggestedBookingIds`:

```csharp
    private static int? ParseBookingIdInt(string code)
    {
        var digits = new string(code.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var id) ? id : null;
    }
```

Add after the end of the controller class (after `AdminChatQuickSendRequest` class):

```csharp
public class LookupBookingRequest
{
    public string BookingCode { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Build and verify**

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add lookup-booking and create-manual cancellation endpoints"
```

---

### Task 4: Add Button + Modal to View

**Files:**
- Modify: `WebHomestay/Views/AdminChatMonitor/Index.cshtml`

- [ ] **Step 1: Add "Hủy thủ công (Zalo)" button to sidebar**

After line 26 (closing `</div>` of `chat-filter-row`), add:

```html
            <button class="btn btn-sm btn-outline-warning w-100 mt-1" id="btn-manual-cancellation" type="button">
                <i class="fas fa-file-pen me-1"></i>Hủy thủ công (Zalo)
            </button>
```

- [ ] **Step 2: Add manual cancellation modal**

After the `auto-reply-modal` (after line 151), add:

```html
<div class="modal fade" id="manual-cancellation-modal" tabindex="-1" aria-labelledby="manual-cancellation-title" aria-hidden="true">
    <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
            <div class="modal-header">
                <div>
                    <span class="modal-kicker">Hủy thủ công</span>
                    <h2 class="modal-title" id="manual-cancellation-title">Hủy đơn từ Zalo</h2>
                </div>
                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Đóng"></button>
            </div>
            <div class="modal-body">
                <div class="mb-3">
                    <label class="form-label" for="manual-booking-code">Mã đặt phòng</label>
                    <div class="input-group">
                        <input class="form-control" id="manual-booking-code" type="text" placeholder="Nhập mã booking..." />
                        <button class="btn btn-outline-primary" type="button" id="btn-lookup-booking">
                            <i class="fas fa-search me-1"></i>Tra cứu
                        </button>
                    </div>
                </div>
                <div id="manual-booking-preview" class="d-none">
                    <div class="manual-preview-card">
                        <div class="manual-preview-row">
                            <strong id="manual-customer-name"></strong>
                            <span id="manual-customer-phone"></span>
                        </div>
                        <div class="manual-preview-row">
                            <span id="manual-customer-email"></span>
                        </div>
                        <div class="manual-preview-row">
                            <span id="manual-room-name"></span>
                            <span id="manual-checkin-dates"></span>
                        </div>
                    </div>
                </div>
                <div class="mb-3">
                    <label class="form-label" for="manual-zalo-image">Ảnh chụp Zalo</label>
                    <input class="form-control" id="manual-zalo-image" type="file" accept="image/png,image/jpeg,image/gif,image/webp" />
                    <div class="form-text">Chụp màn hình tin nhắn Zalo khách yêu cầu hủy (PNG/JPG, tối đa 5MB).</div>
                </div>
                <div id="manual-cancellation-message" class="cancellation-action-message"></div>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-light" data-bs-dismiss="modal">Hủy</button>
                <button type="button" class="btn btn-warning" id="btn-submit-manual-cancellation" disabled>
                    <i class="fas fa-file-pen me-1"></i>Tạo đơn hủy
                </button>
            </div>
        </div>
    </div>
</div>
```

- [ ] **Step 3: Build and verify**

Build may have View warnings but should succeed:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add manual cancellation button and modal to view"
```

---

### Task 5: Add JavaScript Handlers

**Files:**
- Modify: `WebHomestay/wwwroot/js/admin-chat-monitor.js` (before closing `</script>` or at end of file)

- [ ] **Step 1: Add event listener for button + modal logic**

Add after line 65 (the document click handler for `[data-open-cancellation]`), inside the `DOMContentLoaded` block:

```js
    document.getElementById('btn-manual-cancellation')?.addEventListener('click', openManualCancellationModal);
    document.getElementById('btn-lookup-booking')?.addEventListener('click', lookupBookingForCancellation);
    document.getElementById('btn-submit-manual-cancellation')?.addEventListener('click', submitManualCancellation);
    document.getElementById('manual-cancellation-modal')?.addEventListener('hidden.bs.modal', resetManualCancellationForm);
```

- [ ] **Step 2: Add modal open/close + reset functions**

Add after `permanentlyDeleteSession` function (after line 862):

```js
function openManualCancellationModal() {
    const modalEl = document.getElementById('manual-cancellation-modal');
    if (!modalEl) return;
    resetManualCancellationForm();
    if (typeof bootstrap !== 'undefined') bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function resetManualCancellationForm() {
    document.getElementById('manual-booking-code').value = '';
    document.getElementById('manual-zalo-image').value = '';
    document.getElementById('manual-booking-preview').classList.add('d-none');
    document.getElementById('btn-submit-manual-cancellation').disabled = true;
    document.getElementById('manual-cancellation-message').textContent = '';
}
```

- [ ] **Step 3: Add `lookupBookingForCancellation` function**

Add after `resetManualCancellationForm`:

```js
function lookupBookingForCancellation() {
    const code = document.getElementById('manual-booking-code').value.trim();
    const message = document.getElementById('manual-cancellation-message');
    const preview = document.getElementById('manual-booking-preview');
    if (!code) {
        if (message) message.textContent = 'Vui lòng nhập mã booking.';
        return;
    }
    if (message) message.textContent = 'Đang tra cứu...';
    preview.classList.add('d-none');
    document.getElementById('btn-submit-manual-cancellation').disabled = true;

    fetch('/chinhan/hethong/chat-monitor/cancellations/lookup-booking', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ bookingCode: code })
    })
        .then(r => r.ok ? r.json() : r.json().then(err => Promise.reject(err)))
        .then(data => {
            if (message) message.textContent = '';
            document.getElementById('manual-customer-name').textContent = data.customerName || '';
            document.getElementById('manual-customer-phone').textContent = data.customerPhone || '';
            document.getElementById('manual-customer-email').textContent = data.customerEmail || '';
            document.getElementById('manual-room-name').textContent = data.roomName ? 'Phòng: ' + data.roomName : '';
            const checkIn = data.checkIn ? new Date(data.checkIn).toLocaleDateString('vi-VN') : '';
            const checkOut = data.checkOut ? new Date(data.checkOut).toLocaleDateString('vi-VN') : '';
            document.getElementById('manual-checkin-dates').textContent = checkIn && checkOut ? checkIn + ' → ' + checkOut : '';
            preview.classList.remove('d-none');
            checkManualFormReady();
        })
        .catch(err => {
            if (message) message.textContent = err?.error || 'Không tìm thấy booking.';
            preview.classList.add('d-none');
        });
}
```

- [ ] **Step 4: Add `checkManualFormReady` + `submitManualCancellation` functions**

Add after `lookupBookingForCancellation`:

```js
function checkManualFormReady() {
    const hasImage = document.getElementById('manual-zalo-image').files.length > 0;
    const hasPreview = !document.getElementById('manual-booking-preview').classList.contains('d-none');
    document.getElementById('btn-submit-manual-cancellation').disabled = !(hasImage && hasPreview);
}

function submitManualCancellation() {
    const code = document.getElementById('manual-booking-code').value.trim();
    const image = document.getElementById('manual-zalo-image').files[0];
    const message = document.getElementById('manual-cancellation-message');
    if (!code || !image) {
        if (message) message.textContent = 'Vui lòng nhập mã booking và chọn ảnh.';
        return;
    }
    if (message) message.textContent = 'Đang tạo đơn hủy...';

    const formData = new FormData();
    formData.append('bookingCode', code);
    formData.append('image', image);

    fetch('/chinhan/hethong/chat-monitor/cancellations/create-manual', {
        method: 'POST',
        body: formData
    })
        .then(r => r.ok ? r.json() : r.json().then(err => Promise.reject(err)))
        .then(data => {
            if (message) message.textContent = 'Đã tạo đơn hủy thành công.';
            const modalEl = document.getElementById('manual-cancellation-modal');
            if (modalEl && typeof bootstrap !== 'undefined') bootstrap.Modal.getOrCreateInstance(modalEl).hide();
            openCancellation(data.requestId);
            loadSessionsFallback();
        })
        .catch(err => {
            if (message) message.textContent = err?.error || 'Không tạo được đơn hủy.';
        });
}
```

- [ ] **Step 5: Wire up file input change to `checkManualFormReady`**

Also inside `DOMContentLoaded`, add after the manual-cancellation-modal listener:

```js
    document.getElementById('manual-zalo-image')?.addEventListener('change', checkManualFormReady);
```

- [ ] **Step 6: Build and verify**

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add JS handlers for manual cancellation modal"
```

---

### Task 6: Add CSS Styles

**Files:**
- Modify: `WebHomestay/wwwroot/css/admin-chat-monitor.css`

- [ ] **Step 1: Add styles for manual preview card and button**

Append at end of file:

```css
.btn-manual-cancellation {
    border-color: #f59e0b;
    color: #f59e0b;
}
.btn-manual-cancellation:hover {
    background: #f59e0b;
    color: #fff;
}
.manual-preview-card {
    background: rgba(255, 255, 255, 0.06);
    border: 1px solid rgba(148, 163, 184, 0.2);
    border-radius: 12px;
    padding: 14px 16px;
    margin-bottom: 16px;
}
.manual-preview-row {
    display: flex;
    gap: 12px;
    font-size: 0.9rem;
    margin-bottom: 4px;
    color: #cbd5e1;
}
.manual-preview-row strong {
    color: #f1f5f9;
}
```

- [ ] **Step 2: Commit**

```bash
git add -A
git commit -m "feat: add CSS styles for manual cancellation"
```

---

### Task 7: Add Migration + Apply

**Files:**
- Create: new migration via `dotnet ef migrations add`

- [ ] **Step 1: Add EF migration for `is_manual` column**

```bash
dotnet ef migrations add AddManualCancellationFlag --project WebHomestay/WebHomestay.csproj
```

- [ ] **Step 2: Verify migration generates correctly**

Check the generated migration file in `WebHomestay/Migrations/` — it should contain:
```csharp
migrationBuilder.AddColumn<bool>(
    name: "is_manual",
    table: "booking_cancellation_requests",
    type: "boolean",
    nullable: false,
    defaultValue: false);
```

- [ ] **Step 3: Apply migration to DB**

```bash
dotnet ef database update --project WebHomestay/WebHomestay.csproj
```

- [ ] **Step 4: Build and run tests**

```bash
dotnet build WebHomestay/WebHomestay.csproj
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj
```

Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add migration for is_manual column"
```

---

### Task 8: Verify End-to-End

- [ ] **Step 1: Run the app**

```bash
dotnet run --project WebHomestay/WebHomestay.csproj
```

- [ ] **Step 2: Manually verify flow**
  1. Navigate to `/chinhan/hethong/chat-monitor`
  2. Verify "Hủy thủ công (Zalo)" button visible in sidebar
  3. Click button → modal opens with booking code input + file upload
  4. Enter a valid booking code, click "Tra cứu" → preview card shows customer info
  5. Upload a Zalo screenshot image
  6. Click "Tạo đơn hủy" → modal closes → approval modal opens with pending request
  7. Verify the cancellation appears in "Yêu cầu hủy chờ xử lý" filter list
  8. Process the cancellation normally (approve/reject)
