using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Filters;
using WebHomestay.Services;

namespace WebHomestay.Controllers
{
    [AdminAuthorize]
    [Route("admin/bookings")]
    public class AdminBookingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMailService _mailService;
        private readonly ISettingService _settingService;

        public AdminBookingsController(ApplicationDbContext context, IMailService mailService, ISettingService settingService)
        {
            _context = context;
            _mailService = mailService;
            _settingService = settingService;
        }

        [AdminAuthorize(Permission = "bookings.view")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString("AdminRole");
            var branchId = HttpContext.Session.GetInt32("AdminBranchId");

            var query = _context.Bookings
                .Include(b => b.Room)
                    .ThenInclude(r => r.Branch)
                .Include(b => b.RoomSlotInventory)
                    .ThenInclude(i => i.Template)
                .Where(b => !b.IsDeleted && 
                           !( (b.Status == "AwaitingPayment" || b.Status == "PendingPayment" || b.Status == "PENDING") && b.CreatedAt < DateTime.UtcNow.AddMinutes(-5)));

            if (role != "SuperAdmin" && branchId.HasValue)
            {
                query = query.Where(b => b.Room.BranchId == branchId.Value);
            }

            // Auto-complete logic based on system settings
            var checkoutMode = await _settingService.GetStringAsync("CheckoutMode", "Auto");
            ViewBag.CheckoutMode = checkoutMode;

            if (checkoutMode == "Auto")
            {
                var now = DateTime.UtcNow;
                var pastConfirmed = await _context.Bookings
                    .Where(b => !b.IsDeleted && b.Status == "Confirmed" && b.EndTime < now)
                    .ToListAsync();

                if (pastConfirmed.Any())
                {
                    foreach (var b in pastConfirmed) b.Status = "CheckedOut";
                    await _context.SaveChangesAsync();
                }
            }

            var bookings = await query
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
            return View(bookings);
        }

        [AdminAuthorize(Permission = "bookings.delete")]
        [HttpGet("trash")]
        public async Task<IActionResult> Trash()
        {
            var role = HttpContext.Session.GetString("AdminRole");
            var branchId = HttpContext.Session.GetInt32("AdminBranchId");

            var query = _context.Bookings
                .Include(b => b.Room)
                .Include(b => b.RoomSlotInventory)
                    .ThenInclude(i => i.Template)
                .Where(b => b.IsDeleted);

            if (role != "SuperAdmin" && branchId.HasValue)
            {
                query = query.Where(b => b.Room.BranchId == branchId.Value);
            }

            var bookings = await query
                .OrderByDescending(b => b.DeletedAt)
                .ToListAsync();
            return View(bookings);
        }

        [AdminAuthorize(Permission = "bookings.delete")]
        [HttpPost("soft-delete")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null) return NotFound();

            // Condition: CANNOT delete Confirmed or AwaitingApproval as they are active bookings
            var s = booking.Status ?? "";
            bool isRestricted = s.Equals("Confirmed", StringComparison.OrdinalIgnoreCase) || 
                                s.Equals("AwaitingApproval", StringComparison.OrdinalIgnoreCase);

            if (isRestricted)
            {
                string statusVietnamese = s switch {
                    "Confirmed" => "Đã xác nhận",
                    "AwaitingApproval" => "Cần duyệt bill",
                    _ => s
                };
                TempData["ErrorMessage"] = $"Không thể xóa đơn hàng đang vận hành ({statusVietnamese}).";
                return RedirectToAction(nameof(Index));
            }

            booking.IsDeleted = true;
            booking.DeletedAt = DateTime.UtcNow;

            // Revert hourly slot to Available if applicable
            if (booking.BookingMode == BookingMode.Hourly && booking.RoomSlotInventoryId.HasValue)
            {
                var slot = await _context.RoomSlotInventories.FindAsync(booking.RoomSlotInventoryId.Value);
                if (slot != null)
                {
                    slot.Status = "Available";
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đơn hàng đã được chuyển vào thùng rác.";
            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "bookings.edit")]
        [HttpPost("restore")]
        public async Task<IActionResult> Restore(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null) return NotFound();

