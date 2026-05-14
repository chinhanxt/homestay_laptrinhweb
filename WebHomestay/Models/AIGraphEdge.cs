namespace WebHomestay.Models
{
    public class AIGraphEdge
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid FromNodeId { get; set; }
        public AIGraphNode FromNode { get; set; } = null!;
        public Guid ToNodeId { get; set; }
        public AIGraphNode ToNode { get; set; } = null!;
        public string RelationshipType { get; set; } = string.Empty;
        public decimal Weight { get; set; } = 1;
        public string Evidence { get; set; } = string.Empty;
    }
}
