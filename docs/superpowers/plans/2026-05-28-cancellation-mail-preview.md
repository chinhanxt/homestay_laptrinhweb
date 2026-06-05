# Cancellation Mail Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a mandatory server-side email preview flow before staff approve or reject booking cancellation requests.

**Architecture:** Keep templates in `SystemSettings`, render previews in `BookingCancellationService`, and make final approve/reject endpoints the only place that changes status or sends mail. Extend mail delivery with optional attachments while preserving the existing simple send API.

**Tech Stack:** ASP.NET Core MVC, EF Core/PostgreSQL, Razor, Bootstrap/jQuery-style vanilla JS, xUnit with EF InMemory.

---

## File Structure

- Modify `WebHomestay/Services/IBookingCancellationService.cs`: add DTO records for preview/finalization and new service methods.
- Modify `WebHomestay/Services/BookingCancellationService.cs`: render template previews, validate inputs, finalize approve/reject, save audit fields, and attach bill on approval.
- Modify `WebHomestay/Services/IMailService.cs`: add attachment-capable mail API without breaking existing callers.
- Modify `WebHomestay/Services/MailService.cs`: implement optional attachment sending with MimeKit `BodyBuilder.Attachments`.
- Modify `WebHomestay/Models/BookingCancellationRequest.cs`: add audit properties.
- Modify `WebHomestay/Data/ApplicationDbContext.cs`: map new audit columns.
- Create EF migration under `WebHomestay/Migrations/`: add audit columns to `booking_cancellation_requests`.
- Modify `WebHomestay/Program.cs`: seed default cancellation email template settings.
- Modify `WebHomestay/Controllers/AdminSettingsController.cs`: persist email template settings with the cancellation policy form.
- Modify `WebHomestay/Views/AdminSettings/Index.cshtml`: render subject/body fields for approval and rejection templates.
- Modify `WebHomestay/Controllers/AdminChatMonitorController.cs`: add preview endpoints and pass final mail note/body data to finalization.
- Modify `WebHomestay/wwwroot/js/admin-chat-monitor.js`: change approve/reject buttons to open preview first and only finalize from the preview modal.
- Modify `WebHomestay.Tests/Services/BookingCancellationServiceTests.cs`: cover preview rendering, attachment finalization, and audit persistence.

---

### Task 1: Add service contracts for preview and finalization

**Files:**
- Modify: `WebHomestay/Services/IBookingCancellationService.cs`

- [ ] **Step 1: Update DTOs and interface methods**

Replace the existing `ProcessCancellationDto` record and interface with this expanded contract shape, keeping existing policy/create methods intact:

```csharp
public record ProcessCancellationDto(
    int RequestId,
    string StaffReason,
    int AppliedRefundPercent,
    string ProcessedBy,
    IFormFile? RefundBillProof,
    string? NotificationEmailSubject = null,
    string? NotificationEmailBody = null);

public record CancellationEmailPreviewDto(
    string RecipientEmail,
    string Subject,
    string Body,
    string EditableReason,
    string? AttachmentName,
    bool RequiresAttachment);

public interface IBookingCancellationService
{
    Task<CancellationPolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default);
    Task<BookingCancellationRequest> CreateAsync(CreateCancellationRequestDto dto, CancellationToken cancellationToken = default);
    Task<CancellationEmailPreviewDto> BuildApprovalPreviewAsync(ProcessCancellationDto dto, CancellationToken cancellationToken = default);
    Task<CancellationEmailPreviewDto> BuildRejectionPreviewAsync(ProcessCancellationDto dto, CancellationToken cancellationToken = default);
    Task<BookingCancellationRequest> ApproveAsync(ProcessCancellationDto dto, CancellationToken cancellationToken = default);
    Task<BookingCancellationRequest> RejectAsync(ProcessCancellationDto dto, CancellationToken cancellationToken = default);
    Task<string> GetProtectedFilePathAsync(int requestId, string kind, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Build to expose implementation failures**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: FAIL because `BookingCancellationService` does not implement the new preview methods yet.

- [ ] **Step 3: Commit**

```bash
git add WebHomestay/Services/IBookingCancellationService.cs
git commit -m "feat: add cancellation mail preview contracts"
```

---

### Task 2: Add data model audit fields

**Files:**
- Modify: `WebHomestay/Models/BookingCancellationRequest.cs`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`
- Create: `WebHomestay/Migrations/<timestamp>_AddCancellationNotificationAudit.cs`
- Create: `WebHomestay/Migrations/<timestamp>_AddCancellationNotificationAudit.Designer.cs`
- Modify: `WebHomestay/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] **Step 1: Add audit properties to the model**

In `BookingCancellationRequest`, add after `ProcessedAt`:

```csharp
[StringLength(200)]
public string? NotificationEmailSubject { get; set; }

public string? NotificationEmailBody { get; set; }

public DateTime? NotificationEmailSentAt { get; set; }

[StringLength(255)]
public string? NotificationEmailAttachmentName { get; set; }

[StringLength(20)]
public string? NotificationEmailType { get; set; }
```

- [ ] **Step 2: Map audit columns**

In `ApplicationDbContext` inside the `BookingCancellationRequest` entity mapping, add after `ProcessedAt` mapping:

```csharp
entity.Property(e => e.NotificationEmailSubject).HasColumnName("notification_email_subject").HasMaxLength(200);
entity.Property(e => e.NotificationEmailBody).HasColumnName("notification_email_body");
entity.Property(e => e.NotificationEmailSentAt).HasColumnName("notification_email_sent_at");
entity.Property(e => e.NotificationEmailAttachmentName).HasColumnName("notification_email_attachment_name").HasMaxLength(255);
entity.Property(e => e.NotificationEmailType).HasColumnName("notification_email_type").HasMaxLength(20);
```

- [ ] **Step 3: Generate migration**

Run:

```bash
dotnet ef migrations add AddCancellationNotificationAudit --project WebHomestay/WebHomestay.csproj
```

Expected: a migration adding the five nullable columns to `booking_cancellation_requests`.

- [ ] **Step 4: Inspect migration content**

Confirm the migration has these operations:

```csharp
migrationBuilder.AddColumn<string>(
    name: "notification_email_subject",
    table: "booking_cancellation_requests",
    type: "character varying(200)",
    maxLength: 200,
    nullable: true);

