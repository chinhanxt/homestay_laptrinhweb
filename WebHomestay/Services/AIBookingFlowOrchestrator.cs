using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services;

public class AIBookingFlowOrchestrator : IAIBookingFlowOrchestrator
{
    private readonly ApplicationDbContext _context;
    private readonly IAvailabilityService _availabilityService;
    private readonly IBookingCreationService _bookingCreationService;
    private readonly PricingService _pricingService;
    private readonly IMemoryCache _cache;

    public AIBookingFlowOrchestrator(
        ApplicationDbContext context,
        IAvailabilityService availabilityService,
        IBookingCreationService bookingCreationService,
        PricingService pricingService,
        IMemoryCache cache)
    {
        _context = context;
        _availabilityService = availabilityService;
        _bookingCreationService = bookingCreationService;
        _pricingService = pricingService;
        _cache = cache;
    }

    public async Task<AIBookingFlowResponse> HandleChatAsync(PublicAIChatRequest request, CancellationToken cancellationToken = default)
    {
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId.Trim();
        var message = request.Message?.Trim() ?? string.Empty;
        var state = new AIBookingSessionState
        {
            CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? null : request.CustomerName.Trim(),
            BranchId = request.BranchId,
            BookingMode = NormalizeMode(request.BookingMode),
            GuestCount = request.GuestCount <= 0 ? 1 : request.GuestCount
        };

        if (!state.BranchId.HasValue)
        {
            return BuildResponse(sessionId, "need-branch", "Bạn chọn giúp mình chi nhánh trước để mình kiểm tra phòng trống nhé.", state);
        }

        var requestedDate = ExtractDateOrDefaultToday(message, request.StartTime);
        state.HourlyDate = requestedDate;

        if (TryExtractHourlyRange(message, requestedDate, out var rangeStart, out var rangeEnd))
        {
            return await BuildExactOrNearbyHourlySlotsAsync(sessionId, state, rangeStart, rangeEnd, cancellationToken);
        }

        return await BuildAvailableHourlySlotsForDateAsync(sessionId, state, requestedDate, "Mình kiểm tra theo thông tin bạn đã chọn. Các khung giờ còn trống hôm nay là:", cancellationToken);
    }

    public async Task<AIBookingFlowResponse> BuildRoomCardsAsync(AIBookingSessionState state)
    {
        var mode = NormalizeMode(state.BookingMode);
        var rooms = await _context.Rooms
            .AsNoTracking()
            .Include(room => room.Amenities)
            .Where(room => room.BranchId == state.BranchId
                && room.Status == "Available"
                && room.MaxGuests >= state.GuestCount)
            .OrderBy(room => mode == "daily" ? room.PricePerDay : room.PricePerHour)
            .ThenBy(room => room.Name)
            .Select(room => new AIRoomCard
            {
                RoomId = room.Id,
                Name = room.Name,
                Description = room.Description,
                PricePerHour = room.PricePerHour,
                PricePerDay = room.PricePerDay,
                Capacity = room.Capacity,
                MaxGuests = room.MaxGuests,
                ExtraGuestFee = room.ExtraGuestFee,
                ImageUrl = room.ImageUrl,
                Amenities = room.Amenities.Select(amenity => amenity.Name).ToList(),
                DetailsUrl = $"/Rooms/Details/{room.Id}"
            })
            .ToListAsync();

        var responseState = CloneState(state);
        responseState.BookingMode = mode;
        return BuildResponse(state, "select-room", "Mình đã lọc các phòng phù hợp theo chi nhánh và số khách.", responseState,
            new AIUiBlock { Type = "roomCards", Data = new { rooms } });
    }

