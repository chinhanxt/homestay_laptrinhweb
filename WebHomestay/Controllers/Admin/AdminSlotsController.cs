// WebHomestay/Controllers/AdminSlotsController.cs
using Microsoft.AspNetCore.Mvc;
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
using WebHomestay.Services;
using WebHomestay.Filters;

namespace WebHomestay.Controllers.Admin;

[AdminAuthorize]
public class AdminSlotsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ISlotManagementService _slotManagementService;

    public AdminSlotsController(ApplicationDbContext context, ISlotManagementService slotManagementService)
    {
        _context = context;
        _slotManagementService = slotManagementService;
    }

    [AdminAuthorize(Permission = "slots.manage")]
    public IActionResult Index()
    {
        return Redirect("/chinhan/hethong/settings?tab=time");
    }

    [AdminAuthorize(Permission = "slots.manage")]
    [HttpGet]
    public async Task<IActionResult> CreateTemplate() => View();

    [AdminAuthorize(Permission = "slots.manage")]
    [HttpPost]
    public async Task<IActionResult> CreateTemplate(RoomSlotTemplate template)
    {
        if (ModelState.IsValid)
        {
            _context.RoomSlotTemplates.Add(template);
            await _context.SaveChangesAsync();
            return Redirect("/chinhan/hethong/settings?tab=time");
        }
        return View(template);
    }

    [AdminAuthorize(Permission = "slots.manage")]
    [HttpGet]
    public async Task<IActionResult> EditTemplate(int id)
    {
        var template = await _context.RoomSlotTemplates.FindAsync(id);
        if (template == null) return NotFound();
        return View(template);
    }

    [AdminAuthorize(Permission = "slots.manage")]
    [HttpPost]
    public async Task<IActionResult> EditTemplate(int id, string name, string code, int durationMinutes, int cleanupMinutes, TimeOnly? seedStartTime, bool isActive)
    {
        var template = await _context.RoomSlotTemplates.FindAsync(id);
        if (template == null) return NotFound();

        template.Name = name;
        template.Code = code;
        template.DurationMinutes = durationMinutes;
        template.CleanupMinutes = cleanupMinutes;
        template.SeedStartTime = seedStartTime;
        template.IsActive = isActive;

        await _context.SaveChangesAsync();
        
        // Sync changes to all rooms using this template
        await _slotManagementService.SyncTemplateChangesAsync(template.Id);
        
        TempData["SuccessMessage"] = "Đã cập nhật mẫu và đồng bộ lịch trình các phòng.";
        return Redirect("/chinhan/hethong/settings?tab=time");
    }

    [AdminAuthorize(Permission = "slots.manage")]
    [HttpPost]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        var template = await _context.RoomSlotTemplates.FindAsync(id);
        if (template != null)
        {
            template.IsDeleted = true;
            template.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã chuyển mẫu khung giờ vào thùng rác.";
        }
        return Redirect("/chinhan/hethong/settings?tab=time");
    }

    [AdminAuthorize(Permission = "slots.manage")]
    [HttpPost]
    public async Task<IActionResult> RestoreTemplate(int id)
    {
        var template = await _context.RoomSlotTemplates.FindAsync(id);
        if (template != null)
        {
            template.IsDeleted = false;
            template.DeletedAt = null;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã khôi phục mẫu khung giờ.";
        }
        return Redirect("/chinhan/hethong/settings?tab=time");
    }

    [AdminAuthorize(Permission = "slots.manage")]
    [HttpPost]
    public async Task<IActionResult> PermanentDeleteTemplate(int id)
    {
        var template = await _context.RoomSlotTemplates.FindAsync(id);
        if (template != null)
        {
            // Clean up inventory and assignments before hard delete
            if (template.IsDeleted)
            {
                await _slotManagementService.DeleteTemplateAndCleanInventoryAsync(id);
                _context.RoomSlotTemplates.Remove(template);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa vĩnh viễn mẫu khung giờ.";
            }
        }
        return Redirect("/chinhan/hethong/settings?tab=time");
    }

    [AdminAuthorize(Permission = "rooms.schedules")]
    public async Task<IActionResult> RoomSchedules(int roomId)
    {
        var room = await _context.Rooms
            .Include(r => r.SlotAssignments).ThenInclude(a => a.Template)
            .FirstOrDefaultAsync(r => r.Id == roomId);
        
        if (room == null) return NotFound();

        ViewBag.Templates = await _context.RoomSlotTemplates.Where(t => t.IsActive).ToListAsync();
        return View(room);
    }

    [AdminAuthorize(Permission = "rooms.schedules")]
    [HttpPost]
    public async Task<IActionResult> ApplyTemplate(int roomId, int templateId, DateOnly startDate, DateOnly endDate)
    {
        // 1. Generate the inventory slots
        var current = startDate;
        while (current <= endDate)
        {
            await _slotManagementService.ApplyTemplateToDateAsync(roomId, templateId, current);
            current = current.AddDays(1);
        }

        // 2. Create or update the assignment record for UI display
        var existingAssignment = await _context.RoomSlotTemplateAssignments
            .FirstOrDefaultAsync(a => a.RoomId == roomId && a.TemplateId == templateId && a.EffectiveFrom == startDate);

        if (existingAssignment == null)
        {
            _context.RoomSlotTemplateAssignments.Add(new RoomSlotTemplateAssignment
            {
                RoomId = roomId,
                TemplateId = templateId,
                EffectiveFrom = startDate,
                EffectiveTo = endDate,
                IsActive = true
            });
        }
        else
        {
            existingAssignment.EffectiveTo = endDate;
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã áp dụng mẫu cho phòng từ {startDate} đến {endDate}";
        return RedirectToAction(nameof(RoomSchedules), new { roomId });
    }

    [AdminAuthorize(Permission = "rooms.schedules")]
    [HttpPost]
    public async Task<IActionResult> ToggleAssignment(int assignmentId)
    {
        var assignment = await _context.RoomSlotTemplateAssignments.FindAsync(assignmentId);
        if (assignment == null) return NotFound();

        int roomId = assignment.RoomId;
        var result = await _slotManagementService.ToggleAssignmentAsync(assignmentId);

        if (result.success)
        {
            TempData["SuccessMessage"] = result.message;
        }
        else
        {
            TempData["ErrorMessage"] = result.message;
        }

        return RedirectToAction(nameof(RoomSchedules), new { roomId });
    }

    [AdminAuthorize(Permission = "rooms.schedules")]
    [HttpPost]
    public async Task<IActionResult> RemoveAssignment(int assignmentId)
    {
        var assignment = await _context.RoomSlotTemplateAssignments.FindAsync(assignmentId);
        if (assignment == null) return NotFound();
        
        int roomId = assignment.RoomId;
        await _slotManagementService.RemoveAssignmentAndCleanInventoryAsync(assignmentId);
        
        TempData["SuccessMessage"] = "Đã gỡ bỏ lịch trình và dọn dẹp các ô trống.";
        return RedirectToAction(nameof(RoomSchedules), new { roomId });
    }
}
