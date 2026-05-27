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

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static BookingCancellationService CreateService(ApplicationDbContext context, FakeSettingService? settings = null)
    {
        return new BookingCancellationService(context, settings ?? new FakeSettingService(), new FakeMailService(), new FakeWebHostEnvironment());
    }

    private static FormFile CreateImage()
    {
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        return new FormFile(stream, 0, stream.Length, "file", "test.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
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
