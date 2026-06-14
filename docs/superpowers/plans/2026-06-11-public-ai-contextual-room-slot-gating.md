# Public AI Contextual Room And Slot Gating Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nâng cấp chatbot public booking để hiểu câu tự nhiên, thu gọn theo quan hệ cha-con trước khi show block, giải thích đúng sức chứa và phụ thu theo DB, và đưa toàn bộ runtime policy tương ứng vào admin.

**Architecture:** Giữ nguyên public AI flow hiện tại nhưng thêm một lớp state hydration và gating rõ ràng trong `ContextAwareBookingConductor`. Dữ liệu sức chứa, phụ thu, cuối tuần, ngày lễ và availability luôn lấy từ DB; knowledge/admin config chỉ điều khiển cách hỏi, cách giải thích và cách trace.

**Tech Stack:** ASP.NET Core MVC, EF Core InMemory tests, PostgreSQL-backed domain models, vanilla JS in `wwwroot/js/site.js`

---

## File map

- Modify: `WebHomestay/Services/AIBookingFlowModels.cs`
  - Mở rộng state public booking cho requested slot range, flags giải thích pricing, missing parent fields, active room context.
- Modify: `WebHomestay/Services/ContextAwareBookingConductor.cs`
  - Thêm gating cha-con, lọc room theo sức chứa, hourly specific-slot handling, explanation payload và trace reason.
- Modify: `WebHomestay/Services/AI/AdminAIStudioConfigService.cs`
  - Bổ sung default runtime policies và flow metadata phản ánh đúng logic public AI mới.
- Modify: `WebHomestay/wwwroot/js/site.js`
  - Render room cards/summary với surcharge, weekend/holiday pricing, requested-slot badges; không giả định block chỉ đến từ bước đầu.
- Modify: `WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs`
  - Bao phủ gating, occupancy filtering, requested-slot flows.
- Create: `WebHomestay.Tests/Services/PublicBookingRoomExplanationTests.cs`
  - Bao phủ explanation builder và pricing tier logic.
- Create: `WebHomestay/Services/PublicBookingRoomExplanationService.cs`
  - Service dựng các line giải thích từ DB truth cho room cards và booking summary.
- Create: `WebHomestay/Services/IPublicBookingRoomExplanationService.cs`
  - Interface cho explanation builder.
- Modify: `WebHomestay/Program.cs`
  - Đăng ký service mới và seed AI settings / knowledge guidance mặc định nếu còn thiếu.

### Task 1: Extend public booking state and write failing gating tests

**Files:**
- Modify: `WebHomestay/Services/AIBookingFlowModels.cs`
- Modify: `WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs`

- [ ] **Step 1: Write the failing tests for parent-field gating and occupancy rules**

```csharp
[Fact]
public async Task DailyNaturalLanguage_MissingBranch_ReturnsAskInfoWithBranchSelector()
{
    using var context = CreateContext();
    var cache = CreateCache();
    var conductor = CreateConductor(context, cache, extractor =>
    {
        extractor.Setup(e => e.ExtractGuestCount("đi 3 người 14-16/6")).Returns(3);
        extractor.Setup(e => e.ExtractDateTime("đi 3 người 14-16/6"))
            .Returns((new DateTime(2026, 6, 14), null));
    });

    var request = MakeRequest("đi 3 người 14-16/6");
    var result = await conductor.DecideAsync("gate-1", request.Message, request, CancellationToken.None);

    Assert.Equal(ConductorAction.AskInfo, result.Action);
    Assert.Contains(result.UiBlocks, block => JsonSerializer.Serialize(block).Contains("branchSelector"));
}

[Fact]
public async Task ShowRooms_FiltersOutRoomsBeyondMaxGuests_ButKeepsExtraGuestRooms()
{
    using var context = CreateContext();
    var cache = CreateCache();
    SeedRoom(context, id: 1, branchId: 1, capacity: 2, maxGuests: 3, extraGuestFee: 120000m);
    SeedRoom(context, id: 2, branchId: 1, capacity: 2, maxGuests: 2, extraGuestFee: 0m);
    var conductor = CreateConductor(context, cache);

    var request = MakeRequest("đặt phòng", branchId: 1, startTime: new DateTime(2026, 6, 14), guestCount: 3);
    var result = await conductor.DecideAsync("gate-2", request.Message, request, CancellationToken.None);

    var json = JsonSerializer.Serialize(result.UiBlocks);
    Assert.Equal(ConductorAction.ShowRooms, result.Action);
    Assert.Contains("\"roomId\":1", json);
    Assert.DoesNotContain("\"roomId\":2", json);
}
```

