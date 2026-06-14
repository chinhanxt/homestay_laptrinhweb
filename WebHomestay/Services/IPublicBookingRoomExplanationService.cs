namespace WebHomestay.Services;

public interface IPublicBookingRoomExplanationService
{
    Task<PublicBookingRoomExplanation> BuildAsync(int roomId, BookingConfirmedState state, CancellationToken cancellationToken);
}

public class PublicBookingRoomExplanation
{
    public bool FitsStandardOccupancy { get; set; }
    public bool AllowsRequestedGuests { get; set; }
    public int ExtraGuestCount { get; set; }
    public decimal ExtraGuestFeeApplied { get; set; }
    public string PricingTierLabel { get; set; } = "weekday";
    public List<string> Lines { get; set; } = new();
    public string RecommendationReason { get; set; } = string.Empty;
    public decimal DisplayPrice { get; set; }
}