    public async Task<AIBookingFlowResponse> SelectRoomAsync(AIBookingActionRequest request)
    {
        var state = MergeState(request);
        var room = await LoadSelectedRoomAsync(state.SelectedRoomId);
        state.SelectedRoomName = room.Name;
        state.BookingMode = NormalizeMode(state.BookingMode);

        if (state.BookingMode == "daily")
        {
            return await BuildDailyRoomResponseAsync(request.SessionId, state, "dailyRooms", "Phòng đã chọn còn trống cho lịch ngày bạn yêu cầu.");
        }

        var slotDate = state.HourlyDate ?? DateOnly.FromDateTime(DateTime.Today);
        var slots = await _context.RoomSlotInventories
            .AsNoTracking()
            .Where(slot => slot.RoomId == room.Id
                && slot.SlotDate == slotDate
                && slot.Status == "Available")
            .OrderBy(slot => slot.StartTime)
            .ToListAsync();

        var slotOptions = new List<AISlotOption>();
        foreach (var slot in slots)
        {
            if (!await _availabilityService.IsRoomAvailable(room.Id, slot.StartTime, slot.EndTime))
            {
                continue;
            }

            slotOptions.Add(new AISlotOption
            {
                SlotId = slot.Id,
                RoomId = room.Id,
                RoomName = room.Name,
                Label = slot.SlotLabel,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                TotalPrice = await CalculateTotalPriceAsync(room, slot.StartTime, slot.EndTime, true, state.GuestCount)
            });
        }

        return BuildResponse(request, "select-slot", "Bạn chọn khung giờ còn trống, mình sẽ tóm tắt trước khi điền thông tin đặt phòng.", state,
            new AIUiBlock { Type = "hourlySlots", Data = new { slots = slotOptions } });
    }

    public async Task<AIBookingFlowResponse> SelectSlotAsync(AIBookingActionRequest request)
    {
        var state = MergeState(request);
        var selectedRoomId = state.SelectedRoomId;
        var selectedDate = state.HourlyDate;
        var slot = await _context.RoomSlotInventories
            .AsNoTracking()
            .Include(slot => slot.Room)
            .SingleOrDefaultAsync(slot => slot.Id == state.SelectedSlotId);

        if (slot == null
            || slot.Status != "Available"
            || (selectedRoomId.HasValue && slot.RoomId != selectedRoomId.Value)
            || (selectedDate.HasValue && slot.SlotDate != selectedDate.Value)
            || !await _availabilityService.IsRoomAvailable(slot.RoomId, slot.StartTime, slot.EndTime))
        {
            state.SelectedSlotId = null;
            state.SelectedSlotLabel = null;
            return BuildResponse(request, "select-slot", "Khung giờ này không còn phù hợp hoặc vừa được người khác đặt. Bạn vui lòng chọn khung giờ khác nhé.", state);
        }

        state.SelectedRoomId = slot.RoomId;
        state.SelectedRoomName = slot.Room.Name;
        state.SelectedSlotLabel = slot.SlotLabel;
        state.HourlyDate = slot.SlotDate;
        state.BookingMode = "hourly";
        var total = await CalculateTotalPriceAsync(slot.Room, slot.StartTime, slot.EndTime, true, state.GuestCount);
        return BuildSummaryAndFormResponse(request, state, total, BuildHourlySummaryLines(state, slot.StartTime, slot.EndTime), "submit-booking-form");
    }

    public async Task<AIBookingFlowResponse> SelectDailyRoomAsync(AIBookingActionRequest request)
    {
        var state = MergeState(request);
        return await BuildDailyRoomResponseAsync(request.SessionId, state, "booking", "Mình đã tóm tắt lịch ngày, bạn điền thông tin để tạo booking giữ chỗ.");
    }

    public async Task<AIBookingFlowResponse> SubmitBookingFormAsync(AIBookingActionRequest request)
    {
        var state = MergeState(request);
        var form = request.FormSubmission ?? throw new InvalidOperationException("Thiếu thông tin khách đặt phòng.");
        var mode = NormalizeMode(state.BookingMode);

        var createRequest = new CreateBookingRequest
        {
            RoomId = state.SelectedRoomId ?? throw new InvalidOperationException("Chưa chọn phòng."),
            BookingMode = mode == "daily" ? BookingMode.Daily : BookingMode.Hourly,
            SlotInventoryId = state.SelectedSlotId,
            CheckInDate = state.CheckInDate,
            CheckOutDate = state.CheckOutDate,
            CustomerName = form.CustomerName,
            CustomerPhone = form.PhoneNumber,
            CustomerEmail = form.Email,
            CustomerNote = form.Notes,
            GuestCount = state.GuestCount
        };

        var booking = mode == "daily"
            ? await _bookingCreationService.CreateDailyBookingAsync(createRequest)
            : await _bookingCreationService.CreateHourlyBookingAsync(createRequest);

        state.BookingId = booking.Id;
        state.PaymentStatus = booking.PaymentStatus;
        state.CustomerName = booking.CustomerName;

        return BuildResponse(request, "payment", "Booking đã được tạo ở trạng thái chờ thanh toán.", state,
            new AIUiBlock
            {
                Type = "paymentQr",
                Data = new AIPaymentBlock
                {
                    BookingId = booking.Id,
                    PaymentStatus = booking.PaymentStatus,
                    Amount = booking.TotalPrice,
                    PaymentUrl = $"/Bookings/Payment/{booking.Id}",
                    SuccessUrl = $"/Bookings/Success/{booking.Id}",
                    Instructions = "Vui lòng thanh toán để hoàn tất giữ phòng."
                }
            });
    }

