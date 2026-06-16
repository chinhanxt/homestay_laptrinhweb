using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Neo4j.Driver;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Services.AI.Graph
{
    public class Neo4jGraphExpansionService
    {
        private readonly INeo4jGraphClient _graphClient;
        private readonly ApplicationDbContext? _context;

        public Neo4jGraphExpansionService(INeo4jGraphClient graphClient)
        {
            _graphClient = graphClient;
            _context = null;
        }

        public Neo4jGraphExpansionService(INeo4jGraphClient graphClient, ApplicationDbContext context)
        {
            _graphClient = graphClient;
            _context = context;
        }

        public virtual async Task<GraphExpansionContext> ExpandAsync(
            IReadOnlyList<string> seedNodeKeys,
            int maxHops = 2,
            IReadOnlyList<string>? allowedRelationshipTypes = null,
            int? maxNodes = null,
            int? maxEdges = null,
            CancellationToken cancellationToken = default)
        {
            if (seedNodeKeys == null || seedNodeKeys.Count == 0)
            {
                return new GraphExpansionContext();
            }

            try
            {
                // Standardize seed keys to match lowercased labels or standard format
                var seedKeys = seedNodeKeys.Select(k => k.ToLowerInvariant()).ToList();

                var cypher = $@"
                    MATCH (start:Node)
                    WHERE start.key IN $seedKeys OR start.label IN $seedNodeKeys
                    MATCH path = (start)-[*0..{maxHops}]-(target:Node)
                    RETURN path";

                var paths = await _graphClient.ExecuteReadAsync(
                    cypher,
                    new Dictionary<string, object>
                    {
                        { "seedKeys", seedKeys },
                        { "seedNodeKeys", seedNodeKeys.ToList() }
                    },
                    record => record["path"].As<IPath>(),
                    cancellationToken);

                var uniqueNodes = new Dictionary<string, (string NodeType, string Label, string Summary)>();
                var uniqueEdges = new HashSet<string>();

                // Maps internal Neo4j node IDs to their custom Label for edge rendering
                var nodeLabelMap = new Dictionary<string, string>();

                // First pass: collect all unique nodes and build the label mapping
                foreach (var path in paths)
                {
                    foreach (var node in path.Nodes)
                    {
                        var customKey = node.Properties.TryGetValue("key", out var k) ? k.ToString() ?? "" : "";
                        var nodeType = node.Properties.TryGetValue("nodeType", out var nt) ? nt.ToString() ?? "" : "";
                        var label = node.Properties.TryGetValue("label", out var l) ? l.ToString() ?? "" : "";
                        var summary = node.Properties.TryGetValue("summary", out var s) ? s.ToString() ?? "" : "";

                        if (string.IsNullOrEmpty(customKey)) continue;

                        var finalLabel = !string.IsNullOrEmpty(label) ? label : customKey;

                        // Add to label mapping
                        nodeLabelMap[node.ElementId] = finalLabel;
#pragma warning disable CS0618
                        nodeLabelMap[node.Id.ToString()] = finalLabel;
#pragma warning restore CS0618

                        // Add to unique nodes dictionary
                        if (!uniqueNodes.ContainsKey(customKey))
                        {
                            uniqueNodes[customKey] = (nodeType, finalLabel, summary);
                        }
                    }
                }

                // Second pass: collect relationships
                foreach (var path in paths)
                {
                    foreach (var rel in path.Relationships)
                    {
                        // Filter by allowed relationship types if specified
                        if (allowedRelationshipTypes != null && allowedRelationshipTypes.Count > 0)
                        {
                            if (!allowedRelationshipTypes.Any(t => string.Equals(t, rel.Type, StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }
                        }

                        // Resolve start/end node labels
                        var startLabel = nodeLabelMap.TryGetValue(rel.StartNodeElementId, out var sl) ? sl : 
                                         (nodeLabelMap.TryGetValue(rel.StartNodeId.ToString(), out var slOld) ? slOld : "Unknown");

                        var endLabel = nodeLabelMap.TryGetValue(rel.EndNodeElementId, out var el) ? el : 
                                       (nodeLabelMap.TryGetValue(rel.EndNodeId.ToString(), out var elOld) ? elOld : "Unknown");

                        var relType = rel.Type;
                        var edgeStr = $"{startLabel} -[{relType}]-> {endLabel}";

                        uniqueEdges.Add(edgeStr);
                    }
                }

                // Apply max bounds if specified
                var nodesList = uniqueNodes.Values
                    .Select(n => $"[{n.NodeType}] {n.Label}: {n.Summary}")
                    .ToList();

                if (maxNodes.HasValue && nodesList.Count > maxNodes.Value)
                {
                    nodesList = nodesList.Take(maxNodes.Value).ToList();
                }

                var edgesList = uniqueEdges.ToList();
                if (maxEdges.HasValue && edgesList.Count > maxEdges.Value)
                {
                    edgesList = edgesList.Take(maxEdges.Value).ToList();
                }

                return new GraphExpansionContext
                {
                    SeedNodeKeys = seedNodeKeys,
                    NodeSummaries = nodesList,
                    EdgeSummaries = edgesList
                };
            }
            catch (Exception ex)
            {
                // Neo4j driver execution failed (e.g. Neo4j is offline). Fall back to PostgreSQL database!
                if (_context != null)
                {
                    return await ExpandPostgresAsync(seedNodeKeys, maxHops, allowedRelationshipTypes, maxNodes, maxEdges, cancellationToken);
                }

                // If no DB context available, return empty result
                return new GraphExpansionContext
                {
                    SeedNodeKeys = seedNodeKeys,
                    NodeSummaries = new List<string>(),
                    EdgeSummaries = new List<string>()
                };
            }
        }

        private async Task<GraphExpansionContext> ExpandPostgresAsync(
            IReadOnlyList<string> seedNodeKeys,
            int maxHops,
            IReadOnlyList<string>? allowedRelationshipTypes,
            int? maxNodes,
            int? maxEdges,
            CancellationToken cancellationToken)
        {
            var seedKeys = seedNodeKeys.Select(k => k.ToLowerInvariant()).ToList();
            
            // Load all active nodes and edges in memory (for homestay scale, this is very fast and efficient)
            var allNodes = await _context!.AIGraphNodes
                .AsNoTracking()
                .Where(n => !n.IsDeleted && n.IsActive)
                .ToListAsync(cancellationToken);

            var allEdges = await _context.AIGraphEdges
                .AsNoTracking()
                .Where(e => !e.IsDeleted)
                .ToListAsync(cancellationToken);

            var nodeMap = allNodes.ToDictionary(n => n.Id);
            
            // Find start nodes matching seeds (exact or partial matches)
            var startNodes = allNodes
                .Where(n => seedKeys.Contains(n.Label.ToLowerInvariant()) || 
                            seedKeys.Contains(n.NodeType.ToLowerInvariant()) ||
                            seedKeys.Any(k => n.Label.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var visitedNodeIds = new HashSet<Guid>(startNodes.Select(n => n.Id));
            var visitedEdges = new HashSet<AIGraphEdge>();

            var currentHopNodes = new List<AIGraphNode>(startNodes);

            for (int hop = 0; hop < maxHops; hop++)
            {
                var nextHopNodes = new List<AIGraphNode>();
                var currentHopNodeIds = currentHopNodes.Select(n => n.Id).ToHashSet();

                var hopEdges = allEdges
                    .Where(e => currentHopNodeIds.Contains(e.FromNodeId) || currentHopNodeIds.Contains(e.ToNodeId))
                    .ToList();

                if (allowedRelationshipTypes != null && allowedRelationshipTypes.Count > 0)
                {
                    hopEdges = hopEdges
                        .Where(e => allowedRelationshipTypes.Any(t => string.Equals(t, e.RelationshipType, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                }

                foreach (var edge in hopEdges)
                {
                    if (visitedEdges.Add(edge))
                    {
                        var fromNode = nodeMap.GetValueOrDefault(edge.FromNodeId);
                        var toNode = nodeMap.GetValueOrDefault(edge.ToNodeId);

                        if (fromNode != null && visitedNodeIds.Add(fromNode.Id))
                        {
                            nextHopNodes.Add(fromNode);
                        }
                        if (toNode != null && visitedNodeIds.Add(toNode.Id))
                        {
                            nextHopNodes.Add(toNode);
                        }
                    }
                }

                if (nextHopNodes.Count == 0)
                {
                    break;
                }
                currentHopNodes = nextHopNodes;
            }

            var finalNodes = visitedNodeIds
                .Select(id => nodeMap.GetValueOrDefault(id))
                .Where(n => n != null)
                .Cast<AIGraphNode>()
                .ToList();

            var finalEdges = visitedEdges.ToList();

            var nodesList = finalNodes
                .Select(n => $"[{n.NodeType}] {n.Label}: {n.Summary}")
                .ToList();

            if (maxNodes.HasValue && nodesList.Count > maxNodes.Value)
            {
                nodesList = nodesList.Take(maxNodes.Value).ToList();
            }

            var edgesList = finalEdges
                .Select(e => {
                    var fromLabel = nodeMap.GetValueOrDefault(e.FromNodeId)?.Label ?? "Unknown";
                    var toLabel = nodeMap.GetValueOrDefault(e.ToNodeId)?.Label ?? "Unknown";
                    return $"{fromLabel} -[{e.RelationshipType}]-> {toLabel}";
                })
                .ToList();

            if (maxEdges.HasValue && edgesList.Count > maxEdges.Value)
            {
                edgesList = edgesList.Take(maxEdges.Value).ToList();
            }

            return new GraphExpansionContext
            {
                SeedNodeKeys = seedNodeKeys,
                NodeSummaries = nodesList,
                EdgeSummaries = edgesList
            };
        }
    }

    public sealed class GraphExpansionContext
    {
        public IReadOnlyList<string> SeedNodeKeys { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> NodeSummaries { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> EdgeSummaries { get; init; } = Array.Empty<string>();
    }
}
