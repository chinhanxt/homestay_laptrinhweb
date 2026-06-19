using WebHomestay.Models.Entities.Core;
// WebHomestay/Models/RoomSlotOverride.cs
namespace WebHomestay.Models.Entities.Slots;

public class RoomSlotOverride
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public DateOnly TargetDate { get; set; }
    public int? TemplateId { get; set; }
    public int? InventoryId { get; set; }
    public string OverrideType { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