migrationBuilder.AddColumn<string>(
    name: "notification_email_body",
    table: "booking_cancellation_requests",
    type: "text",
    nullable: true);

migrationBuilder.AddColumn<DateTime>(
    name: "notification_email_sent_at",
    table: "booking_cancellation_requests",
    type: "timestamp with time zone",
    nullable: true);

migrationBuilder.AddColumn<string>(
    name: "notification_email_attachment_name",
    table: "booking_cancellation_requests",
    type: "character varying(255)",
    maxLength: 255,
    nullable: true);

migrationBuilder.AddColumn<string>(
    name: "notification_email_type",
    table: "booking_cancellation_requests",
    type: "character varying(20)",
    maxLength: 20,
    nullable: true);
```

- [ ] **Step 5: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: still may FAIL until service methods are implemented, but should not fail due to model/mapping errors.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Models/BookingCancellationRequest.cs WebHomestay/Data/ApplicationDbContext.cs WebHomestay/Migrations
git commit -m "feat: add cancellation notification audit fields"
```

---

### Task 3: Extend mail service with attachment support

**Files:**
- Modify: `WebHomestay/Services/IMailService.cs`
- Modify: `WebHomestay/Services/MailService.cs`
- Modify: `WebHomestay.Tests/Services/BookingCancellationServiceTests.cs` fake mail service later in Task 5

- [ ] **Step 1: Add attachment DTO and method**

Update `IMailService.cs` to:

```csharp
namespace WebHomestay.Services
{
    public record EmailAttachment(string FilePath, string FileName, string ContentType);

    public interface IMailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendEmailAsync(string toEmail, string subject, string body, IReadOnlyCollection<EmailAttachment> attachments);
    }
}
```

- [ ] **Step 2: Implement overload in MailService**

Replace the current method body with overload delegation:

```csharp
public Task SendEmailAsync(string toEmail, string subject, string body)
{
    return SendEmailAsync(toEmail, subject, body, Array.Empty<EmailAttachment>());
}

public async Task SendEmailAsync(string toEmail, string subject, string body, IReadOnlyCollection<EmailAttachment> attachments)
{
    var email = new MimeMessage();
    email.From.Add(new MailboxAddress(_config["MailSettings:DisplayName"], _config["MailSettings:Mail"]));
    email.To.Add(MailboxAddress.Parse(toEmail));
    email.Subject = subject;

    var builder = new BodyBuilder { HtmlBody = body };
    foreach (var attachment in attachments)
    {
        if (File.Exists(attachment.FilePath))
        {
            builder.Attachments.Add(attachment.FileName, await File.ReadAllBytesAsync(attachment.FilePath), ContentType.Parse(attachment.ContentType));
        }
    }
    email.Body = builder.ToMessageBody();

    using var smtp = new SmtpClient();
    try
    {
        await smtp.ConnectAsync(_config["MailSettings:Host"], int.Parse(_config["MailSettings:Port"] ?? "587"), SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(_config["MailSettings:Mail"], _config["MailSettings:Password"]);
        await smtp.SendAsync(email);
    }
    finally
    {
        await smtp.DisconnectAsync(true);
    }
}
```

- [ ] **Step 3: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: compile errors may remain in tests/fakes until Task 5 updates them.

- [ ] **Step 4: Commit**

```bash
git add WebHomestay/Services/IMailService.cs WebHomestay/Services/MailService.cs
git commit -m "feat: support email attachments"
```

---

### Task 4: Implement cancellation email preview and finalization service logic

**Files:**
- Modify: `WebHomestay/Services/BookingCancellationService.cs`

- [ ] **Step 1: Add template constants near existing static fields**

```csharp
public const string ApprovalSubjectSettingKey = "CancellationApprovalEmailSubject";
public const string ApprovalBodySettingKey = "CancellationApprovalEmailBody";
public const string RejectionSubjectSettingKey = "CancellationRejectionEmailSubject";
public const string RejectionBodySettingKey = "CancellationRejectionEmailBody";

public const string DefaultApprovalSubject = "Yêu cầu hủy đặt phòng đã được duyệt";
public const string DefaultApprovalBody = "Xin chào {CustomerName},<br><br>Yêu cầu hủy booking #{BookingId} của quý khách đã được duyệt.<br>Phòng: {RoomName}<br>Thời gian: {StartTime} - {EndTime}<br>Tỷ lệ hoàn tiền: {RefundPercent}%<br>Ghi chú: {StaffReason}<br><br>Chính sách: {PolicyMessage}";
public const string DefaultRejectionSubject = "Yêu cầu hủy đặt phòng chưa được duyệt";
public const string DefaultRejectionBody = "Xin chào {CustomerName},<br><br>Yêu cầu hủy booking #{BookingId} của quý khách chưa được duyệt.<br>Phòng: {RoomName}<br>Thời gian: {StartTime} - {EndTime}<br>Ghi chú: {StaffReason}<br><br>Chính sách: {PolicyMessage}";
```

- [ ] **Step 2: Add preview methods**

Add public methods before `ApproveAsync`:

