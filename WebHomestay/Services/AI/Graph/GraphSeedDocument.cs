using System.Collections.Generic;

namespace WebHomestay.Services.AI.Graph
{
    public sealed class GraphSeedDocument
    {
        public string NodeType { get; set; } = string.Empty;
        public string NodeKey { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
