// WebHomestay/Models/ViewModels/RoomDetailsViewModel.cs
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

public class RoomDetailsViewModel
{
    public Room Room { get; set; } = null!;
    public DateOnly SelectedHourlyDate { get; set; }
    public List<HourlySlotGroupViewModel> HourlyGroups { get; set; } = new();
    public List<CalendarDayViewModel> DailyCalendar { get; set; } = new();
    public List<string> AllImages { get; set; } = new();
}

public class HourlySlotGroupViewModel
{
    public string Title { get; set; } = string.Empty;
    public List<HourlySlotCardViewModel> Slots { get; set; } = new();
}

public class HourlySlotCardViewModel
{
    public int SlotId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class CalendarDayViewModel
{
    public DateOnly Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal PriceDay { get; set; }
    public decimal PriceHour { get; set; }
}
