namespace WebHomestay.Models
{
    public class AIGraphNode
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string NodeType { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string MetadataJson { get; set; } = "{}";
        public bool IsActive { get; set; } = true;
        public ICollection<AIGraphEdge> OutgoingEdges { get; set; } = new List<AIGraphEdge>();
        public ICollection<AIGraphEdge> IncomingEdges { get; set; } = new List<AIGraphEdge>();
    }
}