- [ ] **Step 2: Run the conductor tests to verify the new coverage fails**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~ContextAwareBookingConductorTests`

Expected: FAIL with missing overloads/helpers such as `CreateConductor(... extractor => ...)`, missing state fields, or room filtering assertions not satisfied.

- [ ] **Step 3: Extend the state models with the minimum fields required by the spec**

```csharp
public class AIBookingSessionState
{
    public string? RequestedTimeStart { get; set; }
    public string? RequestedTimeEnd { get; set; }
    public string? RequestedTimeLabel { get; set; }
    public decimal? BranchConfidence { get; set; }
    public bool NeedsWeekendPricingExplanation { get; set; }
    public bool NeedsHolidayPricingExplanation { get; set; }
    public bool HasExtraGuestSurcharge { get; set; }
    public List<string> MissingRequiredFields { get; set; } = new();
    public string? LastRecommendationReason { get; set; }
    public int? ActiveRoomContextId { get; set; }
}

public class BookingConfirmedState
{
    public TimeOnly? RequestedTimeStart { get; set; }
    public TimeOnly? RequestedTimeEnd { get; set; }
    public string? RequestedTimeLabel { get; set; }
    public List<string> MissingRequiredFields { get; set; } = new();
    public bool HasExtraGuestSurcharge { get; set; }
    public bool NeedsWeekendPricingExplanation { get; set; }
    public bool NeedsHolidayPricingExplanation { get; set; }
    public string? LastRecommendationReason { get; set; }
}

public class BookingProgressState
{
    public int? ActiveRoomContextId { get; set; }
}
```

- [ ] **Step 4: Add the test helpers needed by the new specs**

```csharp
private static ContextAwareBookingConductor CreateConductor(
    ApplicationDbContext context,
    IMemoryCache cache,
    Action<Moq.Mock<WebHomestay.Services.AI.IEntityExtractorService>>? configureExtractor = null)
{
    var scopeFactory = new FakeServiceScopeFactory(context);
    var bookingCreation = new FakeBookingCreationService(context);
    var mockAdminChat = new Moq.Mock<IAdminChatService>();
    var mockExtractor = new Moq.Mock<WebHomestay.Services.AI.IEntityExtractorService>();
    mockExtractor.Setup(e => e.ExtractDateTime(Moq.It.IsAny<string>())).Returns((null, null));
    mockExtractor.Setup(e => e.ExtractGuestCount(Moq.It.IsAny<string>())).Returns((int?)null);
    configureExtractor?.Invoke(mockExtractor);

    var aiClient = new FakeAIModelClient();
    var intentClassifier = new WebHomestay.Services.AI.LLMIntentClassifier(aiClient);

    return new ContextAwareBookingConductor(
        context,
        cache,
        scopeFactory,
        bookingCreation,
        mockAdminChat.Object,
        intentClassifier,
        mockExtractor.Object);
}

private static void SeedRoom(ApplicationDbContext context, int id, int branchId, int capacity, int maxGuests, decimal extraGuestFee)
{
    context.Rooms.Add(new Room
    {
        Id = id,
        BranchId = branchId,
        Name = $"Room {id}",
        PricePerHour = 200000m,
        PricePerDay = 1200000m,
        Capacity = capacity,
        MaxGuests = maxGuests,
        ExtraGuestFee = extraGuestFee,
        Status = "Available"
    });
    context.SaveChanges();
}
```

- [ ] **Step 5: Re-run the conductor test suite**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~ContextAwareBookingConductorTests`

Expected: Some old tests pass, new tests still fail because the conductor has not been updated yet.

