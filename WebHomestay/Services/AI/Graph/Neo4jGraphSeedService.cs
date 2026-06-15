using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Services.AI.Graph
{
    public sealed class Neo4jGraphSeedService
    {
        private readonly ApplicationDbContext _context;
        private readonly INeo4jGraphClient _graphClient;

        public Neo4jGraphSeedService(ApplicationDbContext context, INeo4jGraphClient graphClient)
        {
            _context = context;
            _graphClient = graphClient;
        }

        public async Task<int> SeedAsync(CancellationToken cancellationToken)
        {
            // 1. Clear database
            await _graphClient.ExecuteWriteAsync("MATCH (n) DETACH DELETE n", new Dictionary<string, object>(), cancellationToken);

            int count = 0;

            // 2. Fetch data from EF Core
            var branches = await _context.Branches.Where(b => !b.IsDeleted).ToListAsync(cancellationToken);
            var rooms = await _context.Rooms.Where(r => !r.IsDeleted).ToListAsync(cancellationToken);
            var nodes = await _context.AIGraphNodes.Where(n => n.IsActive && !n.IsDeleted).ToListAsync(cancellationToken);
            var edges = await _context.AIGraphEdges.Include(e => e.FromNode).Include(e => e.ToNode).Where(e => !e.IsDeleted).ToListAsync(cancellationToken);

            // 3. Upsert Branches
            foreach (var b in branches)
            {
                var key = $"branch:{b.Id}";
                var metadata = new Dictionary<string, object>
                {
                    { "id", b.Id },
                    { "address", b.Address },
                    { "hotline", b.Hotline ?? "" },
                    { "email", b.Email ?? "" }
                };
                var metadataJson = JsonSerializer.Serialize(metadata);

                await _graphClient.ExecuteWriteAsync(
                    @"MERGE (n:Node { key: $key })
                      ON CREATE SET n.nodeType = $nodeType, n.label = $label, n.summary = $summary, n.metadata = $metadata
                      ON MATCH SET n.nodeType = $nodeType, n.label = $label, n.summary = $summary, n.metadata = $metadata",
                    new Dictionary<string, object>
                    {
                        { "key", key },
                        { "nodeType", "branch" },
                        { "label", b.Name },
                        { "summary", b.Description ?? b.Address },
                        { "metadata", metadataJson }
                    },
                    cancellationToken);
                count++;
            }

            // 4. Upsert Rooms
            foreach (var r in rooms)
            {
                var key = $"room:{r.Id}";
                var metadata = new Dictionary<string, object>
                {
                    { "id", r.Id },
                    { "branchId", r.BranchId },
                    { "pricePerHour", (double)r.PricePerHour },
                    { "pricePerDay", (double)r.PricePerDay },
                    { "capacity", r.Capacity },
                    { "maxGuests", r.MaxGuests }
                };
                var metadataJson = JsonSerializer.Serialize(metadata);

                await _graphClient.ExecuteWriteAsync(
                    @"MERGE (n:Node { key: $key })
                      ON CREATE SET n.nodeType = $nodeType, n.label = $label, n.summary = $summary, n.metadata = $metadata
                      ON MATCH SET n.nodeType = $nodeType, n.label = $label, n.summary = $summary, n.metadata = $metadata",
                    new Dictionary<string, object>
                    {
                        { "key", key },
                        { "nodeType", "room" },
                        { "label", r.Name },
                        { "summary", r.Description ?? "" },
                        { "metadata", metadataJson }
                    },
                    cancellationToken);
                count++;

                // Create relationship from Room to Branch
                var branchKey = $"branch:{r.BranchId}";
                await _graphClient.ExecuteWriteAsync(
                    @"MATCH (room:Node { key: $roomKey }), (branch:Node { key: $branchKey })
                      MERGE (room)-[:BELONGS_TO]->(branch)",
                    new Dictionary<string, object>
                    {
                        { "roomKey", key },
                        { "branchKey", branchKey }
                    },
                    cancellationToken);
            }

            // 5. Upsert AIGraphNodes
            foreach (var n in nodes)
            {
                var key = n.Label.ToLowerInvariant();
                await _graphClient.ExecuteWriteAsync(
                    @"MERGE (n:Node { key: $key })
                      ON CREATE SET n.nodeType = $nodeType, n.label = $label, n.summary = $summary, n.metadata = $metadata
                      ON MATCH SET n.nodeType = $nodeType, n.label = $label, n.summary = $summary, n.metadata = $metadata",
                    new Dictionary<string, object>
                    {
                        { "key", key },
                        { "nodeType", n.NodeType },
                        { "label", n.Label },
                        { "summary", n.Summary },
                        { "metadata", n.MetadataJson }
                    },
                    cancellationToken);
                count++;
            }

            // 6. Upsert AIGraphEdges
            foreach (var e in edges)
            {
                if (e.FromNode == null || e.ToNode == null || e.FromNode.IsDeleted || e.ToNode.IsDeleted || !e.FromNode.IsActive || !e.ToNode.IsActive)
                    continue;

                var fromKey = e.FromNode.Label.ToLowerInvariant();
                var toKey = e.ToNode.Label.ToLowerInvariant();
                var rawType = e.RelationshipType;
                var safeRelType = Regex.Replace(rawType, @"[^a-zA-Z0-9_]", "").ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(safeRelType))
                {
                    safeRelType = "RELATED_TO";
                }

                var cypher = $@"
                    MATCH (from:Node {{ key: $fromKey }})
                    MATCH (to:Node {{ key: $toKey }})
                    MERGE (from)-[r:{safeRelType}]->(to)
                    ON CREATE SET r.weight = $weight, r.evidence = $evidence
                    ON MATCH SET r.weight = $weight, r.evidence = $evidence";

                await _graphClient.ExecuteWriteAsync(
                    cypher,
                    new Dictionary<string, object>
                    {
                        { "fromKey", fromKey },
                        { "toKey", toKey },
                        { "weight", (double)e.Weight },
                        { "evidence", e.Evidence }
                    },
                    cancellationToken);
            }

            return count;
        }
    }
}
