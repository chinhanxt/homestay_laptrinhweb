using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;
using RoomEntity = WebHomestay.Models.Entities.Core.Room;

namespace WebHomestay.Services;

public interface IRoomBookingViewService
{
    Task<RoomDetailsViewModel> BuildAsync(RoomEntity room, DateOnly selectedHourlyDate);
    Task<BookingCheckoutViewModel?> BuildHourlyCheckoutAsync(int roomId, int slotId);
    Task<BookingCheckoutViewModel?> BuildDailyCheckoutAsync(int roomId, DateOnly checkInDate, DateOnly checkOutDate);
}
