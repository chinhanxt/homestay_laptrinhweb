using System.ComponentModel.DataAnnotations;

namespace WebHomestay.Models.Entities.Core
{
    public class ActivityLog
    {
        public int Id { get; set; }

        public int AdminUserId { get; set; }
        public virtual AdminUser? AdminUser { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty; // e.g., "Check-in", "Change Price"

        [Required]
        [StringLength(255)]
        public string Target { get; set; } = string.Empty; // e.g., "Booking #12", "Room #5"

        public string Details { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
