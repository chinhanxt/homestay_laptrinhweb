using System.ComponentModel.DataAnnotations;

namespace WebHomestay.Models
{
    public class Branch
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Address { get; set; } = string.Empty;

        public string? Description { get; set; }

        [StringLength(20)]
        public string? Hotline { get; set; }

        public string? MapUrl { get; set; }
        public int BookingLeadTimeHours { get; set; } = 2;

        // Navigation properties
        public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
        public virtual ICollection<AdminUser> StaffMembers { get; set; } = new List<AdminUser>();
    }
}
