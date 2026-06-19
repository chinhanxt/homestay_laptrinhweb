using WebHomestay.Models.DTOs.AI;
using WebHomestay.Services;
namespace WebHomestay.Services
{
    public class BookingDecision
    {
        public string Action { get; set; } = "reply";
        public AIBookingSessionState State { get; set; } = new();
        public string? Reason { get; set; }
        public List<AIUiBlock> UiBlocks { get; set; } = new();
    }
}
