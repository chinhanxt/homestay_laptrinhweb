using WebHomestay.Models.Configuration;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Entities.AI;

namespace WebHomestay.Services.AI;

public interface IAdminAIRuntimeSimulatorService
{
    IReadOnlyList<AdminAIRuntimePreset> GetPresets();
    Task<AdminAIRuntimeSimulationResult> SimulateAsync(AdminAIStudioConfig config, AdminAIRuntimeState state);
}
