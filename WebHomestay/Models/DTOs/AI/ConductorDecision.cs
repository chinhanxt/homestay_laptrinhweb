using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Enums;
using WebHomestay.Services;

namespace WebHomestay.Models.DTOs.AI;

/// <summary>
/// Represents the decision made by the Enhanced Booking Conductor on how to respond to a customer message.
/// Contains the action to take, updated session state, UI blocks to display, and guidance for the Final Synthesizer.
/// </summary>
public class ConductorDecision
{
    /// <summary>
    /// The action that the conductor has decided to take.
    /// Determines the chatbot's response behavior (reply, show rooms, show slots, etc.).
    /// </summary>
    public ConductorAction Action { get; set; }

    /// <summary>
    /// The updated booking session state after processing the customer's message.
    /// Contains all context information including extracted entities and conversation tracking.
    /// </summary>
    public AIBookingSessionState UpdatedState { get; set; } = null!;

    /// <summary>
    /// List of UI blocks to display to the customer.
    /// Can include roomCards, hourlySlots, dailyRooms, bookingSummary, bookingForm, paymentQr.
    /// Empty list means no UI blocks should be displayed.
    /// </summary>
    public List<object> UiBlocks { get; set; } = new();

    /// <summary>
    /// Optional guidance text for the Final Synthesizer agent.
    /// Provides context about what the LLM should say when generating the response.
    /// Examples: "Explain room prices", "Ask for check-in date", "Confirm booking details".
    /// </summary>
    public string? ResponseGuidance { get; set; }

    /// <summary>
    /// Optional reason why rooms are being displayed (if Action is ShowRooms).
    /// Used for analytics, debugging, and determining the appropriate response tone.
    /// Null if rooms are not being displayed.
    /// </summary>
    public RoomDisplayReason? DisplayReason { get; set; }
}
