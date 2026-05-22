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
    Exit
}

public enum ConductorAction
{
    Reply,
    AskInfo,
    ShowRooms,
    ShowSlots,
    ShowForm,
    AutoBook,
    PaymentQr
}

public class ConductorResult
{
    public ConductorAction Action { get; set; }
    public BookingSessionContainer State { get; set; } = new();
    public List<object> UiBlocks { get; set; } = new();
    public string? Reason { get; set; }
}

public class BookingActionRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int? RoomId { get; set; }
    public int? SlotId { get; set; }
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
