using Microsoft.EntityFrameworkCore;
using WebHomestay.Models;

namespace WebHomestay.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<Amenity> Amenities { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<AdminUser> AdminUsers { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<RoomSlotTemplate> RoomSlotTemplates { get; set; }
        public DbSet<RoomSlotTemplateAssignment> RoomSlotTemplateAssignments { get; set; }
        public DbSet<RoomSlotInventory> RoomSlotInventories { get; set; }
        public DbSet<RoomSlotOverride> RoomSlotOverrides { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure table names to lowercase to match PostgreSQL convention
            modelBuilder.Entity<User>().ToTable("users");
            modelBuilder.Entity<Room>().ToTable("rooms");
            modelBuilder.Entity<Amenity>().ToTable("amenities");
            modelBuilder.Entity<Booking>().ToTable("bookings");
            modelBuilder.Entity<Branch>().ToTable("branches");
            modelBuilder.Entity<AdminUser>().ToTable("admin_users");
            modelBuilder.Entity<ActivityLog>().ToTable("activity_logs");
            modelBuilder.Entity<RoomSlotTemplate>().ToTable("room_slot_templates");
            modelBuilder.Entity<RoomSlotTemplateAssignment>().ToTable("room_slot_template_assignments");
            modelBuilder.Entity<RoomSlotInventory>().ToTable("room_slot_inventories");
            modelBuilder.Entity<SystemSetting>().ToTable("system_settings");
            modelBuilder.Entity<RoomSlotOverride>().ToTable("room_slot_overrides");

            // AdminUser - Branch relationship
            modelBuilder.Entity<AdminUser>()
                .HasOne(a => a.Branch)
                .WithMany(b => b.StaffMembers)
                .HasForeignKey(a => a.BranchId)
                .OnDelete(DeleteBehavior.SetNull);

            // ActivityLog - AdminUser relationship
            modelBuilder.Entity<ActivityLog>()
                .HasOne(l => l.AdminUser)
                .WithMany(u => u.ActivityLogs)
                .HasForeignKey(l => l.AdminUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure column mappings for Branch
            modelBuilder.Entity<Branch>(entity => {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Address).HasColumnName("address");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.Hotline).HasColumnName("hotline");
                entity.Property(e => e.MapUrl).HasColumnName("map_url");
                entity.Property(e => e.BookingLeadTimeHours).HasColumnName("booking_lead_time_hours");
            });

