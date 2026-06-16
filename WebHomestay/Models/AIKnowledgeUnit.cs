namespace WebHomestay.Models
{
    public class AIKnowledgeUnit
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ScopeId { get; set; }
        public AIBrainScope Scope { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public int Priority { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public Pgvector.Vector? Embedding { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
