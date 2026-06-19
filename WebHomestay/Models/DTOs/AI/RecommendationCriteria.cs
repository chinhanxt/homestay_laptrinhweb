using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Enums;
namespace WebHomestay.Models.DTOs.AI;

public class RecommendationCriteria
{
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public List<string> RequiredAmenities { get; set; } = new();
    public int GuestCount { get; set; }
    public BookingIntent Intent { get; set; }
}
