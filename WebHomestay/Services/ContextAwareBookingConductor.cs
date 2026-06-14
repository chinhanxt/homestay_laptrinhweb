using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.AI;
using WebHomestay.Models.ViewModels;
using WebHomestay.Services.AI;

namespace WebHomestay.Services;

public class ContextAwareBookingConductor : IBookingConductor
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBookingCreationService _bookingCreationService;
    private readonly IAdminChatService _adminChatService;
    private readonly AI.LLMIntentClassifier _intentClassifier;
    private readonly AI.IEntityExtractorService _entityExtractor;
    private readonly IPublicBookingRoomExplanationService _roomExplanationService;
    private readonly PricingService _pricingService;
    private const string CacheKeyPrefix = "ai-booking-conductor:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private const string DefaultBookingFormSchema = """
    [
      {"id":"customerName","type":"text","label":"Họ và tên","required":true,"helpText":"Nhập đúng họ tên người đặt phòng.","order":1},
      {"id":"customerPhone","type":"tel","label":"Số điện thoại","required":true,"helpText":"Số điện thoại/Zalo để homestay liên hệ xác nhận.","order":2},
      {"id":"customerEmail","type":"email","label":"Email","required":false,"helpText":"Email nhận thông tin đặt phòng nếu có.","order":3}
    ]
    """;

    public ContextAwareBookingConductor(
        ApplicationDbContext context,
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory,
        IBookingCreationService bookingCreationService,
        IAdminChatService adminChatService,
        AI.LLMIntentClassifier intentClassifier,
        AI.IEntityExtractorService entityExtractor,
        IPublicBookingRoomExplanationService roomExplanationService,
        PricingService pricingService)
    {
        _context = context;
        _cache = cache;
        _scopeFactory = scopeFactory;
        _bookingCreationService = bookingCreationService;
        _adminChatService = adminChatService;
        _intentClassifier = intentClassifier;
        _entityExtractor = entityExtractor;
        _roomExplanationService = roomExplanationService;
        _pricingService = pricingService;
    }

    private string CacheKey(string sessionId) => $"{CacheKeyPrefix}{sessionId}";

    private BookingSessionContainer? GetCachedState(string sessionId)
        => _cache.TryGetValue(CacheKey(sessionId), out BookingSessionContainer? state) ? state : null;

    private void CacheState(string sessionId, BookingSessionContainer state)
        => _cache.Set(CacheKey(sessionId), state, CacheTtl);

    public async Task<ConductorResult> DecideAsync(
        string sessionId,
        string message,
        AIBrainChatRequest request,
        CancellationToken cancellationToken)
    {
        var container = GetCachedState(sessionId) ?? new BookingSessionContainer();
        container.Progress ??= new BookingProgressState();
        container.Confirmed = MergeFromRequest(container.Confirmed, request);
        HydrateStateFromMessage(container, message);

        var guestCount = _entityExtractor.ExtractGuestCount(message);
        if (guestCount.HasValue) container.Confirmed.GuestCount = guestCount.Value;

        var (date, time) = _entityExtractor.ExtractDateTime(message);
        if (date.HasValue)
        {
            if (string.Equals(container.Confirmed.BookingMode, "daily", StringComparison.OrdinalIgnoreCase))
            {
                if (!container.Confirmed.CheckInDate.HasValue) container.Confirmed.CheckInDate = DateOnly.FromDateTime(date.Value);
            }
            else
            {
                container.Confirmed.HourlyDate = DateOnly.FromDateTime(date.Value);
            }
        }
        if (time.HasValue && !container.Confirmed.RequestedTimeStart.HasValue)
        {
            container.Confirmed.RequestedTimeStart = TimeOnly.FromTimeSpan(time.Value);
            container.Confirmed.RequestedTimeLabel = $"{container.Confirmed.RequestedTimeStart:HH\\:mm}";
        }

        var (llmIntentStr, confidence) = await _intentClassifier.ClassifyIntentAsync(message, cancellationToken);
        MessageIntent intent;

        if (confidence >= 0.7 && llmIntentStr == "Hourly_Booking")
        {
            intent = MessageIntent.BookingIntent;
            container.Confirmed.BookingMode = "hourly";
        }
        else if (confidence >= 0.7 && llmIntentStr == "Daily_Booking")
        {
            intent = MessageIntent.BookingIntent;
            container.Confirmed.BookingMode = "daily";
        }
        else
        {
            intent = ClassifyIntent(message, container); // fallback
        }

        await TryBindBranchContextFromMessageAsync(container, message, cancellationToken);
        container.Confirmed.MissingRequiredFields = GetMissingRequiredFields(container.Confirmed);

        await TryBindRoomContextFromMessageAsync(container, message, cancellationToken);
        container.Confirmed.MissingRequiredFields = GetMissingRequiredFields(container.Confirmed);

        if (IsRoomContextOccupancyQuestion(message, container))
        {
            CacheState(sessionId, container);
            return new ConductorResult
            {
                Action = ConductorAction.Reply,
                State = container,
                UiBlocks = new List<object>(),
                Reason = "room-context-occupancy"
            };
        }

        if (IsRoomContextPolicyQuestion(message, container))
        {
            CacheState(sessionId, container);
            return new ConductorResult
            {
                Action = ConductorAction.Reply,
                State = container,
                UiBlocks = new List<object>(),
                Reason = "room-context-policy"
            };
        }

        if (IsRoomContextPriceQuestion(message, container))
        {
            CacheState(sessionId, container);
            return new ConductorResult
            {
                Action = ConductorAction.Reply,
                State = container,
                UiBlocks = new List<object>(),
                Reason = "room-context-price"
            };
        }

        if (IsRoomContextSlotQuestion(message, container))
        {
            if (!container.Confirmed.HourlyDate.HasValue)
            {
                container.Confirmed.MissingRequiredFields = ["hourlyDate"];
                var slotDateBlocks = await BuildMissingFieldBlocksAsync(container.Confirmed, cancellationToken);
                CacheState(sessionId, container);
                return new ConductorResult
                {
                    Action = ConductorAction.AskInfo,
                    State = container,
                    UiBlocks = slotDateBlocks,
                    Reason = "room-context-slots-missing-date"
                };
            }

            container.Progress.SelectedRoomId ??= container.Progress.ActiveRoomContextId;
            var slotBlocks = await BuildSlotUiBlocksAsync(container, cancellationToken);
            CacheState(sessionId, container);
            return new ConductorResult
            {
                Action = ConductorAction.ShowSlots,
                State = container,
                UiBlocks = slotBlocks,
                Reason = "room-context-slots"
            };
        }

        var showCount = GetShowCount(sessionId);

        var studioConfig = await LoadStudioConfigAsync(cancellationToken);
        var action = ResolveAction(intent, container, showCount, studioConfig?.RuntimePolicies);
        var uiBlocks = new List<object>();

        if (action == ConductorAction.AskInfo)
        {
            uiBlocks = await BuildMissingFieldBlocksAsync(container.Confirmed, cancellationToken);
        }

        if (action == ConductorAction.ShowRooms)
        {
            uiBlocks = await BuildRoomCardsAsync(container.Confirmed, cancellationToken);
            IncrementShowCount(sessionId);
        }

        if (action == ConductorAction.Reply && intent == MessageIntent.Exit)
        {
            container.Progress = null;
        }

        if (action == ConductorAction.Reply && intent is MessageIntent.PolicyQuestion or MessageIntent.OffTopic or MessageIntent.Compared)
        {
            if (container.Progress != null)
            {
                container.Progress.SelectedRoomId = null;
                container.Progress.SelectedSlotId = null;
            }
        }

        CacheState(sessionId, container);

        return new ConductorResult
        {
            Action = action,
            State = container,
            UiBlocks = uiBlocks,
            Reason = $"intent={intent}, showCount={showCount}"
        };
    }

    private MessageIntent ClassifyIntent(string message, BookingSessionContainer container)
    {
        var lowered = message.ToLowerInvariant().Trim();

        var exitKeywords = GetSetting("AIPublicBookingExitKeywords", "thôi,bỏ,khác,xóa,hủy,không,để sau")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (exitKeywords.Any(k => IsExitKeywordMatch(lowered, k)))
            return MessageIntent.Exit;

        if (lowered.Contains("wifi") || lowered.Contains("mật khẩu") || lowered.Contains("mat khau") || lowered.Contains("pass")
            || lowered.Contains("mở cửa") || lowered.Contains("mo cua") || lowered.Contains("chìa khóa") || lowered.Contains("chia khoa")
            || lowered.Contains("sự cố") || lowered.Contains("hỏng") || lowered.Contains("thiết bị") || lowered.Contains("tivi")
            || lowered.Contains("điều hòa") || lowered.Contains("bình nóng lạnh") || lowered.Contains("tủ lạnh") || lowered.Contains("vệ sinh")
            || lowered.Contains("tiện ích") || lowered.Contains("tiện nghi") || lowered.Contains("có gì") || lowered.Contains("co gi")
            || lowered.Contains("đường đi") || lowered.Contains("bãi xe") || lowered.Contains("đậu xe") || lowered.Contains("gửi xe")
            || lowered.Contains("cửa") || lowered.Contains("hộp số") || lowered.Contains("hướng dẫn") || lowered.Contains("huong dan"))
        {
            return MessageIntent.PolicyQuestion;
        }

        if (lowered.Contains("chính sách") || lowered.Contains("hủy") || lowered.Contains("check-in")
            || lowered.Contains("check out") || lowered.Contains("trả phòng") || lowered.Contains("giờ nhận")
            || lowered.Contains("hoàn") || lowered.Contains("refund"))
            return MessageIntent.PolicyQuestion;

        if (lowered.Contains("cảm ơn") || lowered.Contains("hello") || lowered.Contains("hi")
            || lowered.Contains("chào") || lowered.Contains("thanks") || lowered.Contains("ok")
            || lowered.StartsWith("bạn tên") || lowered.StartsWith("bạn là"))
            return MessageIntent.OffTopic;

        if (lowered.Contains(" ở đâu") || lowered.Contains("gần") || lowered.Contains("địa chỉ")
            || lowered.Contains("chỗ") || lowered.Contains("vị trí"))
            return MessageIntent.LocationQuestion;

        if (lowered.Contains(" vs ") || lowered.Contains(" so với ") || lowered.Contains(" khác gì ")
            || lowered.Contains("hay") || lowered.Contains("phòng nào rộng") || lowered.Contains("phòng nào đẹp"))
            return MessageIntent.Compared;

        if (lowered.Contains("giá") || lowered.Contains("bao nhiêu") || lowered.Contains("tiền")
            || lowered.Contains("rẻ") || lowered.Contains("đắt") || lowered.Contains("chi phí"))
            return MessageIntent.PriceQuestion;

        if (IsImplicitBookingSignal(lowered, container.Confirmed))
            return MessageIntent.BrowsingRooms;

        var triggerWords = GetSetting("AIPublicBookingTriggerWords", "đặt,chốt,lấy,book,giữ phòng,giữ chỗ")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (triggerWords.Any(w => lowered.Contains(w)))
            return MessageIntent.BookingIntent;

        if (lowered.Contains("xem phòng") || lowered.Contains("còn phòng") || lowered.Contains("có phòng")
            || lowered.Contains("phòng trống") || lowered.Contains("cho xem"))
            return MessageIntent.BrowsingRooms;

        if (container.Confirmed.BranchId.HasValue)
            return MessageIntent.BrowsingRooms;

        return MessageIntent.OffTopic;
    }

    private bool HasEnoughInfo(BookingConfirmedState state)
        => state.BranchId.HasValue
           && state.GuestCount > 0
           && (string.Equals(state.BookingMode, "daily", StringComparison.OrdinalIgnoreCase)
               ? state.CheckInDate.HasValue && state.CheckOutDate.HasValue
               : state.HourlyDate.HasValue || state.CheckInDate.HasValue);

    private ConductorAction ResolveAction(
        MessageIntent intent,
        BookingSessionContainer container,
        int showCount,
        AdminAIRuntimePolicies? runtimePolicies)
    {
        var autoShow = runtimePolicies?.AutoShowRooms
            ?? string.Equals(GetSetting("AIPublicBookingAutoShowRooms", "true"), "true", StringComparison.OrdinalIgnoreCase);
        var maxShows = runtimePolicies?.MaxRoomShows ?? ParseIntSetting("AIPublicBookingMaxRoomShows", 2);
        var mode = GetSetting("AIPublicBookingProactiveMode", "balanced");

        switch (intent)
        {
            case MessageIntent.BookingIntent:
                return HasEnoughInfo(container.Confirmed)
                    ? ConductorAction.ShowRooms
                    : ConductorAction.AskInfo;

            case MessageIntent.BrowsingRooms:
                if (container.Progress?.ActiveRoomContextId.HasValue == true)
                    return ConductorAction.Reply;
                if (!HasEnoughInfo(container.Confirmed))
                    return ConductorAction.AskInfo;
                if (!autoShow)
                    return ConductorAction.Reply;
                if (showCount >= maxShows)
                    return ConductorAction.Reply;
                return ConductorAction.ShowRooms;

            case MessageIntent.PriceQuestion:
                if (container.Progress?.ActiveRoomContextId.HasValue == true)
                    return ConductorAction.Reply;
                if (!HasEnoughInfo(container.Confirmed))
                    return ConductorAction.AskInfo;
                if (mode == "conservative")
                    return ConductorAction.Reply;
                if (showCount >= maxShows)
                    return ConductorAction.Reply;
                return ConductorAction.ShowRooms;

            case MessageIntent.PolicyQuestion:
            case MessageIntent.OffTopic:
            case MessageIntent.LocationQuestion:
            case MessageIntent.Compared:
            case MessageIntent.Exit:
                return ConductorAction.Reply;

            default:
                return ConductorAction.Reply;
        }
    }

    private BookingConfirmedState MergeFromRequest(BookingConfirmedState state, AIBrainChatRequest request)
    {
        if (request.BranchId.HasValue) state.BranchId = request.BranchId;
        if (request.StartTime.HasValue)
        {
            if (string.Equals(state.BookingMode, "daily", StringComparison.OrdinalIgnoreCase))
            {
                state.CheckInDate = DateOnly.FromDateTime(request.StartTime.Value);
            }
            else
            {
                state.HourlyDate = DateOnly.FromDateTime(request.StartTime.Value);
            }
        }
        if (request.EndTime.HasValue)
            state.CheckOutDate = DateOnly.FromDateTime(request.EndTime.Value);
        if (request.GuestCount > 0)
            state.GuestCount = request.GuestCount;
        return state;
    }

    public async Task<BookingActionResult> HandleActionAsync(
        BookingActionRequest actionRequest,
        CancellationToken cancellationToken)
    {
        var container = GetCachedState(actionRequest.SessionId) ?? new BookingSessionContainer();
        container.Progress ??= new BookingProgressState();
        ApplyActionState(container, actionRequest);

        switch (actionRequest.Action)
        {
            case "start-booking":
                container.Confirmed = new BookingConfirmedState();
                container.Progress = new BookingProgressState();
                CacheState(actionRequest.SessionId, container);

                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var branches = await db.Branches
                        .OrderBy(branch => branch.Id)
                        .Select(branch => new
                        {
                            id = branch.Id,
                            name = branch.Name
                        })
                        .ToListAsync(cancellationToken);

                    System.Collections.IEnumerable? flows = null;
                    var studioConfig = await LoadStudioConfigAsync(cancellationToken);
                    var activeFlows = studioConfig?.ConversationFlows
                        ?.Where(flow => flow.Enabled && (flow.Id == "hourly" || flow.Id == "daily"))
                        ?.OrderBy(flow => flow.Priority)
                        ?.ToList();

                    if (activeFlows != null && activeFlows.Any())
                    {
                        flows = activeFlows.Select(flow => new
                        {
                            id = flow.Id,
                            name = string.IsNullOrEmpty(flow.Name) ? (flow.Id == "hourly" ? "Theo giờ" : "Theo ngày") : flow.Name,
                            description = flow.Description
                        }).ToList();
                    }
                    else
                    {
                        flows = new List<object>
                        {
                            new { id = "hourly", name = "Theo giờ", description = "Linh hoạt, nhận phòng bất cứ lúc nào trong ngày." },
                            new { id = "daily", name = "Theo ngày", description = "Thuận tiện, nhận phòng sau 14h, trả phòng trước 12h." }
                        };
                    }

                    return new BookingActionResult
                    {
                        Answer = "Chào bạn! Mình bắt đầu luồng tìm phòng nhé. Bạn vui lòng chọn hình thức đặt phòng mong muốn:",
                        Action = ConductorAction.AskInfo,
                        State = container,
                        UiBlocks = new List<object>
                        {
                            new
                            {
                                type = "bookingModeChoice",
                                data = new
                                {
                                    label = "Chọn hình thức đặt phòng",
                                    branches,
                                    flows
                                }
                            }
                        }
                    };
                }

            case "select-booking-mode":
                container.Confirmed.BookingMode = string.Equals(actionRequest.BookingMode, "daily", StringComparison.OrdinalIgnoreCase)
                    ? "daily"
                    : "hourly";
                if (actionRequest.GuestCount > 0)
                    container.Confirmed.GuestCount = actionRequest.GuestCount;
                CacheState(actionRequest.SessionId, container);

                return new BookingActionResult
                {
                    Answer = container.Confirmed.BookingMode == "daily"
                        ? "Mình sẽ tư vấn đặt theo ngày. Bạn chọn chi nhánh trước nhé."
                        : "Mình sẽ tư vấn đặt theo giờ. Bạn chọn chi nhánh trước nhé.",
                    Action = ConductorAction.AskInfo,
                    State = container,
                    UiBlocks = new List<object>
                    {
                        await BuildBranchSelectorBlockAsync(cancellationToken)
                    }
                };

            case "select-branch":
                if (actionRequest.BranchId.HasValue)
                    container.Confirmed.BranchId = actionRequest.BranchId.Value;
                if (!IsKnownBookingMode(container.Confirmed.BookingMode))
                {
                    var requestedBookingMode = actionRequest.BookingMode;
                    container.Confirmed.BookingMode = IsKnownBookingMode(requestedBookingMode)
                        ? requestedBookingMode!
                        : "hourly";
                }
                if (actionRequest.GuestCount > 0)
                    container.Confirmed.GuestCount = actionRequest.GuestCount;
                CacheState(actionRequest.SessionId, container);

                return new BookingActionResult
                {
                    Answer = container.Confirmed.BookingMode == "daily"
                        ? "Bạn chọn ngày nhận và ngày trả phòng nhé."
                        : "Bạn chọn ngày muốn đặt theo giờ nhé.",
                    Action = ConductorAction.AskInfo,
                    State = container,
                    UiBlocks = new List<object>
                    {
                        new
                        {
                            type = container.Confirmed.BookingMode == "daily" ? "dateRangePicker" : "singleDatePicker",
                            data = new { }
                        }
                    }
                };

            case "select-room":
                var selectedRoomIdValue = actionRequest.RoomId ?? container.Progress.SelectedRoomId ?? container.Progress.ActiveRoomContextId;
                container.Progress.SelectedRoomId = selectedRoomIdValue;
                container.Progress.ActiveRoomContextId = selectedRoomIdValue;
                CacheState(actionRequest.SessionId, container);

                if (!selectedRoomIdValue.HasValue)
                {
                    return new BookingActionResult
                    {
                        Answer = "Mình chưa xác định được phòng bạn đang chọn. Bạn chọn lại giúp mình nhé.",
                        Action = ConductorAction.ShowRooms,
                        State = container,
                        UiBlocks = await BuildRoomCardsAsync(container.Confirmed, cancellationToken)
                    };
                }

                var hasDates = container.Confirmed.HourlyDate.HasValue || container.Confirmed.CheckInDate.HasValue;
                if (!hasDates)
                {
                    return new BookingActionResult
                    {
                        Answer = "Bạn vui lòng chọn ngày đặt phòng nhé:",
                        Action = ConductorAction.AskInfo,
                        State = container,
                        UiBlocks = new List<object>
                        {
                            new
                            {
                                type = "dateSelector",
                                data = new { }
                            }
                        }
                    };
                }

                return await BuildSelectedRoomConsultResponseAsync(container, selectedRoomIdValue.Value, cancellationToken);

            case "commit-room":
                if (actionRequest.RoomId.HasValue)
                {
                    container.Progress.SelectedRoomId = actionRequest.RoomId;
                    container.Progress.ActiveRoomContextId = actionRequest.RoomId;
                }
                CacheState(actionRequest.SessionId, container);

                var isDailyMode = container.Confirmed.CheckOutDate.HasValue;
                if (isDailyMode)
                {
                    return await BuildDailyCheckoutCtaAsync(container, actionRequest.SessionId, container.Progress.SelectedRoomId ?? 0, cancellationToken);
                }

                return await BuildSlotsResponse(container, cancellationToken);

            case "confirm-dates":
                if (!string.IsNullOrEmpty(actionRequest.BookingMode))
                    container.Confirmed.BookingMode = actionRequest.BookingMode;
                if (!string.IsNullOrEmpty(actionRequest.CheckInDate)
                    && DateOnly.TryParse(actionRequest.CheckInDate, out var ciDate))
                {
                    container.Confirmed.HourlyDate = ciDate;
                    container.Confirmed.CheckInDate = ciDate;
                }
                if (!string.IsNullOrEmpty(actionRequest.CheckOutDate)
                    && DateOnly.TryParse(actionRequest.CheckOutDate, out var coDate))
                {
                    container.Confirmed.CheckOutDate = coDate;
                }
                CacheState(actionRequest.SessionId, container);

                var selectedRoomId = container.Progress?.SelectedRoomId;
                if (!selectedRoomId.HasValue)
                {
                    var roomCards = await BuildRoomCardsAsync(container.Confirmed, cancellationToken);
                    
                    bool hasRooms = false;
                    if (roomCards.Count > 0)
                    {
                        var firstBlock = roomCards[0];
                        var dataProp = firstBlock.GetType().GetProperty("data")?.GetValue(firstBlock);
                        if (dataProp != null)
                        {
                            var roomsProp = dataProp.GetType().GetProperty("rooms")?.GetValue(dataProp) as System.Collections.IEnumerable;
                            if (roomsProp != null)
                            {
                                var enumerator = roomsProp.GetEnumerator();
                                if (enumerator.MoveNext())
                                {
                                    hasRooms = true;
                                }
                            }
                        }
                    }

                    if (!hasRooms)
                    {
                        var isDaily = string.Equals(container.Confirmed.BookingMode, "daily", StringComparison.OrdinalIgnoreCase);
                        return new BookingActionResult
                        {
                            Answer = "Rất tiếc, ngày này hiện tại bên mình không còn phòng trống nào khả dụng. Bạn vui lòng chọn ngày khác giúp mình nhé.",
                            Action = ConductorAction.AskInfo,
                            State = container,
                            UiBlocks = new List<object>
                            {
                                new
                                {
                                    type = isDaily ? "dateRangePicker" : "singleDatePicker",
                                    data = new { }
                                }
                            }
                        };
                    }

                    IncrementShowCount(actionRequest.SessionId);
                    return new BookingActionResult
                    {
                        Answer = "Cảm ơn bạn! Dưới đây là các phòng trống. Bạn chọn phòng nhé.",
                        Action = ConductorAction.ShowRooms,
                        State = container,
                        UiBlocks = roomCards
                    };
                }

                return await BuildSelectedRoomConsultResponseAsync(container, selectedRoomId.Value, cancellationToken);

            case "select-slot":
                container.Progress.SelectedSlotId = actionRequest.SlotId;
                CacheState(actionRequest.SessionId, container);
                return await BuildHourlyCheckoutCtaAsync(container, actionRequest.SlotId ?? 0, cancellationToken);

            case "submit-booking-form":
            case "submit-form":
                if (actionRequest.FormData != null)
                {
                    container.Progress.CustomerName = actionRequest.FormData.GetValueOrDefault("customerName");
                    container.Progress.CustomerPhone = actionRequest.FormData.GetValueOrDefault("phoneNumber")
                        ?? actionRequest.FormData.GetValueOrDefault("customerPhone");
                    container.Progress.CustomerEmail = actionRequest.FormData.GetValueOrDefault("email")
                        ?? actionRequest.FormData.GetValueOrDefault("customerEmail");
                    
                    // Task 2 FIX: Update AdminChatSession.CustomerName when form is submitted
                    if (!string.IsNullOrWhiteSpace(container.Progress.CustomerName))
                    {
                        await _adminChatService.UpsertSessionAsync(actionRequest.SessionId, container.Progress.CustomerName);
                    }
                }
                CacheState(actionRequest.SessionId, container);

                try
                {
                    var isDaily = container.Confirmed.CheckOutDate.HasValue;

                    var createReq = new CreateBookingRequest
                    {
                        RoomId = container.Progress.SelectedRoomId ?? 0,
                        BookingMode = isDaily ? BookingMode.Daily : BookingMode.Hourly,
                        SlotInventoryId = isDaily ? null : container.Progress.SelectedSlotId,
                        CheckInDate = isDaily ? container.Confirmed.CheckInDate : null,
                        CheckOutDate = isDaily ? container.Confirmed.CheckOutDate : null,
                        CustomerName = container.Progress.CustomerName ?? "",
                        CustomerPhone = container.Progress.CustomerPhone ?? "",
                        CustomerEmail = container.Progress.CustomerEmail,
                        GuestCount = Math.Max(container.Confirmed.GuestCount, 1),
                        CustomerNote = actionRequest.FormData?.GetValueOrDefault("notes")
                    };

                    var booking = isDaily
                        ? await _bookingCreationService.CreateDailyBookingAsync(createReq)
                        : await _bookingCreationService.CreateHourlyBookingAsync(createReq);

                    return new BookingActionResult
                    {
                        Answer = $"Đặt phòng thành công! Mã đơn: #{booking.Id}. Bạn vui lòng thanh toán để giữ chỗ.",
                        Action = ConductorAction.PaymentQr,
                        State = container,
                        UiBlocks = new List<object>
                        {
                            new
                            {
                                type = "paymentQr",
                                data = new { paymentUrl = $"/bookings/success/{booking.Id}" }
                            }
                        }
                    };
                }
                catch (Exception ex)
                {
                    return new BookingActionResult
                    {
                        Answer = $"Xin lỗi, không thể tạo đơn đặt phòng. {ex.Message}",
                        Action = ConductorAction.Reply,
                        State = container
                    };
                }

            case "submit-contact-phone":
                var contactPhone = actionRequest.FormData?.GetValueOrDefault("phone")
                    ?? actionRequest.FormData?.GetValueOrDefault("customerPhone");
                if (string.IsNullOrWhiteSpace(contactPhone))
                {
                    return new BookingActionResult
                    {
                        Answer = "Bạn nhập giúp mình SĐT/Zalo để nhân viên liên hệ nhé.",
                        Action = ConductorAction.AskInfo,
                        State = container
                    };
                }

                var branchName = await _context.Branches
                    .Where(branch => branch.Id == container.Confirmed.BranchId)
                    .Select(branch => branch.Name)
                    .FirstOrDefaultAsync(cancellationToken);
                await _adminChatService.UpsertSessionAsync(actionRequest.SessionId, contactPhone);
                await _adminChatService.AddSystemMessageAsync(
                    actionRequest.SessionId,
                    $"Khách để lại SĐT/Zalo: {contactPhone}. Chi nhánh đang tư vấn: {branchName ?? "chưa rõ"}. Nhân viên nên liên hệ hỗ trợ chốt đặt phòng.");
                CacheState(actionRequest.SessionId, container);

                return new BookingActionResult
                {
                    Answer = "Mình đã ghi nhận SĐT/Zalo. Nhân viên sẽ liên hệ hỗ trợ bạn chốt đặt phòng.",
                    Action = ConductorAction.Reply,
                    State = container
                };

            default:
                return new BookingActionResult
                {
                    Answer = "Xin lỗi, mình chưa hiểu thao tác này.",
                    Action = ConductorAction.Reply,
                    State = container
                };
        }
    }

    private async Task<BookingActionResult> BuildSlotsResponse(BookingSessionContainer container, CancellationToken cancellationToken)
    {
        var roomId = container.Progress?.SelectedRoomId ?? container.Progress?.ActiveRoomContextId;
        if (!container.Confirmed.HourlyDate.HasValue || !container.Confirmed.BranchId.HasValue || roomId == null)
        {
            return new BookingActionResult
            {
                Answer = "Vui lòng chọn ngày và phòng trước.",
                Action = ConductorAction.AskInfo,
                State = container
            };
        }
        var slotBlocks = await BuildSlotUiBlocksAsync(container, cancellationToken);
        var slots = ExtractSlots(slotBlocks);

        if (slots.Count == 0)
        {
            return new BookingActionResult
            {
                Answer = container.Confirmed.RequestedTimeLabel != null
                    ? $"Rất tiếc, hiện phòng này không còn khung {container.Confirmed.RequestedTimeLabel} trong ngày bạn chọn. Bạn thử ngày khác hoặc khung khác nhé."
                    : "Rất tiếc, ngày này không còn khung giờ trống nào cho phòng này. Bạn vui lòng chọn ngày khác nhé.",
                Action = ConductorAction.AskInfo,
                State = container,
                UiBlocks = new List<object>
                {
                    new
                    {
                        type = "dateSelector",
                        data = new { }
                    }
                }
            };
        }

        var room = await _context.Rooms.FindAsync([roomId.Value], cancellationToken);
        var slotBlocksWithReview = new List<object>();
        if (room != null)
        {
            slotBlocksWithReview.Add(BuildRoomDecisionBlock(
                room.Id,
                room.MaxGuests,
                room.Capacity,
                room.ExtraGuestFee,
                $"/Rooms/Details/{room.Id}",
                "Bạn chỉnh số khách nếu cần, rồi chọn khung giờ còn trống bên dưới để chốt tiếp. Giá từng slot đã hiển thị sẵn.",
                Math.Max(container.Confirmed.GuestCount, 1),
                0m,
                showCommitButton: false));
        }
        slotBlocksWithReview.AddRange(slotBlocks);

        return new BookingActionResult
        {
            Answer = $"Có {slots.Count} khung giờ trống. Bạn chọn giờ nào?",
            Action = ConductorAction.ShowSlots,
            State = container,
            UiBlocks = slotBlocksWithReview
        };
    }

    private async Task<BookingActionResult> BuildSelectedRoomConsultResponseAsync(
        BookingSessionContainer container,
        int selectedRoomId,
        CancellationToken cancellationToken)
    {
        var selectedRoom = await _context.Rooms.FindAsync([selectedRoomId], cancellationToken);
        if (selectedRoom == null)
        {
            return new BookingActionResult
            {
                Answer = "Mình chưa tìm thấy phòng này. Bạn chọn phòng khác giúp mình nhé.",
                Action = ConductorAction.ShowRooms,
                State = container,
                UiBlocks = await BuildRoomCardsAsync(container.Confirmed, cancellationToken)
            };
        }

        var explanation = await _roomExplanationService.BuildAsync(selectedRoom.Id, container.Confirmed, cancellationToken);
        var consultLines = new List<string>
        {
            $"Phòng: {selectedRoom.Name}",
            $"Sức chứa chuẩn {selectedRoom.Capacity} khách, tối đa {selectedRoom.MaxGuests} khách",
            $"Giá giờ: {selectedRoom.PricePerHour:N0}đ/h",
            $"Giá ngày: {selectedRoom.PricePerDay:N0}đ/ngày"
        };
        if (!string.IsNullOrWhiteSpace(selectedRoom.Description))
        {
            consultLines.Insert(1, selectedRoom.Description);
        }
        consultLines.AddRange(explanation.Lines);

        var consultBlocks = new List<object>();
        var isDailyConsult = container.Confirmed.CheckOutDate.HasValue
            || string.Equals(container.Confirmed.BookingMode, "daily", StringComparison.OrdinalIgnoreCase);
        decimal baseTotalPrice = 0m;

        if (isDailyConsult
            && container.Confirmed.CheckInDate.HasValue
            && container.Confirmed.CheckOutDate.HasValue)
        {
            baseTotalPrice = await _pricingService.CalculateStayPriceAsync(
                selectedRoom.Id,
                container.Confirmed.CheckInDate.Value.ToDateTime(TimeOnly.MinValue),
                container.Confirmed.CheckOutDate.Value.ToDateTime(TimeOnly.MinValue),
                false);
            var extraGuestTotal = Math.Max(0, container.Confirmed.GuestCount - selectedRoom.Capacity) * selectedRoom.ExtraGuestFee;
            consultLines.Add($"Tạm tính {Math.Max(1, container.Confirmed.CheckOutDate.Value.DayNumber - container.Confirmed.CheckInDate.Value.DayNumber)} đêm: {baseTotalPrice:N0}đ.");
            if (extraGuestTotal > 0)
            {
                consultLines.Add($"Phụ thu thêm khách dự kiến: {extraGuestTotal:N0}đ.");
            }
        }

        consultBlocks.Add(new
        {
            type = "bookingSummary",
            data = new
            {
                title = "Tư vấn phòng đang chọn",
                lines = consultLines,
                totalPrice = baseTotalPrice,
                pricing = new
                {
                    capacity = selectedRoom.Capacity,
                    maxGuests = selectedRoom.MaxGuests,
                    extraGuestFee = selectedRoom.ExtraGuestFee,
                    baseTotalPrice
                }
            }
        });

        if (isDailyConsult)
        {
            consultBlocks.Add(BuildRoomDecisionBlock(
                selectedRoom.Id,
                selectedRoom.MaxGuests,
                selectedRoom.Capacity,
                selectedRoom.ExtraGuestFee,
                $"/Rooms/Details/{selectedRoom.Id}",
                "Bạn chỉnh số khách nếu cần. Mình đã tạm tính tiền và phụ thu trước khi bạn quyết định chốt phòng.",
                Math.Max(container.Confirmed.GuestCount, 1),
                baseTotalPrice,
                showCommitButton: true,
                commitLabel: "Xác nhận phòng này"));

            return new BookingActionResult
            {
                Answer = "Mình đã tính trước tổng tiền và phụ thu dự kiến cho phòng này. Bạn xem kỹ rồi quyết định chốt nhé.",
                Action = ConductorAction.AskInfo,
                State = container,
                UiBlocks = consultBlocks
            };
        }

        var slotBlocksForConsult = await BuildSlotUiBlocksAsync(container, cancellationToken);
        if (slotBlocksForConsult.Count == 0)
        {
            consultBlocks.Add(BuildRoomDecisionBlock(
                selectedRoom.Id,
                selectedRoom.MaxGuests,
                selectedRoom.Capacity,
                selectedRoom.ExtraGuestFee,
                $"/Rooms/Details/{selectedRoom.Id}",
                "Bạn chỉnh số khách nếu cần. Hiện ngày này chưa có khung giờ phù hợp để chốt ngay cho phòng này.",
                Math.Max(container.Confirmed.GuestCount, 1),
                baseTotalPrice,
                showCommitButton: false,
                commitLabel: "Chọn ngày khác"));

            return new BookingActionResult
            {
                Answer = "Mình đã tư vấn nhanh phòng này. Hiện ngày bạn chọn chưa có slot phù hợp để chốt ngay, bạn đổi ngày hoặc giờ giúp mình nhé.",
                Action = ConductorAction.AskInfo,
                State = container,
                UiBlocks = consultBlocks
            };
        }

        consultBlocks.Add(BuildRoomDecisionBlock(
            selectedRoom.Id,
            selectedRoom.MaxGuests,
            selectedRoom.Capacity,
            selectedRoom.ExtraGuestFee,
            $"/Rooms/Details/{selectedRoom.Id}",
            "Bạn chỉnh số khách nếu cần, rồi chọn khung giờ còn trống bên dưới để chốt tiếp. Giá từng slot đã hiển thị sẵn.",
            Math.Max(container.Confirmed.GuestCount, 1),
            baseTotalPrice,
            showCommitButton: false));
        consultBlocks.AddRange(slotBlocksForConsult);

        return new BookingActionResult
        {
            Answer = "Mình đã mở luôn các khung giờ còn trống của phòng này để bạn chọn và chốt nhanh.",
            Action = ConductorAction.ShowSlots,
            State = container,
            UiBlocks = consultBlocks
        };
    }

    private object BuildRoomDecisionBlock(
        int roomId,
        int maxGuests,
        int capacity,
        decimal extraGuestFee,
        string detailsUrl,
        string message,
        int guestCount,
        decimal baseTotalPrice,
        bool showCommitButton,
        string commitLabel = "Chốt phòng này")
    {
        return new
        {
            type = "roomDecisionCta",
            data = new
            {
                roomId,
                detailsUrl,
                detailsLabel = "Xem chi tiết phòng",
                commitLabel,
                message,
                showCommitButton,
                guestCount,
                maxGuests,
                capacity,
                extraGuestFee,
                baseTotalPrice
            }
        };
    }

    private async Task<BookingActionResult> BuildDailyCheckoutCtaAsync(
        BookingSessionContainer container,
        string sessionId,
        int roomId,
        CancellationToken cancellationToken)
    {
        if (roomId <= 0 || !container.Confirmed.CheckInDate.HasValue || !container.Confirmed.CheckOutDate.HasValue)
        {
            return new BookingActionResult
            {
                Answer = "Bạn chọn phòng và ngày nhận/trả trước nhé.",
                Action = ConductorAction.AskInfo,
                State = container
            };
        }

        var checkInDate = container.Confirmed.CheckInDate.Value;
        var checkOutDate = container.Confirmed.CheckOutDate.Value;
        var interval = BookingTimeRules.BuildDailyStay(checkInDate, checkOutDate);
        if (!await IsRoomAvailableAsync(roomId, interval.Start, interval.End, cancellationToken))
        {
            container.Progress ??= new BookingProgressState();
            container.Progress.SelectedRoomId = null;
            CacheState(sessionId, container);
            var roomCards = await BuildRoomCardsAsync(container.Confirmed, cancellationToken);
            return new BookingActionResult
            {
                Answer = "Phòng này đã có khách trong ngày bạn chọn. Mình gửi lại các phòng còn trống để bạn chọn tiếp nhé.",
                Action = ConductorAction.ShowRooms,
                State = container,
                UiBlocks = roomCards
            };
        }

        var room = await _context.Rooms
            .Include(r => r.Branch)
            .Where(r => r.Id == roomId)
            .FirstOrDefaultAsync(cancellationToken);
        var baseTotalPrice = await _pricingService.CalculateStayPriceAsync(roomId, interval.Start, interval.End, false);
        var extraGuestTotal = room == null ? 0m : Math.Max(0, Math.Max(container.Confirmed.GuestCount, 1) - room.Capacity) * room.ExtraGuestFee;
        var finalTotal = baseTotalPrice + extraGuestTotal;

        var checkoutUrl = $"/Bookings/CheckoutDaily?roomId={roomId}&checkInDate={checkInDate:yyyy-MM-dd}&checkOutDate={checkOutDate:yyyy-MM-dd}&guestCount={Math.Max(container.Confirmed.GuestCount, 1)}";

        return BuildCheckoutCtaResult(
            container,
            "Phòng còn trống cho ngày bạn chọn. Bạn bấm nút bên dưới để qua trang điền thông tin chính thức, hoặc để lại SĐT/Zalo để nhân viên hỗ trợ.",
            checkoutUrl,
            [
                $"Phòng: {room?.Name ?? "Đã chọn"}",
                $"Nhận: {checkInDate:dd/MM/yyyy}",
                $"Trả: {checkOutDate:dd/MM/yyyy}",
                $"Khách: {Math.Max(container.Confirmed.GuestCount, 1)}",
                $"Tiền phòng: {baseTotalPrice:N0}đ",
                extraGuestTotal > 0 ? $"Phụ thu khách thêm: {extraGuestTotal:N0}đ" : "Chưa phát sinh phụ thu khách thêm."
            ],
            finalTotal,
            BuildBranchContactData(room?.Branch));
    }

    private async Task<BookingActionResult> BuildHourlyCheckoutCtaAsync(
        BookingSessionContainer container,
        int slotId,
        CancellationToken cancellationToken)
    {
        var roomId = container.Progress?.SelectedRoomId ?? 0;
        if (roomId <= 0 || slotId <= 0)
        {
            return new BookingActionResult
            {
                Answer = "Bạn chọn phòng và khung giờ trước nhé.",
                Action = ConductorAction.AskInfo,
                State = container
            };
        }

        var slot = await _context.RoomSlotInventories
            .Include(slot => slot.Room)
            .ThenInclude(room => room.Branch)
            .FirstOrDefaultAsync(slot => slot.Id == slotId && slot.RoomId == roomId, cancellationToken);

        if (slot == null || !await IsHourlySlotAvailableAsync(slotId, roomId, Math.Max(container.Confirmed.GuestCount, 1), cancellationToken))
        {
            return new BookingActionResult
            {
                Answer = "Khung giờ này vừa có khách giữ chỗ hoặc không còn khả dụng. Bạn chọn khung giờ khác nhé.",
                Action = ConductorAction.ShowSlots,
                State = container,
                UiBlocks = (await BuildSlotsResponse(container, cancellationToken)).UiBlocks
            };
        }

        var baseTotalPrice = await _pricingService.CalculateStayPriceAsync(roomId, slot.StartTime, slot.EndTime, true);
        var extraGuestTotal = slot.Room == null ? 0m : Math.Max(0, Math.Max(container.Confirmed.GuestCount, 1) - slot.Room.Capacity) * slot.Room.ExtraGuestFee;
        var finalTotal = baseTotalPrice + extraGuestTotal;

        var checkoutUrl = $"/Bookings/CheckoutHourly?roomId={roomId}&slotId={slotId}&guestCount={Math.Max(container.Confirmed.GuestCount, 1)}";

        return BuildCheckoutCtaResult(
            container,
            "Khung giờ này còn trống. Bạn bấm nút bên dưới để qua trang điền thông tin chính thức, hoặc để lại SĐT/Zalo để nhân viên hỗ trợ.",
            checkoutUrl,
            [
                $"Phòng: {slot.Room?.Name ?? "Đã chọn"}",
                $"Khung giờ: {slot.SlotLabel}",
                $"Ngày: {slot.SlotDate:dd/MM/yyyy}",
                $"Khách: {Math.Max(container.Confirmed.GuestCount, 1)}",
                $"Tiền slot: {baseTotalPrice:N0}đ",
                extraGuestTotal > 0 ? $"Phụ thu khách thêm: {extraGuestTotal:N0}đ" : "Chưa phát sinh phụ thu khách thêm."
            ],
            finalTotal,
            BuildBranchContactData(slot.Room?.Branch));
    }

    private BookingActionResult BuildCheckoutCtaResult(
        BookingSessionContainer container,
        string answer,
        string checkoutUrl,
        List<string> summaryLines,
        decimal totalPrice,
        object? branchContact = null)
    {
        var uiBlocks = new List<object>
        {
            new
            {
                type = "bookingSummary",
                data = new
                {
                    title = "Xác nhận lựa chọn phòng",
                    lines = summaryLines,
                    totalPrice
                }
            },
            new
            {
                type = "bookingCta",
                data = new
                {
                    target = checkoutUrl,
                    message = "Hoàn tất thông tin ở trang đặt phòng chính thức.",
                    secondaryMessage = "Nếu muốn nhân viên gọi/Zalo hỗ trợ, bạn nhập SĐT bên dưới."
                }
            }
        };

        if (branchContact != null)
        {
            uiBlocks.Add(new
            {
                type = "branchContactCapture",
                data = branchContact
            });
        }

        return new BookingActionResult
        {
            Answer = answer,
            Action = ConductorAction.BookingCta,
            State = container,
            UiBlocks = uiBlocks
        };
    }

    private static object? BuildBranchContactData(Branch? branch)
    {
        if (branch == null) return null;
        return new
        {
            branchId = branch.Id,
            branchName = branch.Name,
            address = branch.Address,
            hotline = branch.Hotline,
            email = branch.Email,
            mapUrl = branch.MapUrl,
            message = "Chi nhánh bạn đang đặt. Nếu cần nhân viên hỗ trợ, nhập SĐT/Zalo để admin liên hệ."
        };
    }

    private async Task<bool> IsHourlySlotAvailableAsync(
        int slotId,
        int roomId,
        int guestCount,
        CancellationToken cancellationToken)
    {
        var slot = await _context.RoomSlotInventories
            .AsNoTracking()
            .Include(item => item.Room)
            .FirstOrDefaultAsync(item => item.Id == slotId, cancellationToken);

        if (slot == null) return false;
        if (slot.RoomId != roomId) return false;
        if (slot.Status != "Available") return false;
        if (slot.Room.Status != "Available") return false;
        if (slot.Room.MaxGuests < guestCount) return false;
        if (slot.StartTime < await ResolveLeadTimeCutoffAsync(slot.Room.BranchId, cancellationToken)) return false;

        return await IsRoomAvailableAsync(roomId, slot.StartTime, slot.EndTime, cancellationToken);
    }

    private async Task<bool> IsRoomAvailableAsync(
        int roomId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
        var hasOverlap = await _context.Bookings
            .AnyAsync(booking => booking.RoomId == roomId
                && !booking.IsDeleted
                && booking.Status != "Cancelled"
                && !(booking.Status == "AwaitingPayment" && booking.CreatedAt < fiveMinutesAgo)
                && booking.StartTime < end
                && booking.EndTime > start,
                cancellationToken);

        if (hasOverlap) return false;

        var hasBlockedSlot = await _context.RoomSlotInventories
            .AnyAsync(slot => slot.RoomId == roomId
                && slot.Status == "Blocked"
                && slot.StartTime < end
                && slot.EndTime > start,
                cancellationToken);

        return !hasBlockedSlot;
    }

    private List<object> LoadBookingFormFields()
    {
        var schemaJson = GetSetting("AIBookingFormSchema", DefaultBookingFormSchema);
        if (string.IsNullOrWhiteSpace(schemaJson) || schemaJson == "[]")
            return new List<object>();

        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            var fields = new List<object>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var type = el.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
                var label = el.TryGetProperty("label", out var labelEl) ? labelEl.GetString() : null;
                var required = el.TryGetProperty("required", out var reqEl) && reqEl.ValueKind == JsonValueKind.True;
                var helpText = el.TryGetProperty("helpText", out var helpEl) ? helpEl.GetString() : null;

                if (string.IsNullOrWhiteSpace(id)) continue;

                fields.Add(new
                {
                    name = id,
                    type = type == "image" ? "file" : (type ?? "text"),
                    label = label ?? id,
                    required,
                    placeholder = helpText ?? ""
                });
            }
            return fields;
        }
        catch
        {
            return new List<object>();
        }
    }

    private int GetShowCount(string sessionId)
        => _cache.TryGetValue($"show-count:{sessionId}", out int count) ? count : 0;

    private void IncrementShowCount(string sessionId)
        => _cache.Set($"show-count:{sessionId}", GetShowCount(sessionId) + 1, CacheTtl);

    private string GetSetting(string key, string fallback)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var setting = db.SystemSettings
                .FirstOrDefault(s => s.GroupName == "AI" && s.SettingKey == key);
            return setting != null && !string.IsNullOrWhiteSpace(setting.SettingValue)
                ? setting.SettingValue
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private int ParseIntSetting(string key, int fallback)
    {
        return int.TryParse(GetSetting(key, fallback.ToString()), out var parsed) ? parsed : fallback;
    }

    private async Task<DateTime> ResolveLeadTimeCutoffAsync(int? branchId, CancellationToken cancellationToken)
    {
        var leadTimeHours = ParseIntSetting("BookingLeadTimeHours", 2);
        if (branchId.HasValue)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var branchLeadTime = await db.Branches
                .Where(branch => branch.Id == branchId.Value)
                .Select(branch => (int?)branch.BookingLeadTimeHours)
                .FirstOrDefaultAsync(cancellationToken);

            if (branchLeadTime.HasValue)
            {
                leadTimeHours = Math.Max(0, branchLeadTime.Value);
            }
        }

        return DateTime.Now.AddHours(leadTimeHours);
    }

    private static bool IsKnownBookingMode(string? bookingMode)
        => string.Equals(bookingMode, "hourly", StringComparison.OrdinalIgnoreCase)
           || string.Equals(bookingMode, "daily", StringComparison.OrdinalIgnoreCase);

    private async Task<AdminAIStudioConfigResponse?> LoadStudioConfigAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetService<IAdminAIStudioConfigService>();
            return service == null ? null : await service.GetAsync();
        }
        catch
        {
            return null;
        }
    }

    private async Task<object> BuildBranchSelectorBlockAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branches = await db.Branches
            .OrderBy(branch => branch.Id)
            .Select(branch => new
            {
                id = branch.Id,
                name = branch.Name
            })
            .ToListAsync(cancellationToken);

        return new
        {
            type = "branchSelector",
            data = new
            {
                fieldKey = "branchId",
                label = "Chọn chi nhánh",
                branches
            }
        };
    }

    private async Task<List<object>> BuildRoomCardsAsync(BookingConfirmedState state, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var leadTimeCutoff = await ResolveLeadTimeCutoffAsync(state.BranchId, cancellationToken);
        var rooms = await db.Rooms
            .Where(r => r.BranchId == state.BranchId && r.Status == "Available")
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                r.PricePerHour,
                r.PricePerDay,
                r.Capacity,
                r.MaxGuests,
                r.ImageUrl,
                Amenities = r.Amenities.Select(a => a.Name).ToList()
            })
            .ToListAsync(cancellationToken);

        var availableHourlyRoomIds = new HashSet<int>();
        if (state.HourlyDate.HasValue && string.Equals(state.BookingMode, "hourly", StringComparison.OrdinalIgnoreCase))
        {
            var slotQuery = db.RoomSlotInventories
                .Where(slot => slot.SlotDate == state.HourlyDate.Value
                    && slot.StartTime >= leadTimeCutoff
                    && slot.Status == "Available");

            if (state.RequestedTimeStart.HasValue && state.RequestedTimeEnd.HasValue)
            {
                slotQuery = slotQuery.Where(slot =>
                    TimeOnly.FromDateTime(slot.StartTime) == state.RequestedTimeStart.Value
                    && TimeOnly.FromDateTime(slot.EndTime) == state.RequestedTimeEnd.Value);
            }

            availableHourlyRoomIds = (await slotQuery
                .Select(slot => slot.RoomId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var requestedSlotRoomIds = new HashSet<int>();
        if (state.HourlyDate.HasValue && state.RequestedTimeStart.HasValue && state.RequestedTimeEnd.HasValue)
        {
            requestedSlotRoomIds = (await db.RoomSlotInventories
                .Where(slot => slot.SlotDate == state.HourlyDate.Value
                    && slot.StartTime >= leadTimeCutoff
                    && slot.Status == "Available"
                    && TimeOnly.FromDateTime(slot.StartTime) == state.RequestedTimeStart.Value
                    && TimeOnly.FromDateTime(slot.EndTime) == state.RequestedTimeEnd.Value)
                .Select(slot => slot.RoomId)
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var filteredRooms = new List<object>();
        foreach (var room in rooms)
        {
            var explanation = await _roomExplanationService.BuildAsync(room.Id, state, cancellationToken);
            if (!explanation.AllowsRequestedGuests)
            {
                continue;
            }

            if (state.HourlyDate.HasValue
                && string.Equals(state.BookingMode, "hourly", StringComparison.OrdinalIgnoreCase)
                && !availableHourlyRoomIds.Contains(room.Id))
            {
                continue;
            }

            var matchesRequestedSlot = requestedSlotRoomIds.Count == 0 || requestedSlotRoomIds.Contains(room.Id);
            if (requestedSlotRoomIds.Count > 0 && !matchesRequestedSlot)
            {
                continue;
            }

            filteredRooms.Add(new
            {
                roomId = room.Id,
                name = room.Name,
                description = room.Description,
                pricePerHour = room.PricePerHour,
                pricePerDay = room.PricePerDay,
                capacity = room.Capacity,
                maxGuests = room.MaxGuests,
                imageUrl = room.ImageUrl,
                amenities = room.Amenities,
                fitsStandardOccupancy = explanation.FitsStandardOccupancy,
                allowsRequestedGuests = explanation.AllowsRequestedGuests,
                extraGuestCount = explanation.ExtraGuestCount,
                extraGuestFeeApplied = explanation.ExtraGuestFeeApplied,
                pricingTierLabel = explanation.PricingTierLabel,
                pricingExplanation = explanation.Lines,
                recommendationReason = explanation.RecommendationReason,
                requestedSlotAvailable = matchesRequestedSlot
            });
        }

        return new List<object>
        {
            new
            {
                type = "roomCards",
                data = new
                {
                    rooms = filteredRooms
                }
            }
        };
    }

    private async Task<List<object>> BuildSlotUiBlocksAsync(BookingSessionContainer container, CancellationToken cancellationToken)
    {
        var date = container.Confirmed.HourlyDate;
        var roomId = container.Progress?.SelectedRoomId ?? container.Progress?.ActiveRoomContextId;
        if (!date.HasValue || !roomId.HasValue)
        {
            return new List<object>();
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var room = await db.Rooms
            .Where(r => r.Id == roomId.Value)
            .Select(r => new { r.Name, r.PricePerHour })
            .FirstOrDefaultAsync(cancellationToken);

        if (room == null)
        {
            return new List<object>();
        }

        var leadTimeCutoff = await ResolveLeadTimeCutoffAsync(container.Confirmed.BranchId, cancellationToken);

        var baseQuery = db.RoomSlotInventories
            .Where(s => s.RoomId == roomId.Value
                        && s.SlotDate == date.Value
                        && s.StartTime >= leadTimeCutoff
                        && s.Status == "Available");

        var slotRecords = await baseQuery
            .OrderBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

        if (container.Confirmed.RequestedTimeStart.HasValue && container.Confirmed.RequestedTimeEnd.HasValue)
        {
            slotRecords = slotRecords
                .Where(s => TimeOnly.FromDateTime(s.StartTime) == container.Confirmed.RequestedTimeStart.Value
                    && TimeOnly.FromDateTime(s.EndTime) == container.Confirmed.RequestedTimeEnd.Value)
                .ToList();
        }

        var slots = slotRecords.Select(s => new
        {
            slotId = s.Id,
            roomId = roomId.Value,
            roomName = room.Name,
            label = s.SlotLabel,
            totalPrice = room.PricePerHour > 0
                ? Math.Round((decimal)(s.EndTime - s.StartTime).TotalHours * room.PricePerHour, 0)
                : 0m,
            status = s.Status == "Available" ? "available" : "booked"
        }).ToList();

        if (slots.Count == 0)
        {
            return new List<object>();
        }

        return new List<object>
        {
            new
            {
                type = "hourlySlots",
                data = new { slots }
            }
        };
    }

    private void HydrateStateFromMessage(BookingSessionContainer container, string message)
    {
        var lowered = message.ToLowerInvariant();

        if (TryExtractDateRange(lowered, out var checkIn, out var checkOut))
        {
            container.Confirmed.BookingMode = "daily";
            container.Confirmed.CheckInDate = checkIn;
            container.Confirmed.CheckOutDate = checkOut;
            container.Confirmed.HourlyDate = null;
            container.Confirmed.RequestedTimeStart = null;
            container.Confirmed.RequestedTimeEnd = null;
            container.Confirmed.RequestedTimeLabel = null;
        }

        if (TryExtractTimeRange(lowered, out var start, out var end, out var label))
        {
            container.Confirmed.BookingMode = "hourly";
            container.Confirmed.RequestedTimeStart = start;
            container.Confirmed.RequestedTimeEnd = end;
            container.Confirmed.RequestedTimeLabel = label;
            container.Confirmed.CheckOutDate = null;
        }

        if (IsHourlyModeSignal(lowered))
        {
            container.Confirmed.BookingMode = "hourly";
            container.Confirmed.CheckOutDate = null;
        }

        if (!TryExtractDateRange(lowered, out _, out _) && container.Confirmed.RequestedTimeStart.HasValue)
        {
            var extractedDate = _entityExtractor.ExtractDateTime(message).Date;
            if (extractedDate.HasValue)
            {
                container.Confirmed.HourlyDate = DateOnly.FromDateTime(extractedDate.Value);
            }
        }
    }

    private static bool TryExtractDateRange(string text, out DateOnly checkIn, out DateOnly checkOut)
    {
        checkIn = default;
        checkOut = default;
        var match = Regex.Match(text, @"(?<!\d)(\d{1,2})\s*[-]\s*(\d{1,2})\s*[\/-]\s*(\d{1,2})(?:\s*[\/-]\s*(\d{2,4}))?");
        if (!match.Success)
        {
            return false;
        }

        var currentYear = DateTime.UtcNow.AddHours(7).Year;
        var startDay = int.Parse(match.Groups[1].Value);
        var endDay = int.Parse(match.Groups[2].Value);
        var month = int.Parse(match.Groups[3].Value);
        var year = match.Groups[4].Success ? NormalizeYear(int.Parse(match.Groups[4].Value)) : currentYear;

        try
        {
            checkIn = new DateOnly(year, month, startDay);
            checkOut = new DateOnly(year, month, endDay);
            if (checkOut <= checkIn)
            {
                return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryExtractTimeRange(string text, out TimeOnly start, out TimeOnly end, out string label)
    {
        start = default;
        end = default;
        label = string.Empty;
        var match = Regex.Match(text, @"(?<!\d)(\d{1,2})(?:[:h](\d{2}))?\s*[-–]\s*(\d{1,2})(?:[:h](\d{2}))?\s*h?");
        if (!match.Success)
        {
            return false;
        }

        if (!TryParseTime(match.Groups[1].Value, match.Groups[2].Value, out start)
            || !TryParseTime(match.Groups[3].Value, match.Groups[4].Value, out end)
            || end <= start)
        {
            return false;
        }

        label = $"{start:HH\\:mm}-{end:HH\\:mm}";
        return true;
    }

    private static bool TryParseTime(string hourText, string minuteText, out TimeOnly time)
    {
        time = default;
        if (!int.TryParse(hourText, out var hour))
        {
            return false;
        }

        var minute = 0;
        if (!string.IsNullOrWhiteSpace(minuteText) && !int.TryParse(minuteText, out minute))
        {
            return false;
        }

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            return false;
        }

        time = new TimeOnly(hour, minute);
        return true;
    }

    private static int NormalizeYear(int year)
        => year < 100 ? 2000 + year : year;

    private static bool IsHourlyModeSignal(string lowered)
    {
        if (Regex.IsMatch(lowered, @"\b\d{1,2}\s*[-:h]"))
        {
            return true;
        }

        string[] hourlyPhrases =
        [
            "khung giờ",
            "theo giờ",
            "phòng giờ",
            "giờ trống",
            "thuê giờ",
            "đặt giờ",
            "book giờ",
            "ở theo giờ"
        ];

        return hourlyPhrases.Any(lowered.Contains);
    }

    private static List<string> GetMissingRequiredFields(BookingConfirmedState state)
    {
        var missing = new List<string>();
        if (!state.BranchId.HasValue)
        {
            missing.Add("branchId");
        }

        var isDaily = string.Equals(state.BookingMode, "daily", StringComparison.OrdinalIgnoreCase);
        if (isDaily)
        {
            if (!state.CheckInDate.HasValue)
            {
                missing.Add("checkInDate");
            }
            if (!state.CheckOutDate.HasValue)
            {
                missing.Add("checkOutDate");
            }
        }
        else
        {
            if (!state.HourlyDate.HasValue && !state.CheckInDate.HasValue)
            {
                missing.Add("hourlyDate");
            }
        }

        if (state.GuestCount <= 0)
        {
            missing.Add("guestCount");
        }

        return missing;
    }

    private async Task<List<object>> BuildMissingFieldBlocksAsync(BookingConfirmedState state, CancellationToken cancellationToken)
    {
        var uiBlocks = new List<object>();
        if (state.MissingRequiredFields.Contains("branchId"))
        {
            uiBlocks.Add(await BuildBranchSelectorBlockAsync(cancellationToken));
            return uiBlocks;
        }

        if (state.MissingRequiredFields.Contains("checkInDate") || state.MissingRequiredFields.Contains("checkOutDate"))
        {
            uiBlocks.Add(new
            {
                type = "dateRangePicker",
                data = new { }
            });
            return uiBlocks;
        }

        if (state.MissingRequiredFields.Contains("hourlyDate"))
        {
            uiBlocks.Add(new
            {
                type = "singleDatePicker",
                data = new { }
            });
        }

        return uiBlocks;
    }

    private static bool IsImplicitBookingSignal(string lowered, BookingConfirmedState state)
    {
        return state.CheckInDate.HasValue
            || state.CheckOutDate.HasValue
            || state.HourlyDate.HasValue
            || state.RequestedTimeStart.HasValue
            || Regex.IsMatch(lowered, @"\b\d+\s*(người|khách|nguoi|khach)\b")
            || Regex.IsMatch(lowered, @"\b\d{1,2}\s*[-/]\s*\d{1,2}\b");
    }

    private static bool IsRoomContextOccupancyQuestion(string message, BookingSessionContainer container)
    {
        if (container.Progress?.ActiveRoomContextId == null)
        {
            return false;
        }

        var lowered = message.ToLowerInvariant();
        return lowered.Contains("phòng này")
            && (lowered.Contains("được không") || lowered.Contains("bao nhiêu người") || lowered.Contains("ở 3 người") || lowered.Contains("ở 4 người"));
    }

    private static bool IsRoomContextPriceQuestion(string message, BookingSessionContainer container)
    {
        if (container.Progress?.ActiveRoomContextId == null)
        {
            return false;
        }

        var lowered = message.ToLowerInvariant();
        return lowered.Contains("giá")
            || lowered.Contains("bao nhiêu")
            || lowered.Contains("chi phí")
            || lowered.Contains("phụ thu")
            || lowered.Contains("cuối tuần")
            || lowered.Contains("thứ 7")
            || lowered.Contains("chủ nhật")
            || lowered.Contains("lễ");
    }

    private static bool IsRoomContextPolicyQuestion(string message, BookingSessionContainer container)
    {
        if (container.Progress?.ActiveRoomContextId == null)
        {
            return false;
        }

        var lowered = message.ToLowerInvariant();
        return lowered.Contains("phụ thu")
            || lowered.Contains("cuối tuần")
            || lowered.Contains("thứ 7")
            || lowered.Contains("chủ nhật")
            || lowered.Contains("lễ")
            || lowered.Contains("tối đa")
            || lowered.Contains("quá số người")
            || lowered.Contains("vượt chuẩn")
            || lowered.Contains("ở thêm");
    }

    private static bool IsRoomContextSlotQuestion(string message, BookingSessionContainer container)
    {
        if (container.Progress?.ActiveRoomContextId == null)
        {
            return false;
        }

        var lowered = message.ToLowerInvariant();
        return lowered.Contains("khung giờ")
            || lowered.Contains("giờ nào")
            || lowered.Contains("slot nào")
            || lowered.Contains("còn giờ")
            || Regex.IsMatch(lowered, @"\b\d{1,2}(?::\d{2}|h)?\s*[-–]\s*\d{1,2}(?::\d{2}|h)?");
    }

    private async Task TryBindBranchContextFromMessageAsync(BookingSessionContainer container, string message, CancellationToken cancellationToken)
    {
        if (container.Confirmed.BranchId.HasValue)
        {
            return;
        }

        var normalizedMessage = NormalizeForRoomMatch(message);
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return;
        }

        var branches = await _context.Branches
            .AsNoTracking()
            .Select(branch => new { branch.Id, branch.Name, branch.Address })
            .ToListAsync(cancellationToken);

        var match = branches
            .Select(branch => new
            {
                branch.Id,
                branch.Name,
                Score = Math.Max(
                    CalculateTextMatchScore(normalizedMessage, branch.Name, requireNumericTokens: false, minimumMatchedTokens: 1),
                    CalculateTextMatchScore(normalizedMessage, branch.Address, requireNumericTokens: false, minimumMatchedTokens: 2))
            })
            .Where(candidate => candidate.Score >= 20)
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Name.Length)
            .FirstOrDefault();

        if (match == null)
        {
            return;
        }

        container.Confirmed.BranchId = match.Id;
        container.Confirmed.BranchName = match.Name;
    }

    private async Task TryBindRoomContextFromMessageAsync(BookingSessionContainer container, string message, CancellationToken cancellationToken)
    {
        var lowered = message.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(lowered))
        {
            return;
        }

        if (container.Progress?.ActiveRoomContextId.HasValue == true)
        {
            return;
        }

        IQueryable<Room> query = _context.Rooms.AsNoTracking().Where(r => r.Status == "Available");
        if (container.Confirmed.BranchId.HasValue)
        {
            query = query.Where(r => r.BranchId == container.Confirmed.BranchId.Value);
        }

        var rooms = await query
            .Select(r => new { r.Id, r.Name, r.BranchId })
            .ToListAsync(cancellationToken);

        var normalizedMessage = NormalizeForRoomMatch(lowered);
        var match = rooms
            .Select(room => new
            {
                room.Id,
                room.Name,
                room.BranchId,
                Score = CalculateRoomMatchScore(normalizedMessage, room.Name)
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Name.Length)
            .FirstOrDefault();

        if (match == null)
        {
            return;
        }

        var branchName = await _context.Branches
            .AsNoTracking()
            .Where(branch => branch.Id == match.BranchId)
            .Select(branch => branch.Name)
            .FirstOrDefaultAsync(cancellationToken);

        container.Progress ??= new BookingProgressState();
        container.Progress.ActiveRoomContextId = match.Id;
        if (!container.Progress.SelectedRoomId.HasValue)
        {
            container.Progress.SelectedRoomId = match.Id;
        }
        container.Confirmed.BranchId ??= match.BranchId;
        container.Confirmed.BranchName ??= branchName;
    }

    private static List<JsonElement> ExtractSlots(List<object> uiBlocks)
    {
        var slots = new List<JsonElement>();
        foreach (var block in uiBlocks)
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(block));
            if (!doc.RootElement.TryGetProperty("type", out var typeElement)
                || !string.Equals(typeElement.GetString(), "hourlySlots", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (doc.RootElement.TryGetProperty("data", out var dataElement)
                && dataElement.TryGetProperty("slots", out var slotElement)
                && slotElement.ValueKind == JsonValueKind.Array)
            {
                slots.AddRange(slotElement.EnumerateArray());
            }
        }

        return slots;
    }

    private static bool IsExitKeywordMatch(string lowered, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        if (keyword.Equals("không", StringComparison.OrdinalIgnoreCase))
        {
            return lowered == "không" || lowered == "không cần nữa" || lowered == "không đặt nữa";
        }

        return lowered.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static int CalculateRoomMatchScore(string normalizedMessage, string roomName)
        => CalculateTextMatchScore(normalizedMessage, roomName, requireNumericTokens: true, minimumMatchedTokens: 2);

    private static int CalculateTextMatchScore(string normalizedMessage, string candidateName, bool requireNumericTokens, int minimumMatchedTokens)
    {
        var normalizedCandidate = NormalizeForRoomMatch(candidateName);
        if (string.IsNullOrWhiteSpace(normalizedCandidate) || string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return 0;
        }

        if (normalizedMessage.Contains(normalizedCandidate, StringComparison.Ordinal))
        {
            return 1000 + normalizedCandidate.Length;
        }

        var compactMessage = normalizedMessage.Replace(" ", string.Empty, StringComparison.Ordinal);
        var compactCandidate = normalizedCandidate.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(compactCandidate) && compactMessage.Contains(compactCandidate, StringComparison.Ordinal))
        {
            return 950 + compactCandidate.Length;
        }

        var candidateTokens = normalizedCandidate.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matchedTokens = candidateTokens.Count(token => normalizedMessage.Contains(token, StringComparison.Ordinal));
        if (matchedTokens == 0)
        {
            return 0;
        }

        if (matchedTokens < minimumMatchedTokens)
        {
            return 0;
        }

        var numericTokens = candidateTokens.Where(token => token.Any(char.IsDigit)).ToList();
        var matchedNumericTokens = numericTokens.Count(token => normalizedMessage.Contains(token, StringComparison.Ordinal));
        matchedNumericTokens += numericTokens.Count(token => compactMessage.Contains(token.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal));
        matchedNumericTokens = Math.Min(matchedNumericTokens, numericTokens.Count);

        if (requireNumericTokens && matchedNumericTokens == 0 && numericTokens.Count > 0)
        {
            return 0;
        }

        return matchedTokens * 10 + matchedNumericTokens * 50;
    }

    private static void ApplyActionState(BookingSessionContainer container, BookingActionRequest actionRequest)
    {
        if (actionRequest.BranchId.HasValue)
        {
            container.Confirmed.BranchId = actionRequest.BranchId.Value;
        }

        if (actionRequest.GuestCount > 0)
        {
            container.Confirmed.GuestCount = actionRequest.GuestCount;
        }

        if (IsKnownBookingMode(actionRequest.BookingMode))
        {
            container.Confirmed.BookingMode = actionRequest.BookingMode!;
        }

        if (!string.IsNullOrWhiteSpace(actionRequest.HourlyDate)
            && DateOnly.TryParse(actionRequest.HourlyDate, out var hourlyDate))
        {
            container.Confirmed.HourlyDate = hourlyDate;
        }

        if (!string.IsNullOrWhiteSpace(actionRequest.CheckInDate)
            && DateOnly.TryParse(actionRequest.CheckInDate, out var checkInDate))
        {
            container.Confirmed.CheckInDate = checkInDate;
            if (!string.Equals(container.Confirmed.BookingMode, "daily", StringComparison.OrdinalIgnoreCase))
            {
                container.Confirmed.HourlyDate = checkInDate;
            }
        }

        if (!string.IsNullOrWhiteSpace(actionRequest.CheckOutDate)
            && DateOnly.TryParse(actionRequest.CheckOutDate, out var checkOutDate))
        {
            container.Confirmed.CheckOutDate = checkOutDate;
        }
    }

    private static string NormalizeForRoomMatch(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var normalizedChar = character switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => character
            };

            builder.Append(char.IsLetterOrDigit(normalizedChar) ? char.ToLowerInvariant(normalizedChar) : ' ');
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }
}
