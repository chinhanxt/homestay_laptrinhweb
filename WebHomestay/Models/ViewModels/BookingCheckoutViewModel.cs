// WebHomestay/Models/ViewModels/BookingCheckoutViewModel.cs
using WebHomestay.Models;

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
}
