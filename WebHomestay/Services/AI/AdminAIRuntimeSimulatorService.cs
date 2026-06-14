using WebHomestay.Models.AI;

namespace WebHomestay.Services.AI;

public class AdminAIRuntimeSimulatorService : IAdminAIRuntimeSimulatorService
{
    public IReadOnlyList<AdminAIRuntimePreset> GetPresets()
    {
        return AdminAIRuntimePresetCatalog.All;
    }

    public Task<AdminAIRuntimeSimulationResult> SimulateAsync(AdminAIStudioConfig config, AdminAIRuntimeState state)
    {
        state ??= new AdminAIRuntimeState();
        state.CapturedFields ??= new Dictionary<string, string>();

        var result = NewResult(state);

        if (state.NeedHumanHandoff || IsFlow(state, "handoff"))
        {
            AddHandoff(result, config, state);
            return Task.FromResult(result);
        }

        if (string.IsNullOrWhiteSpace(state.FlowId))
        {
            result.ConversationState.Stage = "entry";
            result.AssistantReply = "Ban muon dat theo gio hay theo ngay a?";
            AddDirective(result, config, "bookingModeChoice", "Chon hinh thuc dat", ["bookingMode"]);
            result.NextActions.Add("wait_for_booking_mode");
            return Task.FromResult(result);
        }

        if (IsFlow(state, "hourly"))
        {
            SimulateHourly(config, state, result);
            return Task.FromResult(result);
        }

        if (IsFlow(state, "daily"))
        {
            SimulateDaily(config, state, result);
            return Task.FromResult(result);
        }

        result.ConversationState.Stage = "fallback";
        result.AssistantReply = "Minh can them thong tin de tiep tuc ho tro.";
        result.NextActions.Add("ask_missing_information");
        return Task.FromResult(result);
    }

    private static void SimulateHourly(AdminAIStudioConfig config, AdminAIRuntimeState state, AdminAIRuntimeSimulationResult result)
    {
        result.ConversationState.FlowId = "hourly";

        if (!Has(state, "branchId"))
        {
            Missing(result, "branchId");
            result.ConversationState.Stage = "collect_branch";
            result.AssistantReply = "Ban muon xem phong o chi nhanh nao a?";
            AddDirective(result, config, "branchSelector", "Chon chi nhanh", ["branchId"]);
            result.NextActions.Add("collect_branch");
            return;
        }

        if (!Has(state, "hourlyDate"))
        {
            Missing(result, "hourlyDate");
            result.ConversationState.Stage = "collect_hourly_date";
            result.AssistantReply = "Ban muon dat theo gio vao ngay nao a?";
            AddDirective(result, config, "singleDatePicker", "Chon ngay", ["hourlyDate"]);
            result.NextActions.Add("collect_hourly_date");
            return;
        }

        if (!Has(state, "hourlySlot"))
        {
            Missing(result, "hourlySlot");
            result.ConversationState.Stage = "show_hourly_slots";
            result.AssistantReply = "Cac khung gio trong hien co day a.";
            AddDirective(result, config, "timeSlotGrid", "Khung gio trong", ["hourlySlot"]);
            result.NextActions.Add("show_slots");
            return;
        }

        if (!Has(state, "roomId"))
        {
            Missing(result, "roomId");
            result.ConversationState.Stage = "show_rooms";
            result.AssistantReply = "Minh goi y cac phong phu hop voi khung gio ban chon.";
            AddDirective(result, config, "roomCards", "Phong phu hop", ["roomId"]);
            result.NextActions.Add("show_rooms");
            return;
        }

        AddBookingCta(result, config, state);
    }

