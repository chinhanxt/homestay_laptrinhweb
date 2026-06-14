// WebHomestay/Models/RoomSlotTemplate.cs
namespace WebHomestay.Models;

public class RoomSlotTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int CleanupMinutes { get; set; }
    public TimeOnly? SeedStartTime { get; set; }
    public TimeOnly? FixedStartTime { get; set; }
    public TimeOnly? FixedEndTime { get; set; }
    public bool CrossesMidnight { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
