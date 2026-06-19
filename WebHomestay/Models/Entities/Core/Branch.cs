using System.ComponentModel.DataAnnotations;
using WebHomestay.Models.Enums;

namespace WebHomestay.Models.Entities.Core
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

        [StringLength(100)]
        [EmailAddress]
        public string? Email { get; set; }

        public string? MapUrl { get; set; }
        public int BookingLeadTimeHours { get; set; } = 2;
        public int BookingLeadTimeValue { get; set; } = 2;
        public string? BookingLeadTimeUnit { get; set; } = BranchLeadTimeUnit.Hours;
        public int BookingLeadTimeDays { get; set; } = 1;

        // Soft delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // Navigation properties
        public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
        public virtual ICollection<AdminUser> StaffMembers { get; set; } = new List<AdminUser>();
    }
}
