using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;
using Xunit;

namespace WebHomestay.Tests.Services;

public class ContextAwareBookingConductorTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static IMemoryCache CreateCache()
    {
        return new MemoryCache(new MemoryCacheOptions());
    }

    private static ContextAwareBookingConductor CreateConductor(ApplicationDbContext context, IMemoryCache cache)
    {
        var scopeFactory = new FakeServiceScopeFactory(context);
        return new ContextAwareBookingConductor(context, cache, scopeFactory);
    }

    private static void SeedSetting(ApplicationDbContext context, string key, string value)
    {
        context.SystemSettings.Add(new SystemSetting
        {
            SettingKey = key,
            SettingValue = value,
            GroupName = "AI",
            LastUpdated = DateTime.UtcNow
        });
        context.SaveChanges();
    }

    private static AIBrainChatRequest MakeRequest(string message, int? branchId = null, DateTime? startTime = null, int guestCount = 0)
    {
        return new AIBrainChatRequest
        {
            SessionId = "test-session",
            Message = message,
            BranchId = branchId,
            StartTime = startTime,
            GuestCount = guestCount,
            Mode = ChatMode.PublicBooking
        };
    }

    [Fact]
    public async Task BookingIntent_NoBranch_ReturnsAskInfo()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        var conductor = CreateConductor(context, cache);
        var request = MakeRequest("đặt phòng", guestCount: 2);

        var result = await conductor.DecideAsync("s1", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.AskInfo, result.Action);
    }

    [Fact]
    public async Task BookingIntent_WithFullInfo_ReturnsShowRooms()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        SeedSetting(context, "AIPublicBookingTriggerWords", "đặt,book");
        SeedSetting(context, "AIPublicBookingAutoShowRooms", "true");
        SeedSetting(context, "AIPublicBookingMaxRoomShows", "2");
        SeedSetting(context, "AIPublicBookingRoomCooldown", "3");
        SeedSetting(context, "AIPublicBookingProactiveMode", "balanced");
        var conductor = CreateConductor(context, cache);
        var request = MakeRequest("đặt phòng", branchId: 1, startTime: DateTime.Today.AddDays(1), guestCount: 2);

        var result = await conductor.DecideAsync("s2", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.ShowRooms, result.Action);
    }

    [Fact]
    public async Task PolicyQuestion_ReturnsReply()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        var conductor = CreateConductor(context, cache);
        var request = MakeRequest("chính sách hủy phòng thế nào?", branchId: 1, startTime: DateTime.Today.AddDays(1), guestCount: 2);

        var result = await conductor.DecideAsync("s3", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.Reply, result.Action);
        Assert.Empty(result.UiBlocks);
    }

    [Fact]
    public async Task ExitIntent_ClearsProgress()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        var conductor = CreateConductor(context, cache);
        SeedSetting(context, "AIPublicBookingExitKeywords", "thôi,bỏ,khác,xóa,hủy,không,để sau");

        // Set up state with progress
        var container = new BookingSessionContainer
        {
            Confirmed = new BookingConfirmedState { BranchId = 1, GuestCount = 2, BookingMode = "hourly" },
            Progress = new BookingProgressState { SelectedRoomId = 5 }
        };
        cache.Set("ai-booking-conductor:s4", container, TimeSpan.FromMinutes(30));

        var request = MakeRequest("thôi bỏ đi", branchId: 1, startTime: DateTime.Today.AddDays(1), guestCount: 2);

        var result = await conductor.DecideAsync("s4", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.Reply, result.Action);
        Assert.Null(result.State.Progress);
        Assert.NotNull(result.State.Confirmed.BranchId); // confirmed preserved
    }

    [Fact]
    public async Task ShowRooms_ExceedsMax_ReturnsReply()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        SeedSetting(context, "AIPublicBookingAutoShowRooms", "true");
        SeedSetting(context, "AIPublicBookingMaxRoomShows", "1");
        SeedSetting(context, "AIPublicBookingProactiveMode", "balanced");
        var conductor = CreateConductor(context, cache);
        var request = MakeRequest("xem phòng", branchId: 1, startTime: DateTime.Today.AddDays(1), guestCount: 2);

        // First call: show rooms (count 0 -> 1)
        var result1 = await conductor.DecideAsync("s5", request.Message, request, CancellationToken.None);
        Assert.Equal(ConductorAction.ShowRooms, result1.Action);

        // Second call: show rooms would be count 1 -> refused (max=1)
        var result2 = await conductor.DecideAsync("s5", request.Message, request, CancellationToken.None);
        Assert.Equal(ConductorAction.Reply, result2.Action);
    }

    [Fact]
    public async Task OffTopicMessage_ReturnsReply()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        var conductor = CreateConductor(context, cache);
        var request = MakeRequest("cảm ơn bạn", branchId: 1, startTime: DateTime.Today.AddDays(1), guestCount: 2);

        var result = await conductor.DecideAsync("s6", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.Reply, result.Action);
    }

    [Fact]
    public async Task HandleAction_SelectRoom_ReturnsShowSlots()
    {
        using var context = CreateContext();
        var cache = CreateCache();

        var slotDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        context.RoomSlotInventories.Add(new RoomSlotInventory
        {
            RoomId = 5,
            SlotDate = slotDate,
            SlotLabel = "09:00-11:00",
            StartTime = slotDate.ToDateTime(TimeOnly.Parse("09:00")),
            EndTime = slotDate.ToDateTime(TimeOnly.Parse("11:00")),
            Status = "Available"
        });
        context.SaveChanges();

        var conductor = CreateConductor(context, cache);

        // Pre-set confirmed state with date
        var container = new BookingSessionContainer
        {
            Confirmed = new BookingConfirmedState
            {
                BranchId = 1,
                HourlyDate = slotDate,
                GuestCount = 2,
                BookingMode = "hourly"
            }
        };
        cache.Set("ai-booking-conductor:s7", container, TimeSpan.FromMinutes(30));

        var actionRequest = new BookingActionRequest
        {
            SessionId = "s7",
            Action = "select-room",
            RoomId = 5
        };

        var result = await conductor.HandleActionAsync(actionRequest, CancellationToken.None);

        Assert.Equal(ConductorAction.ShowSlots, result.Action);
        Assert.NotEmpty(result.UiBlocks);
    }
}