- [ ] **Step 6: Commit the state and test scaffolding**

```bash
git add WebHomestay/Services/AIBookingFlowModels.cs WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs
git commit -m "test: add public booking gating coverage"
```

### Task 2: Implement conductor gating for natural language, parent dependencies, and active room context

**Files:**
- Modify: `WebHomestay/Services/ContextAwareBookingConductor.cs`
- Modify: `WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs`

- [ ] **Step 1: Add failing tests for requested-slot behavior and room context follow-up**

```csharp
[Fact]
public async Task HourlySpecificSlotRequest_ShowsOnlyRoomsAvailableInThatRange()
{
    using var context = CreateContext();
    var cache = CreateCache();
    SeedRoom(context, id: 7, branchId: 1, capacity: 2, maxGuests: 3, extraGuestFee: 90000m);
    SeedRoom(context, id: 8, branchId: 1, capacity: 2, maxGuests: 3, extraGuestFee: 90000m);
    SeedHourlySlot(context, roomId: 7, slotDate: new DateOnly(2026, 6, 14), start: "08:00", end: "10:00");
    SeedHourlySlot(context, roomId: 8, slotDate: new DateOnly(2026, 6, 14), start: "10:00", end: "12:00");
    var conductor = CreateConductor(context, cache, extractor =>
    {
        extractor.Setup(e => e.ExtractGuestCount(Moq.It.IsAny<string>())).Returns(2);
    });

    var request = MakeRequest("14/6 còn 8-10h không", branchId: 1, startTime: new DateTime(2026, 6, 14), guestCount: 2);
    var result = await conductor.DecideAsync("slot-1", request.Message, request, CancellationToken.None);

    var json = JsonSerializer.Serialize(result.UiBlocks);
    Assert.Contains("\"roomId\":7", json);
    Assert.DoesNotContain("\"roomId\":8", json);
}

[Fact]
public async Task FollowUpQuestion_UsesActiveRoomContextForOccupancyExplanation()
{
    using var context = CreateContext();
    var cache = CreateCache();
    SeedRoom(context, id: 11, branchId: 1, capacity: 2, maxGuests: 3, extraGuestFee: 100000m);
    var conductor = CreateConductor(context, cache);
    cache.Set("ai-booking-conductor:ctx-1", new BookingSessionContainer
    {
        Confirmed = new BookingConfirmedState { BranchId = 1, GuestCount = 2, BookingMode = "daily" },
        Progress = new BookingProgressState { SelectedRoomId = 11, ActiveRoomContextId = 11 }
    }, TimeSpan.FromMinutes(30));

    var request = MakeRequest("phòng này ở 3 người được không", branchId: 1, startTime: new DateTime(2026, 6, 14), guestCount: 3);
    var result = await conductor.DecideAsync("ctx-1", request.Message, request, CancellationToken.None);

    Assert.Equal(ConductorAction.Reply, result.Action);
    Assert.Equal(11, result.State.Progress?.ActiveRoomContextId);
}
```

- [ ] **Step 2: Run the targeted tests to verify current behavior fails**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter "FullyQualifiedName~HourlySpecificSlotRequest|FullyQualifiedName~FollowUpQuestion_UsesActiveRoomContextForOccupancyExplanation"`

Expected: FAIL because the conductor does not yet parse/keep requested slot range or active room context.

- [ ] **Step 3: Implement parent-field gating helpers inside the conductor**

```csharp
private static List<string> GetMissingRequiredFields(BookingConfirmedState state, MessageIntent intent)
{
    var missing = new List<string>();
    if (!state.BranchId.HasValue) missing.Add("branchId");

    if (string.Equals(state.BookingMode, "daily", StringComparison.OrdinalIgnoreCase))
    {
        if (!state.CheckInDate.HasValue) missing.Add("checkInDate");
        if (!state.CheckOutDate.HasValue) missing.Add("checkOutDate");
    }
    else
    {
        if (!state.HourlyDate.HasValue) missing.Add("hourlyDate");
    }

    if (state.GuestCount <= 0) missing.Add("guestCount");
    return missing;
}

