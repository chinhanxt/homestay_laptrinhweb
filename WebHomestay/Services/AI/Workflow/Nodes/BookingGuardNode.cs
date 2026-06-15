using WebHomestay.Models.AI;

namespace WebHomestay.Services.AI.Workflow.Nodes;

public sealed class BookingGuardNode : IWorkflowNode
{
    private readonly IBookingConductor _bookingConductor;
    private readonly IConversationManager _conversationManager;
    private readonly ILogger<BookingGuardNode> _logger;

    public BookingGuardNode(
        IBookingConductor bookingConductor,
        IConversationManager conversationManager,
        ILogger<BookingGuardNode> logger)
    {
        _bookingConductor = bookingConductor;
        _conversationManager = conversationManager;
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

            if (decision.Action != ConductorAction.Reply)
            {
                ShortCircuited = true;
                GuardAnswer = decision.Action switch
                {
                    ConductorAction.AskInfo => "Vui lòng cung cấp thêm thông tin để tôi hỗ trợ đặt phòng.",
                    ConductorAction.ShowRooms => "Đang hiển thị danh sách phòng phù hợp...",
                    ConductorAction.ShowSlots => "Đang hiển thị khung giờ trống...",
                    ConductorAction.AutoBook => "Đang tiến hành đặt phòng...",
                    _ => "Đang xử lý yêu cầu của bạn..."
                };

                state.FinalAnswer = GuardAnswer;
                _logger.LogInformation("Booking guard short-circuited with action {Action} for session {SessionId}",
                    decision.Action, state.SessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Booking guard failed for session {SessionId}", state.SessionId);
        }
    }

    public void Reset()
    {
        ShortCircuited = false;
        GuardAnswer = string.Empty;
    }
}
