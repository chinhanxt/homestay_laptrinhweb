using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;

var builder = WebApplication.CreateBuilder(args);

var localGroqKeyPath = Path.Combine(builder.Environment.ContentRootPath, "..", "key.md");
if (File.Exists(localGroqKeyPath))
{
    var localGroqKey = File.ReadAllText(localGroqKeyPath).Trim();
    if (localGroqKey.Contains('=')) localGroqKey = localGroqKey.Split('=', 2)[1].Trim().Trim('"');
    builder.Configuration["AIModel:Provider"] = "groq";
    builder.Configuration["AIModel:ApiKey"] = localGroqKey;
    builder.Configuration["AIModel:Model"] = string.IsNullOrWhiteSpace(builder.Configuration["AIModel:Model"])
        ? "llama-3.3-70b-versatile"
        : builder.Configuration["AIModel:Model"];
}

// Fix PostgreSQL DateTime issue
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Add services to the container.
builder.Services.AddControllersWithViews();
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
builder.Services.AddScoped<WebHomestay.Services.ISlotManagementService, WebHomestay.Services.SlotManagementService>();
builder.Services.AddScoped<WebHomestay.Services.IMailService, WebHomestay.Services.MailService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<WebHomestay.Services.ISettingService, WebHomestay.Services.SettingService>();
builder.Services.AddScoped<WebHomestay.Services.IStatisticsService, WebHomestay.Services.StatisticsService>();
builder.Services.AddScoped<WebHomestay.Services.IImageMaskingService, WebHomestay.Services.ImageMaskingService>();
builder.Services.AddScoped<WebHomestay.Services.IPaymentQrSettingsService, WebHomestay.Services.PaymentQrSettingsService>();
builder.Services.AddHttpClient<WebHomestay.Services.IAIModelClient, WebHomestay.Services.AIModelClient>();
builder.Services.AddScoped<WebHomestay.Services.IAIBrainOrchestrator, WebHomestay.Services.AIBrainOrchestrator>();
builder.Services.AddScoped<WebHomestay.Services.IBookingConductor, WebHomestay.Services.ContextAwareBookingConductor>();
builder.Services.AddScoped<WebHomestay.Services.PricingService>();
builder.Services.AddScoped<WebHomestay.Services.IPermissionResolveService, WebHomestay.Services.PermissionResolveService>();
builder.Services.AddHostedService<WebHomestay.Services.BookingCleanupService>();

// Add Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".WebHomestay.Session";
});

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

app.Run();
