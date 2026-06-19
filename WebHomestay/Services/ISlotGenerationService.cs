using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;
// WebHomestay/Services/ISlotGenerationService.cs

namespace WebHomestay.Services.Slots;

public interface ISlotGenerationService
{
    List<RoomSlotInventory> GenerateRollingSlots(int roomId, RoomSlotTemplate template, DateOnly slotDate);
    RoomSlotInventory GenerateFixedSlot(int roomId, RoomSlotTemplate template, DateOnly slotDate);
}