private static bool IsSpecificHourlyRequest(BookingConfirmedState state, string message)
{
    return state.RequestedTimeStart.HasValue
        && state.RequestedTimeEnd.HasValue
        && (message.Contains('-') || message.Contains('h'));
}

private static void ApplyActiveRoomContext(BookingSessionContainer container, int? roomId)
{
    container.Progress ??= new BookingProgressState();
    container.Progress.ActiveRoomContextId = roomId ?? container.Progress.ActiveRoomContextId;
}
```

- [ ] **Step 4: Update `DecideAsync` to hydrate state first, then gate by dependencies**

```csharp
container.Confirmed = MergeFromRequest(container.Confirmed, request);
HydrateRequestedTimeRange(container.Confirmed, message);
container.Confirmed.MissingRequiredFields = GetMissingRequiredFields(container.Confirmed, intent);

if (container.Confirmed.MissingRequiredFields.Count > 0)
{
    var uiBlocks = await BuildMissingFieldBlocksAsync(container.Confirmed, cancellationToken);
    CacheState(sessionId, container);
    return new ConductorResult
    {
        Action = ConductorAction.AskInfo,
        State = container,
        UiBlocks = uiBlocks,
        Reason = $"missing={string.Join(',', container.Confirmed.MissingRequiredFields)}"
    };
}

if (IsSpecificHourlyRequest(container.Confirmed, message))
{
    var uiBlocks = await BuildRequestedRangeRoomCardsAsync(container.Confirmed, cancellationToken);
    CacheState(sessionId, container);
    return new ConductorResult
    {
        Action = ConductorAction.ShowRooms,
        State = container,
        UiBlocks = uiBlocks,
        Reason = "hourly-specific-range"
    };
}
```

- [ ] **Step 5: Update select-room and commit-room actions to keep room context**

```csharp
case "select-room":
    container.Progress.SelectedRoomId = actionRequest.RoomId;
    container.Progress.ActiveRoomContextId = actionRequest.RoomId;
    CacheState(actionRequest.SessionId, container);
    // existing summary response continues here
    break;

case "commit-room":
    if (actionRequest.RoomId.HasValue)
    {
        container.Progress.SelectedRoomId = actionRequest.RoomId;
        container.Progress.ActiveRoomContextId = actionRequest.RoomId;
    }
    CacheState(actionRequest.SessionId, container);
    // existing CTA / slot logic continues here
    break;
```

- [ ] **Step 6: Run the full conductor test suite**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~ContextAwareBookingConductorTests`

Expected: PASS for gating, requested slot filtering, and room-context follow-up tests.

- [ ] **Step 7: Commit the conductor implementation**

```bash
git add WebHomestay/Services/ContextAwareBookingConductor.cs WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs
git commit -m "feat: add contextual gating for public booking conductor"
```

### Task 3: Add room explanation service for occupancy, surcharge, weekend, and holiday truth

**Files:**
- Create: `WebHomestay/Services/IPublicBookingRoomExplanationService.cs`
- Create: `WebHomestay/Services/PublicBookingRoomExplanationService.cs`
- Create: `WebHomestay.Tests/Services/PublicBookingRoomExplanationTests.cs`
- Modify: `WebHomestay/Program.cs`
- Modify: `WebHomestay/Services/ContextAwareBookingConductor.cs`

- [ ] **Step 1: Write failing tests for pricing tier and occupancy explanation**

```csharp
[Fact]
public async Task BuildRoomExplanation_WhenGuestCountExceedsCapacity_IncludesSurchargeLine()
{
    using var context = TestDbFactory.Create();
    context.Rooms.Add(new Room
    {
        Id = 21,
        BranchId = 1,
        Name = "Family 21",
        Capacity = 2,
        MaxGuests = 3,
        ExtraGuestFee = 150000m,
        PricePerDay = 1400000m,
        PriceWeekendPerDay = 1600000m,
        PriceHolidayPerDay = 1800000m,
        Status = "Available"
    });
    await context.SaveChangesAsync();
    var service = new PublicBookingRoomExplanationService(context);

    var result = await service.BuildAsync(21, new BookingConfirmedState
    {
        GuestCount = 3,
        BookingMode = "daily",
        CheckInDate = new DateOnly(2026, 6, 13),
        CheckOutDate = new DateOnly(2026, 6, 15)
    }, CancellationToken.None);

    Assert.Contains(result.Lines, line => line.Contains("phụ thu"));
    Assert.Equal("weekend", result.PricingTierLabel);
}
```

