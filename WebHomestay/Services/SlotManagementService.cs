// WebHomestay/Services/SlotManagementService.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Services;

public class SlotManagementService : ISlotManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly ISlotGenerationService _generationService;

    public SlotManagementService(ApplicationDbContext context, ISlotGenerationService generationService)
    {
        _context = context;
        _generationService = generationService;
    }

    public async Task ApplyTemplateToDateAsync(int roomId, int templateId, DateOnly targetDate)
    {
        var template = await _context.RoomSlotTemplates.FindAsync(templateId);
        if (template == null) return;

        // Clear existing for this template on this date
        var existing = await _context.RoomSlotInventories
            .Where(i => i.RoomId == roomId && i.SlotDate == targetDate && i.TemplateId == templateId)
            .ToListAsync();
        
        _context.RoomSlotInventories.RemoveRange(existing.Where(i => i.Status == "Available"));

        var newSlots = _generationService.GenerateRollingSlots(roomId, template, targetDate);
        _context.RoomSlotInventories.AddRange(newSlots);

        await _context.SaveChangesAsync();
    }

    public async Task ClearInventoryAsync(int roomId, DateOnly targetDate)
    {
        var existing = await _context.RoomSlotInventories
            .Where(i => i.RoomId == roomId && i.SlotDate == targetDate && i.Status == "Available")
            .ToListAsync();
        
        _context.RoomSlotInventories.RemoveRange(existing);
        await _context.SaveChangesAsync();
    }

    public async Task SyncTemplateChangesAsync(int templateId)
    {
        // Find all rooms currently using this template
        var assignments = await _context.RoomSlotTemplateAssignments
            .Where(a => a.TemplateId == templateId && a.IsActive)
            .ToListAsync();

        foreach (var assignment in assignments)
        {
            var start = assignment.EffectiveFrom > DateOnly.FromDateTime(DateTime.UtcNow) 
                        ? assignment.EffectiveFrom 
                        : DateOnly.FromDateTime(DateTime.UtcNow);
            
            var end = assignment.EffectiveTo ?? start.AddDays(30); // Default to 30 days if no end date

            var current = start;
            while (current <= end)
            {
                await ApplyTemplateToDateAsync(assignment.RoomId, templateId, current);
                current = current.AddDays(1);
            }
        }
    }

    public async Task DeleteTemplateAndCleanInventoryAsync(int templateId)
    {
        var template = await _context.RoomSlotTemplates.FindAsync(templateId);
        if (template == null) return;

        // 1. Remove all available inventory for this template
        var inventory = await _context.RoomSlotInventories
            .Where(i => i.TemplateId == templateId && i.Status == "Available")
            .ToListAsync();
        _context.RoomSlotInventories.RemoveRange(inventory);

        // 2. Remove all assignments
        var assignments = await _context.RoomSlotTemplateAssignments
            .Where(a => a.TemplateId == templateId)
            .ToListAsync();
        _context.RoomSlotTemplateAssignments.RemoveRange(assignments);

        // 3. Remove template
        _context.RoomSlotTemplates.Remove(template);

        await _context.SaveChangesAsync();
    }

    public async Task RemoveAssignmentAndCleanInventoryAsync(int assignmentId)
    {
        var assignment = await _context.RoomSlotTemplateAssignments.FindAsync(assignmentId);
        if (assignment == null) return;

        // Remove available inventory for this room/template in the future
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var inventory = await _context.RoomSlotInventories
            .Where(i => i.RoomId == assignment.RoomId && 
                        i.TemplateId == assignment.TemplateId && 
                        i.SlotDate >= today &&
                        i.Status == "Available")
            .ToListAsync();
        
        _context.RoomSlotInventories.RemoveRange(inventory);
        _context.RoomSlotTemplateAssignments.Remove(assignment);

        await _context.SaveChangesAsync();
    }
}
