using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Enums;
namespace WebHomestay.Models.DTOs.AI;

/// <summary>
/// Represents the reason why rooms are being displayed to the customer.
/// Used for analytics, debugging, and determining response guidance.
/// </summary>
public enum RoomDisplayReason
{
    /// <summary>
    /// Customer directly asked about room prices.
    /// </summary>
    DirectPriceInquiry,

    /// <summary>
    /// Proactively showing rooms because minimal context (branch, date) is available.
    /// Triggered in aggressive proactive mode.
    /// </summary>
    ProactiveWithContext,

    /// <summary>
    /// Customer wants to compare multiple rooms.
    /// </summary>
    RoomComparison,

    /// <summary>
    /// Customer asked about specific amenities, rooms filtered accordingly.
    /// </summary>
    AmenityFilter,

    /// <summary>
    /// Cooldown period has expired, and it's appropriate to show rooms again.
    /// </summary>
    CooldownExpired
}
