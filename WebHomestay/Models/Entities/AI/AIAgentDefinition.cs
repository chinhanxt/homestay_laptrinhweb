namespace WebHomestay.Models.Entities.AI
{
    public class AIAgentDefinition
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
