using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models.AI;

namespace WebHomestay.Services.AI.Workflow.Nodes;

public sealed class BookingGuardNode : IWorkflowNode
{
    private readonly IBookingConductor _bookingConductor;
    private readonly IConversationManager _conversationManager;
    private readonly ApplicationDbContext _context;
    private readonly IPublicBookingRoomExplanationService _roomExplanationService;
    private readonly ILogger<BookingGuardNode> _logger;

    public BookingGuardNode(
        IBookingConductor bookingConductor,
        IConversationManager conversationManager,
        ApplicationDbContext context,
        IPublicBookingRoomExplanationService roomExplanationService,
        ILogger<BookingGuardNode> logger)
    {
        _bookingConductor = bookingConductor;
        _conversationManager = conversationManager;
        _context = context;
        _roomExplanationService = roomExplanationService;
        _logger = logger;
    }

    public string Name => "guard";

    public bool ShortCircuited { get; private set; }
    public string GuardAnswer { get; private set; } = string.Empty;

    public async Task ExecuteAsync(WorkflowState state, CancellationToken cancellationToken)
    {
        try
        {
            var request = new AIBrainChatRequest
            {
                SessionId = state.SessionId,
                Message = state.UserMessage,
                Mode = ChatMode.PublicBooking
            };

            var decision = await _bookingConductor.DecideAsync(state.SessionId, state.UserMessage, request, cancellationToken);

            var shouldHandleDeterministically = (decision.Action != ConductorAction.Reply
                || IsDeterministicConductorReply(decision.Reason))
                && !SemanticQueryHelper.IsSemanticQuery(state.UserMessage);

            if (shouldHandleDeterministically)
            {
                ShortCircuited = true;
                GuardAnswer = await BuildConductorReplyAsync(decision, cancellationToken);

                state.FinalAnswer = GuardAnswer;
                state.BookingAction = decision.Action.ToString();
                state.UiBlocks = decision.UiBlocks ?? new List<object>();

                _logger.LogInformation("Booking guard short-circuited with action {Action} and {UiBlocksCount} UI blocks for session {SessionId}",
                    decision.Action, state.UiBlocks.Count, state.SessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Booking guard failed for session {SessionId}", state.SessionId);
        }
    }

    private static bool IsDeterministicConductorReply(string? reason)
    {
        return string.Equals(reason, "room-context-occupancy", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reason, "room-context-price", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reason, "room-context-policy", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> BuildConductorReplyAsync(ConductorResult decision, CancellationToken cancellationToken)
    {
        var confirmed = decision.State.Confirmed;
        if (IsDeterministicConductorReply(decision.Reason))
        {
            var roomId = decision.State.Progress?.ActiveRoomContextId ?? decision.State.Progress?.SelectedRoomId;
            if (roomId.HasValue)
            {
                var room = await _context.Rooms
                    .AsNoTracking()
                    .Where(r => r.Id == roomId.Value)
                    .Select(r => new
                    {
                        r.Id,
                        r.Name,
                        r.PricePerHour,
                        r.PricePerDay,
                        r.ExtraGuestFee
                    })
                    .FirstOrDefaultAsync(cancellationToken);
                if (room != null)
                {
                    var explanation = await _roomExplanationService.BuildAsync(room.Id, confirmed, cancellationToken);
                    if (string.Equals(decision.Reason, "room-context-occupancy", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"{room.Name}: {string.Join(" ", explanation.Lines)}";
                    }

                    if (string.Equals(decision.Reason, "room-context-policy", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"{room.Name}: {string.Join(" ", explanation.Lines)} {(explanation.ExtraGuestCount > 0 ? $"Nếu đi {Math.Max(confirmed.GuestCount, 1)} khách thì phụ thu hiện tại là {explanation.ExtraGuestFeeApplied:N0}đ/khách." : "Nếu đi đúng tiêu chuẩn thì chưa phát sinh phụ thu thêm.")}";
                    }

                    if (string.Equals(decision.Reason, "room-context-price", StringComparison.OrdinalIgnoreCase))
                    {
                        var priceLine = string.Equals(confirmed.BookingMode, "daily", StringComparison.OrdinalIgnoreCase)
                            ? $"Giá tham chiếu hiện tại là {explanation.DisplayPrice:N0}đ/ngày."
                            : $"Giá tham chiếu hiện tại là {explanation.DisplayPrice:N0}đ/giờ.";
                        return $"{room.Name}: {priceLine} Giá niêm yết giờ {room.PricePerHour:N0}đ/h, ngày {room.PricePerDay:N0}đ/ngày. {string.Join(" ", explanation.Lines)}";
                    }
                }
            }
        }

        return decision.Action switch
        {
            ConductorAction.AskInfo when confirmed.MissingRequiredFields.Contains("branchId")
                => "Mình đã hiểu nhu cầu sơ bộ rồi. Bạn chọn chi nhánh giúp mình để mình lọc đúng phòng nhé.",
            ConductorAction.AskInfo when confirmed.MissingRequiredFields.Contains("checkInDate") || confirmed.MissingRequiredFields.Contains("checkOutDate")
                => "Mình cần ngày nhận và ngày trả phòng để kiểm tra đúng phòng trống cho bạn nhé.",
            ConductorAction.AskInfo when confirmed.MissingRequiredFields.Contains("hourlyDate")
                => confirmed.RequestedTimeLabel != null
                    ? $"Mình cần đúng ngày đặt để kiểm tra khung {confirmed.RequestedTimeLabel} cho bạn nhé."
                    : "Bạn cho mình ngày muốn đặt theo giờ để mình kiểm tra đúng khung còn trống nhé.",
            ConductorAction.AskInfo when string.Equals(decision.Reason, "room-context-slots-missing-date", StringComparison.OrdinalIgnoreCase)
                => "Mình đã hiểu phòng bạn đang hỏi rồi. Bạn chọn đúng ngày muốn xem khung giờ để mình kiểm tra chuẩn cho phòng đó nhé.",
            ConductorAction.ShowRooms when confirmed.RequestedTimeLabel != null
                => $"Mình đã lọc các phòng còn khung {confirmed.RequestedTimeLabel} cho {Math.Max(confirmed.GuestCount, 1)} khách. Bạn xem các lựa chọn phù hợp bên dưới nhé.",
            ConductorAction.ShowSlots when string.Equals(decision.Reason, "room-context-slots", StringComparison.OrdinalIgnoreCase)
                => "Mình đã lấy các khung giờ còn trống của đúng phòng bạn vừa hỏi. Bạn xem bên dưới nhé.",
            ConductorAction.ShowRooms when string.Equals(confirmed.BookingMode, "daily", StringComparison.OrdinalIgnoreCase)
                => $"Mình đã lọc các phòng phù hợp cho {Math.Max(confirmed.GuestCount, 1)} khách trong thời gian bạn chọn. Nếu phòng vượt chuẩn sức chứa thì mình đã kèm lưu ý phụ thu rõ ở thẻ phòng.",
            ConductorAction.ShowSlots
                => "Mình đã kiểm tra các khung giờ trống của phòng bạn chọn. Bạn xem các slot còn khả dụng bên dưới nhé.",
            _ => "Mình đang kiểm tra nhu cầu của bạn theo dữ liệu thật để tư vấn đúng nhất."
        };
    }

    public void Reset()
    {
        ShortCircuited = false;
        GuardAnswer = string.Empty;
    }
}
