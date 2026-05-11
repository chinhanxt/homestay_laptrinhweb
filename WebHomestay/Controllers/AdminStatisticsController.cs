using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebHomestay.Services;
using WebHomestay.Filters;

namespace WebHomestay.Controllers
{
    [AdminAuthorize]
    [Route("admin/statistics")]
    public class AdminStatisticsController : Controller
    {
        private readonly IStatisticsService _statsService;

        public AdminStatisticsController(IStatisticsService statsService)
        {
            _statsService = statsService;
        }

        [Route("")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("GetStats")]
        public async Task<IActionResult> GetStats(DateTime? start, DateTime? end, int? branchId)
        {
            var startDate = start ?? DateTime.Today.AddDays(-30);
            var endDate = end ?? DateTime.Today;
            
            var stats = await _statsService.GetDashboardStatsAsync(startDate, endDate, branchId);
            return Json(stats);
        }

        [HttpGet("GetBranches")]
        public async Task<IActionResult> GetBranches()
        {
            var branches = await _statsService.GetBranchesAsync();
            return Json(branches);
        }

        [HttpGet("Export")]
        public async Task<IActionResult> Export(string type, DateTime start, DateTime end, int? branchId)
        {
            var stats = await _statsService.GetDashboardStatsAsync(start, end, branchId);

            if (type == "excel")
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Statistics");
                
                // Headers
                worksheet.Cell(1, 1).Value = "Room Name";
                worksheet.Cell(1, 2).Value = "Branch";
                worksheet.Cell(1, 3).Value = "Total Orders";
                worksheet.Cell(1, 4).Value = "Revenue (VND)";
                
                var range = worksheet.Range(1, 1, 1, 4);
                range.Style.Font.Bold = true;
                range.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;

                // Data
                int row = 2;
                foreach (var rp in stats.RoomPerformance)
                {
                    worksheet.Cell(row, 1).Value = rp.RoomName;
                    worksheet.Cell(row, 2).Value = rp.BranchName;
                    worksheet.Cell(row, 3).Value = rp.TotalOrders;
                    worksheet.Cell(row, 4).Value = rp.Revenue;
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using var stream = new System.IO.MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();

                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                    $"HomestayStats_{start:yyyyMMdd}_{end:yyyyMMdd}.xlsx");
            }

            return BadRequest("Unsupported export type");
        }
    }
}
