using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;

var builder = WebApplication.CreateBuilder(args);

var localGroqKeyPath = Path.Combine(builder.Environment.ContentRootPath, "..", "key.md");
if (File.Exists(localGroqKeyPath))
{
    var content = File.ReadAllText(localGroqKeyPath).Trim();
    var provider = "groq";
    var model = "llama-3.3-70b-versatile";
    var key = content;

    // Detect if the user specified gemini at the end or if the key structure looks like Gemini
    if (content.EndsWith("gemini", StringComparison.OrdinalIgnoreCase))
    {
        provider = "gemini";
        model = "gemini-2.5-flash";
        key = content.Substring(0, content.Length - 6).Trim();
        Environment.SetEnvironmentVariable("AI_ENDPOINT_OVERRIDE", null);
    }
    else if (content.Contains('='))
    {
        var parts = content.Split('=', 2);
        provider = parts[0].Trim().ToLowerInvariant();
        
        var keyAndEndpoint = parts[1].Trim().Trim('"').Split('|', 2);
        key = keyAndEndpoint[0].Trim();
        
        if (keyAndEndpoint.Length > 1)
        {
            Environment.SetEnvironmentVariable("AI_ENDPOINT_OVERRIDE", keyAndEndpoint[1].Trim());
        }
        else
        {
            Environment.SetEnvironmentVariable("AI_ENDPOINT_OVERRIDE", null);
        }

        if (provider == "openrouter")
        {
            model = "openai/gpt-4o-mini";
        }
        else if (provider == "9router")
        {
            model = "cx/gpt-5.3-codex";
        }
    }
    else
    {
        Environment.SetEnvironmentVariable("AI_ENDPOINT_OVERRIDE", null);
    }

    builder.Configuration["AIModel:Provider"] = provider;
    builder.Configuration["AIModel:ApiKey"] = key;
    builder.Configuration["AIModel:Model"] = model;
}

// Fix PostgreSQL DateTime issue
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Conventions.Add(new WebHomestay.Helpers.AdminRoutePrefixConvention("chinhan/hethong"));
});
builder.Services.Configure<WebHomestay.Services.AIModelOptions>(builder.Configuration.GetSection("AIModel"));

// Configure PostgreSQL Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register Custom Services
builder.Services.AddScoped<WebHomestay.Services.IAvailabilityService, WebHomestay.Services.AvailabilityService>();
builder.Services.AddScoped<WebHomestay.Services.ISlotGenerationService, WebHomestay.Services.SlotGenerationService>();
builder.Services.AddScoped<WebHomestay.Services.IRoomBookingViewService, WebHomestay.Services.RoomBookingViewService>();
builder.Services.AddScoped<WebHomestay.Services.IBookingCreationService, WebHomestay.Services.BookingCreationService>();
builder.Services.AddScoped<WebHomestay.Services.IBookingCancellationService, WebHomestay.Services.BookingCancellationService>();
builder.Services.AddScoped<WebHomestay.Services.ISlotManagementService, WebHomestay.Services.SlotManagementService>();
builder.Services.AddScoped<WebHomestay.Services.IMailService, WebHomestay.Services.MailService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<WebHomestay.Services.ISettingService, WebHomestay.Services.SettingService>();
builder.Services.AddScoped<WebHomestay.Services.IStatisticsService, WebHomestay.Services.StatisticsService>();
builder.Services.AddScoped<WebHomestay.Services.IImageMaskingService, WebHomestay.Services.ImageMaskingService>();
builder.Services.AddScoped<WebHomestay.Services.IPaymentQrSettingsService, WebHomestay.Services.PaymentQrSettingsService>();
builder.Services.AddScoped<WebHomestay.Services.AI.IAdminAIStudioConfigService, WebHomestay.Services.AI.AdminAIStudioConfigService>();
builder.Services.AddScoped<WebHomestay.Services.AI.IAdminAIRuntimeSimulatorService, WebHomestay.Services.AI.AdminAIRuntimeSimulatorService>();
builder.Services.AddScoped<WebHomestay.Services.AI.IAdminAIReseedService, WebHomestay.Services.AI.AdminAIReseedService>();
builder.Services.AddHttpClient<WebHomestay.Services.IAIModelClient, WebHomestay.Services.AIModelClient>();
builder.Services.AddScoped<WebHomestay.Services.AI.IConversationManager, WebHomestay.Services.AI.ConversationManager>();
builder.Services.AddScoped<WebHomestay.Services.AI.IEntityExtractorService, WebHomestay.Services.AI.EntityExtractorService>();
builder.Services.AddScoped<WebHomestay.Services.IPublicBookingRoomExplanationService, WebHomestay.Services.PublicBookingRoomExplanationService>();
builder.Services.AddScoped<WebHomestay.Services.AI.LLMIntentClassifier>();
builder.Services.AddScoped<WebHomestay.Services.IAIBrainOrchestrator, WebHomestay.Services.AI.SemanticKernelOrchestrator>();
builder.Services.AddScoped<WebHomestay.Services.IBookingConductor, WebHomestay.Services.ContextAwareBookingConductor>();
builder.Services.AddScoped<WebHomestay.Services.PricingService>();
builder.Services.AddScoped<WebHomestay.Services.IPermissionResolveService, WebHomestay.Services.PermissionResolveService>();
builder.Services.AddScoped<WebHomestay.Services.IAdminChatService, WebHomestay.Services.AdminChatService>();
builder.Services.AddScoped<WebHomestay.Services.IAdminChatQuickSendService, WebHomestay.Services.AdminChatQuickSendService>();
builder.Services.AddHostedService<WebHomestay.Services.BookingCleanupService>();

