using WebHomestay.Models.AI;
using WebHomestay.Services.AI;
using Xunit;

namespace WebHomestay.Tests.Services;

public class AdminAIRuntimeSimulatorServiceTests
{
    [Fact]
    public async Task SimulateAsync_NewConversation_ShowsBookingModeChoice()
    {
        var service = new AdminAIRuntimeSimulatorService();

        var result = await service.SimulateAsync(BuildConfig(), new AdminAIRuntimeState());

        Assert.Equal("entry", result.ConversationState.Stage);
        Assert.Contains(result.UiDirectives, item => item.Type == "bookingModeChoice");
        Assert.Contains("wait_for_booking_mode", result.NextActions);
    }

    [Fact]
    public async Task SimulateAsync_HourlyFlowWithoutBranch_ShowsBranchSelector()
    {
        var service = new AdminAIRuntimeSimulatorService();
        var state = new AdminAIRuntimeState { FlowId = "hourly" };

        var result = await service.SimulateAsync(BuildConfig(), state);

        Assert.Equal("collect_branch", result.ConversationState.Stage);
        Assert.Contains("branchId", result.ConversationState.MissingFields);
        Assert.Contains(result.UiDirectives, item => item.Type == "branchSelector");
    }

    [Fact]
    public async Task SimulateAsync_HourlyFlowWithBranchButMissingDate_ShowsDatePicker()
    {
        var service = new AdminAIRuntimeSimulatorService();
        var state = new AdminAIRuntimeState
        {
            FlowId = "hourly",
            CapturedFields = new Dictionary<string, string> { ["branchId"] = "1" }
        };

        var result = await service.SimulateAsync(BuildConfig(), state);

        Assert.Equal("collect_hourly_date", result.ConversationState.Stage);
        Assert.Contains(result.UiDirectives, item => item.Type == "singleDatePicker");
        Assert.Contains("hourlyDate", result.ConversationState.MissingFields);
    }

    [Fact]
    public async Task SimulateAsync_HourlyFlowWithBranchAndDate_ShowsSlotGrid()
    {
        var service = new AdminAIRuntimeSimulatorService();
        var state = new AdminAIRuntimeState
        {
            FlowId = "hourly",
            CapturedFields = new Dictionary<string, string>
            {
                ["branchId"] = "1",
                ["hourlyDate"] = "2026-06-10"
            }
        };

        var result = await service.SimulateAsync(BuildConfig(), state);

        Assert.Equal("show_hourly_slots", result.ConversationState.Stage);
        Assert.Contains(result.UiDirectives, item => item.Type == "timeSlotGrid");
        Assert.Contains("show_slots", result.NextActions);
    }

    [Fact]
    public async Task SimulateAsync_DailyFlowWithBranchButMissingRange_ShowsDateRangePicker()
    {
        var service = new AdminAIRuntimeSimulatorService();
        var state = new AdminAIRuntimeState
        {
            FlowId = "daily",
            CapturedFields = new Dictionary<string, string> { ["branchId"] = "1" }
        };

        var result = await service.SimulateAsync(BuildConfig(), state);

        Assert.Equal("collect_date_range", result.ConversationState.Stage);
        Assert.Contains(result.UiDirectives, item => item.Type == "dateRangePicker");
        Assert.Contains("checkInDate", result.ConversationState.MissingFields);
        Assert.Contains("checkOutDate", result.ConversationState.MissingFields);
    }

    [Fact]
    public async Task SimulateAsync_DailyFlowWithRangeButMissingRoom_ShowsRoomCards()
    {
        var service = new AdminAIRuntimeSimulatorService();
        var state = new AdminAIRuntimeState
        {
            FlowId = "daily",
            CapturedFields = new Dictionary<string, string>
            {
                ["branchId"] = "1",
                ["checkInDate"] = "2026-06-10",
                ["checkOutDate"] = "2026-06-12",
                ["guestCount"] = "2"
            }
        };

        var result = await service.SimulateAsync(BuildConfig(), state);

        Assert.Equal("show_rooms", result.ConversationState.Stage);
        Assert.Contains(result.UiDirectives, item => item.Type == "roomCards");
        Assert.Contains("show_rooms", result.NextActions);
    }

