using System.ComponentModel.DataAnnotations;

namespace WebHomestay.Models
{
    public class Amenity
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        public string? IconClass { get; set; }

        // Navigation property
        public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
    }
}
