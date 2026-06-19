using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;
// WebHomestay/Services/ISlotManagementService.cs
namespace WebHomestay.Services;

public interface ISlotManagementService
{
    Task ApplyTemplateToDateAsync(int roomId, int templateId, DateOnly targetDate);
    Task ClearInventoryAsync(int roomId, DateOnly targetDate);
    Task SyncTemplateChangesAsync(int templateId);
    Task DeleteTemplateAndCleanInventoryAsync(int templateId);
    Task RemoveAssignmentAndCleanInventoryAsync(int assignmentId);
    Task<(bool success, string message)> ToggleAssignmentAsync(int assignmentId);
}