            booking.IsDeleted = false;
            booking.DeletedAt = null;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Trash));
        }

        [AdminAuthorize(Permission = "bookings.edit")]
        [HttpPost("permanent-delete")]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null) return NotFound();

            _context.Bookings.Remove(booking);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Trash));
        }

        [AdminAuthorize(Permission = "bookings.view")]
        [HttpGet("details/{id}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var booking = await _context.Bookings.Include(b => b.Room).FirstOrDefaultAsync(m => m.Id == id);
            if (booking == null) return NotFound();
            return View(booking);
        }

        [AdminAuthorize(Permission = "bookings.edit")]
        [HttpPost("approve")]
        public async Task<IActionResult> Approve(int id, string smartLockCode, string wifiPassword, string instructions)
        {
            var booking = await _context.Bookings.Include(b => b.Room).Include(b => b.Room.Branch).FirstOrDefaultAsync(b => b.Id == id);
            if (booking == null) return NotFound();

            var checkoutMode = await _settingService.GetStringAsync("CheckoutMode", "Auto");
            if (checkoutMode == "Auto" && booking.EndTime < DateTime.UtcNow)
            {
                booking.Status = "CheckedOut";
            }
            else
            {
                booking.Status = "Confirmed";
            }
            booking.PaymentStatus = "Paid";
            booking.SmartLockCode = smartLockCode;
            booking.WifiPassword = wifiPassword;
            booking.CheckInInstructions = instructions;

            await _context.SaveChangesAsync();

            // Prepare Professional Email Body
            string emailBody = $@"
                <div style='font-family: sans-serif; max-width: 600px; margin: auto; border: 1px solid #eee; padding: 20px; border-radius: 10px;'>
                    <div style='text-align: center; border-bottom: 2px solid #007bff; padding-bottom: 10px;'>
                        <h2 style='color: #007bff; margin: 0;'>XÁC NHẬN THANH TOÁN THÀNH CÔNG</h2>
                        <p style='color: #666;'>Cảm ơn bạn đã lựa chọn StayEasy Homestay</p>
                    </div>
                    
                    <div style='padding: 20px 0;'>
                        <p>Chào <strong>{booking.CustomerName}</strong>,</p>
                        <p>Chúng tôi đã xác nhận thanh toán cho đơn đặt phòng <strong>#{booking.Id}</strong>. Dưới đây là thông tin nhận phòng chi tiết của bạn:</p>
                        
                        <div style='background: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p style='margin: 5px 0;'><strong>Phòng:</strong> {booking.Room.Name}</p>
                            <p style='margin: 5px 0;'><strong>Địa chỉ:</strong> {booking.Room.Branch?.Address}</p>
                            <p style='margin: 5px 0;'><strong>Mã số cửa (PIN):</strong> <span style='font-size: 1.5rem; color: #007bff; font-weight: bold;'>{smartLockCode}</span></p>
                            <p style='margin: 5px 0;'><strong>Wifi Password:</strong> {wifiPassword}</p>
                        </div>

                        <h4 style='color: #007bff;'>HƯỚNG DẪN CHECK-IN</h4>
                        <ol style='color: #555;'>
                            <li>Tới địa chỉ trên đúng khung giờ check-in (thường sau 14:00).</li>
                            <li>Nhập mã PIN <strong style='color:#007bff'>{smartLockCode}</strong> vào khóa điện tử để mở cửa.</li>
                            <li>{instructions}</li>
                        </ol>

                        <h4 style='color: #007bff;'>NỘI QUY & QUY ĐỊNH</h4>
                        <ul style='color: #555; font-size: 0.9rem;'>
                            <li>Không hút thuốc trong phòng.</li>
                            <li>Không mang thú cưng (nếu chưa đăng ký).</li>
                            <li>Vui lòng tắt các thiết bị điện khi ra khỏi phòng.</li>
                            <li>Giờ Check-out: Trước 12:00 ngày kết thúc.</li>
                        </ul>

                        <h4 style='color: #dc3545;'>LƯU Ý VỀ PHỤ THU</h4>
                        <p style='color: #555; font-size: 0.9rem;'>
                            - Check-out muộn: Phụ thu 50.000đ/giờ.<br/>
                            - Hỏng hóc trang thiết bị: Bồi thường theo giá trị thực tế.<br/>
                            - Thêm khách: Phụ thu theo quy định của từng phòng.
                        </p>
                    </div>

                    <div style='text-align: center; border-top: 1px solid #eee; padding-top: 20px; color: #999; font-size: 0.8rem;'>
                        <p>StayEasy Homestay - Trải nghiệm tự động, tiện nghi như ở nhà.</p>
                        <p>Hotline hỗ trợ: {booking.Room.Branch?.Hotline}</p>
                    </div>
                </div>
            ";

            await _mailService.SendEmailAsync(booking.CustomerEmail ?? "", "Vé Điện Tử - StayEasy Homestay", emailBody);

            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "bookings.edit")]
        [HttpPost("update-status")]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null) return NotFound();
            

            string oldStatus = booking.Status;
            booking.Status = status;

            // If changing to Cancelled, revert slot
            if (status == "Cancelled" && booking.BookingMode == BookingMode.Hourly && booking.RoomSlotInventoryId.HasValue)
            {
                var slot = await _context.RoomSlotInventories.FindAsync(booking.RoomSlotInventoryId.Value);
                if (slot != null)
                {
                    slot.Status = "Available";
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [AdminAuthorize(Permission = "bookings.edit")]
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null) return NotFound();

            if (booking.Status == "Confirmed")
            {
                booking.Status = "CheckedOut";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đơn hàng #{id} đã được xác nhận hoàn thành.";
            }
            
            return RedirectToAction(nameof(Index));
        }
    }
}
