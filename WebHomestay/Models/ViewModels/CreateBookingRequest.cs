// WebHomestay/Models/ViewModels/CreateBookingRequest.cs
using System.ComponentModel.DataAnnotations;
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
