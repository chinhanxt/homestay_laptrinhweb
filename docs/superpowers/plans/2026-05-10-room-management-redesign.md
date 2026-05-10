# Room Management Redesign and Pricing Logic Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a professional 2-panel room management UI with weekend/holiday pricing and mandatory 5-image upload.

**Architecture:**
- **Data Layer:** Update `Room` model with pricing fields and image storage; add a global `Holiday` model.
- **Business Logic:** Centralized `PricingService` to determine the applicable price based on the date priority (Holiday > Weekend > Regular).
- **UI Layer:** Responsive 2-panel layout for room management using modern CSS, replacing URL inputs with file uploads.

**Tech Stack:** ASP.NET Core MVC, Entity Framework Core, SQL Server/PostgreSQL.

---

### Task 1: Database and Model Updates

**Files:**
- Create: `WebHomestay/Models/Holiday.cs`
- Modify: `WebHomestay/Models/Room.cs`
- Modify: `WebHomestay/Data/ApplicationDbContext.cs`

- [ ] **Step 1: Create Holiday model**
```csharp
namespace WebHomestay.Models
{
    public class Holiday
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string? Description { get; set; }
    }
}
```

- [ ] **Step 2: Update Room model**
Add `PriceWeekend`, `PriceHoliday`, and `AdditionalImages` (string/JSON) to `WebHomestay/Models/Room.cs`. Remove "Occupied" from status comments.
```csharp
// In WebHomestay/Models/Room.cs
public decimal PriceWeekend { get; set; }
public decimal PriceHoliday { get; set; }
public string? AdditionalImages { get; set; } // Store as JSON array of strings
```

- [ ] **Step 3: Update DbContext**
Add `DbSet<Holiday> Holidatys { get; set; }` to `WebHomestay/Data/ApplicationDbContext.cs`.

- [ ] **Step 4: Create and apply migration**
Run: `dotnet ef migrations add UpdateRoomPricingAndHolidays`
Run: `dotnet ef database update`

- [ ] **Step 5: Commit**
```bash
git add WebHomestay/Models/ WebHomestay/Data/
git commit -m "db: add holiday model and update room pricing fields"
```

---

### Task 2: Pricing Logic Service

**Files:**
- Create: `WebHomestay/Services/PricingService.cs`
- Modify: `WebHomestay/Program.cs`

- [ ] **Step 1: Implement PricingService**
```csharp
namespace WebHomestay.Services
{
    public class PricingService
    {
        private readonly ApplicationDbContext _context;
        public PricingService(ApplicationDbContext context) => _context = context;

        public async Task<decimal> GetRoomPriceForDate(int roomId, DateTime date)
        {
            var room = await _context.Rooms.FindAsync(roomId);
            if (room == null) return 0;

            var isHoliday = await _context.Holidays.AnyAsync(h => h.Date.Date == date.Date);
            if (isHoliday) return room.PriceHoliday;

            var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
            if (isWeekend) return room.PriceWeekend;

            return room.PricePerDay;
        }
    }
}
```

- [ ] **Step 2: Register service in Program.cs**
```csharp
builder.Services.AddScoped<PricingService>();
```

- [ ] **Step 3: Commit**
```bash
git add WebHomestay/Services/ WebHomestay/Program.cs
git commit -m "feat: add PricingService for priority-based price calculation"
```

---

### Task 3: Global Holiday Management UI

**Files:**
- Create: `WebHomestay/Controllers/AdminHolidaysController.cs`
- Create: `WebHomestay/Views/AdminHolidays/Index.cshtml`

- [ ] **Step 1: Create AdminHolidaysController**
Implement basic CRUD for the `Holiday` model with `AdminAuthorize` attribute.

- [ ] **Step 2: Create Index view with Date Matrix**
Create a view to list and add holiday dates using a simple table or calendar view.

- [ ] **Step 3: Commit**
```bash
git add WebHomestay/Controllers/AdminHolidaysController.cs WebHomestay/Views/AdminHolidays/
git commit -m "feat: add global holiday management UI"
```

---

### Task 4: Redesign Room Create/Edit UI

**Files:**
- Modify: `WebHomestay/Views/AdminRooms/Create.cshtml`
- Modify: `WebHomestay/Views/AdminRooms/Edit.cshtml`
- Modify: `WebHomestay/wwwroot/css/admin.css` (or equivalent)

- [ ] **Step 1: Update CSS for 2-panel layout**
```css
.room-form-container {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 30px;
}
.panel-card {
    background: white;
    padding: 20px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.05);
}
```

- [ ] **Step 2: Redesign Edit.cshtml**
Implement the 2-panel layout.
Left Panel: Basic Info, Status, Main Image (file), 5 Illustration Images (file slots).
Right Panel: Pricing (Regular, Weekend, Holiday, Hourly, Extra Guest), Capacity.

- [ ] **Step 3: Redesign Create.cshtml**
Sync with Edit.cshtml layout.

- [ ] **Step 4: Commit**
```bash
git add WebHomestay/Views/AdminRooms/ WebHomestay/wwwroot/css/
git commit -m "ui: redesign room management with 2-panel layout"
```

---

### Task 5: Controller Logic for File Uploads and Pricing

**Files:**
- Modify: `WebHomestay/Controllers/AdminRoomsController.cs`

- [ ] **Step 1: Update Edit and Create POST methods**
Change `Bind` to include new fields. Handle `IFormFile` for `MainImage` and `AdditionalImages`.
```csharp
[HttpPost]
public async Task<IActionResult> Edit(int id, Room room, IFormFile mainImage, List<IFormFile> illustrationImages)
{
    // Handle file saving to wwwroot/uploads/rooms/
    // Save paths to room.ImageUrl and room.AdditionalImages (JSON string)
}
```

- [ ] **Step 2: Implement File Saving Helper**
Add a private method to save uploaded files and return the path.

- [ ] **Step 3: Commit**
```bash
git add WebHomestay/Controllers/AdminRoomsController.cs
git commit -m "feat: handle file uploads and update pricing fields in controller"
```
