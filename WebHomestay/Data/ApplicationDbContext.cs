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
        public DbSet<Holiday> Holidays { get; set; }
        public DbSet<AIKnowledgeCollection> AIKnowledgeCollections { get; set; }
        public DbSet<AIKnowledgeArticle> AIKnowledgeArticles { get; set; }
        public DbSet<AIBrainScope> AIBrainScopes { get; set; }
        public DbSet<AIKnowledgeUnit> AIKnowledgeUnits { get; set; }
        public DbSet<AIGraphNode> AIGraphNodes { get; set; }
        public DbSet<AIGraphEdge> AIGraphEdges { get; set; }
        public DbSet<AIAgentDefinition> AIAgentDefinitions { get; set; }
        public DbSet<AIConversationTrace> AIConversationTraces { get; set; }
        public DbSet<RolePermissionTemplate> RolePermissionTemplates { get; set; }
        public DbSet<AdminChatSession> AdminChatSessions => Set<AdminChatSession>();
        public DbSet<AdminChatMessage> AdminChatMessages => Set<AdminChatMessage>();
        public DbSet<BookingCancellationRequest> BookingCancellationRequests => Set<BookingCancellationRequest>();

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
            modelBuilder.Entity<Holiday>().ToTable("holidays");
            modelBuilder.Entity<AIKnowledgeCollection>().ToTable("ai_knowledge_collections");
            modelBuilder.Entity<AIKnowledgeArticle>().ToTable("ai_knowledge_articles");
            modelBuilder.Entity<AIBrainScope>().ToTable("ai_brain_scopes");
            modelBuilder.Entity<AIKnowledgeUnit>().ToTable("ai_knowledge_units");
            modelBuilder.Entity<AIGraphNode>().ToTable("ai_graph_nodes");
            modelBuilder.Entity<AIGraphEdge>().ToTable("ai_graph_edges");
            modelBuilder.Entity<AIAgentDefinition>().ToTable("ai_agent_definitions");
            modelBuilder.Entity<AIConversationTrace>().ToTable("ai_conversation_traces");

            // AdminUser - Branch relationship + column mappings
            modelBuilder.Entity<AdminUser>(entity => {
                entity.HasOne(a => a.Branch)
                    .WithMany(b => b.StaffMembers)
                    .HasForeignKey(a => a.BranchId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            });

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
                entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100);
                entity.Property(e => e.MapUrl).HasColumnName("map_url");
                entity.Property(e => e.BookingLeadTimeHours).HasColumnName("booking_lead_time_hours");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
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
                entity.Property(e => e.Embedding).HasColumnName("embedding");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
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
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
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


            modelBuilder.Entity<AIBrainScope>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
                entity.Property(e => e.Order).HasColumnName("order");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            modelBuilder.Entity<AIKnowledgeUnit>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ScopeId).HasColumnName("scope_id");
                entity.Property(e => e.Title).HasColumnName("title");
                entity.Property(e => e.Content).HasColumnName("content");
                entity.Property(e => e.Tags).HasColumnName("tags");
                entity.Property(e => e.Priority).HasColumnName("priority");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
                entity.Property(e => e.LastUpdated).HasColumnName("last_updated");
                entity.Property(e => e.Embedding).HasColumnName("embedding");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
                entity.HasOne(e => e.Scope).WithMany(e => e.KnowledgeUnits).HasForeignKey(e => e.ScopeId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AIGraphNode>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.NodeType).HasColumnName("node_type");
                entity.Property(e => e.Label).HasColumnName("label");
                entity.Property(e => e.Summary).HasColumnName("summary");
                entity.Property(e => e.MetadataJson).HasColumnName("metadata_json");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            });

            modelBuilder.Entity<AIGraphEdge>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.FromNodeId).HasColumnName("from_node_id");
                entity.Property(e => e.ToNodeId).HasColumnName("to_node_id");
                entity.Property(e => e.RelationshipType).HasColumnName("relationship_type");
                entity.Property(e => e.Weight).HasColumnName("weight");
                entity.Property(e => e.Evidence).HasColumnName("evidence");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
                entity.HasOne(e => e.FromNode).WithMany(e => e.OutgoingEdges).HasForeignKey(e => e.FromNodeId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.ToNode).WithMany(e => e.IncomingEdges).HasForeignKey(e => e.ToNodeId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AIAgentDefinition>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Role).HasColumnName("role");
                entity.Property(e => e.SystemPrompt).HasColumnName("system_prompt");
                entity.Property(e => e.Order).HasColumnName("order");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
                entity.Property(e => e.LastUpdated).HasColumnName("last_updated");
            });

            modelBuilder.Entity<AIConversationTrace>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.SessionId).HasColumnName("session_id");
                entity.Property(e => e.CustomerMessage).HasColumnName("customer_message");
                entity.Property(e => e.PersonaSummary).HasColumnName("persona_summary");
                entity.Property(e => e.LiveSystemSnapshot).HasColumnName("live_system_snapshot");
                entity.Property(e => e.RetrievedKnowledgeJson).HasColumnName("retrieved_knowledge_json");
                entity.Property(e => e.GraphReasoningJson).HasColumnName("graph_reasoning_json");
                entity.Property(e => e.GuardResult).HasColumnName("guard_result");
                entity.Property(e => e.FinalAnswer).HasColumnName("final_answer");
                entity.Property(e => e.ModelProvider).HasColumnName("model_provider");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            modelBuilder.Entity<AdminChatSession>(entity =>
            {
                entity.ToTable("admin_chat_sessions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.SessionId).HasColumnName("session_id");
                entity.HasIndex(e => e.SessionId).IsUnique();
                entity.Property(e => e.CustomerName).HasColumnName("customer_name");
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20);
                entity.Property(e => e.PausedBy).HasColumnName("paused_by");
                entity.Property(e => e.PausedAt).HasColumnName("paused_at");
                entity.Property(e => e.AutoReplyMessage).HasColumnName("auto_reply_message");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.LastActivityAt).HasColumnName("last_activity_at");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            });

            modelBuilder.Entity<AdminChatMessage>(entity =>
            {
                entity.ToTable("admin_chat_messages");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.SessionId).HasColumnName("session_id");
                entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(20);
                entity.Property(e => e.Content).HasColumnName("content");
                entity.Property(e => e.FormBlockJson).HasColumnName("form_block_json");
                entity.Property(e => e.FormBlockType).HasColumnName("form_block_type");
                entity.Property(e => e.CreatedBy).HasColumnName("created_by");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.IsRead).HasColumnName("is_read");
                entity.HasIndex(e => new { e.SessionId, e.CreatedAt });
            });

            modelBuilder.Entity<BookingCancellationRequest>(entity =>
            {
                entity.ToTable("booking_cancellation_requests");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ChatSessionId).HasColumnName("chat_session_id").HasMaxLength(100);
                entity.Property(e => e.BookingId).HasColumnName("booking_id");
                entity.Property(e => e.SubmittedBookingCode).HasColumnName("submitted_booking_code").HasMaxLength(50);
                entity.Property(e => e.CustomerName).HasColumnName("customer_name").HasMaxLength(200);
                entity.Property(e => e.CustomerPhone).HasColumnName("customer_phone").HasMaxLength(20);
                entity.Property(e => e.CustomerEmail).HasColumnName("customer_email").HasMaxLength(200);
                entity.Property(e => e.ConfirmationEmailProofPath).HasColumnName("confirmation_email_proof_path");
                entity.Property(e => e.RefundQrImagePath).HasColumnName("refund_qr_image_path");
                entity.Property(e => e.RefundBankName).HasColumnName("refund_bank_name").HasMaxLength(100);
                entity.Property(e => e.RefundBankAccountNumber).HasColumnName("refund_bank_account_number").HasMaxLength(50);
                entity.Property(e => e.RefundBankAccountHolder).HasColumnName("refund_bank_account_holder").HasMaxLength(200);
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20);
                entity.Property(e => e.SuggestedBookingIdsJson).HasColumnName("suggested_booking_ids_json");
                entity.Property(e => e.PolicyNoticeHoursSnapshot).HasColumnName("policy_notice_hours_snapshot");
                entity.Property(e => e.RefundPercentBeforeNoticeSnapshot).HasColumnName("refund_percent_before_notice_snapshot");
                entity.Property(e => e.RefundPercentAfterNoticeSnapshot).HasColumnName("refund_percent_after_notice_snapshot");
                entity.Property(e => e.PolicyMessageSnapshot).HasColumnName("policy_message_snapshot");
                entity.Property(e => e.AppliedRefundPercent).HasColumnName("applied_refund_percent");
                entity.Property(e => e.RefundStatus).HasColumnName("refund_status").HasMaxLength(20);
                entity.Property(e => e.RefundBillProofPath).HasColumnName("refund_bill_proof_path");
                entity.Property(e => e.StaffReason).HasColumnName("staff_reason");
                entity.Property(e => e.ProcessedBy).HasColumnName("processed_by").HasMaxLength(100);
                entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
                entity.Property(e => e.NotificationEmailSubject).HasColumnName("notification_email_subject").HasMaxLength(200);
                entity.Property(e => e.NotificationEmailBody).HasColumnName("notification_email_body");
                entity.Property(e => e.NotificationEmailSentAt).HasColumnName("notification_email_sent_at");
                entity.Property(e => e.NotificationEmailAttachmentName).HasColumnName("notification_email_attachment_name").HasMaxLength(255);
                entity.Property(e => e.NotificationEmailType).HasColumnName("notification_email_type").HasMaxLength(20);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
                entity.HasIndex(e => new { e.ChatSessionId, e.Status });
                entity.HasIndex(e => e.BookingId);
                entity.HasOne(e => e.Booking).WithMany().HasForeignKey(e => e.BookingId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Holiday>(entity =>
            {
                entity.ToTable("holidays");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Date).HasColumnName("date");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            });

            modelBuilder.Entity<RolePermissionTemplate>(entity =>
            {
                entity.ToTable("role_permission_templates");
                entity.Property(e => e.Role).HasColumnName("role");
                entity.Property(e => e.PermissionsJson).HasColumnName("permissions_json");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            });

            // Update Room mapping for new fields
            modelBuilder.Entity<Room>(entity => {
                entity.Property(e => e.PriceWeekendPerHour).HasColumnName("price_weekend_per_hour");
                entity.Property(e => e.PriceWeekendPerDay).HasColumnName("price_weekend_per_day");
                entity.Property(e => e.PriceHolidayPerHour).HasColumnName("price_holiday_per_hour");
                entity.Property(e => e.PriceHolidayPerDay).HasColumnName("price_holiday_per_day");
                entity.Property(e => e.AdditionalImages).HasColumnName("additional_images");
            });
        }
    }
}
