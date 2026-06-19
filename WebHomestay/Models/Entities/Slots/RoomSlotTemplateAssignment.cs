using WebHomestay.Models.Entities.Core;
// WebHomestay/Models/RoomSlotTemplateAssignment.cs
namespace WebHomestay.Models.Entities.Slots;

public class RoomSlotTemplateAssignment
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public virtual Room Room { get; set; } = null!;
    public int TemplateId { get; set; }
    public virtual RoomSlotTemplate Template { get; set; } = null!;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}
