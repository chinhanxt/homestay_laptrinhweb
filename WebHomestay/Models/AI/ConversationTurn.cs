namespace WebHomestay.Models.AI;

/// <summary>
/// Represents a single turn in the conversation between the customer and the AI chatbot.
/// Used for context tracking and reference resolution across multiple conversation turns.
/// </summary>
public class ConversationTurn
{
    /// <summary>
    /// Timestamp when this conversation turn occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The customer's message in this turn.
    /// </summary>
    public string UserMessage { get; set; } = string.Empty;

    /// <summary>
    /// The AI's response to the customer in this turn.
    /// </summary>
    public string AIResponse { get; set; } = string.Empty;

    /// <summary>
    /// Dictionary of entities extracted from the user message during this turn.
    /// Common keys: "branch_name", "date", "time", "guest_count", "price_range", 
    /// "amenity", "room_name", "duration_hours".
    /// Used for context tracking and reference resolution.
    /// </summary>
    public Dictionary<string, object>? ExtractedEntities { get; set; }
}
