using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Services.AI.Retrieval
{
    public sealed class RetrievalContextAssembler
    {
        private readonly ApplicationDbContext _context;
        private readonly IVectorSearchService _vectorSearchService;
        private readonly WebHomestay.Services.AI.Graph.Neo4jGraphExpansionService _expansionService;

        public RetrievalContextAssembler(
            ApplicationDbContext context,
            IVectorSearchService vectorSearchService,
            WebHomestay.Services.AI.Graph.Neo4jGraphExpansionService expansionService)
        {
            _context = context;
            _vectorSearchService = vectorSearchService;
            _expansionService = expansionService;
        }

        public async Task<(IReadOnlyList<VectorSearchResult> Hits, GraphExpansionResult Graph)> BuildPolicyContextAsync(
            float[] queryEmbedding,
            VectorSearchFilter? filter,
            CancellationToken cancellationToken)
        {
            var hits = await _vectorSearchService.SearchKnowledgeAsync(queryEmbedding, filter, 5, cancellationToken);
            
            var seedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hit in hits)
            {
                if (!string.IsNullOrWhiteSpace(hit.Tags))
                {
                    var splitTags = hit.Tags.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var tag in splitTags)
                    {
                        var trimmed = tag.Trim().ToLowerInvariant();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            seedKeys.Add(trimmed);
                        }
                    }
                }
                
                if (!string.IsNullOrWhiteSpace(hit.EntityId))
                {
                    if (string.Equals(hit.EntityType, "room", StringComparison.OrdinalIgnoreCase))
                    {
                        seedKeys.Add($"room:{hit.EntityId}");
                    }
                    else if (string.Equals(hit.EntityType, "branch", StringComparison.OrdinalIgnoreCase))
                    {
                        seedKeys.Add($"branch:{hit.EntityId}");
                    }
                    else
                    {
                        seedKeys.Add(hit.EntityId.ToLowerInvariant());
                    }
                }
            }

            if (filter != null)
            {
                if (filter.BranchId.HasValue)
                {
                    seedKeys.Add($"branch:{filter.BranchId.Value}");
                }
                if (filter.RoomId.HasValue)
                {
                    seedKeys.Add($"room:{filter.RoomId.Value}");
                }
            }

            if (seedKeys.Count == 0)
            {
                return (hits, new GraphExpansionResult());
            }

            var expansionContext = await _expansionService.ExpandAsync(
                seedKeys.ToList(),
                maxHops: 2,
                cancellationToken: cancellationToken);

            var relatedLabels = new List<string>();
            foreach (var nodeSum in expansionContext.NodeSummaries)
            {
                var match = System.Text.RegularExpressions.Regex.Match(nodeSum, @"^\[[^\]]+\]\s*([^:]+):");
                if (match.Success)
                {
                    relatedLabels.Add(match.Groups[1].Value.Trim());
                }
                else
                {
                    relatedLabels.Add(nodeSum);
                }
            }

            return (
                hits,
                new GraphExpansionResult
                {
                    RelatedNodeLabels = relatedLabels,
                    RelatedEdges = expansionContext.EdgeSummaries,
                    SeedNodeKeys = expansionContext.SeedNodeKeys,
                    NodeSummaries = expansionContext.NodeSummaries,
                    EdgeSummaries = expansionContext.EdgeSummaries
                });
        }
    }
}