    public Task<AIBookingFlowResponse> HandleActionAsync(AIBookingActionRequest request)
    {
        return request.Action switch
        {
            "select-room" => SelectRoomAsync(request),
            "select-slot" => SelectSlotAsync(request),
            "select-daily-room" => SelectDailyRoomAsync(request),
            "submit-booking-form" => SubmitBookingFormAsync(request),
            _ => throw new InvalidOperationException($"Không hỗ trợ hành động AI booking: {request.Action}")
        };
    }

    private async Task<AIBookingFlowResponse> BuildAvailableHourlySlotsForDateAsync(string sessionId, AIBookingSessionState state, DateOnly date, string message, CancellationToken cancellationToken)
    {
        var slots = await BuildHourlySlotOptionsAsync(state, date, null, null, cancellationToken);
        var step = slots.Count == 0 ? "no-availability" : "select-slot";
        var responseMessage = slots.Count == 0
            ? "Hiện không còn khung giờ phù hợp theo thông tin bạn đã chọn. Bạn thử đổi giờ, ngày hoặc chi nhánh giúp mình nhé."
            : message;
        return BuildResponse(sessionId, step, responseMessage, state, new AIUiBlock { Type = "hourlySlots", Data = new { slots } });
    }

    private async Task<AIBookingFlowResponse> BuildExactOrNearbyHourlySlotsAsync(string sessionId, AIBookingSessionState state, DateTime rangeStart, DateTime rangeEnd, CancellationToken cancellationToken)
    {
        state.HourlyDate = DateOnly.FromDateTime(rangeStart);
        var exactSlots = await BuildHourlySlotOptionsAsync(state, state.HourlyDate.Value, rangeStart, rangeEnd, cancellationToken);
        exactSlots = exactSlots
            .Where(slot => slot.StartTime == rangeStart && slot.EndTime == rangeEnd)
            .ToList();

        if (exactSlots.Count > 0)
        {
            return BuildResponse(sessionId, "select-slot", "Đúng khung giờ bạn hỏi hiện còn phòng. Bạn chọn phòng/slot bên dưới nhé.", state,
                new AIUiBlock { Type = "hourlySlots", Data = new { slots = exactSlots } });
        }

        var nearbyStart = rangeStart.AddHours(-2);
        var nearbyEnd = rangeEnd.AddHours(2);
        var nearbySlots = await BuildHourlySlotOptionsAsync(state, state.HourlyDate.Value, nearbyStart, nearbyEnd, cancellationToken);
        nearbySlots = nearbySlots
            .Where(slot => slot.StartTime >= nearbyStart && slot.EndTime <= nearbyEnd)
            .OrderBy(slot => Math.Abs((slot.StartTime - rangeStart).TotalMinutes))
            .ThenBy(slot => slot.TotalPrice)
            .ToList();

        if (nearbySlots.Count == 0)
        {
            return BuildResponse(sessionId, "no-availability", "Khung giờ bạn hỏi hiện không còn phòng, và trong vòng gần 2 giờ cũng chưa có slot phù hợp. Bạn thử đổi giờ hoặc ngày giúp mình nhé.", state,
                new AIUiBlock { Type = "hourlySlots", Data = new { slots = nearbySlots } });
        }

        return BuildResponse(sessionId, "select-slot", "Khung giờ bạn hỏi đã hết. Mình gợi ý các giờ còn trống gần đó trong vòng 2 giờ cùng ngày nhé.", state,
            new AIUiBlock { Type = "hourlySlots", Data = new { slots = nearbySlots } });
    }

