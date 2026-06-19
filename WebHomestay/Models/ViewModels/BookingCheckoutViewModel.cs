// WebHomestay/Models/ViewModels/BookingCheckoutViewModel.cs
using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Models.ViewModels;

public class BookingCheckoutViewModel
{
    public Room Room { get; set; } = null!;
    public BookingMode BookingMode { get; set; }
    public int? SlotInventoryId { get; set; }
    public string? SlotLabel { get; set; }
    public DateOnly? CheckInDate { get; set; }
    public DateOnly? CheckOutDate { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal TotalPrice { get; set; }
    public int Capacity { get; set; }
    public int MaxGuests { get; set; }
    public decimal ExtraGuestFee { get; set; }
    public int GuestCount { get; set; } = 1;
}