- [ ] **Step 2: Run the explanation tests to verify the service does not exist yet**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~PublicBookingRoomExplanationTests`

Expected: FAIL with missing type `PublicBookingRoomExplanationService`.

- [ ] **Step 3: Create the service contract and result model**

```csharp
public interface IPublicBookingRoomExplanationService
{
    Task<PublicBookingRoomExplanation> BuildAsync(int roomId, BookingConfirmedState state, CancellationToken cancellationToken);
}

public class PublicBookingRoomExplanation
{
    public bool FitsStandardOccupancy { get; set; }
    public bool AllowsRequestedGuests { get; set; }
    public int ExtraGuestCount { get; set; }
    public decimal ExtraGuestFeeApplied { get; set; }
    public string PricingTierLabel { get; set; } = "weekday";
    public List<string> Lines { get; set; } = new();
    public string RecommendationReason { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Implement DB-truth explanation logic**

```csharp
public async Task<PublicBookingRoomExplanation> BuildAsync(int roomId, BookingConfirmedState state, CancellationToken cancellationToken)
{
    var room = await _context.Rooms.FirstAsync(r => r.Id == roomId, cancellationToken);
    var guestCount = Math.Max(state.GuestCount, 1);
    var extraGuestCount = Math.Max(0, guestCount - room.Capacity);
    var allowsRequestedGuests = guestCount <= room.MaxGuests;
    var pricingTier = await ResolvePricingTierAsync(state, cancellationToken);

    var lines = new List<string>
    {
        $"Tiêu chuẩn {room.Capacity} khách, tối đa {room.MaxGuests} khách."
    };

    if (extraGuestCount > 0 && allowsRequestedGuests)
    {
        lines.Add($"Vượt {extraGuestCount} khách so với chuẩn, có phụ thu {room.ExtraGuestFee:N0}đ/khách.");
    }

    lines.Add(pricingTier switch
    {
        "holiday" => "Thời gian này áp dụng giá ngày lễ.",
        "weekend" => "Thời gian này áp dụng giá cuối tuần.",
        _ => "Thời gian này áp dụng giá ngày thường."
    });

    return new PublicBookingRoomExplanation
    {
        FitsStandardOccupancy = guestCount <= room.Capacity,
        AllowsRequestedGuests = allowsRequestedGuests,
        ExtraGuestCount = extraGuestCount,
        ExtraGuestFeeApplied = extraGuestCount > 0 ? room.ExtraGuestFee : 0m,
        PricingTierLabel = pricingTier,
        Lines = lines,
        RecommendationReason = allowsRequestedGuests
            ? "Phù hợp số khách và dữ liệu giá hiện tại."
            : "Vượt số khách tối đa của phòng."
    };
}
```

- [ ] **Step 5: Register the service and inject it into the conductor**

```csharp
builder.Services.AddScoped<IPublicBookingRoomExplanationService, PublicBookingRoomExplanationService>();
```

```csharp
private readonly IPublicBookingRoomExplanationService _roomExplanationService;

public ContextAwareBookingConductor(
    ApplicationDbContext context,
    IMemoryCache cache,
    IServiceScopeFactory scopeFactory,
    IBookingCreationService bookingCreationService,
    IAdminChatService adminChatService,
    AI.LLMIntentClassifier intentClassifier,
    AI.IEntityExtractorService entityExtractor,
    IPublicBookingRoomExplanationService roomExplanationService)
{
    _roomExplanationService = roomExplanationService;
}
```

- [ ] **Step 6: Use the explanation service when building room cards and booking summary**

```csharp
var explanation = await _roomExplanationService.BuildAsync(r.Id, state, cancellationToken);
if (!explanation.AllowsRequestedGuests) continue;

rooms.Add(new
{
    roomId = r.Id,
    name = r.Name,
    description = r.Description,
    pricePerHour = r.PricePerHour,
    pricePerDay = r.PricePerDay,
    capacity = r.Capacity,
    maxGuests = r.MaxGuests,
    imageUrl = r.ImageUrl,
    amenities = r.Amenities,
    fitsStandardOccupancy = explanation.FitsStandardOccupancy,
    allowsRequestedGuests = explanation.AllowsRequestedGuests,
    extraGuestCount = explanation.ExtraGuestCount,
    extraGuestFeeApplied = explanation.ExtraGuestFeeApplied,
    pricingTierLabel = explanation.PricingTierLabel,
    pricingExplanation = explanation.Lines,
    recommendationReason = explanation.RecommendationReason
});
```

- [ ] **Step 7: Run the explanation tests and conductor tests**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter "FullyQualifiedName~PublicBookingRoomExplanationTests|FullyQualifiedName~ContextAwareBookingConductorTests"`

Expected: PASS.

- [ ] **Step 8: Commit the explanation service**

```bash
git add WebHomestay/Services/IPublicBookingRoomExplanationService.cs WebHomestay/Services/PublicBookingRoomExplanationService.cs WebHomestay/Services/ContextAwareBookingConductor.cs WebHomestay/Program.cs WebHomestay.Tests/Services/PublicBookingRoomExplanationTests.cs
git commit -m "feat: explain public booking room fit from db truth"
```

### Task 4: Surface the public AI runtime policy and knowledge guidance in admin

**Files:**
- Modify: `WebHomestay/Services/AI/AdminAIStudioConfigService.cs`
- Modify: `WebHomestay/Program.cs`
- Modify: `WebHomestay/Views/AdminAI/Index.cshtml`
- Modify: `WebHomestay/wwwroot/js/admin-ai-brain-center.js`

- [ ] **Step 1: Add a failing config round-trip test or smoke assertion for new runtime policy fields**

```csharp
[Fact]
public async Task StudioConfig_DefaultRuntimePolicies_ExposePublicBookingDependencyRules()
{
    using var context = TestDbFactory.Create();
    var service = new AdminAIStudioConfigService(context);

    var config = await service.GetAsync();

    Assert.Contains("branch -> room -> slot", config.RuntimePolicies.DisplayRule);
    Assert.True(config.RuntimePolicies.AutoShowRooms);
    Assert.True(config.RuntimePolicies.AutoShowSlots);
}
```

- [ ] **Step 2: Run the targeted admin AI tests or build to confirm missing policy content**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIStudioConfig`

Expected: FAIL if no matching tests exist yet; otherwise add the new test file and watch it fail on default assertions.

- [ ] **Step 3: Extend the default admin studio config to mirror the public chatbot policy**

```csharp
RuntimePolicies = new AdminAIRuntimePolicies
{
    RememberKnownCustomerInputs = true,
    AutoShowRooms = true,
    AutoShowSlots = true,
    MaxRoomShows = 6,
    RoomCooldownTurns = 3,
    MaxSlotShows = 8,
    SlotCooldownTurns = 2,
    DisplayRule = "Thu gọn theo quan hệ cha-con: branch -> room -> slot; chỉ show block khi đủ parent fields.",
    ClosingRule = "Vượt Capacity thì cảnh báo phụ thu; vượt MaxGuests thì loại khỏi gợi ý; luôn dùng DB làm nguồn thật."
}
```

- [ ] **Step 4: Seed/update public booking knowledge guidance in `Program.cs`**

```csharp
new SystemSetting { SettingKey = "AIPublicBookingPrompt", SettingValue = "Luôn giải thích sức chứa chuẩn, tối đa, phụ thu, cuối tuần và ngày lễ theo dữ liệu thật.", Description = "Prompt public booking", GroupName = "AI" },
new SystemSetting { SettingKey = "AIPublicBookingDependencyRule", SettingValue = "branch->room->slot", Description = "Quy tắc cha con public booking", GroupName = "AI" },
new SystemSetting { SettingKey = "AIPublicBookingGuestOverflowRule", SettingValue = "capacity_warn_max_filter", Description = "Vượt chuẩn thì cảnh báo, vượt tối đa thì loại", GroupName = "AI" },
```

- [ ] **Step 5: Expose the new runtime-policy copy in admin UI**

```javascript
function renderRuntimePolicies(policies) {
  setText('[data-ai-display-rule]', policies.displayRule || '');
  setText('[data-ai-closing-rule]', policies.closingRule || '');
  setToggle('[name="autoShowRooms"]', !!policies.autoShowRooms);
  setToggle('[name="autoShowSlots"]', !!policies.autoShowSlots);
}
```

```html
<div class="admin-ai-policy-card">
    <h4>Public booking gating</h4>
    <p data-ai-display-rule></p>
    <p data-ai-closing-rule></p>
</div>
```

- [ ] **Step 6: Run build and the config tests**

Run: `dotnet build WebHomestay/WebHomestay.csproj`

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AdminAIStudioConfig`

Expected: PASS and admin AI page compiles with the new policy fields.

- [ ] **Step 7: Commit the admin config work**

```bash
git add WebHomestay/Services/AI/AdminAIStudioConfigService.cs WebHomestay/Program.cs WebHomestay/Views/AdminAI/Index.cshtml WebHomestay/wwwroot/js/admin-ai-brain-center.js
git commit -m "feat: surface public booking gating policy in admin ai"
```

### Task 5: Update public chat rendering for surcharge, pricing tier, and range-aware room cards

**Files:**
- Modify: `WebHomestay/wwwroot/js/site.js`

- [ ] **Step 1: Add a small manual smoke checklist comment block near `renderRoomCards`**

```javascript
// Smoke cases:
// 1. Daily query with 3 guests and room capacity 2/max 3 -> show surcharge line.
// 2. Requested hourly range 8-10h -> show availability badge on matching rooms only.
// 3. Weekend/holiday result -> show pricing tier label in card and summary.
```

- [ ] **Step 2: Update `renderRoomCards` to display the new payload fields**

```javascript
const pricingLines = Array.isArray(room.pricingExplanation)
    ? room.pricingExplanation.map(line => `<li>${escapeHtml(line)}</li>`).join('')
    : '';

const occupancyBadge = room.fitsStandardOccupancy
    ? '<span class="ai-room-badge">Đúng chuẩn số khách</span>'
    : '<span class="ai-room-badge ai-room-badge-warning">Vượt chuẩn, có phụ thu</span>';

const requestedSlotBadge = room.requestedSlotAvailable
    ? `<span class="ai-room-badge ai-room-badge-success">Còn khung ${escapeHtml(bookingState.requestedTimeLabel || '')}</span>`
    : '';

card.innerHTML = `
  <div class="ai-room-card-body">
    ${occupancyBadge}
    ${requestedSlotBadge}
    <div class="ai-room-tier">${escapeHtml(room.pricingTierLabel || '')}</div>
    <ul class="ai-room-meta">${pricingLines}</ul>
  </div>
`;
```

- [ ] **Step 3: Update `renderBookingSummary` so the explanation stays visible after room selection**

```javascript
const extraLines = Array.isArray(data.lines) ? data.lines : [];
const totalHtml = typeof data.totalPrice === 'number'
    ? `<div class="ai-booking-total">${formatMoney(data.totalPrice)}</div>`
    : '';

wrapper.innerHTML = `
  <div class="ai-block-title">${escapeHtml(data.title || 'Tóm tắt lựa chọn')}</div>
  <ul class="ai-booking-summary-lines">
    ${extraLines.map(line => `<li>${escapeHtml(line)}</li>`).join('')}
  </ul>
  ${totalHtml}
`;
```

- [ ] **Step 4: Make `updateBookingState` keep the requested time metadata**

```javascript
bookingState = {
    ...bookingState,
    ...state,
    ...confirmed,
    requestedTimeLabel: confirmed.requestedTimeLabel || state.requestedTimeLabel || bookingState.requestedTimeLabel,
    requestedTimeStart: confirmed.requestedTimeStart || state.requestedTimeStart || bookingState.requestedTimeStart,
    requestedTimeEnd: confirmed.requestedTimeEnd || state.requestedTimeEnd || bookingState.requestedTimeEnd,
    selectedRoomId: progress.selectedRoomId || progress.SelectedRoomId || state.selectedRoomId
};
```

- [ ] **Step 5: Run a build and do a browser smoke check**

Run: `dotnet build WebHomestay/WebHomestay.csproj`

Manual check:
- Open the site and send `quận 7 3 người 14-16/6`
- Send `14/6 còn 8-10h không`
- Select a room and ask `phòng này ở 3 người được không`

Expected: room cards appear without resetting flow, surcharge/pricing labels show, and room-context follow-up stays coherent.

- [ ] **Step 6: Commit the frontend rendering changes**

```bash
git add WebHomestay/wwwroot/js/site.js
git commit -m "feat: render contextual public booking room explanations"
```

### Task 6: Final regression pass for public chat, admin policy, and seeded defaults

**Files:**
- Modify: `WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs`
- Modify: `WebHomestay.Tests/Services/PublicBookingRoomExplanationTests.cs`
- Modify: `docs/superpowers/specs/2026-06-11-public-ai-contextual-room-slot-gating-design.md` (only if implementation forces a spec correction)

- [ ] **Step 1: Add a regression test for slot fallback suggestions**

```csharp
[Fact]
public async Task HourlySpecificSlot_WhenUnavailable_ReturnsAlternativeSlotGuidance()
{
    using var context = CreateContext();
    var cache = CreateCache();
    SeedRoom(context, id: 31, branchId: 1, capacity: 2, maxGuests: 3, extraGuestFee: 80000m);
    SeedHourlySlot(context, roomId: 31, slotDate: new DateOnly(2026, 6, 14), start: "10:00", end: "12:00");
    var conductor = CreateConductor(context, cache);

    var request = MakeRequest("14/6 còn 8-10h không", branchId: 1, startTime: new DateTime(2026, 6, 14), guestCount: 2);
    var result = await conductor.DecideAsync("fallback-1", request.Message, request, CancellationToken.None);

    Assert.Contains("gần nhất", result.Reason + JsonSerializer.Serialize(result.UiBlocks));
}
```

- [ ] **Step 2: Run the full automated suite impacted by this feature**

Run: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj`

Expected: PASS.

- [ ] **Step 3: Run the app locally and verify both public and admin paths**

Run: `dotnet run --project WebHomestay/WebHomestay.csproj`

Manual admin checks:
- `/admin/ai` shows the public booking gating policy text
- public booking flows and admin-config wording stay aligned

Manual public checks:
- free-form daily request
- free-form hourly-by-date request
- free-form specific-slot request
- occupancy overflow but still allowed
- occupancy above max filtered out

- [ ] **Step 4: Update the spec only if implementation realities force a meaningful correction**

```markdown
## Implementation note

- Adjusted requested time parsing to store `TimeOnly?` in confirmed state and string labels in public response state.
```

- [ ] **Step 5: Commit the regression pass**

```bash
git add WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs WebHomestay.Tests/Services/PublicBookingRoomExplanationTests.cs docs/superpowers/specs/2026-06-11-public-ai-contextual-room-slot-gating-design.md
git commit -m "test: cover public booking contextual gating regressions"
```

## Self-review

- Spec coverage:
  - Natural-language first and non-reset flow: Task 2 and Task 5.
  - Parent-child gating: Task 1 and Task 2.
  - Capacity / max guest / surcharge rules: Task 1 and Task 3.
  - Weekend / holiday truth from DB: Task 3.
  - Admin-config parity with public behavior: Task 4.
  - Frontend roomCards/summary updates: Task 5.
  - Regression and fallback verification: Task 6.
- Placeholder scan:
  - No `TODO`, `TBD`, or “implement later” markers remain.
- Type consistency:
  - The plan consistently uses `RequestedTimeStart`, `RequestedTimeEnd`, `RequestedTimeLabel`, `ActiveRoomContextId`, and `IPublicBookingRoomExplanationService`.

