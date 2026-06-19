using WebHomestay.Models.Entities.Core;
// WebHomestay/Models/RoomSlotInventory.cs
namespace WebHomestay.Models.Entities.Slots;

public class RoomSlotInventory
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public virtual Room Room { get; set; } = null!;
    public int TemplateId { get; set; }
    public virtual RoomSlotTemplate Template { get; set; } = null!;
    public DateOnly SlotDate { get; set; }
    public string SlotLabel { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = "Available";
    public int? BookingId { get; set; }
}
