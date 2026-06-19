using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services;

public interface IBranchLeadTimeService
{
    Task<BranchLeadTimeRule> ResolveAsync(int? branchId, CancellationToken cancellationToken = default);
}
