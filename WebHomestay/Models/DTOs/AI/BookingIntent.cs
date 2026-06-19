using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Enums;
namespace WebHomestay.Models.DTOs.AI;

/// <summary>
/// Represents the classified intent of a customer's message in the booking conversation.
/// </summary>
public enum BookingIntent
{
    /// <summary>
    /// Customer wants to book a room by the hour.
    /// </summary>
    HourlyBooking,

    /// <summary>
    /// Customer wants to book a room by the day (overnight stay).
    /// </summary>
    DailyBooking,

    /// <summary>
    /// Customer is inquiring about room prices.
    /// </summary>
    PriceInquiry,

    /// <summary>
    /// Customer wants to compare multiple rooms.
    /// </summary>
    RoomComparison,

    /// <summary>
    /// Customer is asking about room amenities or facilities.
    /// </summary>
    AmenityQuery,

    /// <summary>
    /// Customer wants to modify an existing booking.
    /// </summary>
    ModifyBooking,

    /// <summary>
    /// General inquiry about the homestay or services.
    /// </summary>
    GeneralInquiry,

    /// <summary>
    /// Message is off-topic (not related to booking).
    /// </summary>
    OffTopic,

    /// <summary>
    /// Customer confirms or agrees to a suggestion.
    /// </summary>
    Confirmation,

    /// <summary>
    /// Customer rejects or declines a suggestion.
    /// </summary>
    Rejection,

    /// <summary>
    /// Intent could not be determined.
    /// </summary>
    Unknown
}
