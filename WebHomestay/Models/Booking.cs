using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebHomestay.Models
{
    public class Booking
    {
        public int Id { get; set; }

        public int? UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        public int RoomId { get; set; }
        [ForeignKey("RoomId")]
        public virtual Room Room { get; set; } = null!;

        [Required]
        [StringLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string CustomerPhone { get; set; } = string.Empty;

        [StringLength(200)]
        public string? CustomerEmail { get; set; }

        [StringLength(100)]
        public string? CustomerZalo { get; set; }

        public int GuestCount { get; set; } = 1;

        public string? IdCardFrontPath { get; set; }
        public string? IdCardBackPath { get; set; }

        public string? CustomerNote { get; set; }
        public string? AdminNote { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal TotalPrice { get; set; }

        public string Status { get; set; } = "PendingPayment"; // PendingPayment, AwaitingApproval, Confirmed, CheckedIn, CheckedOut, Cancelled
        public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid, Paid

        public string? PaymentProofUrl { get; set; }
        public string? SmartLockCode { get; set; }
        public string? WifiPassword { get; set; }
        public string? CheckInInstructions { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Soft Delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public BookingMode BookingMode { get; set; } = BookingMode.Daily;
        public int? RoomSlotInventoryId { get; set; }
        public virtual RoomSlotInventory? RoomSlotInventory { get; set; }
        [StringLength(100)]
        public string? SlotLabel { get; set; }
    }
}
