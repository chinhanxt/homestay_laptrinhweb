using System.ComponentModel.DataAnnotations;

namespace WebHomestay.Models.Entities.Core
{
    public class SystemSetting
    {
        public int Id { get; set; }
        [Required, StringLength(100)]
        public string SettingKey { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string GroupName { get; set; } = "General";
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
