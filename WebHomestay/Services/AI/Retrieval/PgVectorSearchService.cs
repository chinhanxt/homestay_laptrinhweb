using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using WebHomestay.Data;

namespace WebHomestay.Services.AI.Retrieval
{
    public sealed class PgVectorSearchService : IVectorSearchService
    {
        private readonly ApplicationDbContext _context;

        public PgVectorSearchService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<VectorSearchResult>> SearchKnowledgeAsync(
            float[] queryEmbedding,
            VectorSearchFilter? filter,
            int take,
            CancellationToken cancellationToken)
        {
            EnsureVectorSearchIsSupported();

            var effectiveFilter = filter ?? new VectorSearchFilter();
            var queryVector = new Vector(queryEmbedding);
            var roomTag = effectiveFilter.RoomId.HasValue ? $"room-{effectiveFilter.RoomId.Value}" : string.Empty;
            var branchTag = effectiveFilter.BranchId.HasValue ? $"branch-{effectiveFilter.BranchId.Value}" : string.Empty;

            var baseQuery = _context.AIKnowledgeUnits
                .AsNoTracking()
                .Where(x => !effectiveFilter.ActiveOnly || x.IsActive)
                .Where(x => !x.IsDeleted)
                .Where(x => x.Embedding != null);

            if (effectiveFilter.ScopeId is Guid scopeId)
            {
                baseQuery = baseQuery.Where(x => x.ScopeId == scopeId);
            }

            if (effectiveFilter.RoomId.HasValue)
            {
                baseQuery = baseQuery.Where(x =>
                    !EF.Functions.Like(x.Tags, "%room-%") ||
                    x.Tags.Contains(roomTag));
            }

            if (effectiveFilter.BranchId.HasValue)
            {
                baseQuery = baseQuery.Where(x =>
                    !EF.Functions.Like(x.Tags, "%branch-%") ||
                    x.Tags.Contains(branchTag));
            }

            var rows = await baseQuery
                .OrderBy(x => x.Embedding!.CosineDistance(queryVector))
                .Take(take)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.Content,
                    x.Tags,
                    Distance = x.Embedding!.CosineDistance(queryVector)
                })
                .ToListAsync(cancellationToken);

            return rows
                .Select(x => new VectorSearchResult
                {
                    EntityType = "knowledge",
                    EntityId = x.Id.ToString(),
                    Title = x.Title,
                    Content = x.Content,
                    Tags = x.Tags,
                    Score = 1.0 - x.Distance
                })
                .ToList();
        }

        public async Task<IReadOnlyList<VectorSearchResult>> SearchRoomsAsync(
            float[] queryEmbedding,
            int? branchId,
            int take,
            CancellationToken cancellationToken)
        {
            EnsureVectorSearchIsSupported();

            var queryVector = new Vector(queryEmbedding);

            var baseQuery = _context.Rooms
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.Status == "Available")
                .Where(x => x.Embedding != null);

            if (branchId is int bId)
            {
                baseQuery = baseQuery.Where(x => x.BranchId == bId);
            }

            var rows = await baseQuery
                .OrderBy(x => x.Embedding!.CosineDistance(queryVector))
                .Take(take)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Description,
                    Distance = x.Embedding!.CosineDistance(queryVector)
                })
                .ToListAsync(cancellationToken);

            return rows
                .Select(x => new VectorSearchResult
                {
                    EntityType = "room",
                    EntityId = x.Id.ToString(),
                    Title = x.Name,
                    Content = x.Description ?? string.Empty,
                    Tags = string.Empty,
                    Score = 1.0 - x.Distance
                })
                .ToList();
        }

        private void EnsureVectorSearchIsSupported()
        {
            if (!string.Equals(_context.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Pgvector search requires the PostgreSQL provider. The current provider does not support database-side vector ranking.");
            }
        }
    }
}
