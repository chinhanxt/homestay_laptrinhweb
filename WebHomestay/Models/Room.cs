using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebHomestay.Models
{
    public class Room
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal PricePerHour { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal PricePerDay { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal ExtraGuestFee { get; set; } = 0;

        [Column(TypeName = "decimal(10, 2)")]
        public decimal PriceWeekendPerHour { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal PriceWeekendPerDay { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal PriceHolidayPerHour { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal PriceHolidayPerDay { get; set; }

        public int Capacity { get; set; } = 2;
        public int MaxGuests { get; set; } = 4;

        public string Status { get; set; } = "Available"; // Available, Maintenance

        public string? ImageUrl { get; set; }
        public string? AdditionalImages { get; set; } // Store as JSON array of strings

        public int BranchId { get; set; }
        public virtual Branch? Branch { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public virtual ICollection<RoomSlotTemplateAssignment> SlotAssignments { get; set; } = new List<RoomSlotTemplateAssignment>();
        public virtual ICollection<RoomSlotInventory> SlotInventories { get; set; } = new List<RoomSlotInventory>();
        public virtual ICollection<RoomSlotOverride> SlotOverrides { get; set; } = new List<RoomSlotOverride>();
        public float[]? Embedding { get; set; }

        // Soft delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
