// WebHomestay/Services/IBookingCreationService.cs
using WebHomestay.Models;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services;

public interface IBookingCreationService
{
    Task<Booking> CreateHourlyBookingAsync(CreateBookingRequest request);
    Task<Booking> CreateDailyBookingAsync(CreateBookingRequest request);
}
