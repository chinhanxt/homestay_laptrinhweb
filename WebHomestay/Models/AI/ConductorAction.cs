namespace WebHomestay.Models.AI;

/// <summary>
/// Represents the action that the Booking Conductor should take in response to a customer message.
/// Determines how the chatbot responds and what UI components to display.
/// </summary>
public enum ConductorAction
{
    /// <summary>
    /// Send a text reply to the customer without additional UI blocks.
    /// </summary>
    Reply,

    /// <summary>
    /// Display available rooms to the customer.
    /// </summary>
    ShowRooms,

    /// <summary>
    /// Display available hourly time slots to the customer.
    /// </summary>
    ShowSlots,

    /// <summary>
    /// Display the booking form (pre-filled or empty).
    /// </summary>
    ShowForm,

    /// <summary>
    /// Automatically create a booking when all required information is present and intent is clear.
    /// </summary>
    AutoBook,

    /// <summary>
    /// Ask a clarification question to disambiguate the customer's intent.
    /// </summary>
    Clarify,

    /// <summary>
    /// Provide explicit options to the customer (e.g., "1) Đặt theo giờ 2) Đặt theo ngày").
    /// </summary>
    ProvideOptions
}
