using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class BookingCancellationServiceTests
{
    [Fact]
    public async Task GetPolicyAsync_ReturnsDefaults_WhenSettingsAbsent()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var policy = await service.GetPolicyAsync();

        Assert.Equal(24, policy.NoticeHours);
        Assert.Equal(50, policy.RefundPercentBeforeNotice);
        Assert.Equal(0, policy.RefundPercentAfterNotice);
        Assert.Equal("Yêu cầu hủy sẽ được nhân viên kiểm tra và phản hồi qua email.", policy.PolicyMessage);
        Assert.Equal("Manual", policy.HandlingMode);
    }

    [Fact]
    public async Task CreateAsync_WithBookingIdAndMatchingEmailOrPhone_AutoLinksAndStoresSuggestions()
    {
        await using var context = CreateContext();
        context.Bookings.Add(CreateBooking(12, "Khach A", "0900000000", "khach@example.com"));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var request = await service.CreateAsync(new CreateCancellationRequestDto(
            "chat-1", "BOOKING-12", "Khach A", "0900000000", "KHACH@example.com",
            CreateImage(), null, null, null, null));

        Assert.Equal(12, request.BookingId);
        Assert.Contains(12, JsonSerializer.Deserialize<List<int>>(request.SuggestedBookingIdsJson!)!);
    }

    [Fact]
    public async Task CreateAsync_WithoutBookingId_SuggestsMatchingBookingsByEmailPhoneName()
    {
        await using var context = CreateContext();
        context.Bookings.AddRange(
            CreateBooking(21, "Nguyen Van A", "0900000001", "a@example.com"),
            CreateBooking(22, "Tran Thi B", "0900000002", "b@example.com"),
            CreateBooking(23, "Le Van C", "0900000003", "c@example.com"));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var request = await service.CreateAsync(new CreateCancellationRequestDto(
            "chat-2", null, "Nguyen Van A", "0900000002", "a@example.com",
            CreateImage(), null, null, null, null));

        var suggestedIds = JsonSerializer.Deserialize<List<int>>(request.SuggestedBookingIdsJson!)!;
        Assert.Null(request.BookingId);
        Assert.Contains(21, suggestedIds);
        Assert.Contains(22, suggestedIds);
    }

    [Fact]
    public async Task ApproveAsync_InAutoMode_CancelsBookingAndReleasesHourlySlot()
    {
        await using var context = CreateContext();
        context.RoomSlotInventories.Add(CreateSlot(5, "Booked"));
        context.Bookings.Add(CreateBooking(31, "Khach", "090", "k@example.com", BookingMode.Hourly, 5, "Confirmed"));
        context.BookingCancellationRequests.Add(CreateRequest(41, 31));
        await context.SaveChangesAsync();
        var service = CreateService(context, new FakeSettingService { Strings = { ["CancellationHandlingMode"] = "Auto" } });

        await service.ApproveAsync(new ProcessCancellationDto(41, "Đủ điều kiện hoàn tiền", 50, "admin", CreateImage()));

        Assert.Equal("Cancelled", (await context.Bookings.FindAsync(31))!.Status);
        Assert.Equal("Available", (await context.RoomSlotInventories.FindAsync(5))!.Status);
    }

    [Fact]
    public async Task ApproveAsync_InManualMode_DoesNotChangeBookingOrSlotStatus()
    {
        await using var context = CreateContext();
        context.RoomSlotInventories.Add(CreateSlot(6, "Booked"));
        context.Bookings.Add(CreateBooking(32, "Khach", "090", "k@example.com", BookingMode.Hourly, 6, "Confirmed"));
        context.BookingCancellationRequests.Add(CreateRequest(42, 32));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await service.ApproveAsync(new ProcessCancellationDto(42, "Xử lý thủ công", 50, "admin", CreateImage()));

        Assert.Equal("Confirmed", (await context.Bookings.FindAsync(32))!.Status);
        Assert.Equal("Booked", (await context.RoomSlotInventories.FindAsync(6))!.Status);
    }

    [Fact]
    public async Task RejectAsync_DoesNotChangeBookingStatus()
    {
        await using var context = CreateContext();
        context.Bookings.Add(CreateBooking(33, "Khach", "090", "k@example.com", status: "Confirmed"));
        context.BookingCancellationRequests.Add(CreateRequest(43, 33));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await service.RejectAsync(new ProcessCancellationDto(43, "Không đủ điều kiện", 0, "admin", null));

        Assert.Equal("Confirmed", (await context.Bookings.FindAsync(33))!.Status);
    }

    [Fact]
    public async Task ApproveAsync_FailsWithoutRefundBillProof()
    {
        await using var context = CreateContext();
        context.Bookings.Add(CreateBooking(34, "Khach", "090", "k@example.com"));
        context.BookingCancellationRequests.Add(CreateRequest(44, 34));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveAsync(new ProcessCancellationDto(44, "Đủ điều kiện", 50, "admin", null)));
    }

    [Fact]
    public async Task ApproveAsync_AndRejectAsync_RequireStaffReason()
    {
        await using var context = CreateContext();
        context.Bookings.Add(CreateBooking(35, "Khach", "090", "k@example.com"));
        context.BookingCancellationRequests.AddRange(CreateRequest(45, 35), CreateRequest(46, 35));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveAsync(new ProcessCancellationDto(45, " ", 50, "admin", CreateImage())));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejectAsync(new ProcessCancellationDto(46, " ", 0, "admin", null)));
    }

    [Fact]
    public async Task CreateAsync_StoresUploadsUnderProtectedLocationWithGuidNameAndDetectedExtension()
    {
        await using var context = CreateContext();
        var env = new FakeWebHostEnvironment();
        var service = CreateService(context, env: env);

        var request = await service.CreateAsync(new CreateCancellationRequestDto(
            "chat-uploads", "B-99", "Khach", "090", "k@example.com",
            CreateImage(PngBytes(), "../../evil.exe", "image/png"), null, null, null, null));

        var uploadsRoot = Path.Combine(env.ContentRootPath, "App_Data", "SecureUploads", "Cancellations", "confirmation");
        Assert.StartsWith(uploadsRoot, request.ConfirmationEmailProofPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(env.WebRootPath, request.ConfirmationEmailProofPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".png", request.ConfirmationEmailProofPath);
        Assert.Matches(@"^[0-9a-f]{32}\.png$", Path.GetFileName(request.ConfirmationEmailProofPath));
        Assert.DoesNotContain("evil", request.ConfirmationEmailProofPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_RejectsOversizedImage()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var tooLarge = CreateImage(new byte[BookingCancellationService.MaxImageBytes + 1], "large.png", "image/png");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new CreateCancellationRequestDto(
            "chat-large", null, "Khach", "090", "k@example.com", tooLarge, null, null, null, null)));
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidContentType()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new CreateCancellationRequestDto(
            "chat-type", null, "Khach", "090", "k@example.com",
            CreateImage(PngBytes(), "proof.png", "text/plain"), null, null, null, null)));
    }

    [Fact]
    public async Task CreateAsync_RejectsSpoofedImageContent()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new CreateCancellationRequestDto(
            "chat-spoof", null, "Khach", "090", "k@example.com",
            CreateImage(new byte[] { 1, 2, 3, 4 }, "proof.png", "image/png"), null, null, null, null)));
    }

    [Theory]
    [InlineData("image/png", ".png")]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/gif", ".gif")]
    [InlineData("image/webp", ".webp")]
    public async Task CreateAsync_AcceptsValidImageSignatures(string contentType, string expectedExtension)
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var request = await service.CreateAsync(new CreateCancellationRequestDto(
            $"chat-{expectedExtension}", null, "Khach", "090", "k@example.com",
            CreateImage(ImageBytes(contentType), $"proof{expectedExtension}", contentType), null, null, null, null));

        Assert.EndsWith(expectedExtension, request.ConfirmationEmailProofPath);
    }

    [Fact]
    public async Task CreateAsync_TrimsValuesAndRejectsOverMaxLengths()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var request = await service.CreateAsync(new CreateCancellationRequestDto(
            " chat-trim ", " BOOK-1 ", " Khach ", " 090 ", " k@example.com ",
            CreateImage(), null, " Bank ", " 123 ", " Holder "));

        Assert.Equal("chat-trim", request.ChatSessionId);
        Assert.Equal("BOOK-1", request.SubmittedBookingCode);
        Assert.Equal("Khach", request.CustomerName);
        Assert.Equal("090", request.CustomerPhone);
        Assert.Equal("k@example.com", request.CustomerEmail);
        Assert.Equal("Bank", request.RefundBankName);
        Assert.Equal("123", request.RefundBankAccountNumber);
        Assert.Equal("Holder", request.RefundBankAccountHolder);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new CreateCancellationRequestDto(
            new string('c', 101), null, "Khach", "090", "k@example.com", CreateImage(), null, null, null, null)));
    }

    [Fact]
    public async Task ApproveAsync_RejectsOverMaxStaffFields()
    {
        await using var context = CreateContext();
        context.Bookings.Add(CreateBooking(36, "Khach", "090", "k@example.com"));
        context.BookingCancellationRequests.Add(CreateRequest(47, 36));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveAsync(new ProcessCancellationDto(47, new string('r', 2001), 50, "admin", CreateImage())));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveAsync(new ProcessCancellationDto(47, "Hợp lệ", 50, new string('a', 101), CreateImage())));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static BookingCancellationService CreateService(ApplicationDbContext context, FakeSettingService? settings = null, FakeWebHostEnvironment? env = null)
    {
        return new BookingCancellationService(context, settings ?? new FakeSettingService(), new FakeMailService(), env ?? new FakeWebHostEnvironment());
    }

    private static FormFile CreateImage()
    {
        return CreateImage(PngBytes(), "test.png", "image/png");
    }

    private static FormFile CreateImage(byte[] bytes, string fileName, string contentType)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
    }

    private static byte[] ImageBytes(string contentType)
    {
        return contentType switch
        {
            "image/png" => PngBytes(),
            "image/jpeg" => new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 },
            "image/gif" => "GIF89a"u8.ToArray(),
            "image/webp" => new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0x04, 0x00, 0x00, 0x00, (byte)'W', (byte)'E', (byte)'B', (byte)'P' },
            _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, null)
        };
    }

    private static byte[] PngBytes()
    {
        return new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
    }

    private static Booking CreateBooking(int id, string name, string phone, string email, BookingMode mode = BookingMode.Daily, int? slotId = null, string status = "Confirmed")
    {
        return new Booking
        {
            Id = id,
            RoomId = 1,
            CustomerName = name,
            CustomerPhone = phone,
            CustomerEmail = email,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(2),
            Status = status,
            BookingMode = mode,
            RoomSlotInventoryId = slotId,
            CreatedAt = DateTime.UtcNow.AddMinutes(id)
        };
    }

    private static RoomSlotInventory CreateSlot(int id, string status)
    {
        return new RoomSlotInventory
        {
            Id = id,
            RoomId = 1,
            TemplateId = 1,
            SlotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            SlotLabel = "09:00-11:00",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = status
        };
    }

    private static BookingCancellationRequest CreateRequest(int id, int bookingId)
    {
        return new BookingCancellationRequest
        {
            Id = id,
            ChatSessionId = $"chat-{id}",
            BookingId = bookingId,
            CustomerName = "Khach",
            CustomerPhone = "090",
            CustomerEmail = "k@example.com",
            ConfirmationEmailProofPath = "proof.png",
            Status = "Pending",
            RefundStatus = "NotRefunded",
            PolicyMessageSnapshot = "Chính sách hủy"
        };
    }

    private class FakeSettingService : ISettingService
    {
        public Dictionary<string, string> Strings { get; } = new();
        public Dictionary<string, int> Ints { get; } = new();

        public Task<string> GetStringAsync(string key, string defaultValue = "") => Task.FromResult(Strings.TryGetValue(key, out var value) ? value : defaultValue);
        public Task<int> GetIntAsync(string key, int defaultValue = 0) => Task.FromResult(Ints.TryGetValue(key, out var value) ? value : defaultValue);
        public Task UpdateSettingAsync(string key, string value)
        {
            Strings[key] = value;
            return Task.CompletedTask;
        }
    }

    private class FakeMailService : IMailService
    {
        public Task SendEmailAsync(string toEmail, string subject, string body) => Task.CompletedTask;
    }

    private class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "WebHomestay.Tests";
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "webhomestay-tests", Guid.NewGuid().ToString());
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
