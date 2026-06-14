using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace WebHomestay.Models
{
    public enum AdminRole
    {
        SuperAdmin, // Cấp 1
        Manager,    // Cấp 2
        Staff       // Cấp 3
    }

    public class AdminUser
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public AdminRole Role { get; set; } = AdminRole.Staff;

        public int? BranchId { get; set; }
        public virtual Branch? Branch { get; set; }

        // Lưu danh sách các quyền chi tiết dưới dạng JSON trong DB
        public string? PermissionsJson { get; set; }

        // Helper property để làm việc với Dictionary trong code
        [NotMapped]
        public Dictionary<string, bool> Permissions
        {
            get
            {
                if (string.IsNullOrEmpty(PermissionsJson)) return new Dictionary<string, bool>();
                try { return JsonSerializer.Deserialize<Dictionary<string, bool>>(PermissionsJson) ?? new Dictionary<string, bool>(); }
                catch { return new Dictionary<string, bool>(); }
            }
            set
            {
                PermissionsJson = JsonSerializer.Serialize(value);
            }
        }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Soft delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    }
}