    private async Task<List<AISlotOption>> BuildHourlySlotOptionsAsync(AIBookingSessionState state, DateOnly date, DateTime? windowStart, DateTime? windowEnd, CancellationToken cancellationToken)
    {
        var query = _context.RoomSlotInventories
            .AsNoTracking()
            .Include(slot => slot.Room)
            .Where(slot => slot.SlotDate == date
                && slot.Status == "Available"
                && slot.Room.BranchId == state.BranchId
                && slot.Room.Status == "Available"
                && slot.Room.MaxGuests >= state.GuestCount);

        if (windowStart.HasValue && windowEnd.HasValue)
        {
            query = query.Where(slot => slot.StartTime < windowEnd.Value && slot.EndTime > windowStart.Value);
        }

        var inventorySlots = await query
            .OrderBy(slot => slot.StartTime)
            .ThenBy(slot => slot.Room.PricePerHour)
            .ToListAsync(cancellationToken);

        var options = new List<AISlotOption>();
        foreach (var slot in inventorySlots)
        {
            if (!await _availabilityService.IsRoomAvailable(slot.RoomId, slot.StartTime, slot.EndTime))
            {
                continue;
            }

            options.Add(new AISlotOption
            {
                SlotId = slot.Id,
                RoomId = slot.RoomId,
                RoomName = slot.Room.Name,
                Label = slot.SlotLabel,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                TotalPrice = await CalculateTotalPriceAsync(slot.Room, slot.StartTime, slot.EndTime, true, state.GuestCount)
            });
        }

        return options;
    }

    private static DateOnly ExtractDateOrDefaultToday(string message, DateTime? requestStartTime)
    {
        var lowered = message.ToLowerInvariant();
        if (lowered.Contains("ngày mai") || lowered.Contains("ngay mai")) return DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        if (lowered.Contains("hôm nay") || lowered.Contains("hom nay")) return DateOnly.FromDateTime(DateTime.Today);

        var match = Regex.Match(lowered, @"(?:ngày\s*)?(\d{1,2})[/-](\d{1,2})(?:[/-](\d{2,4}))?");
        if (match.Success)
        {
            var day = int.Parse(match.Groups[1].Value);
            var month = int.Parse(match.Groups[2].Value);
            var year = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : DateTime.Today.Year;
            if (year < 100) year += 2000;
            if (DateOnly.TryParse($"{year:D4}-{month:D2}-{day:D2}", out var parsed)) return parsed;
        }

        if (requestStartTime.HasValue) return DateOnly.FromDateTime(requestStartTime.Value);
        return DateOnly.FromDateTime(DateTime.Today);
    }

    private static bool TryExtractHourlyRange(string message, DateOnly date, out DateTime start, out DateTime end)
    {
        start = default;
        end = default;
        var match = Regex.Match(message.ToLowerInvariant(), @"(\d{1,2})(?:h|:)(\d{2})?\s*[-–đến]+\s*(\d{1,2})(?:h|:)(\d{2})?");
        if (!match.Success) return false;

        var startHour = int.Parse(match.Groups[1].Value);
        var startMinute = match.Groups[2].Success && !string.IsNullOrWhiteSpace(match.Groups[2].Value) ? int.Parse(match.Groups[2].Value) : 0;
        var endHour = int.Parse(match.Groups[3].Value);
        var endMinute = match.Groups[4].Success && !string.IsNullOrWhiteSpace(match.Groups[4].Value) ? int.Parse(match.Groups[4].Value) : 0;
        if (startHour > 23 || endHour > 23 || startMinute > 59 || endMinute > 59) return false;

        start = date.ToDateTime(new TimeOnly(startHour, startMinute));
        end = date.ToDateTime(new TimeOnly(endHour, endMinute));
        return end > start;
    }

    private async Task<AIBookingFlowResponse> BuildDailyRoomResponseAsync(string sessionId, AIBookingSessionState state, string blockPurpose, string message)
    {
        var room = await LoadSelectedRoomAsync(state.SelectedRoomId);
        state.SelectedRoomName = room.Name;
        state.BookingMode = "daily";
        var checkIn = state.CheckInDate ?? DateOnly.FromDateTime(DateTime.Today);
        var checkOut = state.CheckOutDate ?? checkIn.AddDays(1);
        var interval = BookingTimeRules.BuildDailyStay(checkIn, checkOut);

        if (!await _availabilityService.IsRoomAvailable(room.Id, interval.Start, interval.End))
        {
            throw new InvalidOperationException("Phòng không còn trống trong khoảng ngày đã chọn.");
        }

        var total = await CalculateTotalPriceAsync(room, interval.Start, interval.End, false, state.GuestCount);
        if (blockPurpose == "dailyRooms")
        {
            var dailyRooms = new List<AIDailyRoomOption>
            {
                new()
                {
                    RoomId = room.Id,
                    RoomName = room.Name,
                    CheckInDate = checkIn,
                    CheckOutDate = checkOut,
                    TotalPrice = total
                }
            };

            return BuildResponse(sessionId, "select-daily-room", message, state,
                new AIUiBlock { Type = "dailyRooms", Data = new { rooms = dailyRooms } });
        }

        return BuildSummaryAndFormResponse(sessionId, state, total, BuildDailySummaryLines(state, interval.Start, interval.End), "submit-booking-form");
    }

