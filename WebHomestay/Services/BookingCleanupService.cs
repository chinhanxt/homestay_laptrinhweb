using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models;

namespace WebHomestay.Services
{
    public class BookingCleanupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<BookingCleanupService> _logger;

        public BookingCleanupService(IServiceProvider services, ILogger<BookingCleanupService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _services.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        // 1. Auto-delete expired Pending/Awaiting Payment bookings (> 5 mins)
                        var fiveMinsAgo = DateTime.UtcNow.AddMinutes(-5);
                        var expiredBookings = await context.Bookings
                            .Where(b => (b.Status == "AwaitingPayment" || b.Status == "PendingPayment") 
                                        && !b.IsDeleted 
                                        && b.CreatedAt < fiveMinsAgo)
                            .ToListAsync(stoppingToken);

                        if (expiredBookings.Any())
                        {
                            foreach (var b in expiredBookings)
                            {
                                b.Status = "Cancelled";
                                b.IsDeleted = true;
                                b.DeletedAt = DateTime.UtcNow;

                                // Revert hourly slot to Available if applicable
                                if (b.BookingMode == BookingMode.Hourly && b.RoomSlotInventoryId.HasValue)
                                {
                                    var slot = await context.RoomSlotInventories.FindAsync(b.RoomSlotInventoryId.Value);
                                    if (slot != null)
                                    {
                                        slot.Status = "Available";
                                    }
                                }
                            }
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Auto-cancelled {expiredBookings.Count} expired unpaid bookings and reverted slots.");
                        }

                        // 2. Permanently delete bookings in trash > 7 days
                        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                        var oldDeletedBookings = await context.Bookings
                            .Where(b => b.IsDeleted && b.DeletedAt < sevenDaysAgo)
                            .ToListAsync(stoppingToken);

                        if (oldDeletedBookings.Any())
                        {
                            context.Bookings.RemoveRange(oldDeletedBookings);
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Permanently deleted {oldDeletedBookings.Count} bookings from trash.");
                        }

                        // 3. Auto Check-In / Check-Out status transition (Local Time based)
                        var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();
                        var checkoutMode = await settingService.GetStringAsync("CheckoutMode", "Auto");
                        var nowTime = DateTime.Now;
                        bool changed = false;

                        // 3.1. Auto Check-In: Confirmed -> CheckedIn when StartTime reached (Always runs)
                        var bookingsToCheckIn = await context.Bookings
                            .Where(b => !b.IsDeleted && b.Status == "Confirmed" && b.StartTime <= nowTime)
                            .ToListAsync(stoppingToken);

                        if (bookingsToCheckIn.Any())
                        {
                            foreach (var b in bookingsToCheckIn)
                            {
                                b.Status = "CheckedIn";
                            }
                            changed = true;
                            _logger.LogInformation($"Auto-checked in {bookingsToCheckIn.Count} bookings starting at/before {nowTime}.");
                        }

                        // 3.2. Auto Check-Out: Confirmed/CheckedIn -> CheckedOut when EndTime passed (Only if Auto mode)
                        if (checkoutMode == "Auto")
                        {
                            var pastActiveBookings = await context.Bookings
                                .Where(b => !b.IsDeleted && 
                                           (b.Status == "Confirmed" || b.Status == "CheckedIn") && 
                                           b.EndTime < nowTime)
                                .ToListAsync(stoppingToken);

                            if (pastActiveBookings.Any())
                            {
                                foreach (var b in pastActiveBookings)
                                {
                                    b.Status = "CheckedOut";
                                }
                                changed = true;
                                _logger.LogInformation($"Auto-checked out {pastActiveBookings.Count} past active bookings (Mode: Auto).");
                            }
                        }

                        if (changed)
                        {
                            await context.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in BookingCleanupService");
                }

                // Run every 1 minute
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
