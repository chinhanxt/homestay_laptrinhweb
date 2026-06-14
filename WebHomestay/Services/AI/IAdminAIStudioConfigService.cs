using WebHomestay.Models.AI;

namespace WebHomestay.Services.AI;

public interface IAdminAIStudioConfigService
{
    Task<AdminAIStudioConfigResponse> GetAsync();
    Task SaveAsync(AdminAIStudioConfigRequest request);
}
