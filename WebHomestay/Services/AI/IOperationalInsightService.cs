using System.Threading;
using System.Threading.Tasks;

namespace WebHomestay.Services.AI
{
    public interface IOperationalInsightService
    {
        Task<string> GetDailyBriefingJsonAsync(CancellationToken cancellationToken = default);
    }
}
