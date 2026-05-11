// WebHomestay/Controllers/AdminSlotsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;

namespace WebHomestay.Controllers;

public class AdminSlotsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ISlotManagementService _slotManagementService;

    public AdminSlotsController(ApplicationDbContext context, ISlotManagementService slotManagementService)
    {
        _context = context;
        _slotManagementService = slotManagementService;
    }

    public IActionResult Index()
    {
        return Redirect("/admin/settings?tab=time");
    }

    [HttpGet]
    public async Task<IActionResult> CreateTemplate() => View();

    [HttpPost]
    public async Task<IActionResult> CreateTemplate(RoomSlotTemplate template)
    {
        if (ModelState.IsValid)
        {
            _context.RoomSlotTemplates.Add(template);
            await _context.SaveChangesAsync();
            return Redirect("/admin/settings?tab=time");
        }
        return View(template);
    }

    [HttpGet]
    public async Task<IActionResult> EditTemplate(int id)
    {
        var template = await _context.RoomSlotTemplates.FindAsync(id);
        if (template == null) return NotFound();
        return View(template);
    }

    [HttpPost]
    public async Task<IActionResult> EditTemplate(RoomSlotTemplate template)
    {
        if (ModelState.IsValid)
        {
            _context.Entry(template).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            
            // Sync changes to all rooms using this template (Selection A)
            await _slotManagementService.SyncTemplateChangesAsync(template.Id);
            
            TempData["SuccessMessage"] = "Đã cập nhật mẫu và đồng bộ lịch trình các phòng.";
            return Redirect("/admin/settings?tab=time");
        }
        return View(template);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        await _slotManagementService.DeleteTemplateAndCleanInventoryAsync(id);
        TempData["SuccessMessage"] = "Đã xóa mẫu và dọn dẹp các ô lịch trống liên quan.";
        return Redirect("/admin/settings?tab=time");
    }

    public async Task<IActionResult> RoomSchedules(int roomId)
    {
        var room = await _context.Rooms
            .Include(r => r.SlotAssignments).ThenInclude(a => a.Template)
            .FirstOrDefaultAsync(r => r.Id == roomId);
        
        if (room == null) return NotFound();

        ViewBag.Templates = await _context.RoomSlotTemplates.Where(t => t.IsActive).ToListAsync();
        return View(room);
    }

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
