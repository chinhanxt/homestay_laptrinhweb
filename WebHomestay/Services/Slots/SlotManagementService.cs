using WebHomestay.Services;
using WebHomestay.Services.Slots;
using WebHomestay.Services.Room;
using WebHomestay.Services.Chat;
using WebHomestay.Services.Settings;
using WebHomestay.Services.Infrastructure;
// WebHomestay/Services/SlotManagementService.cs
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;

namespace WebHomestay.Services.Slots;

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

        // 1. Lấy tất cả inventory hiện tại của phòng này vào ngày này
        var allExisting = await _context.RoomSlotInventories
            .Where(i => i.RoomId == roomId && i.SlotDate == targetDate)
            .ToListAsync();

        // 2. Xác định các ô đã có khách hoặc đang xử lý (không phải Available)
        var reservedSlots = allExisting.Where(i => i.Status != "Available").ToList();

        // 3. Xóa các ô trống cũ của mẫu này
        var oldAvailableSlots = allExisting
            .Where(i => i.Status == "Available" && i.TemplateId == templateId)
            .ToList();
        
        _context.RoomSlotInventories.RemoveRange(oldAvailableSlots);

        // 4. Sinh lịch mới
        var generatedSlots = _generationService.GenerateRollingSlots(roomId, template, targetDate);

        // 5. Lọc: Chỉ thêm những ô không bị trùng với lịch đã đặt
        var validNewSlots = new List<RoomSlotInventory>();
        foreach (var ns in generatedSlots)
        {
            bool hasOverlap = reservedSlots.Any(rs => ns.StartTime < rs.EndTime && ns.EndTime > rs.StartTime);
            if (!hasOverlap)
            {
                validNewSlots.Add(ns);
            }
        }

        _context.RoomSlotInventories.AddRange(validNewSlots);
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
        var template = await _context.RoomSlotTemplates.FindAsync(templateId);
        if (template == null) return;

        // Tìm tất cả các gán lịch trình đang sử dụng mẫu này
        var assignments = await _context.RoomSlotTemplateAssignments
            .Where(a => a.TemplateId == templateId)
            .ToListAsync();

        if (!template.IsActive)
        {
            // Nếu mẫu bị tắt: Tắt tất cả các gán lịch trình đang hoạt động
            foreach (var assignment in assignments.Where(a => a.IsActive))
            {
                await ToggleAssignmentAsync(assignment.Id);
            }
        }
        else
        {
            // Nếu mẫu đang bật: Đồng bộ khung giờ cho các gán lịch trình đang hoạt động
            foreach (var assignment in assignments.Where(a => a.IsActive))
            {
                var start = assignment.EffectiveFrom > DateOnly.FromDateTime(DateTime.UtcNow) 
                            ? assignment.EffectiveFrom 
                            : DateOnly.FromDateTime(DateTime.UtcNow);
                
                var end = assignment.EffectiveTo ?? start.AddDays(30);

                var current = start;
                while (current <= end)
                {
                    await ApplyTemplateToDateAsync(assignment.RoomId, templateId, current);
                    current = current.AddDays(1);
                }
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

    public async Task<(bool success, string message)> ToggleAssignmentAsync(int assignmentId)
    {
        var assignment = await _context.RoomSlotTemplateAssignments
            .Include(a => a.Template)
            .FirstOrDefaultAsync(a => a.Id == assignmentId);
            
        if (assignment == null) return (false, "Không tìm thấy thông tin lịch trình.");

        // Nếu đang tạm dừng và muốn bật lại
        if (!assignment.IsActive)
        {
            if (assignment.Template != null && !assignment.Template.IsActive)
            {
                return (false, $"Mẫu '{assignment.Template.Name}' đang bị vô hiệu hóa hệ thống. Hãy kích hoạt lại mẫu trước khi tiếp tục lịch trình này.");
            }

            assignment.IsActive = true;
            // Reactivate: Re-generate inventory
            var start = assignment.EffectiveFrom > DateOnly.FromDateTime(DateTime.UtcNow) 
                        ? assignment.EffectiveFrom 
                        : DateOnly.FromDateTime(DateTime.UtcNow);
            
            var end = assignment.EffectiveTo ?? start.AddDays(30);

            var current = start;
            while (current <= end)
            {
                await ApplyTemplateToDateAsync(assignment.RoomId, assignment.TemplateId, current);
                current = current.AddDays(1);
            }
        }
        else
        {
            // Đang hoạt động -> Tạm dừng
            assignment.IsActive = false;
            // Deactivate: Remove available inventory for this room/template in the future
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var inventory = await _context.RoomSlotInventories
                .Where(i => i.RoomId == assignment.RoomId && 
                            i.TemplateId == assignment.TemplateId && 
                            i.SlotDate >= today &&
                            i.Status == "Available")
                .ToListAsync();
            
            _context.RoomSlotInventories.RemoveRange(inventory);
        }

        await _context.SaveChangesAsync();
        return (true, assignment.IsActive ? "Đã tiếp tục lịch trình." : "Đã tạm dừng lịch trình và dọn dẹp các ô trống.");
    }
}
