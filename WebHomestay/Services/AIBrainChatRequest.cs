namespace WebHomestay.Services
{
    public enum ChatMode
    {
        AdminAssistant,
        PublicBooking
    }

    public class AIBrainChatRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int GuestCount { get; set; } = 1;
        public ChatMode Mode { get; set; } = ChatMode.AdminAssistant;
    }
}
