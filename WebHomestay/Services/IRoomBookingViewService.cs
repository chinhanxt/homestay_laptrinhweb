// WebHomestay/Services/IRoomBookingViewService.cs
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services;

public interface IRoomBookingViewService
{
    Task<RoomDetailsViewModel> BuildAsync(Room room, DateOnly selectedHourlyDate);
    Task<BookingCheckoutViewModel?> BuildHourlyCheckoutAsync(int roomId, int slotId);
    Task<BookingCheckoutViewModel?> BuildDailyCheckoutAsync(int roomId, DateOnly checkInDate, DateOnly checkOutDate);
}
