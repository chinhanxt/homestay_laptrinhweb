using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Enums;
namespace WebHomestay.Models.ViewModels;

public class BranchLeadTimeRule
{
    public int BranchId { get; set; }
    public int Value { get; set; }
    public string Unit { get; set; } = WebHomestay.Models.Enums.BranchLeadTimeUnit.Hours;
    public int HourlyLeadTimeHours { get; set; }
    public int DailyLeadTimeDays { get; set; }
    public DateTime? HourlyCutoffUtc { get; set; }
    public DateOnly? EarliestAllowedHourlyDate { get; set; }
    public DateOnly? EarliestAllowedDailyDate { get; set; }

    public bool AllowsHourly(DateTime slotStartUtc)
        => !HourlyCutoffUtc.HasValue || slotStartUtc >= HourlyCutoffUtc.Value;

    public bool AllowsDaily(DateOnly checkInDate)
        => !EarliestAllowedDailyDate.HasValue || checkInDate >= EarliestAllowedDailyDate.Value;
}
