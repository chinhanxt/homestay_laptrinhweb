using WebHomestay.Services.Slots;
using WebHomestay.Services.Room;
using WebHomestay.Services.Chat;
using WebHomestay.Services.Settings;
using WebHomestay.Services.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;
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
using WebHomestay.Models.ViewModels;
using WebHomestay.Models;
using WebHomestay.Services;

namespace WebHomestay.Controllers.Public
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IAvailabilityService _availabilityService;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, IAvailabilityService availabilityService)
        {
            _logger = logger;
            _context = context;
            _availabilityService = availabilityService;
        }

        public async Task<IActionResult> Index()
        {
            var rooms = await _context.Rooms
                .Include(r => r.Amenities)
                .Include(r => r.Branch)
                .OrderBy(r => r.Branch != null ? r.Branch.Name : "")
                .ThenBy(r => r.Name)
                .ToListAsync();

            ViewBag.Branches = await _context.Branches.ToListAsync();
            
            return View(rooms);
        }

        public async Task<IActionResult> Search(int? branchId, DateOnly? checkIn, DateOnly? checkOut, int guests = 1)
        {
            if (!checkIn.HasValue) checkIn = DateOnly.FromDateTime(DateTime.Today);
            if (!checkOut.HasValue) checkOut = checkIn.Value.AddDays(1);
            if (checkOut <= checkIn) checkOut = checkIn.Value.AddDays(1);

            var start = checkIn.Value.ToDateTime(TimeOnly.MinValue);
            var end = checkOut.Value.ToDateTime(TimeOnly.MinValue);

            var query = _context.Rooms
                .Include(r => r.Branch)
                .Include(r => r.Amenities)
                .AsQueryable();

            if (branchId.HasValue && branchId > 0)
                query = query.Where(r => r.BranchId == branchId.Value);

            var allRooms = await query.ToListAsync();
            var availableRooms = new List<Room>();

            foreach (var room in allRooms)
            {
                if (await _availabilityService.IsRoomAvailable(room.Id, start, end))
                    availableRooms.Add(room);
            }

            var model = new SearchResultViewModel
            {
                Rooms = availableRooms,
                CheckIn = checkIn.Value,
                CheckOut = checkOut.Value,
                Guests = guests,
                BranchId = branchId ?? 0,
                Branches = await _context.Branches.ToListAsync()
            };

            ViewBag.Branches = await _context.Branches.ToListAsync();
            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
