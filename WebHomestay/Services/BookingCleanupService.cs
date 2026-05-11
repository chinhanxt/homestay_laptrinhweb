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

                        // 3. Auto-update past confirmed bookings to CheckedOut (Completed)
                        var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();
                        var checkoutMode = await settingService.GetStringAsync("CheckoutMode", "Auto");

                        if (checkoutMode == "Auto")
                        {
                            var now = DateTime.UtcNow;
                            var pastConfirmed = await context.Bookings
                                .Where(b => !b.IsDeleted && b.Status == "Confirmed" && b.EndTime < now)
                                .ToListAsync(stoppingToken);

                            if (pastConfirmed.Any())
                            {
                                foreach (var b in pastConfirmed)
                                {
                                    b.Status = "CheckedOut";
                                }
                                await context.SaveChangesAsync(stoppingToken);
                                _logger.LogInformation($"Auto-completed {pastConfirmed.Count} past confirmed bookings (Mode: Auto).");
                            }
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
