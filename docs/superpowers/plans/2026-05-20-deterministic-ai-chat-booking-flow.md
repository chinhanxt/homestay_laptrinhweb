# Deterministic AI Chat Booking Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the public AI chat use form context and real slot availability to guide customers through room selection, nearby-slot suggestions, booking form submission, and pending booking creation.

**Architecture:** Keep `AIChatController` thin and move booking decisions into `AIBookingFlowOrchestrator`. Availability truth comes from `RoomSlotInventories`, booking overlap checks, lead-time rules, room capacity, and `BookingCreationService` revalidation. The public JS renders typed UI blocks and posts typed actions; AI wording stays short and never decides availability.

**Tech Stack:** ASP.NET Core MVC `net10.0`, EF Core InMemory tests, PostgreSQL-backed EF entities, Razor layout, vanilla JavaScript public chatbot.

---

## File Structure and Responsibilities

- Modify `WebHomestay/Services/AIBookingFlowModels.cs`
  - Add request fields for explicit date/time context and selected IDs when actions are posted.
  - Add data shape for unavailable/nearby slot responses.
- Modify `WebHomestay/Services/IAIBookingFlowOrchestrator.cs`
  - Add `HandleChatAsync(PublicAIChatRequest request, CancellationToken cancellationToken)` as the single deterministic entry point for `/ai/chat`.
- Modify `WebHomestay/Services/AIBookingFlowOrchestrator.cs`
  - Parse Vietnamese date/time booking intent.
  - Default vague availability questions to form context and today.
  - Check exact hourly slots first, then same-day nearby slots within ±2 hours.
  - Build concise deterministic responses and UI blocks.
- Modify `WebHomestay/Controllers/AIChatController.cs`
  - Stop letting `AIBrainOrchestrator` decide booking availability.
  - Delegate `/ai/chat` to `AIBookingFlowOrchestrator.HandleChatAsync`.
  - Keep AI provider only as optional copy support later; not needed for availability decisions.
- Modify `WebHomestay/Services/AvailabilityService.cs`
  - Add slot-level validation for AI and booking creation.
- Modify `WebHomestay/Services/IAvailabilityService.cs`
  - Add the new slot validation method signature.
- Modify `WebHomestay/Services/BookingCreationService.cs`
  - Verify selected hourly slot belongs to the requested room and is valid before creating a booking.
- Modify `WebHomestay/wwwroot/js/site.js`
  - Send optional date/time context if the UI/session has it.
  - Render empty and nearby slot responses clearly.
  - Keep button actions compatible with new action payload fields.
- Modify `WebHomestay/Views/Shared/_Layout.cshtml`
  - Update the note to say chat can create a pending booking, not only “tham khảo”.
- Modify `WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs`
  - Add TDD coverage for context-aware availability, tomorrow no-slot behavior, exact time, nearby slots, and no pre-form booking.
- Create or modify `WebHomestay.Tests/Services/BookingCreationServiceTests.cs`
  - Add hardening tests for slot/room mismatch and blocked/booked slot rejection if no equivalent test already exists.

---

### Task 1: Add deterministic chat contract and orchestrator entry point

**Files:**
- Modify: `WebHomestay/Services/AIBookingFlowModels.cs`
- Modify: `WebHomestay/Services/IAIBookingFlowOrchestrator.cs`
- Modify: `WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs`

- [ ] **Step 1: Add failing test for vague availability using form context and today**

Append this test to `WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs` before the helper methods:

```csharp
[Fact]
public async Task HandleChatAsync_WhenGuestAsksVagueAvailability_UsesFormContextAndToday()
{
    await using var context = CreateContext(nameof(HandleChatAsync_WhenGuestAsksVagueAvailability_UsesFormContextAndToday));
    SeedBranchesAndRooms(context);
    var today = DateOnly.FromDateTime(DateTime.Today);
    context.RoomSlotTemplates.Add(new RoomSlotTemplate
    {
        Id = 2,
        Name = "Trưa",
        Code = "NOON",
        DurationMinutes = 60,
        CleanupMinutes = 0,
        FixedStartTime = new TimeOnly(12, 0),
        FixedEndTime = new TimeOnly(13, 0),
        IsActive = true
    });
    context.RoomSlotInventories.Add(new RoomSlotInventory
    {
        Id = 200,
        RoomId = 10,
        TemplateId = 2,
        SlotDate = today,
        SlotLabel = "12:00-13:00",
        StartTime = today.ToDateTime(new TimeOnly(12, 0)),
        EndTime = today.ToDateTime(new TimeOnly(13, 0)),
        Status = "Available"
    });
    await context.SaveChangesAsync();
    var service = CreateService(context);

    var response = await service.HandleChatAsync(new PublicAIChatRequest
    {
        SessionId = "ctx1",
        Message = "còn phòng không?",
        CustomerName = "Nhân",
        BranchId = 1,
        BookingMode = "hourly",
        GuestCount = 2
    }, CancellationToken.None);

    Assert.Equal("select-slot", response.CurrentStep);
    Assert.Equal(today, response.State.HourlyDate);
    Assert.Equal(1, response.State.BranchId);
    Assert.Equal(2, response.State.GuestCount);
    var block = Assert.Single(response.UiBlocks, block => block.Type == "hourlySlots");
    var slots = GetSlots(block.Data);
    var slot = Assert.Single(slots);
    Assert.Equal(200, slot.SlotId);
    Assert.Contains("còn", response.Message, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~HandleChatAsync_WhenGuestAsksVagueAvailability_UsesFormContextAndToday
```

