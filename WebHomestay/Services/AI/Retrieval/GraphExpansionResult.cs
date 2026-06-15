using System;
using System.Collections.Generic;

namespace WebHomestay.Services.AI.Retrieval
{
    public sealed class GraphExpansionResult
    {
        public IReadOnlyList<string> RelatedNodeLabels { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> RelatedEdges { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> SeedNodeKeys { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> NodeSummaries { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> EdgeSummaries { get; init; } = Array.Empty<string>();
    }
}
