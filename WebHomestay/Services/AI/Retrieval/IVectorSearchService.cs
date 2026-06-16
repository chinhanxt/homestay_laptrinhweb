using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebHomestay.Services.AI.Retrieval
{
    public interface IVectorSearchService
    {
        Task<IReadOnlyList<VectorSearchResult>> SearchKnowledgeAsync(
            float[] queryEmbedding,
            VectorSearchFilter? filter,
            int take,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<VectorSearchResult>> SearchRoomsAsync(
            float[] queryEmbedding,
            int? branchId,
            int take,
            CancellationToken cancellationToken = default);
    }
}
