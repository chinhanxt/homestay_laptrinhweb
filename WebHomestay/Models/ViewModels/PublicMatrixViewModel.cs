using WebHomestay.Models;

namespace WebHomestay.Models.ViewModels
{
    public class PublicMatrixViewModel
    {
        public List<DateTime> Dates { get; set; } = new();
        public List<BranchMatrixData> Branches { get; set; } = new();
    }

    public class BranchMatrixData
    {
        public Branch Branch { get; set; } = null!;
        public List<RoomMatrixData> Rooms { get; set; } = new();
    }

    public class RoomMatrixData
    {
        public Room Room { get; set; } = null!;
        public Dictionary<DateTime, string> Availability { get; set; } = new(); // Date -> Status (Available, Booked, AwaitingApproval)
    }
}
