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

        public int Capacity { get; set; } = 2;
        public int MaxGuests { get; set; } = 4;

        public string Status { get; set; } = "Available"; // Available, Occupied, Maintenance

        public string? ImageUrl { get; set; }

        public int BranchId { get; set; }
        public virtual Branch? Branch { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public virtual ICollection<RoomSlotTemplateAssignment> SlotAssignments { get; set; } = new List<RoomSlotTemplateAssignment>();
        public virtual ICollection<RoomSlotInventory> SlotInventories { get; set; } = new List<RoomSlotInventory>();
        public virtual ICollection<RoomSlotOverride> SlotOverrides { get; set; } = new List<RoomSlotOverride>();
    }
}
