# Branch QR Payment Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the System Settings AI test tab with branch-specific QR payment settings and use those settings on the booking payment page.

**Architecture:** Keep persistence in the existing `SystemSettings` table with branch-scoped keys. Add small payment QR view models and a focused service that reads/writes settings, validates uploads, resolves QR priority, and builds the booking payment display model. Wire admin save/upload through `AdminSettingsController` and pass the resolved display model from `BookingsController.Success` to `Views/Bookings/Success.cshtml`.

**Tech Stack:** ASP.NET Core MVC, Razor, EF Core/PostgreSQL through existing `ApplicationDbContext`, `SystemSettings`, Bootstrap/jQuery-style admin UI, xUnit tests.

---

## File Structure

- Create `WebHomestay/Models/ViewModels/PaymentQrSettingsViewModel.cs` — admin form state for one branch and booking payment display state.
- Create `WebHomestay/Services/PaymentQrSettingsService.cs` — branch-scoped SystemSettings keys, QR upload validation, save/load, VietQR URL generation, and booking payment display resolution.
- Modify `WebHomestay/Program.cs` — register `IPaymentQrSettingsService`.
- Modify `WebHomestay/Controllers/AdminSettingsController.cs` — inject `IPaymentQrSettingsService`, load branch QR settings into `ViewBag.PaymentQrSettings`, add POST action for saving QR settings.
- Modify `WebHomestay/Controllers/BookingsController.cs` — inject `IPaymentQrSettingsService`, set `ViewBag.PaymentQrDisplay` in `Success`.
- Modify `WebHomestay/Views/AdminSettings/Index.cshtml` — replace AI sidebar tab/panel with QR settings UI.
- Modify `WebHomestay/Views/Bookings/Success.cshtml` — replace hard-coded QR, bank details, countdown, and messages with `PaymentQrDisplayViewModel` values.
- Create `WebHomestay.Tests/Services/PaymentQrSettingsServiceTests.cs` — unit tests for default values, save/load, QR priority, countdown fallback, and generated VietQR.

---

### Task 1: Add QR settings view models and service contract

**Files:**
- Create: `WebHomestay/Models/ViewModels/PaymentQrSettingsViewModel.cs`
- Modify: `WebHomestay/Services/PaymentQrSettingsService.cs` if creating interface and service in one file is preferred

- [ ] **Step 1: Create the view model file**

Create `WebHomestay/Models/ViewModels/PaymentQrSettingsViewModel.cs` with:

```csharp
namespace WebHomestay.Models.ViewModels
{
    public class PaymentQrSettingsViewModel
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string QrImagePath { get; set; } = string.Empty;
        public string QrImageUrl { get; set; } = string.Empty;
        public string BankCode { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string BankAccountName { get; set; } = string.Empty;
        public string TransferContentTemplate { get; set; } = "chinhan {BookingId}";
        public int CountdownMinutes { get; set; } = 5;
        public string BeforeBillMessage { get; set; } = "Vui lòng tải lên ảnh chụp màn hình bill thanh toán thành công để chúng tôi xác nhận nhanh nhất.";
        public string AfterBillMessage { get; set; } = "Chúng tôi đã nhận được Bill của bạn. Nhân viên sẽ đối soát và gửi mã phòng qua Email sớm nhất.";
        public string ExpiredMessage { get; set; } = "Rất tiếc, thời gian giữ chỗ của bạn đã kết thúc. Vui lòng quay lại trang chủ để chọn lại.";
    }

    public class PaymentQrDisplayViewModel
    {
        public string QrImageSrc { get; set; } = string.Empty;
        public bool HasQrImage { get; set; }
        public string MissingQrMessage { get; set; } = string.Empty;
        public string BankCode { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string BankAccountName { get; set; } = string.Empty;
        public string TransferContent { get; set; } = string.Empty;
        public int CountdownMinutes { get; set; } = 5;
        public string BeforeBillMessage { get; set; } = string.Empty;
        public string AfterBillMessage { get; set; } = string.Empty;
        public string ExpiredMessage { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 2: Create the service file with interface skeleton**

Create `WebHomestay/Services/PaymentQrSettingsService.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services
{
    public interface IPaymentQrSettingsService
    {
        Task<PaymentQrSettingsViewModel> GetSettingsAsync(int branchId, string branchName);
        Task<Dictionary<int, PaymentQrSettingsViewModel>> GetSettingsForBranchesAsync(IEnumerable<Branch> branches);
        Task<(bool Success, string Message)> SaveSettingsAsync(PaymentQrSettingsViewModel model, IFormFile? qrImage, string webRootPath);
        Task<PaymentQrDisplayViewModel> BuildDisplayAsync(Booking booking);
    }

    public class PaymentQrSettingsService : IPaymentQrSettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISettingService _settingService;

        public PaymentQrSettingsService(ApplicationDbContext context, ISettingService settingService)
        {
            _context = context;
            _settingService = settingService;
        }

        public Task<PaymentQrSettingsViewModel> GetSettingsAsync(int branchId, string branchName)
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<int, PaymentQrSettingsViewModel>> GetSettingsForBranchesAsync(IEnumerable<Branch> branches)
        {
            throw new NotImplementedException();
        }

        public Task<(bool Success, string Message)> SaveSettingsAsync(PaymentQrSettingsViewModel model, IFormFile? qrImage, string webRootPath)
        {
            throw new NotImplementedException();
        }

        public Task<PaymentQrDisplayViewModel> BuildDisplayAsync(Booking booking)
        {
            throw new NotImplementedException();
        }
    }
}
```

- [ ] **Step 3: Register the service**

In `WebHomestay/Program.cs`, find the existing service registrations and add:

```csharp
builder.Services.AddScoped<IPaymentQrSettingsService, PaymentQrSettingsService>();
```

- [ ] **Step 4: Build to verify the skeleton compiles**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: build succeeds. The `NotImplementedException` methods compile because they are not executed during build.

- [ ] **Step 5: Commit**

```bash
git add WebHomestay/Models/ViewModels/PaymentQrSettingsViewModel.cs WebHomestay/Services/PaymentQrSettingsService.cs WebHomestay/Program.cs
git commit -m "feat: add payment QR settings service skeleton"
```

---

### Task 2: Implement settings load/save and service tests

**Files:**
- Modify: `WebHomestay/Services/PaymentQrSettingsService.cs`
- Create: `WebHomestay.Tests/Services/PaymentQrSettingsServiceTests.cs`

- [ ] **Step 1: Write tests for defaults and save/load**

Create `WebHomestay.Tests/Services/PaymentQrSettingsServiceTests.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services
{
    public class PaymentQrSettingsServiceTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private static PaymentQrSettingsService CreateService(ApplicationDbContext context)
        {
            return new PaymentQrSettingsService(context, new SettingService(context, new MemoryCache(new MemoryCacheOptions())));
        }

        [Fact]
        public async Task GetSettingsAsync_ReturnsDefaults_WhenBranchHasNoSettings()
        {
            using var context = CreateContext();
            var service = CreateService(context);

            var settings = await service.GetSettingsAsync(7, "Quận 1");

            Assert.Equal(7, settings.BranchId);
            Assert.Equal("Quận 1", settings.BranchName);
            Assert.Equal(5, settings.CountdownMinutes);
            Assert.Equal("chinhan {BookingId}", settings.TransferContentTemplate);
            Assert.Contains("bill thanh toán", settings.BeforeBillMessage);
        }

        [Fact]
        public async Task SaveSettingsAsync_PersistsBranchScopedSettings()
        {
            using var context = CreateContext();
            context.Branches.Add(new Branch { Id = 3, Name = "Quận 3" });
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.SaveSettingsAsync(new PaymentQrSettingsViewModel
            {
                BranchId = 3,
                BranchName = "Quận 3",
                QrImageUrl = "https://example.com/qr.png",
                BankCode = "MB",
                BankAccountNumber = "123456789",
                BankAccountName = "chinhan",
                TransferContentTemplate = "chinhan {BookingId} {BranchName}",
                CountdownMinutes = 8,
                BeforeBillMessage = "Gửi bill sau khi chuyển khoản.",
                AfterBillMessage = "Đã nhận bill.",
                ExpiredMessage = "Đơn đã hết hạn."
            }, null, "");

            Assert.True(result.Success);
            var settings = await service.GetSettingsAsync(3, "Quận 3");
            Assert.Equal("https://example.com/qr.png", settings.QrImageUrl);
            Assert.Equal("MB", settings.BankCode);
            Assert.Equal("123456789", settings.BankAccountNumber);
            Assert.Equal("chinhan", settings.BankAccountName);
            Assert.Equal("chinhan {BookingId} {BranchName}", settings.TransferContentTemplate);
            Assert.Equal(8, settings.CountdownMinutes);
            Assert.Equal("Gửi bill sau khi chuyển khoản.", settings.BeforeBillMessage);
            Assert.Equal("Đã nhận bill.", settings.AfterBillMessage);
            Assert.Equal("Đơn đã hết hạn.", settings.ExpiredMessage);
        }

        [Fact]
        public async Task SaveSettingsAsync_RejectsCountdownBelowOneMinute()
        {
            using var context = CreateContext();
            context.Branches.Add(new Branch { Id = 4, Name = "Quận 4" });
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var result = await service.SaveSettingsAsync(new PaymentQrSettingsViewModel
            {
                BranchId = 4,
                CountdownMinutes = 0
            }, null, "");

            Assert.False(result.Success);
            Assert.Contains("Thời gian", result.Message);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail on NotImplementedException**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter PaymentQrSettingsServiceTests
```

Expected: tests fail because `PaymentQrSettingsService` methods throw `NotImplementedException`.

- [ ] **Step 3: Implement settings load/save without upload handling yet**

Replace the body of `PaymentQrSettingsService` with:

```csharp
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services
{
    public interface IPaymentQrSettingsService
    {
        Task<PaymentQrSettingsViewModel> GetSettingsAsync(int branchId, string branchName);
        Task<Dictionary<int, PaymentQrSettingsViewModel>> GetSettingsForBranchesAsync(IEnumerable<Branch> branches);
        Task<(bool Success, string Message)> SaveSettingsAsync(PaymentQrSettingsViewModel model, IFormFile? qrImage, string webRootPath);
        Task<PaymentQrDisplayViewModel> BuildDisplayAsync(Booking booking);
    }

    public class PaymentQrSettingsService : IPaymentQrSettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISettingService _settingService;
        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        public PaymentQrSettingsService(ApplicationDbContext context, ISettingService settingService)
        {
            _context = context;
            _settingService = settingService;
        }

        public async Task<PaymentQrSettingsViewModel> GetSettingsAsync(int branchId, string branchName)
        {
            var settings = await _context.SystemSettings
                .Where(s => s.SettingKey.StartsWith($"PaymentQr:{branchId}:"))
                .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue ?? string.Empty);

            string Get(string name, string defaultValue = "")
            {
                return settings.TryGetValue(Key(branchId, name), out var value) && !string.IsNullOrWhiteSpace(value)
                    ? value
                    : defaultValue;
            }

            var countdownValue = Get("CountdownMinutes", "5");
            var countdown = int.TryParse(countdownValue, out var parsedCountdown) && parsedCountdown > 0
                ? parsedCountdown
                : 5;

            return new PaymentQrSettingsViewModel
            {
                BranchId = branchId,
                BranchName = branchName,
                QrImagePath = Get("QrImagePath"),
                QrImageUrl = Get("QrImageUrl"),
                BankCode = Get("BankCode"),
                BankAccountNumber = Get("BankAccountNumber"),
                BankAccountName = Get("BankAccountName"),
                TransferContentTemplate = Get("TransferContentTemplate", "chinhan {BookingId}"),
                CountdownMinutes = countdown,
                BeforeBillMessage = Get("BeforeBillMessage", "Vui lòng tải lên ảnh chụp màn hình bill thanh toán thành công để chúng tôi xác nhận nhanh nhất."),
                AfterBillMessage = Get("AfterBillMessage", "Chúng tôi đã nhận được Bill của bạn. Nhân viên sẽ đối soát và gửi mã phòng qua Email sớm nhất."),
                ExpiredMessage = Get("ExpiredMessage", "Rất tiếc, thời gian giữ chỗ của bạn đã kết thúc. Vui lòng quay lại trang chủ để chọn lại.")
            };
        }

        public async Task<Dictionary<int, PaymentQrSettingsViewModel>> GetSettingsForBranchesAsync(IEnumerable<Branch> branches)
        {
            var result = new Dictionary<int, PaymentQrSettingsViewModel>();
            foreach (var branch in branches)
            {
                result[branch.Id] = await GetSettingsAsync(branch.Id, branch.Name);
            }
            return result;
        }

        public async Task<(bool Success, string Message)> SaveSettingsAsync(PaymentQrSettingsViewModel model, IFormFile? qrImage, string webRootPath)
        {
            var branchExists = await _context.Branches.AnyAsync(b => b.Id == model.BranchId);
            if (!branchExists)
            {
                return (false, "Không tìm thấy chi nhánh cần cấu hình.");
            }

            if (model.CountdownMinutes < 1)
            {
                return (false, "Thời gian đếm ngược phải từ 1 phút trở lên.");
            }

            var current = await GetSettingsAsync(model.BranchId, model.BranchName);
            var qrImagePath = current.QrImagePath;

            if (qrImage != null && qrImage.Length > 0)
            {
                var extension = Path.GetExtension(qrImage.FileName);
                if (!AllowedImageExtensions.Contains(extension))
                {
                    return (false, "Ảnh QR chỉ hỗ trợ định dạng JPG, PNG hoặc WEBP.");
                }

                var uploadDir = Path.Combine(webRootPath, "uploads", "payment-qr");
                Directory.CreateDirectory(uploadDir);
                var fileName = $"branch-{model.BranchId}-qr{extension.ToLowerInvariant()}";
                var filePath = Path.Combine(uploadDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await qrImage.CopyToAsync(stream);
                }

                qrImagePath = $"/uploads/payment-qr/{fileName}";
            }

            await Update(model.BranchId, "QrImagePath", qrImagePath);
            await Update(model.BranchId, "QrImageUrl", model.QrImageUrl?.Trim() ?? string.Empty);
            await Update(model.BranchId, "BankCode", model.BankCode?.Trim() ?? string.Empty);
            await Update(model.BranchId, "BankAccountNumber", model.BankAccountNumber?.Trim() ?? string.Empty);
            await Update(model.BranchId, "BankAccountName", model.BankAccountName?.Trim() ?? string.Empty);
            await Update(model.BranchId, "TransferContentTemplate", string.IsNullOrWhiteSpace(model.TransferContentTemplate) ? "chinhan {BookingId}" : model.TransferContentTemplate.Trim());
            await Update(model.BranchId, "CountdownMinutes", model.CountdownMinutes.ToString());
            await Update(model.BranchId, "BeforeBillMessage", model.BeforeBillMessage?.Trim() ?? string.Empty);
            await Update(model.BranchId, "AfterBillMessage", model.AfterBillMessage?.Trim() ?? string.Empty);
            await Update(model.BranchId, "ExpiredMessage", model.ExpiredMessage?.Trim() ?? string.Empty);

            return (true, "Cập nhật cấu hình mã QR thành công.");
        }

        public async Task<PaymentQrDisplayViewModel> BuildDisplayAsync(Booking booking)
        {
            var branch = booking.Room?.Branch;
            if (branch == null)
            {
                return BuildMissingDisplay();
            }

            var settings = await GetSettingsAsync(branch.Id, branch.Name);
            var transferContent = BuildTransferContent(settings.TransferContentTemplate, booking, branch.Name);
            var generatedQr = BuildGeneratedQrUrl(settings, booking.TotalPrice, transferContent);
            var qrSrc = FirstNonEmpty(settings.QrImagePath, settings.QrImageUrl, generatedQr);

            return new PaymentQrDisplayViewModel
            {
                QrImageSrc = qrSrc,
                HasQrImage = !string.IsNullOrWhiteSpace(qrSrc),
                MissingQrMessage = string.IsNullOrWhiteSpace(qrSrc) ? "Chi nhánh này chưa cấu hình mã QR thanh toán." : string.Empty,
                BankCode = settings.BankCode,
                BankAccountNumber = settings.BankAccountNumber,
                BankAccountName = settings.BankAccountName,
                TransferContent = transferContent,
                CountdownMinutes = settings.CountdownMinutes > 0 ? settings.CountdownMinutes : 5,
                BeforeBillMessage = settings.BeforeBillMessage,
                AfterBillMessage = settings.AfterBillMessage,
                ExpiredMessage = settings.ExpiredMessage
            };
        }

        private async Task Update(int branchId, string name, string value)
        {
            var key = Key(branchId, name);
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
            if (setting == null)
            {
                _context.SystemSettings.Add(new SystemSetting
                {
                    SettingKey = key,
                    SettingValue = value,
                    GroupName = "PaymentQr",
                    Description = $"Payment QR setting {name} for branch {branchId}",
                    LastUpdated = DateTime.UtcNow
                });
            }
            else
            {
                setting.SettingValue = value;
                setting.LastUpdated = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        private static string Key(int branchId, string name) => $"PaymentQr:{branchId}:{name}";

        private static string BuildTransferContent(string template, Booking booking, string branchName)
        {
            return (string.IsNullOrWhiteSpace(template) ? "chinhan {BookingId}" : template)
                .Replace("{BookingId}", booking.Id.ToString())
                .Replace("{BranchName}", branchName);
        }

        private static string BuildGeneratedQrUrl(PaymentQrSettingsViewModel settings, decimal amount, string transferContent)
        {
            if (string.IsNullOrWhiteSpace(settings.BankCode) || string.IsNullOrWhiteSpace(settings.BankAccountNumber))
            {
                return string.Empty;
            }

            return $"https://img.vietqr.io/image/{Uri.EscapeDataString(settings.BankCode)}-{Uri.EscapeDataString(settings.BankAccountNumber)}-compact.png?amount={amount:0}&addInfo={Uri.EscapeDataString(transferContent)}";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }

        private static PaymentQrDisplayViewModel BuildMissingDisplay()
        {
            return new PaymentQrDisplayViewModel
            {
                HasQrImage = false,
                MissingQrMessage = "Không tìm thấy chi nhánh để lấy cấu hình mã QR thanh toán.",
                CountdownMinutes = 5,
                BeforeBillMessage = "Vui lòng tải lên ảnh chụp màn hình bill thanh toán thành công để chúng tôi xác nhận nhanh nhất.",
                AfterBillMessage = "Chúng tôi đã nhận được Bill của bạn. Nhân viên sẽ đối soát và gửi mã phòng qua Email sớm nhất.",
                ExpiredMessage = "Rất tiếc, thời gian giữ chỗ của bạn đã kết thúc. Vui lòng quay lại trang chủ để chọn lại."
            };
        }
    }
}
```

- [ ] **Step 4: Run the service tests**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter PaymentQrSettingsServiceTests
```

Expected: tests pass.

- [ ] **Step 5: Commit**

```bash
git add WebHomestay/Services/PaymentQrSettingsService.cs WebHomestay.Tests/Services/PaymentQrSettingsServiceTests.cs
git commit -m "feat: persist branch payment QR settings"
```

---

### Task 3: Add QR priority and generated VietQR tests

**Files:**
- Modify: `WebHomestay.Tests/Services/PaymentQrSettingsServiceTests.cs`
- Modify: `WebHomestay/Services/PaymentQrSettingsService.cs` only if tests reveal issues

- [ ] **Step 1: Add display behavior tests**

Append these tests to `PaymentQrSettingsServiceTests`:

```csharp
        [Fact]
        public async Task BuildDisplayAsync_PrefersUploadedImageOverUrlAndGeneratedQr()
        {
            using var context = CreateContext();
            var branch = new Branch { Id = 5, Name = "Quận 5" };
            var room = new Room { Id = 10, Name = "A101", BranchId = 5, Branch = branch };
            var booking = new Booking { Id = 99, Room = room, TotalPrice = 750000 };
            context.Branches.Add(branch);
            context.SystemSettings.AddRange(
                new SystemSetting { SettingKey = "PaymentQr:5:QrImagePath", SettingValue = "/uploads/payment-qr/branch-5-qr.png", GroupName = "PaymentQr" },
                new SystemSetting { SettingKey = "PaymentQr:5:QrImageUrl", SettingValue = "https://example.com/manual.png", GroupName = "PaymentQr" },
                new SystemSetting { SettingKey = "PaymentQr:5:BankCode", SettingValue = "MB", GroupName = "PaymentQr" },
                new SystemSetting { SettingKey = "PaymentQr:5:BankAccountNumber", SettingValue = "123456789", GroupName = "PaymentQr" }
            );
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var display = await service.BuildDisplayAsync(booking);

            Assert.True(display.HasQrImage);
            Assert.Equal("/uploads/payment-qr/branch-5-qr.png", display.QrImageSrc);
        }

        [Fact]
        public async Task BuildDisplayAsync_UsesManualUrl_WhenNoUploadedImageExists()
        {
            using var context = CreateContext();
            var branch = new Branch { Id = 6, Name = "Quận 6" };
            var room = new Room { Id = 11, Name = "B201", BranchId = 6, Branch = branch };
            var booking = new Booking { Id = 100, Room = room, TotalPrice = 650000 };
            context.Branches.Add(branch);
            context.SystemSettings.Add(new SystemSetting { SettingKey = "PaymentQr:6:QrImageUrl", SettingValue = "https://example.com/manual.png", GroupName = "PaymentQr" });
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var display = await service.BuildDisplayAsync(booking);

            Assert.Equal("https://example.com/manual.png", display.QrImageSrc);
        }

        [Fact]
        public async Task BuildDisplayAsync_GeneratesVietQr_WhenBankConfigExists()
        {
            using var context = CreateContext();
            var branch = new Branch { Id = 7, Name = "Quận 7" };
            var room = new Room { Id = 12, Name = "C301", BranchId = 7, Branch = branch };
            var booking = new Booking { Id = 101, Room = room, TotalPrice = 880000 };
            context.Branches.Add(branch);
            context.SystemSettings.AddRange(
                new SystemSetting { SettingKey = "PaymentQr:7:BankCode", SettingValue = "MB", GroupName = "PaymentQr" },
                new SystemSetting { SettingKey = "PaymentQr:7:BankAccountNumber", SettingValue = "123456789", GroupName = "PaymentQr" },
                new SystemSetting { SettingKey = "PaymentQr:7:TransferContentTemplate", SettingValue = "chinhan {BookingId} {BranchName}", GroupName = "PaymentQr" }
            );
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var display = await service.BuildDisplayAsync(booking);

            Assert.Contains("https://img.vietqr.io/image/MB-123456789-compact.png", display.QrImageSrc);
            Assert.Contains("amount=880000", display.QrImageSrc);
            Assert.Equal("chinhan 101 Quận 7", display.TransferContent);
        }

        [Fact]
        public async Task BuildDisplayAsync_ReturnsMissingMessage_WhenNoQrSourceExists()
        {
            using var context = CreateContext();
            var branch = new Branch { Id = 8, Name = "Quận 8" };
            var room = new Room { Id = 13, Name = "D401", BranchId = 8, Branch = branch };
            var booking = new Booking { Id = 102, Room = room, TotalPrice = 550000 };
            context.Branches.Add(branch);
            await context.SaveChangesAsync();
            var service = CreateService(context);

            var display = await service.BuildDisplayAsync(booking);

            Assert.False(display.HasQrImage);
            Assert.Contains("chưa cấu hình", display.MissingQrMessage);
            Assert.Equal(5, display.CountdownMinutes);
        }
```

- [ ] **Step 2: Run the display tests**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter PaymentQrSettingsServiceTests
```

Expected: tests pass. If a Room or Booking required property fails compilation, set the required property to the simplest valid value matching the model definition.

- [ ] **Step 3: Commit**

```bash
git add WebHomestay.Tests/Services/PaymentQrSettingsServiceTests.cs WebHomestay/Services/PaymentQrSettingsService.cs
git commit -m "test: cover payment QR display priority"
```

---

### Task 4: Wire admin controller actions

**Files:**
- Modify: `WebHomestay/Controllers/AdminSettingsController.cs`

- [ ] **Step 1: Modify constructor dependencies**

Change fields and constructor to:

```csharp
        private readonly ApplicationDbContext _context;
        private readonly ISettingService _settingService;
        private readonly IPaymentQrSettingsService _paymentQrSettingsService;
        private readonly IWebHostEnvironment _environment;

        public AdminSettingsController(
            ApplicationDbContext context,
            ISettingService settingService,
            IPaymentQrSettingsService paymentQrSettingsService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _settingService = settingService;
            _paymentQrSettingsService = paymentQrSettingsService;
            _environment = environment;
        }
```

- [ ] **Step 2: Load QR settings in Index**

After `ViewBag.Templates = templates;`, add:

```csharp
                ViewBag.PaymentQrSettings = await _paymentQrSettingsService.GetSettingsForBranchesAsync(branches);
```

- [ ] **Step 3: Add save action**

Add below `UpdateBranch`:

```csharp
        [HttpPost("payment-qr")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePaymentQr(WebHomestay.Models.ViewModels.PaymentQrSettingsViewModel model, IFormFile? qrImage)
        {
            var result = await _paymentQrSettingsService.SaveSettingsAsync(model, qrImage, _environment.WebRootPath);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Index));
        }
```

- [ ] **Step 4: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add WebHomestay/Controllers/AdminSettingsController.cs
git commit -m "feat: add admin payment QR settings action"
```

---

### Task 5: Replace settings AI tab with QR admin UI

**Files:**
- Modify: `WebHomestay/Views/AdminSettings/Index.cshtml`

- [ ] **Step 1: Add typed QR settings variable**

Near the existing Razor variables at the top, after `var templates = ...`, add:

```csharp
    var paymentQrSettings = ViewBag.PaymentQrSettings as Dictionary<int, WebHomestay.Models.ViewModels.PaymentQrSettingsViewModel> ?? new Dictionary<int, WebHomestay.Models.ViewModels.PaymentQrSettingsViewModel>();
```

- [ ] **Step 2: Replace the sidebar tab label**

Replace:

```html
            <button class="nav-link" id="tab-ai" data-bs-toggle="pill" data-bs-target="#panel-ai" type="button" role="tab">
                <i class="fas fa-robot me-3"></i> <span>AI Tư vấn đặt phòng</span>
            </button>
```

with:

```html
            <button class="nav-link" id="tab-payment-qr" data-bs-toggle="pill" data-bs-target="#panel-payment-qr" type="button" role="tab">
                <i class="fas fa-qrcode me-3"></i> <span>Cấu hình mã QR</span>
            </button>
```

- [ ] **Step 3: Show error toast**

After the existing `TempData["SuccessMessage"]` toast block, add:

```cshtml
        @if (TempData["ErrorMessage"] != null)
        {
            <div class="lux-toast-container show" id="temp-toast-error">
                <div class="lux-toast" style="border-left: 4px solid #ef4444;">
                    <div class="lux-toast-content">@TempData["ErrorMessage"]</div>
                </div>
            </div>
        }
```

- [ ] **Step 4: Replace the AI panel with QR panel**

Replace the entire block from:

```html
            <!-- PANEL: AI TEST CHATBOT -->
            <div class="tab-pane fade" id="panel-ai" role="tabpanel">
```

through its matching closing `</div>` at the end of that tab pane with:

```cshtml
            <!-- PANEL: CẤU HÌNH MÃ QR -->
            <div class="tab-pane fade" id="panel-payment-qr" role="tabpanel">
                <div class="panel-header">
                    <h2>Cấu hình mã QR</h2>
                    <p>Thiết lập mã QR thanh toán, nội dung chuyển khoản và thông báo bill riêng cho từng chi nhánh.</p>
                </div>

                <div class="settings-bento-grid">
                    @foreach (var branch in branches)
                    {
                        var qr = paymentQrSettings.ContainsKey(branch.Id)
                            ? paymentQrSettings[branch.Id]
                            : new WebHomestay.Models.ViewModels.PaymentQrSettingsViewModel { BranchId = branch.Id, BranchName = branch.Name };
                        var previewSrc = !string.IsNullOrWhiteSpace(qr.QrImagePath) ? qr.QrImagePath : qr.QrImageUrl;

                        <div class="bento-item full-width">
                            <div class="bento-inner">
                                <div class="d-flex justify-content-between align-items-start gap-3 mb-4">
                                    <div>
                                        <h5 class="bento-title"><i class="fas fa-qrcode me-2 text-primary"></i>@branch.Name</h5>
                                        <p class="text-muted small mb-0">Ưu tiên ảnh upload, sau đó URL QR, cuối cùng tự tạo VietQR từ thông tin ngân hàng.</p>
                                    </div>
                                    <span class="badge rounded-pill text-bg-light border px-3 py-2">Countdown: @qr.CountdownMinutes phút</span>
                                </div>

                                <form asp-action="UpdatePaymentQr" method="post" enctype="multipart/form-data" class="row g-4">
                                    @Html.AntiForgeryToken()
                                    <input type="hidden" name="BranchId" value="@branch.Id" />
                                    <input type="hidden" name="BranchName" value="@branch.Name" />

                                    <div class="col-lg-4">
                                        <div class="bg-white border rounded-4 p-3 text-center h-100">
                                            @if (!string.IsNullOrWhiteSpace(previewSrc))
                                            {
                                                <img src="@previewSrc" alt="QR @branch.Name" class="img-fluid mb-3" style="max-height: 220px;" />
                                            }
                                            else
                                            {
                                                <div class="text-muted small py-5 border rounded-4 bg-light">Chưa có ảnh QR. Nếu nhập ngân hàng và số tài khoản, trang thanh toán sẽ tự tạo VietQR.</div>
                                            }
                                            <label class="form-label small fw-bold text-muted mt-3">Upload ảnh QR</label>
                                            <input type="file" name="qrImage" class="form-control premium-input" accept="image/*" />
                                            @if (!string.IsNullOrWhiteSpace(qr.QrImagePath))
                                            {
                                                <div class="small text-muted mt-2">Ảnh hiện tại: @qr.QrImagePath</div>
                                            }
                                        </div>
                                    </div>

                                    <div class="col-lg-8">
                                        <div class="row g-3">
                                            <div class="col-12">
                                                <label class="form-label small fw-bold text-muted">URL ảnh QR thủ công</label>
                                                <input type="url" name="QrImageUrl" value="@qr.QrImageUrl" class="form-control premium-input" placeholder="https://..." />
                                            </div>
                                            <div class="col-md-4">
                                                <label class="form-label small fw-bold text-muted">Mã ngân hàng</label>
                                                <input type="text" name="BankCode" value="@qr.BankCode" class="form-control premium-input" placeholder="MB, VCB..." />
                                            </div>
                                            <div class="col-md-4">
                                                <label class="form-label small fw-bold text-muted">Số tài khoản</label>
                                                <input type="text" name="BankAccountNumber" value="@qr.BankAccountNumber" class="form-control premium-input" />
                                            </div>
                                            <div class="col-md-4">
                                                <label class="form-label small fw-bold text-muted">Chủ tài khoản</label>
                                                <input type="text" name="BankAccountName" value="@qr.BankAccountName" class="form-control premium-input" />
                                            </div>
                                            <div class="col-md-8">
                                                <label class="form-label small fw-bold text-muted">Mẫu nội dung chuyển khoản</label>
                                                <input type="text" name="TransferContentTemplate" value="@qr.TransferContentTemplate" class="form-control premium-input" />
                                                <div class="form-text">Hỗ trợ: {BookingId}, {BranchName}</div>
                                            </div>
                                            <div class="col-md-4">
                                                <label class="form-label small fw-bold text-muted">Thời gian đếm ngược (phút)</label>
                                                <input type="number" name="CountdownMinutes" value="@qr.CountdownMinutes" min="1" class="form-control premium-input" />
                                            </div>
                                            <div class="col-12">
                                                <label class="form-label small fw-bold text-muted">Thông báo trước khi gửi bill</label>
                                                <textarea name="BeforeBillMessage" rows="2" class="form-control premium-input">@qr.BeforeBillMessage</textarea>
                                            </div>
                                            <div class="col-12">
                                                <label class="form-label small fw-bold text-muted">Thông báo sau khi gửi bill</label>
                                                <textarea name="AfterBillMessage" rows="2" class="form-control premium-input">@qr.AfterBillMessage</textarea>
                                            </div>
                                            <div class="col-12">
                                                <label class="form-label small fw-bold text-muted">Thông báo khi hết thời gian</label>
                                                <textarea name="ExpiredMessage" rows="2" class="form-control premium-input">@qr.ExpiredMessage</textarea>
                                            </div>
                                            <div class="col-12 text-end">
                                                <button type="submit" class="btn-premium rounded-pill px-4">
                                                    <i class="fas fa-save me-2"></i>Lưu cấu hình QR
                                                </button>
                                            </div>
                                        </div>
                                    </div>
                                </form>
                            </div>
                        </div>
                    }
                </div>
            </div>
```

- [ ] **Step 5: Remove unused AI panel JavaScript only if it is exclusively for deleted panel**

Search inside `Index.cshtml` for `sendBrainConsoleMessage`, `loadBrainTraces`, `ai-brain`. Remove functions that only target the deleted `panel-ai`. Do not remove shared AI Brain Center code in other files.

- [ ] **Step 6: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: Razor compilation succeeds.

- [ ] **Step 7: Commit**

```bash
git add WebHomestay/Views/AdminSettings/Index.cshtml
git commit -m "feat: replace settings AI tab with QR settings UI"
```

---

### Task 6: Wire booking payment page to QR display settings

**Files:**
- Modify: `WebHomestay/Controllers/BookingsController.cs`
- Modify: `WebHomestay/Views/Bookings/Success.cshtml`

- [ ] **Step 1: Inject QR settings service into BookingsController**

Add field:

```csharp
        private readonly IPaymentQrSettingsService _paymentQrSettingsService;
```

Update constructor parameters:

```csharp
            IImageMaskingService maskingService,
            IPaymentQrSettingsService paymentQrSettingsService)
```

Set the field after `_maskingService = maskingService;`:

```csharp
            _paymentQrSettingsService = paymentQrSettingsService;
```

- [ ] **Step 2: Set payment display ViewBag in Success**

In `Success(int id)`, before `return View(booking);`, add:

```csharp
            ViewBag.PaymentQrDisplay = await _paymentQrSettingsService.BuildDisplayAsync(booking);
```

- [ ] **Step 3: Add typed display variable to Success view**

At the top of `WebHomestay/Views/Bookings/Success.cshtml`, after `ViewData["Title"] = ...`, add:

```csharp
    var paymentQr = ViewBag.PaymentQrDisplay as WebHomestay.Models.ViewModels.PaymentQrDisplayViewModel
        ?? new WebHomestay.Models.ViewModels.PaymentQrDisplayViewModel
        {
            CountdownMinutes = 5,
            BeforeBillMessage = "Vui lòng tải lên ảnh chụp màn hình bill thanh toán thành công để chúng tôi xác nhận nhanh nhất.",
            AfterBillMessage = "Chúng tôi đã nhận được Bill của bạn. Nhân viên sẽ đối soát và gửi mã phòng qua Email sớm nhất.",
            ExpiredMessage = "Rất tiếc, thời gian giữ chỗ của bạn đã kết thúc. Vui lòng quay lại trang chủ để chọn lại."
        };
```

- [ ] **Step 4: Replace cancelled message**

Replace:

```html
<p class="text-muted mb-4">Rất tiếc, thời gian giữ chỗ của bạn đã kết thúc. Vui lòng quay lại trang chủ để chọn lại.</p>
```

with:

```html
<p class="text-muted mb-4">@paymentQr.ExpiredMessage</p>
```

- [ ] **Step 5: Replace hard-coded QR image block**

Replace lines that render the hard-coded VietQR image:

```html
                                    <img src="https://img.vietqr.io/image/MB-123456789-compact.png?amount=@Model.TotalPrice.ToString("0")&addInfo=STAYEASY%20@Model.Id" 
                                         alt="QR Code" class="img-fluid" style="max-width: 200px;" />
```

with:

```cshtml
                                    @if (paymentQr.HasQrImage)
                                    {
                                        <img src="@paymentQr.QrImageSrc" alt="QR Code" class="img-fluid" style="max-width: 200px;" />
                                    }
                                    else
                                    {
                                        <div class="text-muted small p-4">@paymentQr.MissingQrMessage</div>
                                    }
```

- [ ] **Step 6: Replace hard-coded bank details**

Replace:

```html
<div class="fw-bold">MB BANK (Ngân hàng Quân Đội)</div>
```

with:

```html
<div class="fw-bold">@(string.IsNullOrWhiteSpace(paymentQr.BankCode) ? "Chưa cấu hình" : paymentQr.BankCode)</div>
```

Replace:

```html
<div class="fw-bold fs-5">123456789</div>
```

with:

```html
<div class="fw-bold fs-5">@(string.IsNullOrWhiteSpace(paymentQr.BankAccountNumber) ? "Chưa cấu hình" : paymentQr.BankAccountNumber)</div>
```

Replace:

```html
<div class="fw-bold">NGUYEN VAN A</div>
```

with:

```html
<div class="fw-bold">@(string.IsNullOrWhiteSpace(paymentQr.BankAccountName) ? "Chưa cấu hình" : paymentQr.BankAccountName)</div>
```

Replace:

```html
<div class="fw-bold text-accent fs-5" style="color: var(--luxury-accent);">STAYEASY @Model.Id</div>
```

with:

```html
<div class="fw-bold text-accent fs-5" style="color: var(--luxury-accent);">@paymentQr.TransferContent</div>
```

- [ ] **Step 7: Replace countdown and messages**

Replace:

```html
<div id="countdown" class="display-4 fw-bold mb-3" style="font-family: var(--luxury-font-heading); color: var(--luxury-primary);">05:00</div>
```

with:

```html
<div id="countdown" class="display-4 fw-bold mb-3" style="font-family: var(--luxury-font-heading); color: var(--luxury-primary);">@paymentQr.CountdownMinutes.ToString("00"):00</div>
```

Replace:

```html
<p class="text-muted small mb-4">Vui lòng tải lên ảnh chụp màn hình bill thanh toán thành công để chúng tôi xác nhận nhanh nhất.</p>
```

with:

```html
<p class="text-muted small mb-4">@paymentQr.BeforeBillMessage</p>
```

Replace:

```html
<p class="text-muted small">Chúng tôi đã nhận được Bill của bạn. Nhân viên sẽ đối soát và gửi mã phòng qua Email sớm nhất.</p>
```

with:

```html
<p class="text-muted small">@paymentQr.AfterBillMessage</p>
```

Replace:

```html
<p class="text-muted mb-4">Cảm ơn quý khách đã tin tưởng chinhan. Chúng tôi đang xử lý đơn hàng của bạn.</p>
```

with:

```html
<p class="text-muted mb-4">@paymentQr.AfterBillMessage</p>
```

- [ ] **Step 8: Replace countdown JavaScript duration**

Replace:

```javascript
            var expireAt = new Date(createdAt.getTime() + 5 * 60000);
```

with:

```javascript
            var expireAt = new Date(createdAt.getTime() + @paymentQr.CountdownMinutes * 60000);
```

- [ ] **Step 9: Build**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: build succeeds.

- [ ] **Step 10: Commit**

```bash
git add WebHomestay/Controllers/BookingsController.cs WebHomestay/Views/Bookings/Success.cshtml
git commit -m "feat: use branch QR settings on payment page"
```

---

### Task 7: End-to-end validation

**Files:**
- No source changes expected unless validation finds a bug.

- [ ] **Step 1: Run service tests**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter PaymentQrSettingsServiceTests
```

Expected: pass.

- [ ] **Step 2: Run full test suite**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj
```

Expected: pass. If unrelated existing tests fail, capture exact failing test names and output before deciding whether to fix or report.

- [ ] **Step 3: Build app**

Run:

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: pass.

- [ ] **Step 4: Start the app for manual UI verification**

Run:

```bash
dotnet run --project WebHomestay/WebHomestay.csproj
```

Expected: app starts and prints the local listening URL.

- [ ] **Step 5: Manually verify admin settings UI**

In the browser:

1. Open `/admin/settings`.
2. Confirm the sidebar no longer shows `AI Tư vấn đặt phòng`.
3. Confirm the sidebar shows `Cấu hình mã QR`.
4. Open `Cấu hình mã QR`.
5. For one branch, upload a PNG QR image, set countdown to `6`, set bank fields, set all three messages, and save.
6. Confirm success toast appears.
7. Reopen `/admin/settings` and confirm the saved values are still displayed for that branch.
8. Confirm another branch did not inherit the same values.

- [ ] **Step 6: Manually verify booking payment page**

In the browser:

1. Create a booking for the branch configured in Step 5.
2. Open the booking success/payment page.
3. Confirm the uploaded QR image is displayed.
4. Confirm bank/account/transfer content match the branch settings.
5. Confirm countdown starts from `06:00`.
6. Confirm the before-bill message appears above the upload control.
7. Upload a bill screenshot.
8. Confirm the after-bill message appears on the waiting/confirmation UI.

- [ ] **Step 7: Commit validation fixes if needed**

If manual validation required source changes, commit only those changes:

```bash
git add WebHomestay/Controllers/BookingsController.cs WebHomestay/Views/Bookings/Success.cshtml WebHomestay/Views/AdminSettings/Index.cshtml WebHomestay/Services/PaymentQrSettingsService.cs WebHomestay.Tests/Services/PaymentQrSettingsServiceTests.cs
git commit -m "fix: polish branch QR payment settings flow"
```

If no fixes were needed, do not create an empty commit.

---

## Self-Review Notes

- Spec coverage: The plan covers replacing the AI settings tab, branch-scoped `SystemSettings`, QR upload, manual URL, generated VietQR, countdown default/fallback, before/after/expired messages, and payment page integration.
- Placeholder scan: No `TBD`, `TODO`, or vague implementation-only steps remain. The only placeholder wording is user-facing transfer template support for `{BookingId}` and `{BranchName}`, implemented explicitly in Task 2.
- Type consistency: `PaymentQrSettingsViewModel`, `PaymentQrDisplayViewModel`, `IPaymentQrSettingsService`, and `PaymentQrSettingsService` names are consistent across tasks.