```csharp
public async Task<CancellationEmailPreviewDto> BuildApprovalPreviewAsync(ProcessCancellationDto dto, CancellationToken cancellationToken = default)
{
    if (dto.RefundBillProof is null || dto.RefundBillProof.Length == 0) throw new InvalidOperationException("Vui lòng tải lên ảnh chứng từ hoàn tiền.");
    var request = await LoadPendingRequestAsync(dto.RequestId, cancellationToken);
    if (!request.BookingId.HasValue) throw new InvalidOperationException("Yêu cầu hủy chưa được liên kết với booking.");
    var staffReason = RequireTrimmed(dto.StaffReason, 2000, "Vui lòng nhập lý do xử lý.", "Lý do xử lý không được vượt quá 2000 ký tự.");
    var subject = await RenderTemplateAsync(ApprovalSubjectSettingKey, DefaultApprovalSubject, request, dto.AppliedRefundPercent, staffReason, cancellationToken);
    var body = await RenderTemplateAsync(ApprovalBodySettingKey, DefaultApprovalBody, request, dto.AppliedRefundPercent, staffReason, cancellationToken);
    return new CancellationEmailPreviewDto(request.CustomerEmail, subject, body, staffReason, dto.RefundBillProof.FileName, true);
}

public async Task<CancellationEmailPreviewDto> BuildRejectionPreviewAsync(ProcessCancellationDto dto, CancellationToken cancellationToken = default)
{
    var request = await LoadPendingRequestAsync(dto.RequestId, cancellationToken);
    var staffReason = RequireTrimmed(dto.StaffReason, 2000, "Vui lòng nhập lý do xử lý.", "Lý do xử lý không được vượt quá 2000 ký tự.");
    var subject = await RenderTemplateAsync(RejectionSubjectSettingKey, DefaultRejectionSubject, request, 0, staffReason, cancellationToken);
    var body = await RenderTemplateAsync(RejectionBodySettingKey, DefaultRejectionBody, request, 0, staffReason, cancellationToken);
    return new CancellationEmailPreviewDto(request.CustomerEmail, subject, body, staffReason, null, false);
}
```

- [ ] **Step 3: Change ApproveAsync to use final email data and attachment mail**

Inside `ApproveAsync`, after saving `request.RefundBillProofPath`, compute subject/body from `dto.NotificationEmailSubject`/`dto.NotificationEmailBody` or render defaults. After `SaveChangesAsync`, send with attachment and then save audit only after send succeeds. Use this shape:

```csharp
var subject = string.IsNullOrWhiteSpace(dto.NotificationEmailSubject)
    ? await RenderTemplateAsync(ApprovalSubjectSettingKey, DefaultApprovalSubject, request, request.AppliedRefundPercent ?? 0, staffReason, cancellationToken)
    : dto.NotificationEmailSubject.Trim();
var body = string.IsNullOrWhiteSpace(dto.NotificationEmailBody)
    ? await RenderTemplateAsync(ApprovalBodySettingKey, DefaultApprovalBody, request, request.AppliedRefundPercent ?? 0, staffReason, cancellationToken)
    : dto.NotificationEmailBody.Trim();
var attachmentName = Path.GetFileName(dto.RefundBillProof.FileName);
var attachment = new EmailAttachment(request.RefundBillProofPath!, attachmentName, dto.RefundBillProof.ContentType);

await _context.SaveChangesAsync(cancellationToken);
await _mailService.SendEmailAsync(request.CustomerEmail, subject, body, new[] { attachment });

request.NotificationEmailSubject = subject;
request.NotificationEmailBody = body;
request.NotificationEmailSentAt = DateTime.UtcNow;
request.NotificationEmailAttachmentName = attachmentName;
request.NotificationEmailType = "Approved";
await _context.SaveChangesAsync(cancellationToken);
return request;
```

Keep existing Auto mode booking cancellation/slot release logic before the first `SaveChangesAsync`.

- [ ] **Step 4: Change RejectAsync to use final email data and audit**

Before final save/send in `RejectAsync`, compute:

```csharp
var subject = string.IsNullOrWhiteSpace(dto.NotificationEmailSubject)
    ? await RenderTemplateAsync(RejectionSubjectSettingKey, DefaultRejectionSubject, request, 0, staffReason, cancellationToken)
    : dto.NotificationEmailSubject.Trim();
var body = string.IsNullOrWhiteSpace(dto.NotificationEmailBody)
    ? await RenderTemplateAsync(RejectionBodySettingKey, DefaultRejectionBody, request, 0, staffReason, cancellationToken)
    : dto.NotificationEmailBody.Trim();
```

Then send and audit:

```csharp
await _context.SaveChangesAsync(cancellationToken);
await _mailService.SendEmailAsync(request.CustomerEmail, subject, body);

request.NotificationEmailSubject = subject;
request.NotificationEmailBody = body;
request.NotificationEmailSentAt = DateTime.UtcNow;
request.NotificationEmailAttachmentName = null;
request.NotificationEmailType = "Rejected";
await _context.SaveChangesAsync(cancellationToken);
return request;
```

- [ ] **Step 5: Add template rendering helpers**

Add helper methods near the old `BuildApprovalEmail` methods, then remove `BuildApprovalEmail` and `BuildRejectionEmail`:

```csharp
private async Task<string> RenderTemplateAsync(string settingKey, string defaultTemplate, BookingCancellationRequest request, int refundPercent, string staffReason, CancellationToken cancellationToken)
{
    var template = await _settingService.GetStringAsync(settingKey, defaultTemplate);
    if (string.IsNullOrWhiteSpace(template)) template = defaultTemplate;
    var booking = request.BookingId.HasValue
        ? await _context.Bookings.Include(b => b.Room).FirstOrDefaultAsync(b => b.Id == request.BookingId.Value, cancellationToken)
        : null;

    return template
        .Replace("{CustomerName}", request.CustomerName)
        .Replace("{BookingId}", request.BookingId?.ToString() ?? request.SubmittedBookingCode ?? "")
        .Replace("{RoomName}", booking?.Room?.Name ?? "")
        .Replace("{StartTime}", booking?.StartTime.ToString("dd/MM/yyyy HH:mm") ?? "")
        .Replace("{EndTime}", booking?.EndTime.ToString("dd/MM/yyyy HH:mm") ?? "")
        .Replace("{RefundPercent}", refundPercent.ToString())
        .Replace("{StaffReason}", staffReason)
        .Replace("{PolicyMessage}", request.PolicyMessageSnapshot ?? "");
}
```

