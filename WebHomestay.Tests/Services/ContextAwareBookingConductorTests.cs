using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;
using WebHomestay.Services;
using Xunit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using WebHomestay.Hubs;
using WebHomestay.Services.AI;
using Moq;
using AdminAIStudioConfigResponse = WebHomestay.Models.AI.AdminAIStudioConfigResponse;
using AdminAIHandoffConfig = WebHomestay.Models.AI.AdminAIHandoffConfig;
using AdminAIConversationFlow = WebHomestay.Models.AI.AdminAIConversationFlow;

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

    private static ContextAwareBookingConductor CreateConductor(
        ApplicationDbContext context,
        IMemoryCache cache,
        Action<Moq.Mock<WebHomestay.Services.AI.IEntityExtractorService>>? configureExtractor = null,
        Action<ServiceCollection>? configureServices = null,
        IAdminChatService? adminChatService = null)
    {
        var scopeFactory = new FakeServiceScopeFactory(context, configureServices);
        var bookingCreation = new FakeBookingCreationService(context);
        
        var mockAdminChat = new Moq.Mock<IAdminChatService>();
        mockAdminChat.Setup(service => service.HasBranchPromptBeenShownAsync(Moq.It.IsAny<string>()))
            .ReturnsAsync(false);
        mockAdminChat.Setup(service => service.MarkBranchPromptShownAsync(Moq.It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        mockAdminChat.Setup(service => service.GetUnreadCustomerMessageCountAsync(Moq.It.IsAny<int?>()))
            .ReturnsAsync(0);
        var adminChat = adminChatService ?? mockAdminChat.Object;
        
        var mockExtractor = new Moq.Mock<WebHomestay.Services.AI.IEntityExtractorService>();
        mockExtractor.Setup(e => e.ExtractDateTime(Moq.It.IsAny<string>())).Returns((null, null));
        mockExtractor.Setup(e => e.ExtractGuestCount(Moq.It.IsAny<string>())).Returns((int?)null);
        configureExtractor?.Invoke(mockExtractor);

        var aiClient = new FakeAIModelClient();
        var intentClassifier = new WebHomestay.Services.AI.LLMIntentClassifier(aiClient);
        var explanationService = new PublicBookingRoomExplanationService(context);
        var pricingService = new PricingService(context);

        return new ContextAwareBookingConductor(
            context, 
            cache, 
            scopeFactory, 
            bookingCreation, 
            adminChat, 
            intentClassifier, 
            mockExtractor.Object,
            explanationService,
            pricingService);
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

    private static void SeedRoom(ApplicationDbContext context, int id, int branchId, int capacity, int maxGuests, decimal extraGuestFee)
    {
        context.Rooms.Add(new Room
        {
            Id = id,
            BranchId = branchId,
            Name = $"Room {id}",
            PricePerHour = 200000m,
            PricePerDay = 1200000m,
            Capacity = capacity,
            MaxGuests = maxGuests,
            ExtraGuestFee = extraGuestFee,
            Status = "Available"
        });
        context.SaveChanges();
    }

    private static void SeedHourlySlot(ApplicationDbContext context, int roomId, DateOnly slotDate, string start, string end)
    {
        context.RoomSlotInventories.Add(new RoomSlotInventory
        {
            RoomId = roomId,
            SlotDate = slotDate,
            SlotLabel = $"{start}-{end}",
            StartTime = slotDate.ToDateTime(TimeOnly.Parse(start)),
            EndTime = slotDate.ToDateTime(TimeOnly.Parse(end)),
            Status = "Available"
        });
        context.SaveChanges();
    }

    private static AIBrainChatRequest MakeRequest(string message, int? branchId = null, DateTime? startTime = null, int guestCount = 0, string bookingMode = "unknown")
    {
        return new AIBrainChatRequest
        {
            SessionId = "test-session",
            Message = message,
            BranchId = branchId,
            StartTime = startTime,
            GuestCount = guestCount,
            BookingMode = bookingMode,
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
    public async Task SemanticStyleRoomRequest_WithoutRequiredInfo_ReturnsAskInfoWithBranchSelector()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        var conductor = CreateConductor(context, cache);
        var request = MakeRequest("tui cần tìm 1 phòng lãng mạn");

        var result = await conductor.DecideAsync("semantic-style-1", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.AskInfo, result.Action);
        Assert.Contains(result.UiBlocks, block => JsonSerializer.Serialize(block).Contains("branchSelector"));
    }

    [Fact]
    public async Task DailyNaturalLanguage_MissingBranch_ReturnsAskInfoWithBranchSelector()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        var targetDate = DateTime.Today.AddDays(1);
        var conductor = CreateConductor(context, cache, extractor =>
        {
            extractor.Setup(e => e.ExtractGuestCount("đi 3 người 14-16/6")).Returns(3);
            extractor.Setup(e => e.ExtractDateTime("đi 3 người 14-16/6"))
                .Returns((targetDate, null));
        });

        var request = MakeRequest("đi 3 người 14-16/6");
        var result = await conductor.DecideAsync("gate-1", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.AskInfo, result.Action);
        Assert.Contains(result.UiBlocks, block => JsonSerializer.Serialize(block).Contains("branchSelector"));
    }

    [Fact]
    public async Task NaturalLanguageBranch_BindsWithoutExactBranchName()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        context.Branches.Add(new Branch
        {
            Id = 5,
            Name = "LumiStay Binh Duong City",
            Address = "Binh Duong"
        });
        SeedRoom(context, id: 61, branchId: 5, capacity: 2, maxGuests: 3, extraGuestFee: 100000m);
        var conductor = CreateConductor(context, cache, extractor =>
        {
            extractor.Setup(e => e.ExtractDateTime("14/6 có phòng nào ở chi nhánh bình dương k"))
                .Returns((new DateTime(2026, 6, 14), null));
        });

        var request = MakeRequest("14/6 có phòng nào ở chi nhánh bình dương k", guestCount: 2);
        var result = await conductor.DecideAsync("branch-ctx-1", request.Message, request, CancellationToken.None);

        Assert.NotEqual(ConductorAction.AskInfo, result.Action);
        Assert.Equal(5, result.State.Confirmed.BranchId);
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
        var request = MakeRequest("đặt phòng", branchId: 1, startTime: DateTime.Today.AddDays(1), guestCount: 2, bookingMode: "hourly");

        var result = await conductor.DecideAsync("s2", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.ShowRooms, result.Action);
    }

    [Fact]
    public async Task ShowRooms_FiltersOutRoomsBeyondMaxGuests_ButKeepsExtraGuestRooms()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        SeedSetting(context, "AIPublicBookingTriggerWords", "đặt,book");
        SeedSetting(context, "AIPublicBookingAutoShowRooms", "true");
        SeedSetting(context, "AIPublicBookingMaxRoomShows", "2");
        SeedSetting(context, "AIPublicBookingRoomCooldown", "3");
        SeedSetting(context, "AIPublicBookingProactiveMode", "balanced");
        SeedRoom(context, id: 1, branchId: 1, capacity: 2, maxGuests: 3, extraGuestFee: 120000m);
        SeedRoom(context, id: 2, branchId: 1, capacity: 2, maxGuests: 2, extraGuestFee: 0m);

        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
        SeedHourlySlot(context, roomId: 1, slotDate: targetDate, start: "08:00", end: "10:00");

        var conductor = CreateConductor(context, cache);

        var request = MakeRequest("đặt phòng", branchId: 1, startTime: DateTime.Today.AddDays(2), guestCount: 3, bookingMode: "hourly");
        var result = await conductor.DecideAsync("gate-2", request.Message, request, CancellationToken.None);

        var json = JsonSerializer.Serialize(result.UiBlocks);
        Assert.Equal(ConductorAction.ShowRooms, result.Action);
        Assert.Contains("\"roomId\":1", json);
        Assert.DoesNotContain("\"roomId\":2", json);
    }

    [Fact]
    public async Task ShowRooms_HourlyDateOnly_ShowsOnlyRoomsWithAnyAvailableSlots()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        SeedSetting(context, "AIPublicBookingAutoShowRooms", "true");
        SeedSetting(context, "AIPublicBookingMaxRoomShows", "2");
        SeedSetting(context, "AIPublicBookingProactiveMode", "balanced");
        SeedRoom(context, id: 21, branchId: 1, capacity: 2, maxGuests: 3, extraGuestFee: 120000m);
        SeedRoom(context, id: 22, branchId: 1, capacity: 2, maxGuests: 3, extraGuestFee: 120000m);
        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        SeedHourlySlot(context, roomId: 21, slotDate: targetDate, start: "08:00", end: "10:00");
        var conductor = CreateConductor(context, cache, extractor =>
        {
            extractor.Setup(e => e.ExtractGuestCount(Moq.It.IsAny<string>())).Returns(3);
            extractor.Setup(e => e.ExtractDateTime("14/6, 3 người ạ")).Returns((targetDate.ToDateTime(TimeOnly.MinValue), null));
        });

        var request = MakeRequest("14/6, 3 người ạ", branchId: 1, startTime: targetDate.ToDateTime(TimeOnly.MinValue), guestCount: 3, bookingMode: "hourly");
        var result = await conductor.DecideAsync("gate-hourly-rooms", request.Message, request, CancellationToken.None);

        var json = JsonSerializer.Serialize(result.UiBlocks);
        Assert.Equal(ConductorAction.ShowRooms, result.Action);
        Assert.Contains("\"roomId\":21", json);
        Assert.DoesNotContain("\"roomId\":22", json);
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
        var request = MakeRequest("xem phòng", branchId: 1, startTime: DateTime.Today.AddDays(1), guestCount: 2, bookingMode: "hourly");

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
    public async Task HandleAction_SelectRoom_HourlyFlowShowsSlotsImmediately_AndCommitStillWorks()
    {
        using var context = CreateContext();
        var cache = CreateCache();

        var slotDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        context.Rooms.Add(new Room
        {
            Id = 5,
            BranchId = 1,
            Name = "Sông Xanh DN-401",
            Description = "Phòng rộng",
            PricePerHour = 230000m,
            PricePerDay = 1650000m,
            Capacity = 4,
            MaxGuests = 5,
            Status = "Available"
        });
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

        var commitRequest = new BookingActionRequest
        {
            SessionId = "s7",
            Action = "commit-room",
            RoomId = 5
        };

        var commitResult = await conductor.HandleActionAsync(commitRequest, CancellationToken.None);

        Assert.Equal(ConductorAction.ShowSlots, commitResult.Action);
        Assert.NotEmpty(commitResult.UiBlocks);
    }

    [Fact]
    public async Task SelectRoom_HourlyConsult_ReturnsSummaryAndSlots()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        var slotDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        context.Rooms.Add(new Room
        {
            Id = 70,
            BranchId = 1,
            Name = "An Nhien BD-101",
            PricePerHour = 110000m,
            PricePerDay = 750000m,
            Capacity = 2,
            MaxGuests = 3,
            ExtraGuestFee = 80000m,
            Status = "Available"
        });
        SeedHourlySlot(context, roomId: 70, slotDate: slotDate, start: "08:00", end: "10:00");
        var conductor = CreateConductor(context, cache);
        cache.Set("ai-booking-conductor:consult-hourly", new BookingSessionContainer
        {
            Confirmed = new BookingConfirmedState
            {
                BranchId = 1,
                HourlyDate = slotDate,
                GuestCount = 3,
                BookingMode = "hourly"
            }
        }, TimeSpan.FromMinutes(30));

        var result = await conductor.HandleActionAsync(new BookingActionRequest
        {
            SessionId = "consult-hourly",
            Action = "select-room",
            RoomId = 70,
            GuestCount = 3,
            BranchId = 1,
            BookingMode = "hourly",
            HourlyDate = slotDate.ToString("yyyy-MM-dd")
        }, CancellationToken.None);

        var json = JsonSerializer.Serialize(result.UiBlocks);
        Assert.Equal(ConductorAction.ShowSlots, result.Action);
        Assert.Contains("bookingSummary", json);
        Assert.Contains("hourlySlots", json);
        Assert.Contains("roomDecisionCta", json);
        Assert.Contains("showCommitButton", json);
    }

    [Fact]
    public async Task SelectRoom_DailyConsult_ReturnsTotalPriceAndDecisionBlock()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        context.Rooms.Add(new Room
        {
            Id = 71,
            BranchId = 1,
            Name = "An Nhien BD-102",
            PricePerHour = 110000m,
            PricePerDay = 750000m,
            PriceWeekendPerDay = 900000m,
            Capacity = 2,
            MaxGuests = 3,
            ExtraGuestFee = 100000m,
            Status = "Available"
        });
        context.SaveChanges();

        var conductor = CreateConductor(context, cache);
        cache.Set("ai-booking-conductor:consult-daily", new BookingSessionContainer
        {
            Confirmed = new BookingConfirmedState
            {
                BranchId = 1,
                CheckInDate = new DateOnly(2026, 6, 13),
                CheckOutDate = new DateOnly(2026, 6, 15),
                GuestCount = 3,
                BookingMode = "daily"
            }
        }, TimeSpan.FromMinutes(30));

        var result = await conductor.HandleActionAsync(new BookingActionRequest
        {
            SessionId = "consult-daily",
            Action = "select-room",
            RoomId = 71,
            GuestCount = 3,
            BranchId = 1,
            BookingMode = "daily",
            CheckInDate = "2026-06-13",
            CheckOutDate = "2026-06-15"
        }, CancellationToken.None);

        var json = JsonSerializer.Serialize(result.UiBlocks);
        Assert.Equal(ConductorAction.AskInfo, result.Action);
        Assert.Contains("bookingSummary", json);
        Assert.Contains("roomDecisionCta", json);
        Assert.Contains("\"totalPrice\":1800000", json);
    }

    [Fact]
    public async Task ConfirmDates_WithSelectedRoom_ReturnsConsultReviewInsteadOfDirectCheckout()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        var slotDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        context.Rooms.Add(new Room
        {
            Id = 72,
            BranchId = 1,
            Name = "An Nhien DN-101",
            PricePerHour = 110000m,
            PricePerDay = 750000m,
            Capacity = 2,
            MaxGuests = 3,
            ExtraGuestFee = 80000m,
            Status = "Available"
        });
        SeedHourlySlot(context, roomId: 72, slotDate: slotDate, start: "08:00", end: "10:00");
        var conductor = CreateConductor(context, cache);
        cache.Set("ai-booking-conductor:confirm-room", new BookingSessionContainer
        {
            Confirmed = new BookingConfirmedState
            {
                BranchId = 1,
                GuestCount = 1,
                BookingMode = "hourly"
            },
            Progress = new BookingProgressState
            {
                SelectedRoomId = 72,
                ActiveRoomContextId = 72
            }
        }, TimeSpan.FromMinutes(30));

        var result = await conductor.HandleActionAsync(new BookingActionRequest
        {
            SessionId = "confirm-room",
            Action = "confirm-dates",
            BookingMode = "hourly",
            CheckInDate = slotDate.ToString("yyyy-MM-dd"),
            GuestCount = 1
        }, CancellationToken.None);

        var json = JsonSerializer.Serialize(result.UiBlocks);
        Assert.Equal(ConductorAction.ShowSlots, result.Action);
        Assert.Contains("bookingSummary", json);
        Assert.Contains("roomDecisionCta", json);
        Assert.Contains("hourlySlots", json);
        Assert.DoesNotContain("bookingCta", json);
    }

    [Fact]
    public async Task ConfirmDates_NoSelectedRoom_NoAvailableRooms_ReturnsAskInfoAndPicker()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        
        context.Branches.Add(new Branch { Id = 1, Name = "LumiStay Sai Gon" });
        context.SaveChanges();
        
        var conductor = CreateConductor(context, cache);
        var container = new BookingSessionContainer
        {
            Confirmed = new BookingConfirmedState
            {
                BranchId = 1,
                BookingMode = "hourly",
                GuestCount = 1
            }
        };
        cache.Set("ai-booking-conductor:no-rooms-session", container, TimeSpan.FromMinutes(30));

        var result = await conductor.HandleActionAsync(new BookingActionRequest
        {
            SessionId = "no-rooms-session",
            Action = "confirm-dates",
            BookingMode = "hourly",
            CheckInDate = "2026-06-15",
            GuestCount = 1
        }, CancellationToken.None);

        Assert.Equal(ConductorAction.AskInfo, result.Action);
        Assert.Contains("không còn phòng trống nào khả dụng", result.Answer);
        Assert.NotEmpty(result.UiBlocks);
        var json = JsonSerializer.Serialize(result.UiBlocks);
        Assert.Contains("singleDatePicker", json);
    }

    [Fact]
    public async Task HourlySpecificSlotRequest_ShowsOnlyRoomsAvailableInThatRange()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        SeedRoom(context, id: 7, branchId: 1, capacity: 2, maxGuests: 3, extraGuestFee: 90000m);
        SeedRoom(context, id: 8, branchId: 1, capacity: 2, maxGuests: 3, extraGuestFee: 90000m);
        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        SeedHourlySlot(context, roomId: 7, slotDate: targetDate, start: "08:00", end: "10:00");
        SeedHourlySlot(context, roomId: 8, slotDate: targetDate, start: "10:00", end: "12:00");
        var conductor = CreateConductor(context, cache, extractor =>
        {
            extractor.Setup(e => e.ExtractGuestCount(Moq.It.IsAny<string>())).Returns(2);
        });

        var request = MakeRequest("14/6 còn 8-10h không", branchId: 1, startTime: targetDate.ToDateTime(TimeOnly.Parse("08:00")), guestCount: 2);
        var result = await conductor.DecideAsync("slot-1", request.Message, request, CancellationToken.None);

        var json = JsonSerializer.Serialize(result.UiBlocks);
        Assert.Contains("\"roomId\":7", json);
        Assert.DoesNotContain("\"roomId\":8", json);
    }

    [Fact]
    public async Task FollowUpQuestion_UsesActiveRoomContextForOccupancyExplanation()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        SeedRoom(context, id: 11, branchId: 1, capacity: 2, maxGuests: 3, extraGuestFee: 100000m);
        var conductor = CreateConductor(context, cache);
        cache.Set("ai-booking-conductor:ctx-1", new BookingSessionContainer
        {
            Confirmed = new BookingConfirmedState { BranchId = 1, GuestCount = 2, BookingMode = "daily" },
            Progress = new BookingProgressState { SelectedRoomId = 11, ActiveRoomContextId = 11 }
        }, TimeSpan.FromMinutes(30));

        var request = MakeRequest("phòng này ở 3 người được không", branchId: 1, startTime: DateTime.Today.AddDays(5), guestCount: 3);
        var result = await conductor.DecideAsync("ctx-1", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.Reply, result.Action);
        Assert.Equal(11, result.State.Progress?.ActiveRoomContextId);
    }

    [Fact]
    public async Task FollowUpQuestion_UsesActiveRoomContextForPriceExplanation()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        SeedRoom(context, id: 12, branchId: 1, capacity: 2, maxGuests: 4, extraGuestFee: 120000m);
        var conductor = CreateConductor(context, cache);
        cache.Set("ai-booking-conductor:ctx-price-1", new BookingSessionContainer
        {
            Confirmed = new BookingConfirmedState
            {
                BranchId = 1,
                GuestCount = 3,
                BookingMode = "hourly",
                HourlyDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5))
            },
            Progress = new BookingProgressState { SelectedRoomId = 12, ActiveRoomContextId = 12 }
        }, TimeSpan.FromMinutes(30));

        var request = MakeRequest("phòng này giá sao", branchId: 1, startTime: DateTime.Today.AddDays(5), guestCount: 3);
        var result = await conductor.DecideAsync("ctx-price-1", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.Reply, result.Action);
        Assert.Equal(12, result.State.Progress?.ActiveRoomContextId);
        Assert.Equal("room-context-price", result.Reason);
    }

    [Fact]
    public async Task FollowUpQuestion_UsesActiveRoomContextForPricingPolicyExplanation()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        SeedRoom(context, id: 13, branchId: 1, capacity: 2, maxGuests: 4, extraGuestFee: 150000m);
        var conductor = CreateConductor(context, cache);
        cache.Set("ai-booking-conductor:ctx-policy-1", new BookingSessionContainer
        {
            Confirmed = new BookingConfirmedState
            {
                BranchId = 1,
                GuestCount = 3,
                BookingMode = "daily",
                CheckInDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
                CheckOutDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7))
            },
            Progress = new BookingProgressState { SelectedRoomId = 13, ActiveRoomContextId = 13 }
        }, TimeSpan.FromMinutes(30));

        var request = MakeRequest("phòng này cuối tuần có phụ thu không", branchId: 1, startTime: DateTime.Today.AddDays(5), guestCount: 3);
        var result = await conductor.DecideAsync("ctx-policy-1", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.Reply, result.Action);
        Assert.Equal(13, result.State.Progress?.ActiveRoomContextId);
        Assert.Equal("room-context-policy", result.Reason);
    }

    [Fact]
    public async Task GuestCountUpdate_OnSelectedDailyRoom_RecalculatesSummaryAndKeepsCheckoutFlow()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        context.Rooms.Add(new Room
        {
            Id = 81,
            BranchId = 1,
            Name = "Song Xanh Q1-401",
            PricePerHour = 230000m,
            PricePerDay = 1650000m,
            PriceWeekendPerDay = 2600000m,
            Capacity = 2,
            MaxGuests = 5,
            ExtraGuestFee = 200000m,
            Status = "Available"
        });
        context.SaveChanges();

        var conductor = CreateConductor(context, cache);
        cache.Set("ai-booking-conductor:guest-update-daily", new BookingSessionContainer
        {
            Confirmed = new BookingConfirmedState
            {
                BranchId = 1,
                CheckInDate = new DateOnly(2026, 6, 13),
                CheckOutDate = new DateOnly(2026, 6, 16),
                GuestCount = 2,
                BookingMode = "daily"
            },
            Progress = new BookingProgressState
            {
                SelectedRoomId = 81,
                ActiveRoomContextId = 81
            }
        }, TimeSpan.FromMinutes(30));

        var request = MakeRequest("tui muốn thêm 1 khách nữa tổng là 3", branchId: 1, guestCount: 2);
        var result = await conductor.DecideAsync("guest-update-daily", request.Message, request, CancellationToken.None);

        var json = JsonSerializer.Serialize(result.UiBlocks);
        Assert.Equal(ConductorAction.AskInfo, result.Action);
        Assert.Equal(3, result.State.Confirmed.GuestCount);
        Assert.Equal(81, result.State.Progress?.SelectedRoomId);
        Assert.Equal("room-context-guest-update", result.Reason);
        Assert.Contains("bookingSummary", json);
        Assert.Contains("roomDecisionCta", json);
        Assert.Contains("\"showCommitButton\":true", json);
        Assert.Contains("commitLabel", json);
        Assert.Contains("6850000", json);
    }

    [Fact]
    public async Task RoomNamePriceQuestion_BindsRoomContextAndReturnsReply()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        context.Rooms.Add(new Room
        {
            Id = 42,
            BranchId = 1,
            Name = "Binh Minh Q7-401",
            PricePerHour = 220000m,
            PricePerDay = 1400000m,
            Capacity = 2,
            MaxGuests = 4,
            Status = "Available"
        });
        context.SaveChanges();

        var conductor = CreateConductor(context, cache);
        var container = new BookingSessionContainer
        {
            Confirmed = new BookingConfirmedState
            {
                BranchId = 1,
                HourlyDate = new DateOnly(2026, 6, 14),
                GuestCount = 2,
                BookingMode = "hourly"
            }
        };
        cache.Set("ai-booking-conductor:ctx-price-2", container, TimeSpan.FromMinutes(30));

        var request = MakeRequest("Binh Minh Q7-401 giá sao", branchId: 1, startTime: new DateTime(2026, 6, 14), guestCount: 2);
        var result = await conductor.DecideAsync("ctx-price-2", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.Reply, result.Action);
        Assert.Equal(42, result.State.Progress?.ActiveRoomContextId);
        Assert.Equal("room-context-price", result.Reason);
    }

    [Fact]
    public async Task RoomNamePriceQuestion_WithoutBranch_BindsAccentInsensitiveRoomContext()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        context.Rooms.Add(new Room
        {
            Id = 43,
            BranchId = 9,
            Name = "Bình Minh Q7-401",
            PricePerHour = 220000m,
            PricePerDay = 1400000m,
            Capacity = 2,
            MaxGuests = 4,
            Status = "Available"
        });
        context.SaveChanges();

        var conductor = CreateConductor(context, cache);
        var request = MakeRequest("Binh Minh Q7-401 giá sao");
        var result = await conductor.DecideAsync("ctx-price-3", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.Reply, result.Action);
        Assert.Equal(43, result.State.Progress?.ActiveRoomContextId);
        Assert.Equal(9, result.State.Confirmed.BranchId);
        Assert.Equal("room-context-price", result.Reason);
    }

    [Fact]
    public async Task RoomNameSlotQuestion_BindsRoomContextAndReturnsShowSlots()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        var slotDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        context.Rooms.Add(new Room
        {
            Id = 41,
            BranchId = 1,
            Name = "Binh Minh Q7-301",
            PricePerHour = 190000m,
            PricePerDay = 1350000m,
            Capacity = 2,
            MaxGuests = 4,
            Status = "Available"
        });
        context.RoomSlotInventories.Add(new RoomSlotInventory
        {
            RoomId = 41,
            SlotDate = slotDate,
            SlotLabel = "08:00-10:00",
            StartTime = slotDate.ToDateTime(TimeOnly.Parse("08:00")),
            EndTime = slotDate.ToDateTime(TimeOnly.Parse("10:00")),
            Status = "Available"
        });
        context.SaveChanges();

        var conductor = CreateConductor(context, cache);
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
        cache.Set("ai-booking-conductor:ctx-slot-1", container, TimeSpan.FromMinutes(30));

        var request = MakeRequest("Binh Minh Q7-301 có khung giờ nào", branchId: 1, startTime: slotDate.ToDateTime(TimeOnly.MinValue), guestCount: 2);
        var result = await conductor.DecideAsync("ctx-slot-1", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.ShowSlots, result.Action);
        Assert.Equal(41, result.State.Progress?.ActiveRoomContextId);
        Assert.Contains("\"slotId\":", JsonSerializer.Serialize(result.UiBlocks));
    }

    [Fact]
    public async Task RoomNameSlotQuestion_FromDailyContext_AsksForHourlyDateInsteadOfFalseNoSlots()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        context.Rooms.Add(new Room
        {
            Id = 51,
            BranchId = 1,
            Name = "Binh Minh Q7-302",
            PricePerHour = 210000m,
            PricePerDay = 1450000m,
            Capacity = 2,
            MaxGuests = 4,
            Status = "Available"
        });
        context.SaveChanges();

        var conductor = CreateConductor(context, cache);
        var container = new BookingSessionContainer
        {
            Confirmed = new BookingConfirmedState
            {
                BranchId = 1,
                CheckInDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                CheckOutDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
                GuestCount = 2,
                BookingMode = "daily"
            }
        };
        cache.Set("ai-booking-conductor:ctx-slot-2", container, TimeSpan.FromMinutes(30));

        var request = MakeRequest("Binh Minh Q7-302 có khung giờ nào", branchId: 1, startTime: DateTime.Today.AddDays(1), guestCount: 2);
        var result = await conductor.DecideAsync("ctx-slot-2", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.AskInfo, result.Action);
        Assert.Contains(result.UiBlocks, block => JsonSerializer.Serialize(block).Contains("singleDatePicker"));
    }

    [Fact]
    public async Task HandoffTriggered_ByKeyword_PausesSessionAndReturnsHandoffBlock()
    {
        using var context = CreateContext();
        var cache = CreateCache();

        var sessionId = "handoff-test-session";
        var chatSession = new AdminChatSession
        {
            SessionId = sessionId,
            CustomerName = "Test Guest",
            Status = "active",
            CreatedAt = DateTime.Now,
            LastActivityAt = DateTime.Now
        };
        context.AdminChatSessions.Add(chatSession);
        context.SaveChanges();

        var mockConfigService = new Moq.Mock<IAdminAIStudioConfigService>();
        var studioConfig = new AdminAIStudioConfigResponse
        {
            Handoff = new AdminAIHandoffConfig
            {
                Enabled = true,
                Keywords = new List<string> { "gặp người", "nhân viên" },
                ContactInstruction = "Liên hệ hotline của chúng tôi."
            }
        };
        mockConfigService.Setup(s => s.GetAsync()).ReturnsAsync(studioConfig);

        var mockHubContext = new Moq.Mock<IHubContext<ChatHub>>();
        var mockClients = new Moq.Mock<IHubClients>();
        var mockClientProxy = new Moq.Mock<IClientProxy>();
        
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group("admin_monitor")).Returns(mockClientProxy.Object);
        mockClients.Setup(c => c.Groups(Moq.It.IsAny<IReadOnlyList<string>>())).Returns(mockClientProxy.Object);
        mockClientProxy
            .Setup(cp => cp.SendCoreAsync("sessionUpdate", Moq.It.IsAny<object[]>(), Moq.It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var conductor = CreateConductor(context, cache, configureServices: services =>
        {
            services.AddSingleton(mockConfigService.Object);
            services.AddSingleton(mockHubContext.Object);
        });

        var request = MakeRequest("Tôi muốn gặp người hỗ trợ", branchId: 1);
        var result = await conductor.DecideAsync(sessionId, request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.Reply, result.Action);
        Assert.Equal("handoff-requested", result.Reason);
        Assert.Single(result.UiBlocks);

        var block = result.UiBlocks[0];
        var blockJson = JsonSerializer.Serialize(block);
        
        using var doc = JsonDocument.Parse(blockJson);
        var type = doc.RootElement.GetProperty("type").GetString();
        var data = doc.RootElement.GetProperty("data");
        var contactInstruction = data.GetProperty("contactInstruction").GetString();

        Assert.Equal("handoffContact", type);
        Assert.Equal("Liên hệ hotline của chúng tôi.", contactInstruction);

        var updatedSession = await context.AdminChatSessions.AsNoTracking().FirstOrDefaultAsync(s => s.SessionId == sessionId);
        Assert.NotNull(updatedSession);
        Assert.Equal("paused", updatedSession.Status);
        Assert.Equal("AI Handoff", updatedSession.PausedBy);
        Assert.Equal("handoff_request", updatedSession.PauseReason);
    }

    [Fact]
    public async Task DynamicGating_BasedOnStudioConfig_HidesOrShowsFields()
    {
        using var context = CreateContext();
        var cache = CreateCache();

        var mockConfigService = new Moq.Mock<IAdminAIStudioConfigService>();
        var studioConfig = new AdminAIStudioConfigResponse
        {
            ConversationFlows = new List<AdminAIConversationFlow>
            {
                new AdminAIConversationFlow
                {
                    Id = "daily",
                    Enabled = true,
                    RequiredFieldKeys = new List<string> { "branchId" }
                }
            }
        };
        mockConfigService.Setup(s => s.GetAsync()).ReturnsAsync(studioConfig);

        var conductor = CreateConductor(context, cache, configureServices: services =>
        {
            services.AddSingleton(mockConfigService.Object);
        });

        var request = MakeRequest("đặt phòng theo ngày", bookingMode: "daily");
        var result = await conductor.DecideAsync("gating-session", request.Message, request, CancellationToken.None);

        Assert.NotNull(result.State.Confirmed.MissingRequiredFields);
        Assert.Single(result.State.Confirmed.MissingRequiredFields);
        Assert.Equal("branchId", result.State.Confirmed.MissingRequiredFields[0]);
    }

    [Fact]
    public async Task DailyBookingIntent_WhenDateIsBeforeEarliestAllowed_ReturnsAskInfoWithExplanation()
    {
        using var context = CreateContext();
        var cache = CreateCache();
        context.Branches.Add(new Branch
        {
            Id = 1,
            Name = "LumiStay Riverside Quận 7",
            Address = "Q7",
            BookingLeadTimeValue = 7,
            BookingLeadTimeUnit = BranchLeadTimeUnit.Days,
            BookingLeadTimeDays = 7
        });
        SeedRoom(context, id: 91, branchId: 1, capacity: 2, maxGuests: 3, extraGuestFee: 100000m);

        var conductor = CreateConductor(context, cache, configureServices: services =>
        {
            services.AddSingleton<IBranchLeadTimeService>(new BranchLeadTimeService(context, () => new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc)));
        });

        var request = MakeRequest("đặt phòng ngày 26/6", branchId: 1, startTime: new DateTime(2026, 6, 26), guestCount: 2, bookingMode: "daily");
        var result = await conductor.DecideAsync("leadtime-ai-1", request.Message, request, CancellationToken.None);

        Assert.Equal(ConductorAction.AskInfo, result.Action);
        Assert.Contains("27/06/2026", result.Answer ?? string.Empty);
    }
}

public class FakeBookingCreationService : IBookingCreationService
{
    private readonly ApplicationDbContext _context;

    public FakeBookingCreationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Booking> CreateHourlyBookingAsync(CreateBookingRequest request)
    {
        var booking = new Booking
        {
            Id = new Random().Next(1, 99999),
            RoomId = request.RoomId,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            BookingMode = BookingMode.Hourly,
            Status = "AwaitingPayment"
        };
        _context.Bookings.Add(booking);
        return Task.FromResult(booking);
    }

    public Task<Booking> CreateDailyBookingAsync(CreateBookingRequest request)
    {
        var booking = new Booking
        {
            Id = new Random().Next(1, 99999),
            RoomId = request.RoomId,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            BookingMode = BookingMode.Daily,
            Status = "AwaitingPayment"
        };
        _context.Bookings.Add(booking);
        return Task.FromResult(booking);
    }
}

public class FakeAIModelClient : IAIModelClient
{
    public Task<AIModelResponse> CompleteAsync(AIModelRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AIModelResponse { Content = "{ \"intent\": \"Unknown\", \"confidence\": 0.0 }" });
    }
}
