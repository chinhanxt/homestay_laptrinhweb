namespace WebHomestay.Services
{
    public class AIBookingFlowResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public string CurrentStep { get; set; } = "intent";
        public string Message { get; set; } = string.Empty;
        public AIBookingSessionState State { get; set; } = new();
        public List<AIUiBlock> UiBlocks { get; set; } = new();
    }

    public class ConversationTurn
    {
        public DateTime Timestamp { get; set; }
        public string UserMessage { get; set; } = string.Empty;
        public string AIResponse { get; set; } = string.Empty;
        public Dictionary<string, object>? ExtractedEntities { get; set; }
    }

    public class AIBookingSessionState
    {
        public string? CustomerName { get; set; }
        public string Intent { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public string BookingMode { get; set; } = "unknown";
        public DateOnly? HourlyDate { get; set; }
        public DateOnly? CheckInDate { get; set; }
        public DateOnly? CheckOutDate { get; set; }
        public int GuestCount { get; set; } = 1;
        public int? SelectedRoomId { get; set; }
        public string? SelectedRoomName { get; set; }
        public int? SelectedSlotId { get; set; }
        public string? SelectedSlotLabel { get; set; }
        public int? BookingId { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;

        // Conversation tracking properties (Requirement 5.1, 5.2)
        public List<ConversationTurn> ConversationHistory { get; set; } = new();
        public int MessagesSinceLastRoomDisplay { get; set; }
        public int RoomDisplayCount { get; set; }
        public DateTime? LastRoomDisplayTime { get; set; }

        // Intent tracking properties (Requirement 5.5)
        public string? LastClassifiedIntent { get; set; }
        public decimal LastIntentConfidence { get; set; }
        public int ClarificationAttempts { get; set; }

        // User preference properties (Requirement 6.1)
        public decimal? BudgetMax { get; set; }
        public List<string> PreferredAmenities { get; set; } = new();
        public string? LanguagePreference { get; set; }
        public string? RequestedTimeStart { get; set; }
        public string? RequestedTimeEnd { get; set; }
        public string? RequestedTimeLabel { get; set; }
        public decimal? BranchConfidence { get; set; }
        public bool NeedsWeekendPricingExplanation { get; set; }
        public bool NeedsHolidayPricingExplanation { get; set; }
        public bool HasExtraGuestSurcharge { get; set; }
        public List<string> MissingRequiredFields { get; set; } = new();
        public string? LastRecommendationReason { get; set; }
        public int? ActiveRoomContextId { get; set; }
    }

    public class AIUiBlock
    {
        public string Type { get; set; } = string.Empty;
        public object Data { get; set; } = new { };
    }

    public class PublicAIChatRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public int? BranchId { get; set; }
        public string BookingMode { get; set; } = "hourly";
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int GuestCount { get; set; } = 1;
    }

    public class AIBookingActionRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public AIBookingSessionState State { get; set; } = new();
        public AIBookingFormSubmission? FormSubmission { get; set; }
    }

    public class AIBookingFormSubmission
    {
        public string CustomerName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Notes { get; set; }
        public Dictionary<string, string> Values { get; set; } = new();
    }

    public class AIRoomCard
    {
        public int RoomId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal PricePerHour { get; set; }
        public decimal PricePerDay { get; set; }
        public int Capacity { get; set; }
        public int MaxGuests { get; set; }
        public decimal ExtraGuestFee { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> Amenities { get; set; } = new();
        public string DetailsUrl { get; set; } = string.Empty;
    }

    public class AISlotOption
    {
        public int SlotId { get; set; }
        public int RoomId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class AIDailyRoomOption
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public DateOnly CheckInDate { get; set; }
        public DateOnly CheckOutDate { get; set; }
        public decimal TotalPrice { get; set; }
        public string DetailsUrl { get; set; } = string.Empty;
    }

    public class AIBookingSummaryBlock
    {
        public string Title { get; set; } = string.Empty;
        public AIBookingSessionState State { get; set; } = new();
        public decimal TotalPrice { get; set; }
        public List<string> Lines { get; set; } = new();
    }

    public class AIBookingFormField
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public bool Required { get; set; }
        public string? Value { get; set; }
        public string? Placeholder { get; set; }
        public List<string> Options { get; set; } = new();
    }

    public class AIPaymentBlock
    {
        public int? BookingId { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public string? PaymentUrl { get; set; }
        public string? SuccessUrl { get; set; }
        public string? Instructions { get; set; }
    }

    public class BookingConfirmedState
    {
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public DateOnly? HourlyDate { get; set; }
        public DateOnly? CheckInDate { get; set; }
        public DateOnly? CheckOutDate { get; set; }
        public int GuestCount { get; set; }
        public string BookingMode { get; set; } = "hourly";
        public TimeOnly? RequestedTimeStart { get; set; }
        public TimeOnly? RequestedTimeEnd { get; set; }
        public string? RequestedTimeLabel { get; set; }
        public List<string> MissingRequiredFields { get; set; } = new();
        public bool HasExtraGuestSurcharge { get; set; }
        public bool NeedsWeekendPricingExplanation { get; set; }
        public bool NeedsHolidayPricingExplanation { get; set; }
        public string? LastRecommendationReason { get; set; }
    }

    public class BookingProgressState
    {
        public int? SelectedRoomId { get; set; }
        public int? SelectedSlotId { get; set; }
        public string? SelectedSlotLabel { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public int? BookingId { get; set; }
        public int? ActiveRoomContextId { get; set; }
    }

    public class BookingSessionContainer
    {
        public BookingConfirmedState Confirmed { get; set; } = new();
        public BookingProgressState? Progress { get; set; }
    }
}