- [ ] **Step 6: Run service test build**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BookingCancellationServiceTests
```

Expected: FAIL until tests/fake mail are updated in Task 5.

- [ ] **Step 7: Commit**

```bash
git add WebHomestay/Services/BookingCancellationService.cs
git commit -m "feat: render cancellation notification previews"
```

---

### Task 5: Update and add BookingCancellationService tests

**Files:**
- Modify: `WebHomestay.Tests/Services/BookingCancellationServiceTests.cs`

- [ ] **Step 1: Replace fake mail service with capture-capable fake**

Replace the current fake with:

```csharp
private class FakeMailService : IMailService
{
    public List<(string ToEmail, string Subject, string Body, IReadOnlyCollection<EmailAttachment> Attachments)> Sent { get; } = new();

    public Task SendEmailAsync(string toEmail, string subject, string body)
    {
        Sent.Add((toEmail, subject, body, Array.Empty<EmailAttachment>()));
        return Task.CompletedTask;
    }

    public Task SendEmailAsync(string toEmail, string subject, string body, IReadOnlyCollection<EmailAttachment> attachments)
    {
        Sent.Add((toEmail, subject, body, attachments));
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Change CreateService to return the fake mail when needed**

Add optional mail parameter:

```csharp
private static BookingCancellationService CreateService(ApplicationDbContext context, FakeSettingService? settings = null, FakeWebHostEnvironment? env = null, FakeMailService? mail = null)
{
    return new BookingCancellationService(context, settings ?? new FakeSettingService(), mail ?? new FakeMailService(), env ?? new FakeWebHostEnvironment());
}
```

- [ ] **Step 3: Add approval preview test**

Add:

```csharp
[Fact]
public async Task BuildApprovalPreviewAsync_RendersTemplateAndAttachmentName()
{
    await using var context = CreateContext();
    context.Rooms.Add(new Room { Id = 1, Name = "Phòng A", Status = "Available" });
    context.Bookings.Add(CreateBooking(50, "Khach", "090", "k@example.com"));
    context.BookingCancellationRequests.Add(CreateRequest(60, 50));
    await context.SaveChangesAsync();
    var settings = new FakeSettingService();
    settings.Strings[BookingCancellationService.ApprovalSubjectSettingKey] = "Duyệt #{BookingId}";
    settings.Strings[BookingCancellationService.ApprovalBodySettingKey] = "Xin chào {CustomerName}, {RoomName}, {RefundPercent}%, {StaffReason}, {PolicyMessage}";
    var service = CreateService(context, settings);

    var preview = await service.BuildApprovalPreviewAsync(new ProcessCancellationDto(60, "Đã hoàn tiền", 80, "admin", CreateImage(PngBytes(), "bill.png", "image/png")));

    Assert.Equal("k@example.com", preview.RecipientEmail);
    Assert.Equal("Duyệt #50", preview.Subject);
    Assert.Contains("Khach", preview.Body);
    Assert.Contains("Phòng A", preview.Body);
    Assert.Contains("80%", preview.Body);
    Assert.Contains("Đã hoàn tiền", preview.Body);
    Assert.Equal("bill.png", preview.AttachmentName);
    Assert.True(preview.RequiresAttachment);
}
```

- [ ] **Step 4: Add rejection preview test**

Add:

```csharp
[Fact]
public async Task BuildRejectionPreviewAsync_RendersTemplateWithoutAttachment()
{
    await using var context = CreateContext();
    context.Rooms.Add(new Room { Id = 1, Name = "Phòng B", Status = "Available" });
    context.Bookings.Add(CreateBooking(51, "Khach", "090", "k@example.com"));
    context.BookingCancellationRequests.Add(CreateRequest(61, 51));
    await context.SaveChangesAsync();
    var settings = new FakeSettingService();
    settings.Strings[BookingCancellationService.RejectionSubjectSettingKey] = "Từ chối #{BookingId}";
    settings.Strings[BookingCancellationService.RejectionBodySettingKey] = "Lý do: {StaffReason}";
    var service = CreateService(context, settings);

    var preview = await service.BuildRejectionPreviewAsync(new ProcessCancellationDto(61, "Quá hạn", 0, "admin", null));

    Assert.Equal("Từ chối #51", preview.Subject);
    Assert.Equal("Lý do: Quá hạn", preview.Body);
    Assert.Null(preview.AttachmentName);
    Assert.False(preview.RequiresAttachment);
}
```

- [ ] **Step 5: Add final approval mail/audit test**

Add:

```csharp
[Fact]
public async Task ApproveAsync_SendsAttachmentAndSavesNotificationAudit()
{
    await using var context = CreateContext();
    context.Bookings.Add(CreateBooking(52, "Khach", "090", "k@example.com"));
    context.BookingCancellationRequests.Add(CreateRequest(62, 52));
    await context.SaveChangesAsync();
    var mail = new FakeMailService();
    var service = CreateService(context, mail: mail);

    var request = await service.ApproveAsync(new ProcessCancellationDto(62, "Đã hoàn", 70, "admin", CreateImage(PngBytes(), "bill-final.png", "image/png"), "Subject cuối", "Body cuối"));

    Assert.Equal("Approved", request.Status);
    Assert.Single(mail.Sent);
    Assert.Equal("Subject cuối", mail.Sent[0].Subject);
    Assert.Equal("Body cuối", mail.Sent[0].Body);
    Assert.Single(mail.Sent[0].Attachments);
    Assert.Equal("bill-final.png", mail.Sent[0].Attachments.First().FileName);
    Assert.Equal("Subject cuối", request.NotificationEmailSubject);
    Assert.Equal("Body cuối", request.NotificationEmailBody);
    Assert.Equal("bill-final.png", request.NotificationEmailAttachmentName);
    Assert.Equal("Approved", request.NotificationEmailType);
    Assert.NotNull(request.NotificationEmailSentAt);
}
```

- [ ] **Step 6: Add final rejection mail/audit test**

Add:

```csharp
[Fact]
public async Task RejectAsync_SendsEmailWithoutAttachmentAndSavesNotificationAudit()
{
    await using var context = CreateContext();
    context.Bookings.Add(CreateBooking(53, "Khach", "090", "k@example.com"));
    context.BookingCancellationRequests.Add(CreateRequest(63, 53));
    await context.SaveChangesAsync();
    var mail = new FakeMailService();
    var service = CreateService(context, mail: mail);

    var request = await service.RejectAsync(new ProcessCancellationDto(63, "Không đủ điều kiện", 0, "admin", null, "Từ chối", "Nội dung từ chối"));

    Assert.Equal("Rejected", request.Status);
    Assert.Single(mail.Sent);
    Assert.Equal("Từ chối", mail.Sent[0].Subject);
    Assert.Empty(mail.Sent[0].Attachments);
    Assert.Equal("Rejected", request.NotificationEmailType);
    Assert.Null(request.NotificationEmailAttachmentName);
    Assert.NotNull(request.NotificationEmailSentAt);
}
```

- [ ] **Step 7: Run focused tests**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BookingCancellationServiceTests
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add WebHomestay.Tests/Services/BookingCancellationServiceTests.cs
git commit -m "test: cover cancellation notification previews"
```

---

### Task 6: Add settings UI and persistence for templates

**Files:**
- Modify: `WebHomestay/Program.cs`
- Modify: `WebHomestay/Controllers/AdminSettingsController.cs`
- Modify: `WebHomestay/Views/AdminSettings/Index.cshtml`

- [ ] **Step 1: Seed default settings**

In `Program.cs` where cancellation settings are seeded, add four `SystemSetting` entries:

```csharp
new WebHomestay.Models.SystemSetting { SettingKey = "CancellationApprovalEmailSubject", SettingValue = WebHomestay.Services.BookingCancellationService.DefaultApprovalSubject, Description = "Tiêu đề email chấp nhận hủy đơn", GroupName = "Cancellation" },
new WebHomestay.Models.SystemSetting { SettingKey = "CancellationApprovalEmailBody", SettingValue = WebHomestay.Services.BookingCancellationService.DefaultApprovalBody, Description = "Nội dung email chấp nhận hủy đơn", GroupName = "Cancellation" },
new WebHomestay.Models.SystemSetting { SettingKey = "CancellationRejectionEmailSubject", SettingValue = WebHomestay.Services.BookingCancellationService.DefaultRejectionSubject, Description = "Tiêu đề email từ chối hủy đơn", GroupName = "Cancellation" },
new WebHomestay.Models.SystemSetting { SettingKey = "CancellationRejectionEmailBody", SettingValue = WebHomestay.Services.BookingCancellationService.DefaultRejectionBody, Description = "Nội dung email từ chối hủy đơn", GroupName = "Cancellation" }
```

- [ ] **Step 2: Read template values in Razor**

In `AdminSettings/Index.cshtml`, add near existing cancellation variables:

```csharp
var cancellationApprovalEmailSubject = Model.FirstOrDefault(s => s.SettingKey == "CancellationApprovalEmailSubject")?.SettingValue ?? WebHomestay.Services.BookingCancellationService.DefaultApprovalSubject;
var cancellationApprovalEmailBody = Model.FirstOrDefault(s => s.SettingKey == "CancellationApprovalEmailBody")?.SettingValue ?? WebHomestay.Services.BookingCancellationService.DefaultApprovalBody;
var cancellationRejectionEmailSubject = Model.FirstOrDefault(s => s.SettingKey == "CancellationRejectionEmailSubject")?.SettingValue ?? WebHomestay.Services.BookingCancellationService.DefaultRejectionSubject;
var cancellationRejectionEmailBody = Model.FirstOrDefault(s => s.SettingKey == "CancellationRejectionEmailBody")?.SettingValue ?? WebHomestay.Services.BookingCancellationService.DefaultRejectionBody;
```

- [ ] **Step 3: Add template fields inside cancellation policy form**

After the policy message textarea, add:

```html
<div class="col-12">
    <label class="form-label small fw-bold text-muted">Tiêu đề mail chấp nhận hủy</label>
    <input type="text" name="approvalEmailSubject" value="@cancellationApprovalEmailSubject" class="form-control-premium" />
</div>
<div class="col-12">
    <label class="form-label small fw-bold text-muted">Nội dung mail chấp nhận hủy</label>
    <textarea name="approvalEmailBody" rows="5" class="form-control premium-input">@cancellationApprovalEmailBody</textarea>
    <div class="form-text">Placeholder: {CustomerName}, {BookingId}, {RoomName}, {StartTime}, {EndTime}, {RefundPercent}, {StaffReason}, {PolicyMessage}</div>
</div>
<div class="col-12">
    <label class="form-label small fw-bold text-muted">Tiêu đề mail từ chối hủy</label>
    <input type="text" name="rejectionEmailSubject" value="@cancellationRejectionEmailSubject" class="form-control-premium" />
</div>
<div class="col-12">
    <label class="form-label small fw-bold text-muted">Nội dung mail từ chối hủy</label>
    <textarea name="rejectionEmailBody" rows="5" class="form-control premium-input">@cancellationRejectionEmailBody</textarea>
    <div class="form-text">Placeholder: {CustomerName}, {BookingId}, {RoomName}, {StartTime}, {EndTime}, {RefundPercent}, {StaffReason}, {PolicyMessage}</div>
</div>
```

- [ ] **Step 4: Persist template inputs**

Change `UpdateCancellationPolicy` signature to:

```csharp
public async Task<IActionResult> UpdateCancellationPolicy(string handlingMode, int noticeHours, int refundBefore, int refundAfter, string policyMessage, string approvalEmailSubject, string approvalEmailBody, string rejectionEmailSubject, string rejectionEmailBody)
```

After existing setting updates, add:

```csharp
await _settingService.UpdateSettingAsync("CancellationApprovalEmailSubject", string.IsNullOrWhiteSpace(approvalEmailSubject) ? BookingCancellationService.DefaultApprovalSubject : approvalEmailSubject.Trim());
await _settingService.UpdateSettingAsync("CancellationApprovalEmailBody", string.IsNullOrWhiteSpace(approvalEmailBody) ? BookingCancellationService.DefaultApprovalBody : approvalEmailBody.Trim());
await _settingService.UpdateSettingAsync("CancellationRejectionEmailSubject", string.IsNullOrWhiteSpace(rejectionEmailSubject) ? BookingCancellationService.DefaultRejectionSubject : rejectionEmailSubject.Trim());
await _settingService.UpdateSettingAsync("CancellationRejectionEmailBody", string.IsNullOrWhiteSpace(rejectionEmailBody) ? BookingCancellationService.DefaultRejectionBody : rejectionEmailBody.Trim());
```

- [ ] **Step 5: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: PASS if previous service tasks compile.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Program.cs WebHomestay/Controllers/AdminSettingsController.cs WebHomestay/Views/AdminSettings/Index.cshtml
git commit -m "feat: configure cancellation email templates"
```

---

### Task 7: Add preview endpoints and final endpoint payloads

**Files:**
- Modify: `WebHomestay/Controllers/AdminChatMonitorController.cs`

- [ ] **Step 1: Add approval preview endpoint**

Add before `ApproveCancellation`:

```csharp
[AdminAuthorize(Permission = "chats.view")]
[HttpPost("cancellations/{id:int}/approval-preview")]
[RequestSizeLimit(10 * 1024 * 1024)]
public async Task<IActionResult> PreviewApprovalCancellation(int id, [FromForm] string staffReason, [FromForm] int appliedRefundPercent, [FromForm] IFormFile? refundBillProof)
{
    try
    {
        var processedBy = HttpContext.Session.GetString("AdminUser") ?? "admin";
        var preview = await _bookingCancellationService.BuildApprovalPreviewAsync(new ProcessCancellationDto(id, staffReason, appliedRefundPercent, processedBy, refundBillProof));
        return Ok(preview);
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(new { message = ex.Message });
    }
}
```

- [ ] **Step 2: Add rejection preview endpoint**

Add before `RejectCancellation`:

```csharp
[AdminAuthorize(Permission = "chats.view")]
[HttpPost("cancellations/{id:int}/rejection-preview")]
public async Task<IActionResult> PreviewRejectionCancellation(int id, [FromForm] string staffReason)
{
    try
    {
        var processedBy = HttpContext.Session.GetString("AdminUser") ?? "admin";
        var preview = await _bookingCancellationService.BuildRejectionPreviewAsync(new ProcessCancellationDto(id, staffReason, 0, processedBy, null));
        return Ok(preview);
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(new { message = ex.Message });
    }
}
```

- [ ] **Step 3: Accept final email fields in approve endpoint**

Change `ApproveCancellation` signature to:

```csharp
public async Task<IActionResult> ApproveCancellation(int id, [FromForm] string staffReason, [FromForm] int appliedRefundPercent, [FromForm] IFormFile? refundBillProof, [FromForm] string? notificationEmailSubject, [FromForm] string? notificationEmailBody)
```

Change service call to:

```csharp
var request = await _bookingCancellationService.ApproveAsync(new ProcessCancellationDto(id, staffReason, appliedRefundPercent, processedBy, refundBillProof, notificationEmailSubject, notificationEmailBody));
```

- [ ] **Step 4: Accept final email fields in reject endpoint**

Change `RejectCancellation` signature to:

```csharp
public async Task<IActionResult> RejectCancellation(int id, [FromForm] string staffReason, [FromForm] string? notificationEmailSubject, [FromForm] string? notificationEmailBody)
```

Change service call to:

```csharp
var request = await _bookingCancellationService.RejectAsync(new ProcessCancellationDto(id, staffReason, 0, processedBy, null, notificationEmailSubject, notificationEmailBody));
```

- [ ] **Step 5: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Controllers/AdminChatMonitorController.cs
git commit -m "feat: add cancellation mail preview endpoints"
```

---

### Task 8: Implement admin monitor preview UI

**Files:**
- Modify: `WebHomestay/wwwroot/js/admin-chat-monitor.js`
- Check: `WebHomestay/Views/AdminChatMonitor/Index.cshtml` only if there is no existing modal container suitable for dynamic preview markup

- [x] **Step 1: Add preview state near top of JS file**

Add after `totalUnreadCount`:

```javascript
let cancellationMailPreviewState = null;
```

- [x] **Step 2: Change approve/reject buttons to request preview**

Replace `approveCancellation` and `rejectCancellation` with:

```javascript
function approveCancellation(id) {
    const file = document.getElementById('cancellation-refund-bill')?.files?.[0];
    if (!file) {
        const message = document.getElementById('cancellation-action-message');
        if (message) message.textContent = 'Vui lòng tải lên ảnh chứng từ hoàn tiền trước khi xem preview.';
        return;
    }
    requestCancellationPreview(id, 'approve');
}

function rejectCancellation(id) {
    requestCancellationPreview(id, 'reject');
}
```

- [x] **Step 3: Add preview request helper**

Add:

```javascript
function requestCancellationPreview(id, action) {
    const formData = new FormData();
    formData.append('staffReason', document.getElementById('cancellation-staff-reason')?.value || '');
    if (action === 'approve') {
        formData.append('appliedRefundPercent', document.getElementById('cancellation-refund-percent')?.value || '0');
        const file = document.getElementById('cancellation-refund-bill')?.files?.[0];
        if (file) formData.append('refundBillProof', file);
    }
    const url = action === 'approve'
        ? `/admin/chat-monitor/cancellations/${encodeURIComponent(id)}/approval-preview`
        : `/admin/chat-monitor/cancellations/${encodeURIComponent(id)}/rejection-preview`;
    fetch(url, { method: 'POST', body: formData })
        .then(r => r.ok ? r.json() : r.json().then(err => Promise.reject(err)))
        .then(preview => showCancellationMailPreview(id, action, preview))
        .catch(err => {
            const message = document.getElementById('cancellation-action-message');
            if (message) message.textContent = err?.message || 'Không tạo được preview email.';
        });
}
```

- [x] **Step 4: Add dynamic preview modal renderer**

Add:

```javascript
function showCancellationMailPreview(id, action, preview) {
    cancellationMailPreviewState = {
        id,
        action,
        preview,
        billFile: action === 'approve' ? document.getElementById('cancellation-refund-bill')?.files?.[0] : null
    };
    let modalEl = document.getElementById('cancellation-mail-preview-modal');
    if (!modalEl) {
        modalEl = document.createElement('div');
        modalEl.className = 'modal fade';
        modalEl.id = 'cancellation-mail-preview-modal';
        modalEl.tabIndex = -1;
        document.body.appendChild(modalEl);
    }
    modalEl.innerHTML = `
        <div class="modal-dialog modal-lg modal-dialog-scrollable">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title">Preview email gửi khách</h5>
                    <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                </div>
                <div class="modal-body">
                    <div class="mb-2"><strong>Người nhận:</strong> ${escapeHtml(preview.recipientEmail || '')}</div>
                    <label class="form-label">Tiêu đề</label>
                    <input class="form-control" id="cancellation-preview-subject" value="${escapeHtml(preview.subject || '')}" readonly />
                    <label class="form-label mt-3">Ghi chú/lý do gửi khách</label>
                    <textarea class="form-control" id="cancellation-preview-reason" rows="3">${escapeHtml(preview.editableReason || '')}</textarea>
                    <label class="form-label mt-3">Nội dung email</label>
                    <div class="border rounded p-3 bg-light" id="cancellation-preview-body">${preview.body || ''}</div>
                    ${action === 'approve' ? renderPreviewAttachment(preview.attachmentName) : ''}
                    <div class="text-danger small mt-2" id="cancellation-preview-warning"></div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Đóng</button>
                    <button type="button" class="btn btn-primary" id="btn-finalize-cancellation-mail">${action === 'approve' ? 'Gửi mail & chấp nhận hủy' : 'Gửi mail & từ chối hủy'}</button>
                </div>
            </div>
        </div>`;
    document.getElementById('btn-finalize-cancellation-mail')?.addEventListener('click', finalizeCancellationFromPreview);
    document.getElementById('cancellation-preview-reason')?.addEventListener('input', updatePreviewBodyReason);
    document.getElementById('btn-remove-preview-bill')?.addEventListener('click', removePreviewBill);
    document.getElementById('preview-replacement-bill')?.addEventListener('change', replacePreviewBill);
    updatePreviewSendState();
    if (typeof bootstrap !== 'undefined') bootstrap.Modal.getOrCreateInstance(modalEl).show();
}
```

- [x] **Step 5: Add attachment preview helpers**

Add:

```javascript
function renderPreviewAttachment(name) {
    return `
        <div class="mt-3 border rounded p-3">
            <div class="fw-bold mb-2">Bill hoàn tiền đính kèm</div>
            <div id="preview-bill-name">${escapeHtml(name || '')}</div>
            <div class="d-flex gap-2 mt-2">
                <button class="btn btn-sm btn-outline-danger" type="button" id="btn-remove-preview-bill">Gỡ bill</button>
                <label class="btn btn-sm btn-outline-primary mb-0">
                    Upload bill khác
                    <input class="d-none" type="file" id="preview-replacement-bill" accept="image/*" />
                </label>
            </div>
        </div>`;
}

function removePreviewBill() {
    if (!cancellationMailPreviewState) return;
    cancellationMailPreviewState.billFile = null;
    const name = document.getElementById('preview-bill-name');
    if (name) name.textContent = 'Chưa có bill đính kèm.';
    updatePreviewSendState();
}

function replacePreviewBill(e) {
    const file = e.target.files?.[0];
    if (!file || !cancellationMailPreviewState) return;
    cancellationMailPreviewState.billFile = file;
    const name = document.getElementById('preview-bill-name');
    if (name) name.textContent = file.name;
    updatePreviewSendState();
}

function updatePreviewSendState() {
    const button = document.getElementById('btn-finalize-cancellation-mail');
    const warning = document.getElementById('cancellation-preview-warning');
    const missingBill = cancellationMailPreviewState?.action === 'approve' && !cancellationMailPreviewState.billFile;
    if (button) button.disabled = missingBill;
    if (warning) warning.textContent = missingBill ? 'Chấp nhận hủy cần chứng từ hoàn tiền.' : '';
}
```

- [x] **Step 6: Add reason/body update helper**

Add:

```javascript
function updatePreviewBodyReason() {
    if (!cancellationMailPreviewState) return;
    const reason = document.getElementById('cancellation-preview-reason')?.value || '';
    const originalReason = cancellationMailPreviewState.preview.editableReason || '';
    const body = cancellationMailPreviewState.preview.body || '';
    const bodyEl = document.getElementById('cancellation-preview-body');
    if (bodyEl) bodyEl.innerHTML = body.replaceAll(escapeHtml(originalReason), escapeHtml(reason)).replaceAll(originalReason, escapeHtml(reason));
}
```

- [x] **Step 7: Add finalization helper**

Add:

```javascript
function finalizeCancellationFromPreview() {
    if (!cancellationMailPreviewState) return;
    const { id, action } = cancellationMailPreviewState;
    const formData = new FormData();
    const reason = document.getElementById('cancellation-preview-reason')?.value || '';
    const subject = document.getElementById('cancellation-preview-subject')?.value || '';
    const body = document.getElementById('cancellation-preview-body')?.innerHTML || '';
    formData.append('staffReason', reason);
    formData.append('notificationEmailSubject', subject);
    formData.append('notificationEmailBody', body);
    if (action === 'approve') {
        formData.append('appliedRefundPercent', document.getElementById('cancellation-refund-percent')?.value || '0');
        if (cancellationMailPreviewState.billFile) formData.append('refundBillProof', cancellationMailPreviewState.billFile);
    }
    const url = action === 'approve'
        ? `/admin/chat-monitor/cancellations/${encodeURIComponent(id)}/approve`
        : `/admin/chat-monitor/cancellations/${encodeURIComponent(id)}/reject`;
    postCancellationAction(url, formData, id);
    const modalEl = document.getElementById('cancellation-mail-preview-modal');
    if (modalEl && typeof bootstrap !== 'undefined') bootstrap.Modal.getOrCreateInstance(modalEl).hide();
}
```

- [x] **Step 8: Run a JS smoke check by loading page**

Run the app later in Task 10 and verify no console syntax errors on `/admin/chat-monitor`.

- [ ] **Step 9: Commit**

```bash
git add WebHomestay/wwwroot/js/admin-chat-monitor.js WebHomestay/Views/AdminChatMonitor/Index.cshtml
git commit -m "feat: add cancellation email preview UI"
```

---

### Task 9: Full test and build pass

**Files:**
- No code changes unless fixing failures from prior tasks.

- [ ] **Step 1: Run focused tests**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BookingCancellationServiceTests
```

Expected: PASS.

- [ ] **Step 2: Run all tests**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj
```

Expected: PASS.

- [ ] **Step 3: Build web app**

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: PASS.

- [ ] **Step 4: Commit any fixes**

If any fixes were required:

```bash
git status --short
git add WebHomestay/Services/BookingCancellationService.cs WebHomestay.Tests/Services/BookingCancellationServiceTests.cs WebHomestay/Controllers/AdminChatMonitorController.cs WebHomestay/wwwroot/js/admin-chat-monitor.js WebHomestay/Views/AdminSettings/Index.cshtml WebHomestay/Controllers/AdminSettingsController.cs WebHomestay/Program.cs
git commit -m "fix: stabilize cancellation mail preview flow"
```

Only stage files that actually changed while fixing test/build failures. If no fixes were required, do not create an empty commit.

---

### Task 10: Manual UI verification

**Files:**
- No planned code changes unless manual testing exposes a bug.

- [ ] **Step 1: Run the app**

```bash
dotnet run --project WebHomestay/WebHomestay.csproj
```

Expected: app starts at local development URL. If startup fails because PostgreSQL/mail settings are unavailable, record the exact failure and stop manual verification.

- [ ] **Step 2: Verify settings UI**

Open `/admin/settings` and confirm under Vận hành & Tự động hóa:

- Approval subject field is visible.
- Approval body field is visible.
- Rejection subject field is visible.
- Rejection body field is visible.
- Saving the form persists all four values.

- [ ] **Step 3: Verify approval preview flow**

On `/admin/chat-monitor`, open a pending cancellation request with a linked booking:

- Click Chấp nhận hủy without a bill.
- Expected: inline error, no status change, no mail send.
- Upload a valid bill and click Chấp nhận hủy.
- Expected: preview modal opens with recipient, subject, body, and bill attachment name.
- Click Gỡ bill.
- Expected: final send button disabled and warning visible.
- Upload a replacement bill.
- Expected: final send button enabled and attachment name updated.
- Edit the reason field.
- Expected: body preview reflects the changed reason.
- Click Gửi mail & chấp nhận hủy.
- Expected: request becomes Approved only after this click.

- [ ] **Step 4: Verify rejection preview flow**

Open another pending cancellation request:

- Enter a rejection reason and click Từ chối.
- Expected: preview opens without bill attachment section.
- Click Gửi mail & từ chối hủy.
- Expected: request becomes Rejected only after final confirmation.

- [ ] **Step 5: Commit manual-test fixes only if needed**

```bash
git status --short
git add WebHomestay/wwwroot/js/admin-chat-monitor.js WebHomestay/Views/AdminChatMonitor/Index.cshtml WebHomestay/Views/AdminSettings/Index.cshtml WebHomestay/Controllers/AdminChatMonitorController.cs
git commit -m "fix: polish cancellation mail preview UI"
```

Only stage files that actually changed while fixing manual UI issues.

---

## Self-Review Notes

- Spec coverage: The plan includes settings templates, preview endpoints, required approval bill attachment, final-only status changes, mail attachment support, audit fields, tests, and manual UI verification.
- Placeholder scan: No `TBD` or `TODO` placeholders are intentional plan steps; implementation details are specified per task.
- Type consistency: DTO names are introduced in Task 1 and reused consistently in controller, service, and tests.
