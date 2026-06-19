namespace WebHomestay.Models.Entities.AI
{
    public class AIBrainScope
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int Order { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public ICollection<AIKnowledgeUnit> KnowledgeUnits { get; set; } = new List<AIKnowledgeUnit>();
    }
}
