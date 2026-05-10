using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebHomestay.Services;

namespace WebHomestay.Controllers
{
    // Note: You might want to add [Authorize(Roles = "Admin,Staff")] here later
    public class StatisticsController : Controller
    {
        private readonly IStatisticsService _statsService;

        public StatisticsController(IStatisticsService statsService)
        {
            _statsService = statsService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetStats(DateTime? start, DateTime? end, int? branchId)
        {
            // Default to last 30 days if not provided
            var startDate = start ?? DateTime.Today.AddDays(-30);
            var endDate = end ?? DateTime.Today;
            
            var stats = await _statsService.GetDashboardStatsAsync(startDate, endDate, branchId);
            return Json(stats);
        }
    }
}
