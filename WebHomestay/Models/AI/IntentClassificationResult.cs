namespace WebHomestay.Models.AI;

/// <summary>
/// Result of intent classification for a customer message.
/// Contains the detected intent, confidence score, extracted entities, and optional clarification question.
/// </summary>
public class IntentClassificationResult
{
    /// <summary>
    /// The classified booking intent.
    /// </summary>
    public BookingIntent Intent { get; set; }

    /// <summary>
    /// Confidence score of the classification (0.0 to 1.0).
    /// A score below 0.7 typically requires clarification.
    /// </summary>
    public decimal ConfidenceScore { get; set; }

    /// <summary>
    /// Dictionary of extracted entities from the message.
    /// Common keys: "branch_name", "date", "time", "guest_count", "price_range", 
    /// "amenity", "room_name", "duration_hours".
    /// </summary>
    public Dictionary<string, object> ExtractedEntities { get; set; } = new();

    /// <summary>
    /// Optional clarification question to ask the customer when confidence is low.
    /// Null if confidence is high enough or no clarification is needed.
    /// </summary>
    public string? ClarificationQuestion { get; set; }
}
