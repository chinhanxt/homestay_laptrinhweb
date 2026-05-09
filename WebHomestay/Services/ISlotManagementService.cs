// WebHomestay/Services/ISlotManagementService.cs
namespace WebHomestay.Services;

public interface ISlotManagementService
{
    Task ApplyTemplateToDateAsync(int roomId, int templateId, DateOnly targetDate);
    Task ClearInventoryAsync(int roomId, DateOnly targetDate);
    Task SyncTemplateChangesAsync(int templateId);
    Task DeleteTemplateAndCleanInventoryAsync(int templateId);
    Task RemoveAssignmentAndCleanInventoryAsync(int assignmentId);
}