    [Fact]
    public async Task SimulateAsync_WhenRoomSelected_ShowsBookingCta()
    {
        var service = new AdminAIRuntimeSimulatorService();
        var state = new AdminAIRuntimeState
        {
            FlowId = "daily",
            CapturedFields = new Dictionary<string, string>
            {
                ["branchId"] = "1",
                ["checkInDate"] = "2026-06-10",
                ["checkOutDate"] = "2026-06-12",
                ["roomId"] = "7"
            }
        };

        var result = await service.SimulateAsync(BuildConfig(), state);

        var cta = Assert.Single(result.UiDirectives.Where(item => item.Type == "bookingCta"));
        Assert.Equal("booking_cta", result.ConversationState.Stage);
        Assert.Contains("open_booking_page", result.NextActions);
        Assert.Contains("roomId=7", cta.Data["target"]);
    }

    [Fact]
    public async Task SimulateAsync_Handoff_ShowsContactBlockAndPhoneNextAction()
    {
        var service = new AdminAIRuntimeSimulatorService();
        var state = new AdminAIRuntimeState { NeedHumanHandoff = true };

        var result = await service.SimulateAsync(BuildConfig(), state);

        Assert.Equal("handoff", result.ConversationState.Stage);
        Assert.Contains(result.UiDirectives, item => item.Type == "handoffContact");
        Assert.Contains("customerPhone", result.ConversationState.MissingFields);
        Assert.Contains("collect_customer_phone", result.NextActions);
        Assert.Contains("show_all_branch_phones", result.NextActions);
    }

    [Fact]
    public void GetPresets_ReturnsKnownSimulatorScenarios()
    {
        var service = new AdminAIRuntimeSimulatorService();

        var presets = service.GetPresets();

        Assert.Contains(presets, preset => preset.Id == "new-chat");
        Assert.Contains(presets, preset => preset.Id == "hourly-ready-for-slots");
        Assert.Contains(presets, preset => preset.Id == "handoff");
    }

    private static AdminAIStudioConfig BuildConfig()
    {
        return new AdminAIStudioConfig
        {
            UiBlockDefinitions =
            [
                new AdminAIUiBlockDefinition
                {
                    Type = "bookingModeChoice",
                    Label = "Chon hinh thuc dat",
                    FieldKeys = ["bookingMode"]
                },
                new AdminAIUiBlockDefinition
                {
                    Type = "branchSelector",
                    Label = "Chon chi nhanh",
                    FieldKeys = ["branchId"]
                },
                new AdminAIUiBlockDefinition
                {
                    Type = "singleDatePicker",
                    Label = "Chon ngay",
                    FieldKeys = ["hourlyDate"]
                },
                new AdminAIUiBlockDefinition
                {
                    Type = "timeSlotGrid",
                    Label = "Khung gio trong",
                    FieldKeys = ["hourlySlot"]
                },
                new AdminAIUiBlockDefinition
                {
                    Type = "dateRangePicker",
                    Label = "Ngay nhan / tra",
                    FieldKeys = ["checkInDate", "checkOutDate"]
                },
                new AdminAIUiBlockDefinition
                {
                    Type = "roomCards",
                    Label = "Phong phu hop",
                    FieldKeys = ["roomId"]
                },
                new AdminAIUiBlockDefinition
                {
                    Type = "bookingCta",
                    Label = "Di den trang dat phong",
                    FieldKeys = ["roomId"]
                },
                new AdminAIUiBlockDefinition
                {
                    Type = "handoffContact",
                    Label = "Lien he chi nhanh",
                    FieldKeys = ["customerPhone"]
                }
            ],
            Handoff = new AdminAIHandoffConfig
            {
                MessageTemplate = "Minh se noi ban voi chi nhanh phu hop nhe."
            }
        };
    }
}
