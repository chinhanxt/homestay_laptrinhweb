namespace WebHomestay.Services
{
    public class AIAgentTestRequest
    {
        public string AgentKey { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int GuestCount { get; set; } = 1;
    }
}
