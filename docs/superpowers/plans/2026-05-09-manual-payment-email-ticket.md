# Xác nhận thanh toán thủ công & Gửi vé điện tử Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Triển khai luồng thanh toán thủ công: Khách tải ảnh bill -> Admin duyệt -> Hệ thống tự động gửi Email chứa vé điện tử (PIN, bản đồ, nội quy).

**Architecture:** Cập nhật Model Booking để lưu ảnh bill và trạng thái duyệt. Xây dựng dịch vụ MailService (SMTP) để gửi thông báo. Nâng cấp UI Checkout (khách) và Dashboard (admin).

**Tech Stack:** ASP.NET Core MVC, Entity Framework Core, MailKit (SMTP), Bootstrap.

---

### Task 1: Cập nhật Model & Database

**Files:**
- Modify: `WebHomestay/Models/Booking.cs`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`

- [ ] **Step 1: Cập nhật Model Booking**
Thêm các trường `PaymentProofUrl`, `SmartLockCode`, `WifiPassword`, `CheckInInstructions` và enum trạng thái.

```csharp
public enum BookingStatus
{
    PendingPayment,
    AwaitingApproval,
    Confirmed,
    Rejected,
    Cancelled
}

// Trong class Booking
public string? PaymentProofUrl { get; set; }
public string? SmartLockCode { get; set; }
public BookingStatus Status { get; set; } = BookingStatus.PendingPayment;
```

- [ ] **Step 2: Tạo Migration và cập nhật DB**
Run: `dotnet ef migrations add UpdateBookingForPayment`
Run: `dotnet ef database update`

- [ ] **Step 3: Commit**

### Task 2: Triển khai MailService (SMTP)

**Files:**
- Create: `WebHomestay/Services/IMailService.cs`
- Create: `WebHomestay/Services/MailService.cs`
- Modify: `WebHomestay/Program.cs`

- [ ] **Step 1: Định nghĩa Interface IMailService**

```csharp
public interface IMailService {
    Task SendEmailAsync(string toEmail, string subject, string body);
}
```

- [ ] **Step 2: Cài đặt MailService sử dụng MailKit**

```csharp
// Sử dụng MailKit.Net.Smtp
public async Task SendEmailAsync(string toEmail, string subject, string body) {
    // Logic gửi mail qua SMTP smtp.gmail.com:587
}
```

- [ ] **Step 3: Đăng ký Service trong Program.cs**

```csharp
builder.Services.AddTransient<IMailService, MailService>();
```

- [ ] **Step 4: Commit**

### Task 3: Giao diện Tải Bill (Customer Side)

**Files:**
- Modify: `WebHomestay/Controllers/BookingsController.cs`
- Modify: `WebHomestay/Views/Bookings/Checkout.cshtml`

- [ ] **Step 1: Cập nhật View Checkout**
Thêm form `enctype="multipart/form-data"` và input file để khách tải ảnh bill.

- [ ] **Step 2: Action UploadBill trong Controller**
Xử lý lưu file vào `wwwroot/uploads/payments/` và cập nhật `PaymentProofUrl`.

- [ ] **Step 3: Commit**

### Task 4: Quản trị Duyệt Bill (Admin Side)

**Files:**
- Modify: `WebHomestay/Controllers/AdminBookingsController.cs`
- Modify: `WebHomestay/Views/AdminBookings/Index.cshtml`

- [ ] **Step 1: Cập nhật Index Admin**
Thêm cột "Minh chứng" hiển thị thumbnail ảnh bill. Sử dụng Modal Bootstrap để phóng to ảnh khi click.

- [ ] **Step 2: Action Approve (POST)**
Nhận `BookingId` và `SmartLockCode`, cập nhật trạng thái đơn hàng và gọi `MailService`.

- [ ] **Step 3: Commit**

### Task 5: Email Template & Hoàn thiện

**Files:**
- Create: `WebHomestay/Views/Shared/_EmailTicketTemplate.cshtml`

- [ ] **Step 1: Thiết kế Template Email HTML**
Bao gồm: Lời chào, Mã phòng, PIN, Link Map, Nội quy.

- [ ] **Step 2: Tích hợp gửi mail thực tế**
Kiểm tra luồng gửi mail từ Admin sang khách hàng.

- [ ] **Step 3: Commit**
