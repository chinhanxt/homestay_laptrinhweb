namespace WebHomestay.Models.Entities.Chat;

public class AdminChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "user" | "admin" | "system"
    public string Content { get; set; } = string.Empty;
    public string? FormBlockJson { get; set; }
    public string? FormBlockType { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsRead { get; set; } = false;
}
