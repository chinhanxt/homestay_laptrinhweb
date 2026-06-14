using WebHomestay.Models.AI;

namespace WebHomestay.Services.AI;

public interface IAdminAIRuntimeSimulatorService
{
    IReadOnlyList<AdminAIRuntimePreset> GetPresets();
    Task<AdminAIRuntimeSimulationResult> SimulateAsync(AdminAIStudioConfig config, AdminAIRuntimeState state);
}
