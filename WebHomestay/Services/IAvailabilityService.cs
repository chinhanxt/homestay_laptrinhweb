using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;
// WebHomestay/Services/IAvailabilityService.cs
namespace WebHomestay.Services;

public interface IAvailabilityService
{
    Task<bool> IsRoomAvailable(int roomId, DateTime start, DateTime end, int? ignoreBookingId = null);
    Task<List<int>> GetAvailableRoomIds(int branchId, DateTime start, DateTime end);
    Task<bool> IsHourlySlotAvailableForRoomAsync(int slotInventoryId, int roomId, int guestCount, CancellationToken cancellationToken = default);
    Task<List<DateOnly>> GetBlockedDatesAsync(int roomId, DateOnly visibleFrom, int visibleDays);
}
