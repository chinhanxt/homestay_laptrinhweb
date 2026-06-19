using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Services;

namespace WebHomestay.Controllers.Public
{
    public class RoomsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IRoomBookingViewService _roomBookingViewService;

        public RoomsController(ApplicationDbContext context, IRoomBookingViewService roomBookingViewService)
        {
            _context = context;
            _roomBookingViewService = roomBookingViewService;
        }

        // GET: Rooms/Details/5
        public async Task<IActionResult> Details(int? id, DateOnly? hourlyDate)
        {
            if (id == null) return NotFound();

            var room = await _context.Rooms
                .Include(r => r.Branch)
                .Include(r => r.Amenities)
                .FirstOrDefaultAsync(m => m.Id == id);
            
            if (room == null) return NotFound();

            var selectedDate = hourlyDate ?? DateOnly.FromDateTime(DateTime.Today);
            var model = await _roomBookingViewService.BuildAsync(room, selectedDate);

            var allImages = new List<string>();
            if (!string.IsNullOrEmpty(room.ImageUrl))
                allImages.Add(room.ImageUrl);
            if (!string.IsNullOrEmpty(room.AdditionalImages))
            {
                var extra = System.Text.Json.JsonSerializer.Deserialize<List<string>>(room.AdditionalImages);
                if (extra != null)
                    allImages.AddRange(extra);
            }
            model.AllImages = allImages;

            return View(model);
        }

        public async Task<IActionResult> GetHourlySlots(int id, DateOnly hourlyDate)
        {
            var room = await _context.Rooms
                .Include(r => r.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);
            
            if (room == null) return NotFound();

            var model = await _roomBookingViewService.BuildAsync(room, hourlyDate);
            return PartialView("_HourlySlots", model);
        }
    }
}