    private AIBookingFlowResponse BuildSummaryAndFormResponse(AIBookingActionRequest request, AIBookingSessionState state, decimal total, List<string> lines, string nextStep)
        => BuildSummaryAndFormResponse(request.SessionId, state, total, lines, nextStep);

    private AIBookingFlowResponse BuildSummaryAndFormResponse(string sessionId, AIBookingSessionState state, decimal total, List<string> lines, string nextStep)
    {
        return BuildResponse(sessionId, nextStep, "Mình đã chuẩn bị tóm tắt đặt phòng. Bạn kiểm tra và điền thông tin để tiếp tục.", state,
            new AIUiBlock
            {
                Type = "bookingSummary",
                Data = new AIBookingSummaryBlock { Title = "Tóm tắt đặt phòng", State = state, TotalPrice = total, Lines = lines }
            },
            new AIUiBlock { Type = "bookingForm", Data = new { fields = BuildBookingFormFields(state) } });
    }

    private async Task<Room> LoadSelectedRoomAsync(int? roomId)
    {
        if (roomId == null) throw new InvalidOperationException("Chưa chọn phòng.");
        return await _context.Rooms.SingleAsync(room => room.Id == roomId.Value);
    }

    private async Task<decimal> CalculateTotalPriceAsync(Room room, DateTime start, DateTime end, bool isHourly, int guestCount)
    {
        var basePrice = await _pricingService.CalculateStayPriceAsync(room.Id, start, end, isHourly);
        return basePrice + Math.Max(0, guestCount - room.Capacity) * room.ExtraGuestFee;
    }

    private static List<string> BuildHourlySummaryLines(AIBookingSessionState state, DateTime start, DateTime end) => new()
    {
        $"Phòng: {state.SelectedRoomName}",
        $"Thời gian: {start:HH:mm} - {end:HH:mm}, {start:dd/MM/yyyy}",
        $"Số khách: {state.GuestCount}"
    };

    private static List<string> BuildDailySummaryLines(AIBookingSessionState state, DateTime start, DateTime end) => new()
    {
        $"Phòng: {state.SelectedRoomName}",
        $"Lưu trú: {start:dd/MM/yyyy} - {end:dd/MM/yyyy}",
        $"Số khách: {state.GuestCount}"
    };

    private static List<AIBookingFormField> BuildBookingFormFields(AIBookingSessionState state) => new()
    {
        new() { Name = "customerName", Label = "Họ tên", Required = true, Value = state.CustomerName },
        new() { Name = "phoneNumber", Label = "Số điện thoại", Required = true, Type = "tel" },
        new() { Name = "email", Label = "Email", Type = "email" },
        new() { Name = "notes", Label = "Ghi chú", Type = "textarea" }
    };

    private AIBookingSessionState MergeState(AIBookingActionRequest request)
    {
        var state = CloneState(request.State);
        if (!string.IsNullOrWhiteSpace(request.SessionId) && _cache.TryGetValue<AIBookingSessionState>(CacheKey(request.SessionId), out var cached))
        {
            state = MergeStates(cached!, state);
        }

        return state;
    }