            // Configure column mappings for Room
            modelBuilder.Entity<Room>(entity => {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.BranchId).HasColumnName("branch_id");
                entity.Property(e => e.PricePerHour).HasColumnName("price_per_hour");
                entity.Property(e => e.PricePerDay).HasColumnName("price_per_day");
                entity.Property(e => e.ExtraGuestFee).HasColumnName("extra_guest_fee");
                entity.Property(e => e.ImageUrl).HasColumnName("image_url");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.Capacity).HasColumnName("capacity");
                entity.Property(e => e.MaxGuests).HasColumnName("max_guests");
                entity.Property(e => e.Status).HasColumnName("status");
            });

            // Configure column mappings for User
            modelBuilder.Entity<User>(entity => {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.FullName).HasColumnName("full_name");
                entity.Property(e => e.Email).HasColumnName("email");
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
                entity.Property(e => e.PhoneNumber).HasColumnName("phone_number");
                entity.Property(e => e.Role).HasColumnName("role");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            // Configure column mappings for Booking
            modelBuilder.Entity<Booking>(entity => {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.RoomId).HasColumnName("room_id");
                entity.Property(e => e.CustomerName).HasColumnName("customer_name");
                entity.Property(e => e.CustomerPhone).HasColumnName("customer_phone");
                entity.Property(e => e.CustomerEmail).HasColumnName("customer_email");
                entity.Property(e => e.CustomerZalo).HasColumnName("customer_zalo");
                entity.Property(e => e.GuestCount).HasColumnName("guest_count");
                entity.Property(e => e.IdCardFrontPath).HasColumnName("id_card_front_path");
                entity.Property(e => e.IdCardBackPath).HasColumnName("id_card_back_path");
                entity.Property(e => e.CustomerNote).HasColumnName("customer_note");
                entity.Property(e => e.AdminNote).HasColumnName("admin_note");
                entity.Property(e => e.StartTime).HasColumnName("start_time");
                entity.Property(e => e.EndTime).HasColumnName("end_time");
                entity.Property(e => e.TotalPrice).HasColumnName("total_price");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.PaymentStatus).HasColumnName("payment_status");
                entity.Property(e => e.PaymentProofUrl).HasColumnName("payment_proof_url");
                entity.Property(e => e.SmartLockCode).HasColumnName("smart_lock_code");
                entity.Property(e => e.WifiPassword).HasColumnName("wifi_password");
                entity.Property(e => e.CheckInInstructions).HasColumnName("check_in_instructions");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
                entity.Property(e => e.BookingMode).HasColumnName("booking_mode");
                entity.Property(e => e.RoomSlotInventoryId).HasColumnName("room_slot_inventory_id");
                entity.Property(e => e.SlotLabel).HasColumnName("slot_label");

                entity.HasOne(b => b.RoomSlotInventory)
                    .WithMany()
                    .HasForeignKey(b => b.RoomSlotInventoryId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // RoomSlotTemplate Mapping
            modelBuilder.Entity<RoomSlotTemplate>(entity => {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Code).HasColumnName("code");
                entity.Property(e => e.DurationMinutes).HasColumnName("duration_minutes");
                entity.Property(e => e.CleanupMinutes).HasColumnName("cleanup_minutes");
                entity.Property(e => e.SeedStartTime).HasColumnName("seed_start_time");
                entity.Property(e => e.FixedStartTime).HasColumnName("fixed_start_time");
                entity.Property(e => e.FixedEndTime).HasColumnName("fixed_end_time");
                entity.Property(e => e.CrossesMidnight).HasColumnName("crosses_midnight");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
            });

            // RoomSlotTemplateAssignment Mapping
            modelBuilder.Entity<RoomSlotTemplateAssignment>(entity => {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.RoomId).HasColumnName("room_id");
                entity.Property(e => e.TemplateId).HasColumnName("template_id");
                entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
                entity.Property(e => e.EffectiveTo).HasColumnName("effective_to");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
            });

            // RoomSlotInventory Mapping
            modelBuilder.Entity<RoomSlotInventory>(entity => {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.RoomId).HasColumnName("room_id");
                entity.Property(e => e.TemplateId).HasColumnName("template_id");
                entity.Property(e => e.SlotDate).HasColumnName("slot_date");
                entity.Property(e => e.SlotLabel).HasColumnName("slot_label");
                entity.Property(e => e.StartTime).HasColumnName("start_time");
                entity.Property(e => e.EndTime).HasColumnName("end_time");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.BookingId).HasColumnName("booking_id");
            });

            // RoomSlotOverride Mapping
            modelBuilder.Entity<RoomSlotOverride>(entity => {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.RoomId).HasColumnName("room_id");
                entity.Property(e => e.TargetDate).HasColumnName("target_date");
                entity.Property(e => e.TemplateId).HasColumnName("template_id");
                entity.Property(e => e.InventoryId).HasColumnName("inventory_id");
                entity.Property(e => e.OverrideType).HasColumnName("override_type");
                entity.Property(e => e.Reason).HasColumnName("reason");
            });

            // Configure column mappings for Amenity
            modelBuilder.Entity<Amenity>(entity => {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.IconClass).HasColumnName("icon_class");
            });

            // Configure many-to-many relationship Room <-> Amenity
            modelBuilder.Entity<Room>()
                .HasMany(r => r.Amenities)
                .WithMany(a => a.Rooms)
                .UsingEntity<Dictionary<string, object>>(
                    "room_amenities",
                    j => j.HasOne<Amenity>().WithMany().HasForeignKey("amenity_id"),
                    j => j.HasOne<Room>().WithMany().HasForeignKey("room_id"),
                    j =>
                    {
                        j.ToTable("room_amenities");
                        j.Property<int>("room_id").HasColumnName("room_id");
                        j.Property<int>("amenity_id").HasColumnName("amenity_id");
                    });

            modelBuilder.Entity<SystemSetting>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.SettingKey).HasColumnName("setting_key");
                entity.Property(e => e.SettingValue).HasColumnName("setting_value");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.GroupName).HasColumnName("group_name");
                entity.Property(e => e.LastUpdated).HasColumnName("last_updated");
            });
        }
    }
}
