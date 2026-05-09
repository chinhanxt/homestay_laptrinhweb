// WebHomestay/Services/ISlotGenerationService.cs
using WebHomestay.Models;

namespace WebHomestay.Services;

public interface ISlotGenerationService
{
    List<RoomSlotInventory> GenerateRollingSlots(int roomId, RoomSlotTemplate template, DateOnly slotDate);
    RoomSlotInventory GenerateFixedSlot(int roomId, RoomSlotTemplate template, DateOnly slotDate);
}
