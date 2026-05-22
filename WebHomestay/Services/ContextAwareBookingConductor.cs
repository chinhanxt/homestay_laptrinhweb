using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Services;

public class ContextAwareBookingConductor : IBookingConductor
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
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
        IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _cache = cache;
        _scopeFactory = scopeFactory;
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
        container.Confirmed = MergeFromRequest(container.Confirmed, request);

        var intent = ClassifyIntent(message, container);
        var showCount = GetShowCount(sessionId);

        var action = ResolveAction(intent, container, showCount);
        var uiBlocks = new List<object>();

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
            container.Progress = null;
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
        if (exitKeywords.Any(k => lowered.Contains(k)))
            return MessageIntent.Exit;

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
           && (state.HourlyDate.HasValue || state.CheckInDate.HasValue)
           && state.GuestCount > 0;

    private ConductorAction ResolveAction(MessageIntent intent, BookingSessionContainer container, int showCount)
    {
        var autoShow = GetSetting("AIPublicBookingAutoShowRooms", "true") == "true";
        var maxShows = int.Parse(GetSetting("AIPublicBookingMaxRoomShows", "2"));
        var cooldown = int.Parse(GetSetting("AIPublicBookingRoomCooldown", "3"));
        var mode = GetSetting("AIPublicBookingProactiveMode", "balanced");

        switch (intent)
        {
            case MessageIntent.BookingIntent:
                return HasEnoughInfo(container.Confirmed)
                    ? ConductorAction.ShowRooms
                    : ConductorAction.AskInfo;

            case MessageIntent.BrowsingRooms:
                if (!HasEnoughInfo(container.Confirmed))
                    return ConductorAction.AskInfo;
                if (!autoShow)
                    return ConductorAction.Reply;
                if (showCount >= maxShows)
                    return ConductorAction.Reply;
                return ConductorAction.ShowRooms;

            case MessageIntent.PriceQuestion:
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
            state.HourlyDate = DateOnly.FromDateTime(request.StartTime.Value);
            state.CheckInDate = DateOnly.FromDateTime(request.StartTime.Value);
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

        switch (actionRequest.Action)
        {
            case "select-room":
                container.Progress.SelectedRoomId = actionRequest.RoomId;
                CacheState(actionRequest.SessionId, container);
                if (container.Confirmed.HourlyDate.HasValue || container.Confirmed.CheckInDate.HasValue)
                {
                    return await BuildSlotsResponse(container, cancellationToken);
                }
                return new BookingActionResult
                {
                    Answer = "Bạn muốn đặt phòng theo giờ hay theo ngày?",
                    Action = ConductorAction.AskInfo,
                    State = container
                };

            case "select-slot":
                container.Progress.SelectedSlotId = actionRequest.SlotId;
                CacheState(actionRequest.SessionId, container);
                return new BookingActionResult
                {
                    Answer = "Bạn điền thông tin để mình tiến hành đặt phòng nhé.",
                    Action = ConductorAction.ShowForm,
                    State = container,
                    UiBlocks = new List<object>
                    {
                        new
                        {
                            type = "bookingForm",
                            data = new { fields = LoadBookingFormFields() }
                        }
                    }
                };

            case "submit-booking-form":
            case "submit-form":
                if (actionRequest.FormData != null)
                {
                    container.Progress.CustomerName = actionRequest.FormData.GetValueOrDefault("customerName");
                    container.Progress.CustomerPhone = actionRequest.FormData.GetValueOrDefault("phoneNumber")
                        ?? actionRequest.FormData.GetValueOrDefault("customerPhone");
                    container.Progress.CustomerEmail = actionRequest.FormData.GetValueOrDefault("email")
                        ?? actionRequest.FormData.GetValueOrDefault("customerEmail");
                }
                CacheState(actionRequest.SessionId, container);
                return new BookingActionResult
                {
                    Answer = "Cảm ơn bạn! Mình đang tiến hành đặt phòng. Bạn vui lòng chuyển khoản theo mã QR bên dưới để giữ chỗ nhé.",
                    Action = ConductorAction.PaymentQr,
                    State = container,
                    UiBlocks = new List<object> { new { type = "paymentQr" } }
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
        var date = container.Confirmed.HourlyDate ?? container.Confirmed.CheckInDate;
        if (!date.HasValue || !container.Confirmed.BranchId.HasValue || container.Progress?.SelectedRoomId == null)
        {
            return new BookingActionResult
            {
                Answer = "Vui lòng chọn ngày và phòng trước.",
                Action = ConductorAction.AskInfo,
                State = container
            };
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var room = await db.Rooms
            .Where(r => r.Id == container.Progress.SelectedRoomId)
            .Select(r => new { r.Name, r.PricePerHour })
            .FirstOrDefaultAsync(cancellationToken);

        var slotRecords = await db.RoomSlotInventories
            .Where(s => s.RoomId == container.Progress.SelectedRoomId
                        && s.SlotDate == date.Value
                        && s.Status == "Available")
            .OrderBy(s => s.StartTime)
            .ToListAsync(cancellationToken);

        var roomName = room?.Name ?? "Phòng";
        var pricePerHour = room?.PricePerHour ?? 0m;
        var roomId = container.Progress.SelectedRoomId ?? 0;

        var slotBlocks = slotRecords.Select(s => new
        {
            slotId = s.Id,
            roomId,
            roomName,
            label = s.SlotLabel,
            totalPrice = pricePerHour > 0
                ? Math.Round((decimal)(s.EndTime - s.StartTime).TotalHours * pricePerHour, 0)
                : 0m,
            status = s.Status == "Available" ? "available" : "booked"
        }).ToList();

        return new BookingActionResult
        {
            Answer = $"Có {slotBlocks.Count} khung giờ trống. Bạn chọn giờ nào?",
            Action = ConductorAction.ShowSlots,
            State = container,
            UiBlocks = new List<object>
            {
                new
                {
                    type = "hourlySlots",
                    data = new { slots = slotBlocks }
                }
            }
        };
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

    private async Task<List<object>> BuildRoomCardsAsync(BookingConfirmedState state, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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

        return new List<object>
        {
            new
            {
                type = "roomCards",
                data = new
                {
                    rooms = rooms.Select(r => new
                    {
                        roomId = r.Id,
                        name = r.Name,
                        description = r.Description,
                        pricePerHour = r.PricePerHour,
                        pricePerDay = r.PricePerDay,
                        capacity = r.Capacity,
                        maxGuests = r.MaxGuests,
                        imageUrl = r.ImageUrl,
                        amenities = r.Amenities
                    }).ToList()
                }
            }
        };
    }
}
