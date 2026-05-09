// WebHomestay/Services/SlotGenerationService.cs
using WebHomestay.Models;

namespace WebHomestay.Services;

public class SlotGenerationService : ISlotGenerationService
{
    public List<RoomSlotInventory> GenerateRollingSlots(int roomId, RoomSlotTemplate template, DateOnly slotDate)
    {
        var slots = new List<RoomSlotInventory>();
        var pointer = template.SeedStartTime ?? new TimeOnly(0, 0);
        var cutoff = new TimeOnly(23, 59);

        while (pointer.AddMinutes(template.DurationMinutes) <= cutoff)
        {
            var start = slotDate.ToDateTime(pointer);
            var end = start.AddMinutes(template.DurationMinutes);
            slots.Add(new RoomSlotInventory
            {
                RoomId = roomId,
                TemplateId = template.Id,
                SlotDate = slotDate,
                SlotLabel = $"{start:HH:mm}-{end:HH:mm}",
                StartTime = start,
                EndTime = end,
                Status = "Available"
            });

            // Avoid infinite loop if duration is 0
            if (template.DurationMinutes + template.CleanupMinutes <= 0) break;

            try 
            {
                var nextPointer = pointer.AddMinutes(template.DurationMinutes + template.CleanupMinutes);
                // If the new pointer is smaller than the old one, we wrapped around
                if (nextPointer < pointer) break; 
                pointer = nextPointer;
            }
            catch { break; }
            
            if (pointer < TimeOnly.FromDateTime(slots.Last().StartTime)) break;
        }

        return slots;
    }

    public RoomSlotInventory GenerateFixedSlot(int roomId, RoomSlotTemplate template, DateOnly slotDate)
    {
        var start = slotDate.ToDateTime(template.FixedStartTime ?? new TimeOnly(0, 0));
        var endDate = template.CrossesMidnight ? slotDate.AddDays(1) : slotDate;
        var end = endDate.ToDateTime(template.FixedEndTime ?? new TimeOnly(0, 0));

        return new RoomSlotInventory
        {
            RoomId = roomId,
            TemplateId = template.Id,
            SlotDate = slotDate,
            SlotLabel = $"{start:HH:mm}-{end:HH:mm}",
            StartTime = start,
            EndTime = end,
            Status = "Available"
        };
    }
}
