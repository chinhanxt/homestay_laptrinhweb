namespace WebHomestay.Models.AI;

/// <summary>
/// Represents the decision on whether to display rooms to the customer.
/// Used by the Room Display Logic component to determine when and why to show room listings.
/// </summary>
public class RoomDisplayDecision
{
    /// <summary>
    /// Whether rooms should be displayed to the customer.
    /// True if all conditions are met (intent, context, cooldown, max shows limit).
    /// </summary>
    public bool ShouldDisplay { get; set; }

    /// <summary>
    /// Optional reason why rooms are being displayed.
    /// Null if ShouldDisplay is false.
    /// Used to determine the appropriate response guidance and for analytics.
    /// </summary>
    public RoomDisplayReason? Reason { get; set; }

    /// <summary>
    /// Optional explanation text that can be shown to the customer.
    /// Provides context about why certain rooms are being shown or why no rooms are available.
    /// Examples: "Phòng này gần với yêu cầu nhất nhưng giá cao hơn một chút",
    /// "Không tìm thấy phòng trong ngân sách của anh/chị".
    /// </summary>
    public string? ExplanationForUser { get; set; }
}
