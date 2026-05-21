using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace WebHomestay.Models;

public class RolePermissionTemplate
{
    [Key]
    [StringLength(20)]
    public string Role { get; set; } = string.Empty;

    public string PermissionsJson { get; set; } = "{}";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public Dictionary<string, bool> Permissions
    {
        get
        {
            if (string.IsNullOrEmpty(PermissionsJson)) return new();
            try { return JsonSerializer.Deserialize<Dictionary<string, bool>>(PermissionsJson) ?? new(); }
            catch { return new(); }
        }
        set
        {
            PermissionsJson = JsonSerializer.Serialize(value);
        }
    }
}