Expected: FAIL because `HandleChatAsync` is not defined on `AIBookingFlowOrchestrator` / `IAIBookingFlowOrchestrator`.

- [ ] **Step 3: Extend `PublicAIChatRequest` and interface**

In `WebHomestay/Services/AIBookingFlowModels.cs`, replace `PublicAIChatRequest` with:

```csharp
public class PublicAIChatRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public int? BranchId { get; set; }
    public string BookingMode { get; set; } = "hourly";
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int GuestCount { get; set; } = 1;
}
```

In `WebHomestay/Services/IAIBookingFlowOrchestrator.cs`, replace the interface with:

```csharp
namespace WebHomestay.Services;

public interface IAIBookingFlowOrchestrator
{
    Task<AIBookingFlowResponse> HandleChatAsync(PublicAIChatRequest request, CancellationToken cancellationToken = default);
    Task<AIBookingFlowResponse> BuildRoomCardsAsync(AIBookingSessionState state);
    Task<AIBookingFlowResponse> SelectRoomAsync(AIBookingActionRequest request);
    Task<AIBookingFlowResponse> SelectSlotAsync(AIBookingActionRequest request);
    Task<AIBookingFlowResponse> SelectDailyRoomAsync(AIBookingActionRequest request);
    Task<AIBookingFlowResponse> SubmitBookingFormAsync(AIBookingActionRequest request);
    Task<AIBookingFlowResponse> HandleActionAsync(AIBookingActionRequest request);
}
```

- [ ] **Step 4: Add minimal `HandleChatAsync` implementation**

In `WebHomestay/Services/AIBookingFlowOrchestrator.cs`, add `using System.Text.RegularExpressions;` at the top.

Add this method inside `AIBookingFlowOrchestrator` before `BuildRoomCardsAsync`:

```csharp
public async Task<AIBookingFlowResponse> HandleChatAsync(PublicAIChatRequest request, CancellationToken cancellationToken = default)
{
    var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId.Trim();
    var message = request.Message?.Trim() ?? string.Empty;
    var state = new AIBookingSessionState
    {
        CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? null : request.CustomerName.Trim(),
        BranchId = request.BranchId,
        BookingMode = NormalizeMode(request.BookingMode),
        GuestCount = request.GuestCount <= 0 ? 1 : request.GuestCount
    };

    if (!state.BranchId.HasValue)
    {
        return BuildResponse(sessionId, "need-branch", "Bạn chọn giúp mình chi nhánh trước để mình kiểm tra phòng trống nhé.", state);
    }

    var requestedDate = ExtractDateOrDefaultToday(message, request.StartTime);
    state.HourlyDate = requestedDate;

    var exactRange = TryExtractHourlyRange(message, requestedDate, out var rangeStart, out var rangeEnd);
    if (exactRange)
    {
        return await BuildExactOrNearbyHourlySlotsAsync(sessionId, state, rangeStart, rangeEnd, cancellationToken);
    }

    return await BuildAvailableHourlySlotsForDateAsync(sessionId, state, requestedDate, "Mình kiểm tra theo thông tin bạn đã chọn. Các khung giờ còn trống hôm nay là:", cancellationToken);
}
```

Also add these helper method stubs after `HandleActionAsync`:

