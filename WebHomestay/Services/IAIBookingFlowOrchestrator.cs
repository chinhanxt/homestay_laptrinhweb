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
