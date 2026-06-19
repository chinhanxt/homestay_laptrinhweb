namespace WebHomestay.Services;

public enum MessageIntent
{
    BookingIntent,
    BrowsingRooms,
    PolicyQuestion,
    OffTopic,
    PriceQuestion,
    LocationQuestion,
    Compared,
    Exit,
    HandoffRequest
}

public enum ConductorAction
{
    Reply,
    AskInfo,
    ShowRooms,
    ShowSlots,
    ShowForm,
    AutoBook,
    PaymentQr,
    BookingCta
}

public class ConductorResult
{
    public ConductorAction Action { get; set; }
    public BookingSessionContainer State { get; set; } = new();
    public List<object> UiBlocks { get; set; } = new();
    public string? Reason { get; set; }
    public string? Answer { get; set; }
}

public class BookingActionRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int? RoomId { get; set; }
    public int? SlotId { get; set; }
    public int? BranchId { get; set; }
    public int GuestCount { get; set; }
    public string? BookingMode { get; set; }
    public string? CheckInDate { get; set; }
    public string? CheckOutDate { get; set; }
    public string? HourlyDate { get; set; }
    public Dictionary<string, string>? FormData { get; set; }
}

public class BookingActionResult
{
    public string Answer { get; set; } = string.Empty;
    public ConductorAction Action { get; set; }
    public BookingSessionContainer State { get; set; } = new();
    public List<object> UiBlocks { get; set; } = new();
}

public interface IBookingConductor
{
    Task<ConductorResult> DecideAsync(
        string sessionId,
        string message,
        AIBrainChatRequest request,
        CancellationToken cancellationToken);

    Task<BookingActionResult> HandleActionAsync(
        BookingActionRequest actionRequest,
        CancellationToken cancellationToken);
}