    private void CacheState(AIBookingActionRequest request, AIBookingSessionState state)
    {
        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            _cache.Set(CacheKey(request.SessionId), CloneState(state), TimeSpan.FromMinutes(30));
        }
    }

    private void CacheState(string sessionId, AIBookingSessionState state)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _cache.Set(CacheKey(sessionId), CloneState(state), TimeSpan.FromMinutes(30));
        }
    }

    private static string CacheKey(string sessionId) => $"ai-booking-flow:{sessionId}";

    private AIBookingFlowResponse BuildResponse(AIBookingActionRequest request, string currentStep, string message, AIBookingSessionState state, params AIUiBlock[] blocks)
    {
        CacheState(request, state);
        return BuildResponse(request.SessionId, currentStep, message, state, blocks);
    }

    private AIBookingFlowResponse BuildResponse(AIBookingSessionState originalState, string currentStep, string message, AIBookingSessionState state, params AIUiBlock[] blocks)
    {
        return BuildResponse(string.Empty, currentStep, message, MergeStates(CloneState(originalState), state), blocks);
    }

    private AIBookingFlowResponse BuildResponse(string sessionId, string currentStep, string message, AIBookingSessionState state, params AIUiBlock[] blocks)
    {
        CacheState(sessionId, state);
        return new AIBookingFlowResponse
        {
            SessionId = sessionId,
            CurrentStep = currentStep,
            Message = message,
            State = state,
            UiBlocks = blocks.ToList()
        };
    }

    private static string NormalizeMode(string? mode)
        => string.Equals(mode, "daily", StringComparison.OrdinalIgnoreCase) ? "daily" : "hourly";

    private static AIBookingSessionState CloneState(AIBookingSessionState state) => new()
    {
        CustomerName = state.CustomerName,
        Intent = state.Intent,
        BranchId = state.BranchId,
        BranchName = state.BranchName,
        BookingMode = state.BookingMode,
        HourlyDate = state.HourlyDate,
        CheckInDate = state.CheckInDate,
        CheckOutDate = state.CheckOutDate,
        GuestCount = state.GuestCount,
        SelectedRoomId = state.SelectedRoomId,
        SelectedRoomName = state.SelectedRoomName,
        SelectedSlotId = state.SelectedSlotId,
        SelectedSlotLabel = state.SelectedSlotLabel,
        BookingId = state.BookingId,
        PaymentStatus = state.PaymentStatus
    };

    private static AIBookingSessionState MergeStates(AIBookingSessionState cached, AIBookingSessionState incoming)
    {
        var branchChanged = incoming.BranchId.HasValue && incoming.BranchId != cached.BranchId;
        var modeChanged = !string.IsNullOrWhiteSpace(incoming.BookingMode) && incoming.BookingMode != "unknown" && incoming.BookingMode != cached.BookingMode;
        var hourlyDateChanged = incoming.HourlyDate.HasValue && incoming.HourlyDate != cached.HourlyDate;
        var checkInChanged = incoming.CheckInDate.HasValue && incoming.CheckInDate != cached.CheckInDate;
        var checkOutChanged = incoming.CheckOutDate.HasValue && incoming.CheckOutDate != cached.CheckOutDate;
        var guestCountChanged = incoming.GuestCount > 0 && incoming.GuestCount != cached.GuestCount;
        var upstreamChanged = branchChanged || modeChanged || hourlyDateChanged || checkInChanged || checkOutChanged || guestCountChanged;

        return new AIBookingSessionState
        {
            CustomerName = incoming.CustomerName ?? cached.CustomerName,
            Intent = string.IsNullOrWhiteSpace(incoming.Intent) ? cached.Intent : incoming.Intent,
            BranchId = incoming.BranchId ?? cached.BranchId,
            BranchName = incoming.BranchName ?? cached.BranchName,
            BookingMode = incoming.BookingMode == "unknown" ? cached.BookingMode : incoming.BookingMode,
            HourlyDate = incoming.HourlyDate ?? cached.HourlyDate,
            CheckInDate = incoming.CheckInDate ?? cached.CheckInDate,
            CheckOutDate = incoming.CheckOutDate ?? cached.CheckOutDate,
            GuestCount = incoming.GuestCount > 0 ? incoming.GuestCount : cached.GuestCount,
            SelectedRoomId = upstreamChanged ? incoming.SelectedRoomId : incoming.SelectedRoomId ?? cached.SelectedRoomId,
            SelectedRoomName = upstreamChanged ? incoming.SelectedRoomName : incoming.SelectedRoomName ?? cached.SelectedRoomName,
            SelectedSlotId = upstreamChanged ? incoming.SelectedSlotId : incoming.SelectedSlotId ?? cached.SelectedSlotId,
            SelectedSlotLabel = upstreamChanged ? incoming.SelectedSlotLabel : incoming.SelectedSlotLabel ?? cached.SelectedSlotLabel,
            BookingId = incoming.BookingId ?? cached.BookingId,
            PaymentStatus = string.IsNullOrWhiteSpace(incoming.PaymentStatus) ? cached.PaymentStatus : incoming.PaymentStatus
        };
    }
}
