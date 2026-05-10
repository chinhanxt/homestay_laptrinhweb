using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services;

namespace WebHomestay.Controllers
{
    public class BookingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAvailabilityService _availabilityService;
        private readonly IRoomBookingViewService _roomBookingViewService;
        private readonly IBookingCreationService _bookingCreationService;
        private readonly IWebHostEnvironment _environment;
        private readonly IImageMaskingService _maskingService;

        public BookingsController(
            ApplicationDbContext context, 
            IAvailabilityService availabilityService, 
            IRoomBookingViewService roomBookingViewService,
            IBookingCreationService bookingCreationService,
            IWebHostEnvironment environment,
            IImageMaskingService maskingService)
        {
            _context = context;
            _availabilityService = availabilityService;
            _roomBookingViewService = roomBookingViewService;
            _bookingCreationService = bookingCreationService;
            _environment = environment;
            _maskingService = maskingService;
        }

        [HttpGet]
        public async Task<IActionResult> CheckoutHourly(int roomId, int slotId)
        {
            var viewModel = await _roomBookingViewService.BuildHourlyCheckoutAsync(roomId, slotId);
            if (viewModel == null) return NotFound();
            return View("Checkout", viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> CheckoutDaily(int roomId, DateOnly checkInDate, DateOnly checkOutDate)
        {
            var viewModel = await _roomBookingViewService.BuildDailyCheckoutAsync(roomId, checkInDate, checkOutDate);
            if (viewModel == null) return NotFound();
            return View("Checkout", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(WebHomestay.Models.ViewModels.CreateBookingRequest request, IFormFile? idCardFront, IFormFile? idCardBack)
        {
            if (request.BookingMode == BookingMode.Daily)
            {
                if (!request.CheckInDate.HasValue || !request.CheckOutDate.HasValue)
                {
                    ModelState.AddModelError("", "Vui lòng chọn ngày nhận và trả phòng.");
                }
            }
            else if (request.BookingMode == BookingMode.Hourly)
            {
                if (!request.SlotInventoryId.HasValue)
                {
                    ModelState.AddModelError("", "Vui lòng chọn khung giờ.");
                }
            }

            if (!ModelState.IsValid)
            {
                var invalidViewModel = request.BookingMode == BookingMode.Hourly
                    ? await _roomBookingViewService.BuildHourlyCheckoutAsync(request.RoomId, request.SlotInventoryId ?? 0)
                    : await _roomBookingViewService.BuildDailyCheckoutAsync(request.RoomId, request.CheckInDate ?? DateOnly.FromDateTime(DateTime.Today), request.CheckOutDate ?? DateOnly.FromDateTime(DateTime.Today).AddDays(1));
                return View("Checkout", invalidViewModel);
            }

            try
            {
                var booking = request.BookingMode == BookingMode.Hourly
                    ? await _bookingCreationService.CreateHourlyBookingAsync(request)
                    : await _bookingCreationService.CreateDailyBookingAsync(request);

                // Handle ID Card uploads if provided
                if (idCardFront != null || idCardBack != null)
                {
                    if (idCardFront != null)
                    {
                        booking.IdCardFrontPath = await SaveSecureFile(idCardFront);
                        booking.IdCardFrontMaskedPath = await _maskingService.MaskIdCardAsync(booking.IdCardFrontPath!, true);
                    }
                    
                    if (idCardBack != null)
                    {
                        booking.IdCardBackPath = await SaveSecureFile(idCardBack);
                        booking.IdCardBackMaskedPath = await _maskingService.MaskIdCardAsync(booking.IdCardBackPath!, false);
                    }
                    
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Success), new { id = booking.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Có lỗi xảy ra khi lưu thông tin đặt phòng. Vui lòng thử lại hoặc liên hệ hỗ trợ.");
                var errorViewModel = request.BookingMode == BookingMode.Hourly
                    ? await _roomBookingViewService.BuildHourlyCheckoutAsync(request.RoomId, request.SlotInventoryId ?? 0)
                    : await _roomBookingViewService.BuildDailyCheckoutAsync(request.RoomId, request.CheckInDate!.Value, request.CheckOutDate!.Value);
                return View("Checkout", errorViewModel);
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadPaymentProof(int bookingId, IFormFile paymentProof)
        {
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null) return NotFound();

            if (paymentProof != null && paymentProof.Length > 0)
            {
                // Save payment proof to wwwroot/uploads/payments
                string uploadDir = Path.Combine(_environment.WebRootPath, "uploads", "payments");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                string fileName = $"bill_{bookingId}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(paymentProof.FileName)}";
                string filePath = Path.Combine(uploadDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await paymentProof.CopyToAsync(stream);
                }

                booking.PaymentProofUrl = "/uploads/payments/" + fileName;
                booking.Status = "AwaitingApproval";
                await _context.SaveChangesAsync();

                TempData["PaymentSuccess"] = true;
                TempData["SuccessMessage"] = "Gửi minh chứng thành công! Vui lòng chờ nhân viên xác nhận.";
            }

            return RedirectToAction("Success", new { id = bookingId });
        }

        public async Task<IActionResult> Success(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.Room)
                .ThenInclude(r => r.Branch)
                .FirstOrDefaultAsync(b => b.Id == id);
            if (booking == null) return NotFound();
            return View(booking);
        }

        private async Task<string?> SaveSecureFile(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;

            // Define secure path outside wwwroot
            string secureDir = Path.Combine(_environment.ContentRootPath, "App_Data", "SecureUploads", "IDCards");
            if (!Directory.Exists(secureDir))
            {
                Directory.CreateDirectory(secureDir);
            }

            // Generate unique filename
            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            string filePath = Path.Combine(secureDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return fileName; // Save only the GUID filename in DB
        }
        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null) return NotFound();

            if (booking.Status == "AwaitingPayment" || booking.Status == "PendingPayment")
            {
                booking.Status = "Cancelled";
                booking.IsDeleted = true;
                booking.DeletedAt = DateTime.UtcNow;

                // Revert slot if hourly
                if (booking.BookingMode == BookingMode.Hourly && booking.RoomSlotInventoryId.HasValue)
                {
                    var slot = await _context.RoomSlotInventories.FindAsync(booking.RoomSlotInventoryId.Value);
                    if (slot != null)
                    {
                        slot.Status = "Available";
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đơn hàng đã được hủy thành công.";
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