```csharp
private async Task<AIBookingFlowResponse> BuildAvailableHourlySlotsForDateAsync(string sessionId, AIBookingSessionState state, DateOnly date, string message, CancellationToken cancellationToken)
{
    var slots = await BuildHourlySlotOptionsAsync(state, date, null, null, cancellationToken);
    var step = slots.Count == 0 ? "no-availability" : "select-slot";
    var responseMessage = slots.Count == 0
        ? "Hiện không còn khung giờ phù hợp theo thông tin bạn đã chọn. Bạn thử đổi giờ, ngày hoặc chi nhánh giúp mình nhé."
        : message;
    return BuildResponse(sessionId, step, responseMessage, state, new AIUiBlock { Type = "hourlySlots", Data = new { slots } });
}

private Task<AIBookingFlowResponse> BuildExactOrNearbyHourlySlotsAsync(string sessionId, AIBookingSessionState state, DateTime rangeStart, DateTime rangeEnd, CancellationToken cancellationToken)
{
    throw new NotImplementedException();
}

private Task<List<AISlotOption>> BuildHourlySlotOptionsAsync(AIBookingSessionState state, DateOnly date, DateTime? windowStart, DateTime? windowEnd, CancellationToken cancellationToken)
{
    throw new NotImplementedException();
}

private static DateOnly ExtractDateOrDefaultToday(string message, DateTime? requestStartTime)
{
    var lowered = message.ToLowerInvariant();
    if (lowered.Contains("ngày mai") || lowered.Contains("ngay mai")) return DateOnly.FromDateTime(DateTime.Today.AddDays(1));
    if (lowered.Contains("hôm nay") || lowered.Contains("hom nay")) return DateOnly.FromDateTime(DateTime.Today);
    if (requestStartTime.HasValue) return DateOnly.FromDateTime(requestStartTime.Value);
    return DateOnly.FromDateTime(DateTime.Today);
}

private static bool TryExtractHourlyRange(string message, DateOnly date, out DateTime start, out DateTime end)
{
    start = default;
    end = default;
    var match = Regex.Match(message.ToLowerInvariant(), @"(\d{1,2})(?:h|:)(\d{2})?\s*[-–đến]+\s*(\d{1,2})(?:h|:)(\d{2})?");
    if (!match.Success) return false;

    var startHour = int.Parse(match.Groups[1].Value);
    var startMinute = match.Groups[2].Success && !string.IsNullOrWhiteSpace(match.Groups[2].Value) ? int.Parse(match.Groups[2].Value) : 0;
    var endHour = int.Parse(match.Groups[3].Value);
    var endMinute = match.Groups[4].Success && !string.IsNullOrWhiteSpace(match.Groups[4].Value) ? int.Parse(match.Groups[4].Value) : 0;
    if (startHour > 23 || endHour > 23 || startMinute > 59 || endMinute > 59) return false;

    start = date.ToDateTime(new TimeOnly(startHour, startMinute));
    end = date.ToDateTime(new TimeOnly(endHour, endMinute));
    return end > start;
}
```

- [ ] **Step 5: Implement `BuildHourlySlotOptionsAsync`**

Replace the stub with:

```csharp
private async Task<List<AISlotOption>> BuildHourlySlotOptionsAsync(AIBookingSessionState state, DateOnly date, DateTime? windowStart, DateTime? windowEnd, CancellationToken cancellationToken)
{
    var query = _context.RoomSlotInventories
        .AsNoTracking()
        .Include(slot => slot.Room)
        .Where(slot => slot.SlotDate == date
            && slot.Status == "Available"
            && slot.Room.BranchId == state.BranchId
            && slot.Room.Status == "Available"
            && slot.Room.MaxGuests >= state.GuestCount);

    if (windowStart.HasValue && windowEnd.HasValue)
    {
        query = query.Where(slot => slot.StartTime < windowEnd.Value && slot.EndTime > windowStart.Value);
    }

    var inventorySlots = await query
        .OrderBy(slot => slot.StartTime)
        .ThenBy(slot => slot.Room.PricePerHour)
        .ToListAsync(cancellationToken);

    var options = new List<AISlotOption>();
    foreach (var slot in inventorySlots)
    {
        if (!await _availabilityService.IsRoomAvailable(slot.RoomId, slot.StartTime, slot.EndTime))
        {
            continue;
        }

        options.Add(new AISlotOption
        {
            SlotId = slot.Id,
            RoomId = slot.RoomId,
            RoomName = slot.Room.Name,
            Label = slot.SlotLabel,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            TotalPrice = await CalculateTotalPriceAsync(slot.Room, slot.StartTime, slot.EndTime, true, state.GuestCount)
        });
    }

    return options;
}
```