    private static void SimulateDaily(AdminAIStudioConfig config, AdminAIRuntimeState state, AdminAIRuntimeSimulationResult result)
    {
        result.ConversationState.FlowId = "daily";

        if (!Has(state, "branchId"))
        {
            Missing(result, "branchId");
            result.ConversationState.Stage = "collect_branch";
            result.AssistantReply = "Ban muon xem phong o chi nhanh nao a?";
            AddDirective(result, config, "branchSelector", "Chon chi nhanh", ["branchId"]);
            result.NextActions.Add("collect_branch");
            return;
        }

        if (!Has(state, "checkInDate") || !Has(state, "checkOutDate"))
        {
            if (!Has(state, "checkInDate")) Missing(result, "checkInDate");
            if (!Has(state, "checkOutDate")) Missing(result, "checkOutDate");
            result.ConversationState.Stage = "collect_date_range";
            result.AssistantReply = "Ban chon ngay nhan va ngay tra phong giup minh nhe.";
            AddDirective(result, config, "dateRangePicker", "Ngay nhan / tra", ["checkInDate", "checkOutDate"]);
            result.NextActions.Add("collect_date_range");
            return;
        }

        if (!Has(state, "roomId"))
        {
            Missing(result, "roomId");
            result.ConversationState.Stage = "show_rooms";
            result.AssistantReply = "Minh goi y cac phong phu hop voi lich cua ban.";
            AddDirective(result, config, "roomCards", "Phong phu hop", ["roomId"]);
            result.NextActions.Add("show_rooms");
            return;
        }

        AddBookingCta(result, config, state);
    }

    private static AdminAIRuntimeSimulationResult NewResult(AdminAIRuntimeState state)
    {
        return new AdminAIRuntimeSimulationResult
        {
            ConversationState = new AdminAIRuntimeConversationState
            {
                FlowId = state.FlowId,
                CapturedFields = new Dictionary<string, string>(state.CapturedFields),
                NeedHumanHandoff = state.NeedHumanHandoff
            }
        };
    }

    private static void AddBookingCta(AdminAIRuntimeSimulationResult result, AdminAIStudioConfig config, AdminAIRuntimeState state)
    {
        result.ConversationState.Stage = "booking_cta";
        result.AssistantReply = "Thong tin da du, ban co the sang trang dat phong chinh thuc.";
        var directive = AddDirective(result, config, "bookingCta", "Di den trang dat phong", ["roomId"]);
        directive.Data["target"] = BuildBookingTarget(state);
        result.NextActions.Add("open_booking_page");
    }

    private static void AddHandoff(AdminAIRuntimeSimulationResult result, AdminAIStudioConfig config, AdminAIRuntimeState state)
    {
        result.ConversationState.FlowId = "handoff";
        result.ConversationState.Stage = "handoff";
        result.ConversationState.NeedHumanHandoff = true;
        result.AssistantReply = FirstText(config.Handoff.MessageTemplate, config.Handoff.ContactInstruction, "Minh se noi ban voi chi nhanh phu hop nhe.");
        AddDirective(result, config, "handoffContact", "Lien he chi nhanh", ["customerPhone"]);
        if (!Has(state, "customerPhone"))
        {
            Missing(result, "customerPhone");
            result.NextActions.Add("collect_customer_phone");
        }
        result.NextActions.Add(Has(state, "branchId") ? "show_selected_branch_phone" : "show_all_branch_phones");
    }

    private static AdminAIUiDirective AddDirective(
        AdminAIRuntimeSimulationResult result,
        AdminAIStudioConfig config,
        string type,
        string fallbackLabel,
        List<string> fieldKeys)
    {
        var block = config.UiBlockDefinitions.FirstOrDefault(item => string.Equals(item.Type, type, StringComparison.OrdinalIgnoreCase));
        var directive = new AdminAIUiDirective
        {
            Type = type,
            Label = FirstText(block?.Label, fallbackLabel),
            FieldKeys = block?.FieldKeys.Count > 0 ? new List<string>(block.FieldKeys) : fieldKeys
        };
        result.UiDirectives.Add(directive);
        return directive;
    }

    private static bool Has(AdminAIRuntimeState state, string key)
    {
        return state.CapturedFields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);
    }

    private static bool IsFlow(AdminAIRuntimeState state, string flowId)
    {
        return string.Equals(state.FlowId, flowId, StringComparison.OrdinalIgnoreCase);
    }

    private static void Missing(AdminAIRuntimeSimulationResult result, string fieldKey)
    {
        if (!result.ConversationState.MissingFields.Contains(fieldKey, StringComparer.Ordinal))
        {
            result.ConversationState.MissingFields.Add(fieldKey);
        }
    }

    private static string FirstText(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string BuildBookingTarget(AdminAIRuntimeState state)
    {
        var query = state.CapturedFields
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}");
        return $"/Bookings/Checkout?{string.Join("&", query)}";
    }
}
