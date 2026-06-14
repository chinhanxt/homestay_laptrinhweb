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
                            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();

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

                        // 2. Permanently delete old trashed bookings (configurable days)
                        var retentionDays = await settingService.GetIntAsync("BookingTrashRetentionDays", 7);
                        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
                        var oldDeletedBookings = await context.Bookings
                            .Where(b => b.IsDeleted && b.DeletedAt < cutoffDate)
                            .ToListAsync(stoppingToken);

                        if (oldDeletedBookings.Any())
                        {
                            context.Bookings.RemoveRange(oldDeletedBookings);
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Permanently deleted {oldDeletedBookings.Count} bookings from trash.");
                        }

                        // 2b. Permanently delete old trashed AI knowledge units
                        var oldDeletedKnowledgeUnits = await context.AIKnowledgeUnits
                            .Where(u => u.IsDeleted && u.DeletedAt < cutoffDate)
                            .ToListAsync(stoppingToken);
                        if (oldDeletedKnowledgeUnits.Any())
                        {
                            context.AIKnowledgeUnits.RemoveRange(oldDeletedKnowledgeUnits);
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Permanently deleted {oldDeletedKnowledgeUnits.Count} AI knowledge units from trash.");
                        }

                        // 2c. Permanently delete old trashed AI graph edges + nodes
                        var oldDeletedEdges = await context.AIGraphEdges
                            .Where(e => e.IsDeleted && e.DeletedAt < cutoffDate)
                            .ToListAsync(stoppingToken);
                        if (oldDeletedEdges.Any())
                        {
                            context.AIGraphEdges.RemoveRange(oldDeletedEdges);
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Permanently deleted {oldDeletedEdges.Count} AI graph edges from trash.");
                        }

                        var oldDeletedNodes = await context.AIGraphNodes
                            .Where(n => n.IsDeleted && n.DeletedAt < cutoffDate)
                            .ToListAsync(stoppingToken);
                        if (oldDeletedNodes.Any())
                        {
                            // Remove related edges first
                            var nodeIds = oldDeletedNodes.Select(n => n.Id).ToList();
                            var relatedEdges = await context.AIGraphEdges
                                .Where(e => nodeIds.Contains(e.FromNodeId) || nodeIds.Contains(e.ToNodeId))
                                .ToListAsync(stoppingToken);
                            if (relatedEdges.Any())
                                context.AIGraphEdges.RemoveRange(relatedEdges);
                            context.AIGraphNodes.RemoveRange(oldDeletedNodes);
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Permanently deleted {oldDeletedNodes.Count} AI graph nodes from trash.");
                        }

                        // 2d. Permanently delete old trashed Branches
                        var oldDeletedBranches = await context.Branches
                            .Where(b => b.IsDeleted && b.DeletedAt < cutoffDate)
                            .ToListAsync(stoppingToken);
                        if (oldDeletedBranches.Any())
                        {
                            context.Branches.RemoveRange(oldDeletedBranches);
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Permanently deleted {oldDeletedBranches.Count} branches from trash.");
                        }

                        // 2e. Permanently delete old trashed Rooms
                        var oldDeletedRooms = await context.Rooms
                            .Where(r => r.IsDeleted && r.DeletedAt < cutoffDate)
                            .ToListAsync(stoppingToken);
                        if (oldDeletedRooms.Any())
                        {
                            context.Rooms.RemoveRange(oldDeletedRooms);
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Permanently deleted {oldDeletedRooms.Count} rooms from trash.");
                        }

                        // 2f. Permanently delete old trashed Staff (AdminUsers)
                        var oldDeletedStaff = await context.AdminUsers
                            .Where(u => u.IsDeleted && u.DeletedAt < cutoffDate)
                            .ToListAsync(stoppingToken);
                        if (oldDeletedStaff.Any())
                        {
                            context.AdminUsers.RemoveRange(oldDeletedStaff);
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Permanently deleted {oldDeletedStaff.Count} staff accounts from trash.");
                        }

                        // 2g. Permanently delete old trashed Chat Sessions
                        var oldDeletedChatSessions = await context.Set<WebHomestay.Models.AdminChatSession>()
                            .Where(s => s.IsDeleted && s.DeletedAt < cutoffDate)
                            .ToListAsync(stoppingToken);
                        if (oldDeletedChatSessions.Any())
                        {
                            context.RemoveRange(oldDeletedChatSessions);
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Permanently deleted {oldDeletedChatSessions.Count} chat sessions from trash.");
                        }

                        // 2h. Permanently delete old trashed Holidays
                        var oldDeletedHolidays = await context.Holidays
                            .Where(h => h.IsDeleted && h.DeletedAt < cutoffDate)
                            .ToListAsync(stoppingToken);
                        if (oldDeletedHolidays.Any())
                        {
                            context.Holidays.RemoveRange(oldDeletedHolidays);
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Permanently deleted {oldDeletedHolidays.Count} holidays from trash.");
                        }

                        // 2i. Permanently delete old trashed RoomSlotTemplates
                        var oldDeletedTemplates = await context.RoomSlotTemplates
                            .Where(t => t.IsDeleted && t.DeletedAt < cutoffDate)
                            .ToListAsync(stoppingToken);
                        if (oldDeletedTemplates.Any())
                        {
                            context.RoomSlotTemplates.RemoveRange(oldDeletedTemplates);
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Permanently deleted {oldDeletedTemplates.Count} room slot templates from trash.");
                        }

                        // 3. Auto Check-In / Check-Out status transition (Local Time based)
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
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
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
