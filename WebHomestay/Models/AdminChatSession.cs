namespace WebHomestay.Models;

public class AdminChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SessionId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string Status { get; set; } = "auto"; // "auto" | "paused"
    public string? PausedBy { get; set; }
    public DateTime? PausedAt { get; set; }
    public string? AutoReplyMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastActivityAt { get; set; } = DateTime.Now;
}
