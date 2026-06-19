using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;
using BookingEntity = WebHomestay.Models.Entities.Core.Booking;

namespace WebHomestay.Services;

public interface IBookingCreationService
{
    Task<BookingEntity> CreateHourlyBookingAsync(CreateBookingRequest request);
    Task<BookingEntity> CreateDailyBookingAsync(CreateBookingRequest request);
}
