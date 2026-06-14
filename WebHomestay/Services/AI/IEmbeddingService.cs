using System.Threading;
using System.Threading.Tasks;

namespace WebHomestay.Services.AI
{
    public interface IEmbeddingService
    {
        Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    }
}