- [ ] **Step 6: Run test to verify it passes**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~HandleChatAsync_WhenGuestAsksVagueAvailability_UsesFormContextAndToday
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add WebHomestay/Services/AIBookingFlowModels.cs WebHomestay/Services/IAIBookingFlowOrchestrator.cs WebHomestay/Services/AIBookingFlowOrchestrator.cs WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs
git commit -m "feat: ground AI chat availability in form context"
```

---

### Task 2: Check exact hourly range before nearby suggestions

**Files:**
- Modify: `WebHomestay/Services/AIBookingFlowOrchestrator.cs`
- Modify: `WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs`

- [ ] **Step 1: Add failing exact-time test**

Append this test before helper methods:

```csharp
[Fact]
public async Task HandleChatAsync_WhenExactHourlyRangeAvailable_ReturnsExactSlotOnly()
{
    await using var context = CreateContext(nameof(HandleChatAsync_WhenExactHourlyRangeAvailable_ReturnsExactSlotOnly));
    SeedBranchesRoomsAndSlots(context);
    context.RoomSlotInventories.Add(new RoomSlotInventory
    {
        Id = 101,
        RoomId = 10,
        TemplateId = 1,
        SlotDate = new DateOnly(2026, 5, 20),
        SlotLabel = "12:00-13:00",
        StartTime = new DateTime(2026, 5, 20, 12, 0, 0),
        EndTime = new DateTime(2026, 5, 20, 13, 0, 0),
        Status = "Available"
    });
    await context.SaveChangesAsync();
    var service = CreateService(context);

    var response = await service.HandleChatAsync(new PublicAIChatRequest
    {
        SessionId = "exact1",
        Message = "ngày 20/5 còn phòng 9h-11h không?",
        BranchId = 1,
        BookingMode = "hourly",
        GuestCount = 2
    }, CancellationToken.None);

    Assert.Equal("select-slot", response.CurrentStep);
    var block = Assert.Single(response.UiBlocks, block => block.Type == "hourlySlots");
    var slots = GetSlots(block.Data);
    var slot = Assert.Single(slots);
    Assert.Equal(100, slot.SlotId);
    Assert.Contains("đúng khung", response.Message, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~HandleChatAsync_WhenExactHourlyRangeAvailable_ReturnsExactSlotOnly
```

Expected: FAIL because `BuildExactOrNearbyHourlySlotsAsync` throws `NotImplementedException` or date parsing does not yet parse `20/5`.

- [ ] **Step 3: Upgrade date parser for dd/MM messages**

Replace `ExtractDateOrDefaultToday` with:

```csharp
private static DateOnly ExtractDateOrDefaultToday(string message, DateTime? requestStartTime)
{
    var lowered = message.ToLowerInvariant();
    if (lowered.Contains("ngày mai") || lowered.Contains("ngay mai")) return DateOnly.FromDateTime(DateTime.Today.AddDays(1));
    if (lowered.Contains("hôm nay") || lowered.Contains("hom nay")) return DateOnly.FromDateTime(DateTime.Today);

    var match = Regex.Match(lowered, @"(?:ngày\s*)?(\d{1,2})[/-](\d{1,2})(?:[/-](\d{2,4}))?");
    if (match.Success)
    {
        var day = int.Parse(match.Groups[1].Value);
        var month = int.Parse(match.Groups[2].Value);
        var year = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : DateTime.Today.Year;
        if (year < 100) year += 2000;
        if (DateOnly.TryParse($"{year:D4}-{month:D2}-{day:D2}", out var parsed)) return parsed;
    }

    if (requestStartTime.HasValue) return DateOnly.FromDateTime(requestStartTime.Value);
    return DateOnly.FromDateTime(DateTime.Today);
}
```

- [ ] **Step 4: Implement exact-first lookup**

Replace `BuildExactOrNearbyHourlySlotsAsync` with:

```csharp
private async Task<AIBookingFlowResponse> BuildExactOrNearbyHourlySlotsAsync(string sessionId, AIBookingSessionState state, DateTime rangeStart, DateTime rangeEnd, CancellationToken cancellationToken)
{
    state.HourlyDate = DateOnly.FromDateTime(rangeStart);
    var exactSlots = await BuildHourlySlotOptionsAsync(state, state.HourlyDate.Value, rangeStart, rangeEnd, cancellationToken);
    exactSlots = exactSlots
        .Where(slot => slot.StartTime == rangeStart && slot.EndTime == rangeEnd)
        .ToList();

    if (exactSlots.Count > 0)
    {
        return BuildResponse(sessionId, "select-slot", "Đúng khung giờ bạn hỏi hiện còn phòng. Bạn chọn phòng/slot bên dưới nhé.", state,
            new AIUiBlock { Type = "hourlySlots", Data = new { slots = exactSlots } });
    }

    var nearbyStart = rangeStart.AddHours(-2);
    var nearbyEnd = rangeEnd.AddHours(2);
    var nearbySlots = await BuildHourlySlotOptionsAsync(state, state.HourlyDate.Value, nearbyStart, nearbyEnd, cancellationToken);
    nearbySlots = nearbySlots
        .Where(slot => slot.StartTime >= nearbyStart && slot.EndTime <= nearbyEnd)
        .OrderBy(slot => Math.Abs((slot.StartTime - rangeStart).TotalMinutes))
        .ThenBy(slot => slot.TotalPrice)
        .ToList();

    if (nearbySlots.Count == 0)
    {
        return BuildResponse(sessionId, "no-availability", "Khung giờ bạn hỏi hiện không còn phòng, và trong vòng gần 2 giờ cũng chưa có slot phù hợp. Bạn thử đổi giờ hoặc ngày giúp mình nhé.", state,
            new AIUiBlock { Type = "hourlySlots", Data = new { slots = nearbySlots } });
    }

    return BuildResponse(sessionId, "select-slot", "Khung giờ bạn hỏi đã hết. Mình gợi ý các giờ còn trống gần đó trong vòng 2 giờ cùng ngày nhé.", state,
        new AIUiBlock { Type = "hourlySlots", Data = new { slots = nearbySlots } });
}
```

- [ ] **Step 5: Run exact-time test**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~HandleChatAsync_WhenExactHourlyRangeAvailable_ReturnsExactSlotOnly
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Services/AIBookingFlowOrchestrator.cs WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs
git commit -m "feat: check exact AI booking time before suggestions"
```

---

### Task 3: Limit nearby suggestions to ±2 hours and do not invent tomorrow slots

**Files:**
- Modify: `WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs`
- Modify: `WebHomestay/Services/AIBookingFlowOrchestrator.cs`

- [ ] **Step 1: Add failing nearby-slot test**

Append this test before helper methods:

```csharp
[Fact]
public async Task HandleChatAsync_WhenExactRangeUnavailable_ReturnsOnlyNearbySlotsWithinTwoHours()
{
    await using var context = CreateContext(nameof(HandleChatAsync_WhenExactRangeUnavailable_ReturnsOnlyNearbySlotsWithinTwoHours));
    SeedBranchesRoomsAndSlots(context);
    var exact = await context.RoomSlotInventories.SingleAsync(slot => slot.Id == 100);
    exact.Status = "Blocked";
    context.RoomSlotInventories.AddRange(
        new RoomSlotInventory
        {
            Id = 102,
            RoomId = 10,
            TemplateId = 1,
            SlotDate = new DateOnly(2026, 5, 20),
            SlotLabel = "08:00-10:00",
            StartTime = new DateTime(2026, 5, 20, 8, 0, 0),
            EndTime = new DateTime(2026, 5, 20, 10, 0, 0),
            Status = "Available"
        },
        new RoomSlotInventory
        {
            Id = 103,
            RoomId = 10,
            TemplateId = 1,
            SlotDate = new DateOnly(2026, 5, 20),
            SlotLabel = "14:00-16:00",
            StartTime = new DateTime(2026, 5, 20, 14, 0, 0),
            EndTime = new DateTime(2026, 5, 20, 16, 0, 0),
            Status = "Available"
        });
    await context.SaveChangesAsync();
    var service = CreateService(context);

    var response = await service.HandleChatAsync(new PublicAIChatRequest
    {
        SessionId = "near1",
        Message = "ngày 20/5 có phòng 9h-11h không?",
        BranchId = 1,
        BookingMode = "hourly",
        GuestCount = 2
    }, CancellationToken.None);

    var block = Assert.Single(response.UiBlocks, block => block.Type == "hourlySlots");
    var slots = GetSlots(block.Data);
    Assert.Single(slots);
    Assert.Equal(102, slots[0].SlotId);
    Assert.DoesNotContain(slots, slot => slot.SlotId == 103);
    Assert.Contains("gần", response.Message, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Add failing tomorrow no-slot test**

Append this test before helper methods:

```csharp
[Fact]
public async Task HandleChatAsync_WhenTomorrowHasNoSlots_DoesNotClaimAvailability()
{
    await using var context = CreateContext(nameof(HandleChatAsync_WhenTomorrowHasNoSlots_DoesNotClaimAvailability));
    SeedBranchesRoomsAndSlots(context);
    await context.SaveChangesAsync();
    var service = CreateService(context);

    var response = await service.HandleChatAsync(new PublicAIChatRequest
    {
        SessionId = "tomorrow1",
        Message = "ngày mai có phòng không?",
        BranchId = 1,
        BookingMode = "hourly",
        GuestCount = 2
    }, CancellationToken.None);

    Assert.Equal("no-availability", response.CurrentStep);
    Assert.Contains("không còn", response.Message, StringComparison.OrdinalIgnoreCase);
    var block = Assert.Single(response.UiBlocks, block => block.Type == "hourlySlots");
    Assert.Empty(GetSlots(block.Data));
}
```

- [ ] **Step 3: Run both tests**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter "FullyQualifiedName~HandleChatAsync_WhenExactRangeUnavailable_ReturnsOnlyNearbySlotsWithinTwoHours|FullyQualifiedName~HandleChatAsync_WhenTomorrowHasNoSlots_DoesNotClaimAvailability"
```

Expected: nearby test may fail if overlap window includes too much; tomorrow test should fail if message does not contain the agreed no-availability wording.

- [ ] **Step 4: Tighten nearby filter and no-slot wording**

In `BuildExactOrNearbyHourlySlotsAsync`, ensure the nearby block is exactly:

```csharp
var nearbyStart = rangeStart.AddHours(-2);
var nearbyEnd = rangeEnd.AddHours(2);
var nearbySlots = await BuildHourlySlotOptionsAsync(state, state.HourlyDate.Value, nearbyStart, nearbyEnd, cancellationToken);
nearbySlots = nearbySlots
    .Where(slot => slot.StartTime >= nearbyStart && slot.EndTime <= nearbyEnd)
    .OrderBy(slot => Math.Abs((slot.StartTime - rangeStart).TotalMinutes))
    .ThenBy(slot => slot.TotalPrice)
    .ToList();
```

In `BuildAvailableHourlySlotsForDateAsync`, ensure the no-slot message is exactly:

```csharp
var responseMessage = slots.Count == 0
    ? "Hiện không còn khung giờ phù hợp theo thông tin bạn đã chọn. Bạn thử đổi giờ, ngày hoặc chi nhánh giúp mình nhé."
    : message;
```

- [ ] **Step 5: Run AI booking flow tests**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AIBookingFlowOrchestratorTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Services/AIBookingFlowOrchestrator.cs WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs
git commit -m "test: cover AI booking nearby and no-slot responses"
```

---

### Task 4: Harden hourly booking creation against invalid slot state

**Files:**
- Modify: `WebHomestay/Services/IAvailabilityService.cs`
- Modify: `WebHomestay/Services/AvailabilityService.cs`
- Modify: `WebHomestay/Services/BookingCreationService.cs`
- Create or modify: `WebHomestay.Tests/Services/BookingCreationServiceTests.cs`

- [ ] **Step 1: Inspect existing `IAvailabilityService`**

Read `WebHomestay/Services/IAvailabilityService.cs` and keep existing signatures. Add the new method without removing current methods:

```csharp
Task<bool> IsHourlySlotAvailableForRoomAsync(int slotInventoryId, int roomId, int guestCount, CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Add failing booking hardening test**

If `WebHomestay.Tests/Services/BookingCreationServiceTests.cs` exists, append this test. If it does not exist, create the file with namespace `WebHomestay.Tests.Services` and reuse the same helpers from `AIBookingFlowOrchestratorTests` by copying minimal setup into this file.

```csharp
[Fact]
public async Task CreateHourlyBookingAsync_WhenSlotBelongsToDifferentRoom_ThrowsAndDoesNotBookSlot()
{
    await using var context = CreateContext(nameof(CreateHourlyBookingAsync_WhenSlotBelongsToDifferentRoom_ThrowsAndDoesNotBookSlot));
    SeedBranchesRoomsAndSlots(context);
    context.Rooms.Add(new Room { Id = 12, BranchId = 1, Name = "Sài Gòn Family", Status = "Available", Capacity = 4, MaxGuests = 4, PricePerHour = 180000, PricePerDay = 900000, ExtraGuestFee = 0 });
    await context.SaveChangesAsync();
    var availability = new AvailabilityService(context);
    var service = new BookingCreationService(context, availability, new PricingService(context));

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateHourlyBookingAsync(new CreateBookingRequest
    {
        RoomId = 12,
        BookingMode = BookingMode.Hourly,
        SlotInventoryId = 100,
        CustomerName = "Nhân",
        CustomerPhone = "0900000000",
        GuestCount = 2
    }));

    Assert.Contains("không hợp lệ", ex.Message, StringComparison.OrdinalIgnoreCase);
    var slot = await context.RoomSlotInventories.SingleAsync(item => item.Id == 100);
    Assert.Equal("Available", slot.Status);
    Assert.Empty(context.Bookings);
}
```

- [ ] **Step 3: Run hardening test**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~CreateHourlyBookingAsync_WhenSlotBelongsToDifferentRoom_ThrowsAndDoesNotBookSlot
```

Expected: FAIL because booking creation currently does not verify inventory room matches requested room.

- [ ] **Step 4: Implement slot-level availability method**

In `WebHomestay/Services/IAvailabilityService.cs`, add the method signature from Step 1.

In `WebHomestay/Services/AvailabilityService.cs`, add this method before `GetBlockedDatesAsync`:

```csharp
public async Task<bool> IsHourlySlotAvailableForRoomAsync(int slotInventoryId, int roomId, int guestCount, CancellationToken cancellationToken = default)
{
    var slot = await _context.RoomSlotInventories
        .AsNoTracking()
        .Include(item => item.Room)
        .SingleOrDefaultAsync(item => item.Id == slotInventoryId, cancellationToken);

    if (slot == null) return false;
    if (slot.RoomId != roomId) return false;
    if (slot.Status != "Available") return false;
    if (slot.Room.Status != "Available") return false;
    if (slot.Room.MaxGuests < guestCount) return false;

    return await IsRoomAvailable(roomId, slot.StartTime, slot.EndTime);
}
```

- [ ] **Step 5: Use slot-level validation in booking creation**

In `WebHomestay/Services/BookingCreationService.cs`, replace the start of `CreateHourlyBookingAsync` through the availability checks with:

```csharp
var inventory = await _context.RoomSlotInventories
    .Include(i => i.Room)
    .SingleAsync(i => i.Id == request.SlotInventoryId);

if (!await _availabilityService.IsHourlySlotAvailableForRoomAsync(inventory.Id, request.RoomId, request.GuestCount))
    throw new InvalidOperationException("Khung giờ đặt phòng không hợp lệ hoặc vừa được người khác đặt.");
```

- [ ] **Step 6: Run hardening test**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~CreateHourlyBookingAsync_WhenSlotBelongsToDifferentRoom_ThrowsAndDoesNotBookSlot
```

Expected: PASS.

- [ ] **Step 7: Run existing booking creation tests**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BookingCreationService
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add WebHomestay/Services/IAvailabilityService.cs WebHomestay/Services/AvailabilityService.cs WebHomestay/Services/BookingCreationService.cs WebHomestay.Tests/Services/BookingCreationServiceTests.cs
git commit -m "fix: validate hourly slot before booking creation"
```

---

### Task 5: Delegate `/ai/chat` to deterministic booking flow

**Files:**
- Modify: `WebHomestay/Controllers/AIChatController.cs`
- Modify: `WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs`

- [ ] **Step 1: Add controller-safe response requirement to orchestrator test**

Append this assertion to the existing vague availability test after `var response = await service.HandleChatAsync(...)`:

```csharp
Assert.False(string.IsNullOrWhiteSpace(response.SessionId));
Assert.NotEqual("chat", response.CurrentStep);
```

- [ ] **Step 2: Run AI booking tests**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AIBookingFlowOrchestratorTests
```

Expected: PASS after Task 1 implementation sets `SessionId`; otherwise fix `BuildResponse(sessionId, ...)` call sites.

- [ ] **Step 3: Simplify `AIChatController.Chat`**

In `WebHomestay/Controllers/AIChatController.cs`, replace the body of `Chat` with:

```csharp
[HttpPost("chat")]
public async Task<IActionResult> Chat([FromBody] PublicAIChatRequest request, CancellationToken cancellationToken)
{
    if (request == null || string.IsNullOrWhiteSpace(request.Message))
    {
        return BadRequest(new AIBookingFlowResponse
        {
            SessionId = request?.SessionId ?? string.Empty,
            CurrentStep = "intent",
            Message = "Bạn nhập giúp mình nhu cầu đặt phòng để mình tư vấn nhé.",
            State = new AIBookingSessionState(),
            UiBlocks = new List<AIUiBlock>()
        });
    }

    try
    {
        var response = await _bookingFlowOrchestrator.HandleChatAsync(request, cancellationToken);
        return Ok(new
        {
            answer = response.Message,
            message = response.Message,
            sessionId = response.SessionId,
            currentStep = response.CurrentStep,
            state = response.State,
            uiBlocks = response.UiBlocks,
            formSchema = "[]"
        });
    }
    catch
    {
        return StatusCode(503, new
        {
            answer = "Xin lỗi, mình chưa kiểm tra được tình trạng phòng lúc này. Bạn thử lại sau ít phút nhé.",
            message = "Xin lỗi, mình chưa kiểm tra được tình trạng phòng lúc này. Bạn thử lại sau ít phút nhé.",
            sessionId = request.SessionId,
            currentStep = "error",
            state = new AIBookingSessionState(),
            uiBlocks = Array.Empty<AIUiBlock>(),
            formSchema = "[]"
        });
    }
}
```

- [ ] **Step 4: Remove unused private parser methods if compiler reports warnings**

If `ResolveGuestCount`, `TryExtractDate`, or `ResolveBranchIdAsync` are no longer used, delete those private methods and the `using System.Text.RegularExpressions;` import from `AIChatController.cs`. Keep `Branches` endpoint.

- [ ] **Step 5: Build project**

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: PASS with existing warnings only.

- [ ] **Step 6: Commit**

```bash
git add WebHomestay/Controllers/AIChatController.cs WebHomestay.Tests/Services/AIBookingFlowOrchestratorTests.cs
git commit -m "fix: route public AI chat through booking flow"
```

---

### Task 6: Improve public chat UI rendering for no-slot and nearby-slot responses

**Files:**
- Modify: `WebHomestay/wwwroot/js/site.js`
- Modify: `WebHomestay/Views/Shared/_Layout.cshtml`
- Modify: `WebHomestay/wwwroot/css/user-premium.css`

- [ ] **Step 1: Update layout note**

In `WebHomestay/Views/Shared/_Layout.cshtml`, replace:

```html
<p class="ai-chat-note">AI chỉ tư vấn tham khảo; đặt phòng chính thức cần hoàn tất trên website.</p>
```

with:

```html
<p class="ai-chat-note">AI kiểm tra phòng trống theo dữ liệu thật và có thể tạo booking chờ thanh toán.</p>
```

- [ ] **Step 2: Render empty slot state in `site.js`**

In `renderHourlySlots(data)`, after `const slots = Array.isArray(data.slots) ? data.slots : [];`, add:

```javascript
if (!slots.length) {
    const empty = document.createElement('div');
    empty.className = 'ai-empty-slots';
    empty.textContent = 'Chưa có khung giờ phù hợp để hiển thị.';
    wrapper.appendChild(empty);
    scrollMessages();
    return;
}
```

- [ ] **Step 3: Show price on slot buttons**

In `renderHourlySlots(data)`, replace the button text assignment with:

```javascript
button.textContent = `${slot.roomName || 'Phòng'}: ${slot.label || 'Khung giờ'} · ${formatMoney(slot.totalPrice)}`;
```

- [ ] **Step 4: Add CSS for empty slots**

Append to `WebHomestay/wwwroot/css/user-premium.css`:

```css
.ai-empty-slots {
    padding: 0.75rem 0.9rem;
    border: 1px dashed rgba(148, 163, 184, 0.45);
    border-radius: 0.75rem;
    color: #64748b;
    background: rgba(248, 250, 252, 0.8);
    font-size: 0.9rem;
}
```

- [ ] **Step 5: Build project**

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: PASS with existing warnings only.

- [ ] **Step 6: Manual UI check**

Run the app:

```bash
dotnet run --project WebHomestay/WebHomestay.csproj
```

Open the public site in a browser and verify:

- Chat opens.
- Branch dropdown loads.
- Asking “còn phòng không?” returns real slot buttons or the empty slot message.
- Asking an unavailable exact time returns nearby slot buttons only when they exist.
- The note says booking can create a pending payment booking.

- [ ] **Step 7: Commit**

```bash
git add WebHomestay/wwwroot/js/site.js WebHomestay/Views/Shared/_Layout.cshtml WebHomestay/wwwroot/css/user-premium.css
git commit -m "feat: clarify AI booking slot UI states"
```

---

### Task 7: Final regression tests and end-to-end booking sanity check

**Files:**
- No required code changes unless tests fail.

- [ ] **Step 1: Run AI booking tests**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~AIBookingFlowOrchestratorTests
```

Expected: PASS.

- [ ] **Step 2: Run booking creation tests**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BookingCreationService
```

Expected: PASS.

- [ ] **Step 3: Run full test project**

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj
```

Expected: PASS. If unrelated pre-existing failures appear, record the failing test names and do not hide them.

- [ ] **Step 4: Build web app**

```bash
dotnet build WebHomestay/WebHomestay.csproj
```

Expected: PASS with existing warnings only.

- [ ] **Step 5: Manual golden path check**

Run:

```bash
dotnet run --project WebHomestay/WebHomestay.csproj
```

In the browser:

1. Open public chat.
2. Enter name, branch, hourly mode, and guest count.
3. Ask “còn phòng không?”.
4. Select a real slot.
5. Submit name and phone in the booking form.
6. Verify a payment block appears and the admin booking list shows a new `AwaitingPayment` / `Unpaid` booking.

- [ ] **Step 6: Manual unavailable path check**

In the browser:

1. Ask for a date with no slot, such as “ngày mai có phòng không?” when tomorrow has no slot inventory.
2. Verify the assistant says no suitable slot exists.
3. Verify no room card or booking form appears.

- [ ] **Step 7: Commit any final fixes only if code changed**

If this task required code fixes:

```bash
git add <changed-files>
git commit -m "fix: stabilize AI chat booking flow"
```

If no code changed, do not create an empty commit.

---

## Self-Review

- Spec coverage: context-aware vague questions are handled in Task 1; exact and nearby hourly behavior in Tasks 2-3; tomorrow no-slot behavior in Task 3; booking creation and revalidation in Task 4; controller delegation in Task 5; UI wiring and manual checks in Tasks 6-7.
- Placeholder scan: no TBD/TODO/fill-later instructions remain; every code-changing step includes concrete code or exact replacement text.
- Type consistency: the plan uses existing `PublicAIChatRequest`, `AIBookingSessionState`, `AIBookingFlowResponse`, `AISlotOption`, `AIBookingActionRequest`, and `AIBookingFormSubmission` names already present in the codebase.
