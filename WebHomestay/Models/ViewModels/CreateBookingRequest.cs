// WebHomestay/Models/ViewModels/CreateBookingRequest.cs
using System.ComponentModel.DataAnnotations;
using WebHomestay.Models;

namespace WebHomestay.Models.ViewModels;

public class CreateBookingRequest
{
    public int RoomId { get; set; }
    public BookingMode BookingMode { get; set; }
    public int? SlotInventoryId { get; set; }
    public DateOnly? CheckInDate { get; set; }
    public DateOnly? CheckOutDate { get; set; }
    [Required] public string CustomerName { get; set; } = string.Empty;
    [Required] public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public int GuestCount { get; set; } = 1;
    public string? CustomerNote { get; set; }
}
