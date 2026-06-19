using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Enums;
namespace WebHomestay.Models.ViewModels
{
    public class SearchResultViewModel
    {
        public List<Room> Rooms { get; set; } = new();
        public DateOnly CheckIn { get; set; }
        public DateOnly CheckOut { get; set; }
        public int Guests { get; set; } = 1;
        public int BranchId { get; set; }
        public List<Branch> Branches { get; set; } = new();
    }
}