// Register new Vector DB and Insight Services
builder.Services.AddScoped<WebHomestay.Services.AI.IEmbeddingService, WebHomestay.Services.AI.EmbeddingService>();
builder.Services.AddScoped<WebHomestay.Services.AI.IOperationalInsightService, WebHomestay.Services.AI.OperationalInsightService>();

// Add Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".WebHomestay.Session";
});

builder.Services.AddSignalR();

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();

    // Seed default settings
    if (!context.SystemSettings.Any(s => s.SettingKey == "BookingLeadTimeHours"))
    {
        context.SystemSettings.Add(new WebHomestay.Models.SystemSetting
        {
            SettingKey = "BookingLeadTimeHours",
            SettingValue = "2",
            Description = "Số giờ tối thiểu phải đặt trước (Theo giờ)",
            GroupName = "Booking"
        });
        context.SaveChanges();
    }

    // Seed ChatMonitor settings
    if (!context.SystemSettings.Any(s => s.SettingKey == "ChatMonitorAutoReplyEnabled"))
    {
        context.SystemSettings.Add(new WebHomestay.Models.SystemSetting
        {
            SettingKey = "ChatMonitorAutoReplyEnabled",
            SettingValue = "true",
            Description = "Bật/tắt tự động trả lời khi admin pause",
            GroupName = "ChatMonitor"
        });
        context.SaveChanges();
    }

    if (!context.SystemSettings.Any(s => s.SettingKey == "ChatMonitorAutoReplyMessage"))
    {
        context.SystemSettings.Add(new WebHomestay.Models.SystemSetting
        {
            SettingKey = "ChatMonitorAutoReplyMessage",
            SettingValue = "Hiện admin đang bận, vui lòng chờ một chút. Chúng tôi sẽ trả lời bạn sớm nhất.",
            Description = "Nội dung tự động trả lời khi admin pause",
            GroupName = "ChatMonitor"
        });
        context.SaveChanges();
    }

    if (!context.SystemSettings.Any(s => s.SettingKey == "ChatMonitorActiveWindowMinutes"))
    {
        context.SystemSettings.Add(new WebHomestay.Models.SystemSetting
        {
            SettingKey = "ChatMonitorActiveWindowMinutes",
            SettingValue = "30",
            Description = "Số phút để xác định session còn hoạt động",
            GroupName = "ChatMonitor"
        });
        context.SaveChanges();
    }

    var cancellationDefaults = new[]
    {
        new WebHomestay.Models.SystemSetting { SettingKey = "CancellationHandlingMode", SettingValue = "Manual", Description = "Chế độ xử lý hủy đơn", GroupName = "Cancellation" },
        new WebHomestay.Models.SystemSetting { SettingKey = "CancellationNoticeHours", SettingValue = "24", Description = "Số giờ tối thiểu trước check-in để hủy thuận lợi", GroupName = "Cancellation" },
        new WebHomestay.Models.SystemSetting { SettingKey = "CancellationRefundPercentBeforeNotice", SettingValue = "50", Description = "Phần trăm hoàn tiền trước ngưỡng hủy", GroupName = "Cancellation" },
        new WebHomestay.Models.SystemSetting { SettingKey = "CancellationRefundPercentAfterNotice", SettingValue = "0", Description = "Phần trăm hoàn tiền sau ngưỡng hủy", GroupName = "Cancellation" },
        new WebHomestay.Models.SystemSetting { SettingKey = "CancellationPolicyMessage", SettingValue = "Yêu cầu hủy trước thời hạn quy định có thể được hoàn theo chính sách. Sau thời hạn quy định, yêu cầu vẫn được tiếp nhận nhưng có thể bị giảm hoặc không hoàn tiền.", Description = "Thông báo chính sách hủy đơn", GroupName = "Cancellation" },
        new WebHomestay.Models.SystemSetting { SettingKey = "CancellationApprovalEmailSubject", SettingValue = WebHomestay.Services.BookingCancellationService.DefaultApprovalSubject, Description = "Tiêu đề email chấp nhận hủy đơn", GroupName = "Cancellation" },
        new WebHomestay.Models.SystemSetting { SettingKey = "CancellationApprovalEmailBody", SettingValue = WebHomestay.Services.BookingCancellationService.DefaultApprovalBody, Description = "Nội dung email chấp nhận hủy đơn", GroupName = "Cancellation" },
        new WebHomestay.Models.SystemSetting { SettingKey = "CancellationRejectionEmailSubject", SettingValue = WebHomestay.Services.BookingCancellationService.DefaultRejectionSubject, Description = "Tiêu đề email từ chối hủy đơn", GroupName = "Cancellation" },
        new WebHomestay.Models.SystemSetting { SettingKey = "CancellationRejectionEmailBody", SettingValue = WebHomestay.Services.BookingCancellationService.DefaultRejectionBody, Description = "Nội dung email từ chối hủy đơn", GroupName = "Cancellation" }
    };

    foreach (var setting in cancellationDefaults)
    {
        if (!context.SystemSettings.Any(s => s.SettingKey == setting.SettingKey))
        {
            context.SystemSettings.Add(setting);
        }
    }

    var aiDefaults = new[]
    {
        new WebHomestay.Models.SystemSetting { SettingKey = "AIFinalSynthesizerStyle", SettingValue = "Giọng thân thiện, rõ ràng, tư vấn như lễ tân chuyên nghiệp. Trả lời ngắn gọn nhưng đủ ý. Nếu thiếu thông tin thì hỏi lại bằng các câu hỏi cụ thể.", Description = "Phong cách trả lời", GroupName = "AI" },
        new WebHomestay.Models.SystemSetting { SettingKey = "AIFinalBasePrompt", SettingValue = "Bạn là Final Response Synthesizer của AI Brain Center cho homestay self check-in/self check-out. Nhiệm vụ duy nhất: viết câu trả lời cuối cùng cho khách dựa trên dữ liệu các agent cung cấp.", Description = "System prompt chính", GroupName = "AI" },
        new WebHomestay.Models.SystemSetting { SettingKey = "AIFinalLanguageRule", SettingValue = "Luôn trả lời bằng tiếng Việt, thân thiện, tự nhiên như nhân viên tư vấn homestay.", Description = "Quy tắc ngôn ngữ", GroupName = "AI" },
        new WebHomestay.Models.SystemSetting { SettingKey = "AIPublicBookingTriggerWords", SettingValue = "đặt,chốt,lấy,book,giữ phòng", Description = "Từ khoá kích hoạt", GroupName = "AI" },
        new WebHomestay.Models.SystemSetting { SettingKey = "AIPublicBookingProactiveMode", SettingValue = "balanced", Description = "Chế độ chủ động", GroupName = "AI" },
        new WebHomestay.Models.SystemSetting { SettingKey = "AIPublicBookingAutoShowRooms", SettingValue = "true", Description = "Tự động gợi ý phòng", GroupName = "AI" },
        new WebHomestay.Models.SystemSetting { SettingKey = "AIPublicBookingMaxRoomShows", SettingValue = "2", Description = "Số lần tối đa show phòng", GroupName = "AI" },
        new WebHomestay.Models.SystemSetting { SettingKey = "AIPublicBookingRoomCooldown", SettingValue = "3", Description = "Số message cooldown sau khi show phòng", GroupName = "AI" },
        new WebHomestay.Models.SystemSetting { SettingKey = "AIPublicBookingPrompt", SettingValue = "Luôn hiểu câu khách tự nhiên trước, sau đó hỏi đúng field cha còn thiếu. Giải thích sức chứa chuẩn, tối đa, phụ thu và giá cuối tuần/ngày lễ theo dữ liệu thật.", Description = "Prompt public booking", GroupName = "AI" },
        new WebHomestay.Models.SystemSetting { SettingKey = "AIPublicBookingDependencyRule", SettingValue = "branch->room->slot", Description = "Quy tắc cha con public booking", GroupName = "AI" },
        new WebHomestay.Models.SystemSetting { SettingKey = "AIPublicBookingGuestOverflowRule", SettingValue = "capacity_warn_max_filter", Description = "Vượt chuẩn thì cảnh báo, vượt tối đa thì loại", GroupName = "AI" }
    };

    foreach (var setting in aiDefaults)
    {
        if (!context.SystemSettings.Any(s => s.SettingKey == setting.SettingKey))
        {
            context.SystemSettings.Add(setting);
        }
    }
    
    var bookingDefaults = new[]
    {
        new WebHomestay.Models.SystemSetting { SettingKey = "BookingTrashRetentionDays", SettingValue = "7", Description = "Số ngày giữ đơn hàng trong thùng rác trước khi tự động xóa vĩnh viễn", GroupName = "Booking" }
    };

    foreach (var setting in bookingDefaults)
    {
        if (!context.SystemSettings.Any(s => s.SettingKey == setting.SettingKey))
        {
            context.SystemSettings.Add(setting);
        }
    }

    context.SaveChanges();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<WebHomestay.Hubs.ChatHub>("/chatHub");

app.Run();
