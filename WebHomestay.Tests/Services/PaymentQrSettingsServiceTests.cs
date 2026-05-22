using Microsoft.EntityFrameworkCore;
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

        [Fact]
        public async Task GetSettingsAsync_ReturnsDefaults_WhenBranchHasNoSettings()
        {
            using var context = CreateContext();
            var service = new PaymentQrSettingsService(context);

            var settings = await service.GetSettingsAsync(7, "Quận 1");

            Assert.Equal(7, settings.BranchId);
            Assert.Equal("Quận 1", settings.BranchName);
            Assert.Equal(5, settings.CountdownMinutes);
            Assert.Equal("STAYLUXE {BookingId}", settings.TransferContentTemplate);
            Assert.Contains("bill thanh toán", settings.BeforeBillMessage);
        }

        [Fact]
        public async Task SaveSettingsAsync_PersistsBranchScopedSettings()
        {
            using var context = CreateContext();
            context.Branches.Add(new Branch { Id = 3, Name = "Quận 3" });
            await context.SaveChangesAsync();
            var service = new PaymentQrSettingsService(context);

            var result = await service.SaveSettingsAsync(new PaymentQrSettingsViewModel
            {
                BranchId = 3,
                BranchName = "Quận 3",
                QrImageUrl = "https://example.com/qr.png",
                BankCode = "MB",
                BankAccountNumber = "123456789",
                BankAccountName = "STAYLUXE",
                TransferContentTemplate = "STAYLUXE {BookingId} {BranchName}",
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
            Assert.Equal("STAYLUXE", settings.BankAccountName);
            Assert.Equal("STAYLUXE {BookingId} {BranchName}", settings.TransferContentTemplate);
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
            var service = new PaymentQrSettingsService(context);

            var result = await service.SaveSettingsAsync(new PaymentQrSettingsViewModel
            {
                BranchId = 4,
                CountdownMinutes = 0
            }, null, "");

            Assert.False(result.Success);
            Assert.Contains("Thời gian", result.Message);
        }

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
            var service = new PaymentQrSettingsService(context);

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
            var service = new PaymentQrSettingsService(context);

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
                new SystemSetting { SettingKey = "PaymentQr:7:TransferContentTemplate", SettingValue = "STAYLUXE {BookingId} {BranchName}", GroupName = "PaymentQr" }
            );
            await context.SaveChangesAsync();
            var service = new PaymentQrSettingsService(context);

            var display = await service.BuildDisplayAsync(booking);

            Assert.Contains("https://img.vietqr.io/image/MB-123456789-compact.png", display.QrImageSrc);
            Assert.Contains("amount=880000", display.QrImageSrc);
            Assert.Equal("STAYLUXE 101 Quận 7", display.TransferContent);
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
            var service = new PaymentQrSettingsService(context);

            var display = await service.BuildDisplayAsync(booking);

            Assert.False(display.HasQrImage);
            Assert.Contains("chưa cấu hình", display.MissingQrMessage);
            Assert.Equal(5, display.CountdownMinutes);
        }
    }
}
