using WebHomestay.Models.Configuration;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Entities.AI;

namespace WebHomestay.Services.AI;

public interface IAdminAIStudioConfigService
{
    Task<AdminAIStudioConfigResponse> GetAsync();
    Task SaveAsync(AdminAIStudioConfigRequest request);
}
