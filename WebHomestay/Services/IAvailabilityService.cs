// WebHomestay/Services/IAvailabilityService.cs
namespace WebHomestay.Services;

public interface IAvailabilityService
{
    Task<bool> IsRoomAvailable(int roomId, DateTime start, DateTime end, int? ignoreBookingId = null);
    Task<List<int>> GetAvailableRoomIds(int branchId, DateTime start, DateTime end);
    Task<bool> IsHourlySlotAvailableForRoomAsync(int slotInventoryId, int roomId, int guestCount, CancellationToken cancellationToken = default);
    Task<List<DateOnly>> GetBlockedDatesAsync(int roomId, DateOnly visibleFrom, int visibleDays);
}
