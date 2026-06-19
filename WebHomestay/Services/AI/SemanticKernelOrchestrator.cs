using System.ClientModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI;
using WebHomestay.Data;
using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Services.AI.Plugins;

namespace WebHomestay.Services.AI
{
    public class SemanticKernelOrchestrator : IPublicBookingBrainOrchestrator
    {
        private readonly ApplicationDbContext _context;
        private readonly AIModelOptions _aiOptions;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SemanticKernelOrchestrator> _logger;
        private readonly IConversationManager _conversationManager;
        private readonly IAdminAIStudioConfigService _studioConfigService;
        private readonly IBookingConductor _bookingConductor;
        private readonly IPublicBookingRoomExplanationService _roomExplanationService;

        public SemanticKernelOrchestrator(
            ApplicationDbContext context,
            IOptions<AIModelOptions> aiOptions,
            IServiceProvider serviceProvider,
            ILogger<SemanticKernelOrchestrator> logger,
            IConversationManager conversationManager,
            IAdminAIStudioConfigService studioConfigService,
            IBookingConductor bookingConductor,
            IPublicBookingRoomExplanationService roomExplanationService)
        {
            _context = context;
            _aiOptions = aiOptions.Value;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _conversationManager = conversationManager;
            _studioConfigService = studioConfigService;
            _bookingConductor = bookingConductor;
            _roomExplanationService = roomExplanationService;
        }

        public async Task<AIBrainChatResponse> ChatAsync(AIBrainChatRequest request, CancellationToken cancellationToken = default)
        {
            var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId;
            var sessionState = await _conversationManager.GetOrCreateStateAsync(sessionId, cancellationToken);
            
            var stageLogs = new List<string>();

            // Stage 1: Deterministic Booking Stage
            var conductorDecision = await RunDeterministicBookingStageAsync(sessionId, request, sessionState, stageLogs, cancellationToken);

            var chatProfiles = AIProviderConfigResolver.GetChatProfiles(_aiOptions);

            // Inject plugins
            var bookingPlugin = ActivatorUtilities.CreateInstance<BookingPlugin>(_serviceProvider, sessionState, request);
            var embeddingService = _serviceProvider.GetRequiredService<WebHomestay.Services.AI.IEmbeddingService>();
            var vectorSearchService = _serviceProvider.GetRequiredService<WebHomestay.Services.AI.Retrieval.IVectorSearchService>();
            var retrievalContextAssembler = _serviceProvider.GetRequiredService<WebHomestay.Services.AI.Retrieval.RetrievalContextAssembler>();
            var knowledgePlugin = new WebHomestay.Services.AI.Plugins.KnowledgeGraphPlugin(_context, embeddingService, vectorSearchService, retrievalContextAssembler, sessionState, request);
            var semanticSearchPlugin = new WebHomestay.Services.AI.Plugins.SemanticSearchPlugin(_context, embeddingService, vectorSearchService, sessionState);
            
            var settings = await GetFinalSynthesizerConfigAsync(cancellationToken);
            await ApplyStudioConfigAsync(settings, cancellationToken);
            var studioConfig = request.Mode == ChatMode.PublicBooking
                ? await TryGetStudioConfigAsync(cancellationToken)
                : null;
            var studioFirstResponse = await TryBuildStudioFirstResponseAsync(request, settings, studioConfig, cancellationToken);
            if (studioFirstResponse != null)
            {
                stageLogs.Add("intent");
                stageLogs.Add("generation");
                var studioTrace = await PersistTraceStageAsync(
                    sessionId,
                    request.Message,
                    "Studio-first public booking",
                    "Studio-first deterministic reply",
                    "[]",
                    studioFirstResponse.Answer,
                    "studio-config",
                    stageLogs,
                    cancellationToken);
                studioFirstResponse.TraceId = studioTrace.Id;
                await _conversationManager.UpdateStateAsync(sessionId, sessionState, cancellationToken);
                return studioFirstResponse;
            }

            if (conductorDecision != null)
            {
                var shouldHandleDeterministically = conductorDecision.Action != ConductorAction.Reply
                    || IsDeterministicConductorReply(conductorDecision.Reason);
                if (shouldHandleDeterministically)
                {
                    stageLogs.Add("guard");
                    var answer = await BuildConductorReplyAsync(conductorDecision, cancellationToken);
                    var conductorResponse = new AIBrainChatResponse
                    {
                        Answer = answer,
                        PersonaSummary = conductorDecision.Reason ?? "conductor",
                        GuardResult = "DB truth gating",
                        ModelProvider = "context-aware-conductor",
                        IsMock = false,
                        FormSchema = "[]",
                        BookingAction = conductorDecision.Action.ToString(),
                        BookingState = sessionState,
                        UiBlocks = conductorDecision.UiBlocks
                    };
                    var conductorTrace = await PersistTraceStageAsync(
                        sessionId,
                        request.Message,
                        "Context-aware booking conductor",
                        conductorResponse.PersonaSummary,
                        "[]",
                        conductorResponse.Answer,
                        conductorResponse.ModelProvider,
                        stageLogs,
                        cancellationToken);
                    conductorResponse.TraceId = conductorTrace.Id;
                    await _conversationManager.UpdateStateAsync(sessionId, sessionState, cancellationToken);
                    return conductorResponse;
                }
            }

            var pluginFirstResponse = await TryBuildPluginFirstResponseAsync(
                request,
                sessionState,
                knowledgePlugin,
                stageLogs,
                cancellationToken);
            if (pluginFirstResponse != null)
            {
                var pluginTrace = await PersistTraceStageAsync(
                    sessionId,
                    request.Message,
                    "Plugin-first public booking",
                    pluginFirstResponse.PersonaSummary,
                    JsonSerializer.Serialize(new
                    {
                        vectorHits = JsonSerializer.Deserialize<object>(knowledgePlugin.RetrievedKnowledgeJson),
                        graphExpansion = JsonSerializer.Deserialize<object>(knowledgePlugin.GraphReasoningJson)
                    }),
                    pluginFirstResponse.Answer,
                    pluginFirstResponse.ModelProvider,
                    stageLogs,
                    cancellationToken);
                pluginFirstResponse.TraceId = pluginTrace.Id;
                await _conversationManager.UpdateStateAsync(sessionId, sessionState, cancellationToken);
                return pluginFirstResponse;
            }

            // Stage 2: Input Embedding Generation
            await TryBuildQueryEmbeddingAsync(request.Message, embeddingService, stageLogs, cancellationToken);

            // Stage 3: Retrieval Stage
            await RunRetrievalStageAsync(request.Message, knowledgePlugin, stageLogs, cancellationToken);

            // Stage 4: Generation/Composition Stage
            var systemPrompt = BuildSystemPrompt(settings, request.Mode);
            var answerContent = await RunGenerationStageAsync(
                chatProfiles,
                bookingPlugin,
                knowledgePlugin,
                semanticSearchPlugin,
                sessionId,
                request,
                systemPrompt,
                stageLogs,
                cancellationToken);

            var combinedRetrievalJson = JsonSerializer.Serialize(new
            {
                vectorHits = JsonSerializer.Deserialize<object>(knowledgePlugin.RetrievedKnowledgeJson),
                graphExpansion = JsonSerializer.Deserialize<object>(knowledgePlugin.GraphReasoningJson)
            });

            // Stage 5: Save trace and update state
            string provider = ResolveProvider();
            var trace = await PersistTraceStageAsync(
                sessionId,
                request.Message,
                "Semantic Kernel Managed",
                bookingPlugin.GetLogs(),
                combinedRetrievalJson,
                answerContent,
                provider,
                stageLogs,
                cancellationToken);

            // Update session state
            await _conversationManager.UpdateStateAsync(sessionId, sessionState, cancellationToken);

            return new AIBrainChatResponse
            {
                TraceId = trace.Id,
                Answer = answerContent,
                PersonaSummary = "Semantic Kernel",
                GuardResult = "Semantic Kernel",
                ModelProvider = provider,
                IsMock = false,
                FormSchema = "[]",
                BookingAction = bookingPlugin.GetAction() ?? "reply",
                BookingState = sessionState,
                UiBlocks = bookingPlugin.GetUiBlocks()
            };
        }

        private Kernel BuildKernel(AIProviderProfile profile)
        {
            var builder = Kernel.CreateBuilder();
            var endpoint = profile.Endpoint;
            if (endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = endpoint.Substring(0, endpoint.Length - "/chat/completions".Length);
            }

            if (!endpoint.EndsWith("/", StringComparison.Ordinal))
            {
                endpoint += "/";
            }

            var openAIClient = new OpenAIClient(new ApiKeyCredential(profile.ApiKey), new OpenAIClientOptions { Endpoint = new Uri(endpoint) });
            builder.AddOpenAIChatCompletion(profile.Model, openAIClient);

            return builder.Build();
        }

        private async Task<Microsoft.SemanticKernel.ChatCompletion.ChatHistory> BuildChatHistoryAsync(string sessionId, string systemPrompt, CancellationToken cancellationToken)
        {
            var history = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory(systemPrompt);
            var turns = await _context.AIConversationTraces
                .Where(t => t.SessionId == sessionId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(6)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            foreach (var turn in turns)
            {
                var userContent = turn.CustomerMessage;
                var contextBuilder = new System.Text.StringBuilder();

                if (!string.IsNullOrWhiteSpace(turn.LiveSystemSnapshot))
                {
                    contextBuilder.AppendLine($"[Hệ thống phản hồi từ database: {turn.LiveSystemSnapshot}]");
                }
                if (!string.IsNullOrWhiteSpace(turn.RetrievedKnowledgeJson))
                {
                    contextBuilder.AppendLine($"[Thông tin chính sách & địa điểm đã tìm: {turn.RetrievedKnowledgeJson}]");
                }

                if (contextBuilder.Length > 0)
                {
                    userContent = $"{contextBuilder.ToString()}User: {turn.CustomerMessage}";
                }

                history.AddUserMessage(userContent);
                history.AddAssistantMessage(turn.FinalAnswer);
            }

            return history;
        }

        private async Task<FinalSynthesizerPromptConfig> GetFinalSynthesizerConfigAsync(CancellationToken cancellationToken)
        {
            var settingsList = await _context.SystemSettings
                .Where(s => s.GroupName == "AI")
                .ToListAsync(cancellationToken);

            var settings = settingsList.ToDictionary(s => s.SettingKey, s => s.SettingValue);

            string GetSetting(string key, string fallback = "") =>
                settings.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val) ? val : fallback;

            return new FinalSynthesizerPromptConfig
            {
                Style = GetSetting("AIFinalSynthesizerStyle"),
                BasePrompt = GetSetting("AIFinalBasePrompt", "Bạn là trợ lý AI thông minh."),
                LanguageRule = GetSetting("AIFinalLanguageRule"),
                DataTruthRule = GetSetting("AIFinalDataTruthRule"),
                MissingInfoRule = GetSetting("AIFinalMissingInfoRule"),
                BookingRule = GetSetting("AIFinalBookingRule"),
                FormRule = GetSetting("AIFinalFormRule"),
                PaymentRule = GetSetting("AIFinalPaymentRule"),
                MemoryRule = GetSetting("AIFinalMemoryRule"),
                ContextFormatRule = GetSetting("AIFinalContextFormatRule"),
                PublicBookingPrompt = GetSetting("AIPublicBookingPrompt"),
                PublicBookingPersonality = GetSetting("AIPublicBookingPersonality")
            };
        }

        private async Task ApplyStudioConfigAsync(FinalSynthesizerPromptConfig config, CancellationToken cancellationToken)
        {
            try
            {
                var studioConfig = await _studioConfigService.GetAsync();
                config.Style = FirstText(studioConfig.AssistantProfile.Tone, config.Style);
                config.BasePrompt = FirstText(studioConfig.AssistantProfile.RolePrompt, config.BasePrompt);
                config.MissingInfoRule = FirstText(studioConfig.AssistantProfile.MissingInfoPrompt, config.MissingInfoRule);
                config.DataTruthRule = FirstText(studioConfig.AssistantProfile.SafetyPrompt, config.DataTruthRule);
                config.PublicBookingPersonality = FirstText(
                    studioConfig.Handoff.Enabled ? studioConfig.Handoff.ContactInstruction : string.Empty,
                    config.PublicBookingPersonality);

                var flowSummary = string.Join("; ", studioConfig.ConversationFlows
                    .Where(flow => flow.Enabled)
                    .OrderBy(flow => flow.Priority)
                    .Select(flow => $"{flow.Id}: {flow.Description}"));
                if (!string.IsNullOrWhiteSpace(flowSummary))
                {
                    config.PublicBookingPrompt = FirstText(config.PublicBookingPrompt, string.Empty)
                        + $"\n[FLOW SALE-ASSIST TỪ ADMIN]\n{flowSummary}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Cannot apply Admin AI studio config to public prompt: {ErrorMessage}", ex.Message);
            }
        }

        private async Task<AdminAIStudioConfigResponse?> TryGetStudioConfigAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _studioConfigService.GetAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Cannot load Admin AI studio config for public reply: {ErrorMessage}", ex.Message);
                return null;
            }
        }

        private async Task<AIBrainChatResponse?> TryBuildStudioFirstResponseAsync(
            AIBrainChatRequest request,
            FinalSynthesizerPromptConfig promptConfig,
            AdminAIStudioConfigResponse? studioConfig,
            CancellationToken cancellationToken)
        {
            if (request.Mode != ChatMode.PublicBooking || studioConfig == null)
            {
                return null;
            }

            if (!IsBookingStartIntent(request.Message))
            {
                return null;
            }

            if (ShouldSkipStudioFirst(request.Message))
            {
                return null;
            }

            var hasConcreteBookingContext = request.BranchId.HasValue
                || request.StartTime.HasValue
                || request.EndTime.HasValue;
            if (hasConcreteBookingContext)
            {
                return null;
            }

            var configuredReply = FirstText(
                studioConfig.AssistantProfile.MissingInfoPrompt,
                promptConfig.MissingInfoRule,
                "Mình sẽ hỏi từng bước để lấy đúng thông tin còn thiếu. Bạn muốn đặt theo giờ hay theo ngày ạ?");
            var reply = ToCustomerSafeReply(configuredReply);
            var branches = await GetPublicBranchesAsync(cancellationToken);

            return new AIBrainChatResponse
            {
                Answer = reply,
                PersonaSummary = "Studio-first",
                GuardResult = "Studio-first",
                ModelProvider = "studio-config",
                IsMock = false,
                FormSchema = "[]",
                BookingAction = "entry",
                BookingState = new AIBookingSessionState(),
                UiBlocks =
                [
                    new
                    {
                        type = "bookingModeChoice",
                        data = new
                        {
                            label = "Chọn hình thức đặt phòng",
                            branches,
                            flows = studioConfig.ConversationFlows
                                .Where(flow => flow.Enabled && (flow.Id == "hourly" || flow.Id == "daily"))
                                .OrderBy(flow => flow.Priority)
                                .Select(flow => new
                                {
                                    id = flow.Id,
                                    name = FirstText(flow.Name, flow.Label, flow.Id),
                                    description = flow.Description
                                })
                                .ToList()
                        }
                    }
                ]
            };
        }

        private async Task<AIBrainChatResponse?> TryBuildConductorResponseAsync(
            string sessionId,
            AIBookingSessionState sessionState,
            AIBrainChatRequest request,
            CancellationToken cancellationToken)
        {
            if (request.Mode != ChatMode.PublicBooking)
            {
                return null;
            }

            var decision = await _bookingConductor.DecideAsync(sessionId, request.Message, request, cancellationToken);
            var shouldHandleDeterministically = decision.Action != ConductorAction.Reply
                || IsDeterministicConductorReply(decision.Reason);
            if (!shouldHandleDeterministically)
            {
                return null;
            }

            ApplyConductorStateToSession(sessionState, decision.State);
            var answer = await BuildConductorReplyAsync(decision, cancellationToken);

            return new AIBrainChatResponse
            {
                Answer = answer,
                PersonaSummary = decision.Reason ?? "conductor",
                GuardResult = "DB truth gating",
                ModelProvider = "context-aware-conductor",
                IsMock = false,
                FormSchema = "[]",
                BookingAction = decision.Action.ToString(),
                BookingState = decision.State,
                UiBlocks = decision.UiBlocks
            };
        }

        private static bool IsDeterministicConductorReply(string? reason)
        {
            return string.Equals(reason, "room-context-occupancy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason, "room-context-price", StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason, "room-context-policy", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> BuildConductorReplyAsync(ConductorResult decision, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(decision.Answer))
            {
                return decision.Answer;
            }

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
                    => confirmed.MissingRequiredFields.Contains("guestCount")
                        || confirmed.MissingRequiredFields.Contains("hourlyDate")
                        || confirmed.MissingRequiredFields.Contains("bookingMode")
                        || confirmed.MissingRequiredFields.Contains("checkInDate")
                        || confirmed.MissingRequiredFields.Contains("checkOutDate")
                            ? "Mình đã hiểu nhu cầu sơ bộ rồi. Để lọc đúng phòng phù hợp, mình cần thêm chi nhánh, thời gian dự kiến và số khách của bạn nhé."
                            : "Mình đã hiểu nhu cầu sơ bộ rồi. Bạn chọn chi nhánh giúp mình để mình lọc đúng phòng nhé.",
                ConductorAction.AskInfo when confirmed.MissingRequiredFields.Contains("bookingMode")
                    => "Mình đã ghi nhận chi nhánh rồi. Bạn chọn giúp mình hình thức đặt theo giờ hay theo ngày nhé.",
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

        private static void ApplyConductorStateToSession(AIBookingSessionState sessionState, BookingSessionContainer state)
        {
            var confirmed = state.Confirmed;
            var progress = state.Progress;
            sessionState.BranchId = confirmed.BranchId;
            sessionState.BranchName = confirmed.BranchName;
            sessionState.BookingMode = confirmed.BookingMode;
            sessionState.HourlyDate = confirmed.HourlyDate;
            sessionState.CheckInDate = confirmed.CheckInDate;
            sessionState.CheckOutDate = confirmed.CheckOutDate;
            sessionState.GuestCount = Math.Max(confirmed.GuestCount, 1);
            sessionState.SelectedRoomId = progress?.SelectedRoomId;
            sessionState.SelectedSlotId = progress?.SelectedSlotId;
            sessionState.RequestedTimeStart = confirmed.RequestedTimeStart?.ToString("HH:mm");
            sessionState.RequestedTimeEnd = confirmed.RequestedTimeEnd?.ToString("HH:mm");
            sessionState.RequestedTimeLabel = confirmed.RequestedTimeLabel;
            sessionState.MissingRequiredFields = [.. confirmed.MissingRequiredFields];
            sessionState.HasExtraGuestSurcharge = confirmed.HasExtraGuestSurcharge;
            sessionState.NeedsWeekendPricingExplanation = confirmed.NeedsWeekendPricingExplanation;
            sessionState.NeedsHolidayPricingExplanation = confirmed.NeedsHolidayPricingExplanation;
            sessionState.LastRecommendationReason = confirmed.LastRecommendationReason;
            sessionState.SemanticPreference = confirmed.SemanticPreference;
            sessionState.ActiveRoomContextId = progress?.ActiveRoomContextId;
        }

        private async Task<List<object>> GetPublicBranchesAsync(CancellationToken cancellationToken)
        {
            var branches = await _context.Branches
                .OrderBy(branch => branch.Id)
                .Select(branch => new
                {
                    id = branch.Id,
                    name = branch.Name
                })
                .ToListAsync(cancellationToken);
            return branches.Cast<object>().ToList();
        }

        private static bool IsBookingStartIntent(string message)
        {
            var text = (message ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var bookingWords = new[] { "đặt phòng", "dat phong", "book phòng", "book phong", "muốn đặt", "muon dat", "lấy phòng", "lay phong" };
            return bookingWords.Any(word => text.Contains(word))
                && !text.Contains("hủy")
                && !text.Contains("huy");
        }

        private async Task<ConductorResult?> RunDeterministicBookingStageAsync(
            string sessionId,
            AIBrainChatRequest request,
            AIBookingSessionState sessionState,
            List<string> stageLogs,
            CancellationToken cancellationToken)
        {
            stageLogs.Add("intent");
            if (request.Mode == ChatMode.PublicBooking)
            {
                var decision = await _bookingConductor.DecideAsync(sessionId, request.Message, request, cancellationToken);
                ApplyConductorStateToSession(sessionState, decision.State);
                return decision;
            }
            return null;
        }

        private async Task<float[]?> TryBuildQueryEmbeddingAsync(
            string query,
            IEmbeddingService embeddingService,
            List<string> stageLogs,
            CancellationToken cancellationToken)
        {
            stageLogs.Add("retrieval");
            try
            {
                return await embeddingService.GetEmbeddingAsync(query, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Embedding generation failed in orchestrator pre-warm: {Message}", ex.Message);
                return null;
            }
        }

        private Task<string> RunRetrievalStageAsync(
            string query,
            KnowledgeGraphPlugin knowledgePlugin,
            List<string> stageLogs,
            CancellationToken cancellationToken)
        {
            if (!stageLogs.Contains("retrieval"))
            {
                stageLogs.Add("retrieval");
            }
            return Task.FromResult("retrieval-ready");
        }

        private async Task<string> RunGenerationStageAsync(
            IReadOnlyList<AIProviderProfile> chatProfiles,
            BookingPlugin bookingPlugin,
            WebHomestay.Services.AI.Plugins.KnowledgeGraphPlugin knowledgePlugin,
            WebHomestay.Services.AI.Plugins.SemanticSearchPlugin semanticSearchPlugin,
            string sessionId,
            AIBrainChatRequest request,
            string systemPrompt,
            List<string> stageLogs,
            CancellationToken cancellationToken)
        {
            stageLogs.Add("guard");
            stageLogs.Add("generation");
            stageLogs.Add("composition");

            var history = await BuildChatHistoryAsync(sessionId, systemPrompt, cancellationToken);
            history.AddUserMessage(request.Message);

            foreach (var profile in chatProfiles)
            {
                var kernel = BuildKernel(profile);
                kernel.Plugins.AddFromObject(bookingPlugin, "BookingPlugin");
                kernel.Plugins.AddFromObject(knowledgePlugin, "KnowledgeGraphPlugin");
                kernel.Plugins.AddFromObject(semanticSearchPlugin, "SemanticSearchPlugin");
                var executionSettings = new OpenAIPromptExecutionSettings
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 0.35,
                    MaxTokens = request.Mode == ChatMode.PublicBooking ? 1000 : 1500
                };

                var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

                try
                {
                    var result = await chatCompletionService.GetChatMessageContentAsync(
                        history,
                        executionSettings,
                        kernel,
                        cancellationToken);

                    return result.Content ?? string.Empty;
                }
                catch (Exception ex) when (profile != chatProfiles.Last() && AIProviderConfigResolver.ShouldFailover(ex.Message))
                {
                    _logger.LogWarning(ex, "Primary chat provider {Provider} failed, trying fallback provider.", profile.Provider);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Semantic Kernel execution failed: {ErrorType} - {ErrorMessage}", ex.GetType().Name, ex.Message);
                    return "Hiện tại hệ thống AI đang hơi bận nên mình chưa diễn đạt chi tiết được. Bạn cứ cho mình biết chi nhánh, ngày dự kiến và số khách, mình vẫn tiếp tục hỗ trợ bạn theo dữ liệu thật nhé.";
                }
            }

            return "Hiện tại hệ thống AI đang hơi bận nên mình chưa diễn đạt chi tiết được. Bạn cứ cho mình biết chi nhánh, ngày dự kiến và số khách, mình vẫn tiếp tục hỗ trợ bạn theo dữ liệu thật nhé.";
        }

        private async Task<AIConversationTrace> PersistTraceStageAsync(
            string sessionId,
            string customerMessage,
            string personaSummary,
            string liveSystemSnapshot,
            string retrievedKnowledgeJson,
            string finalAnswer,
            string provider,
            List<string> stageLogs,
            CancellationToken cancellationToken)
        {
            var trace = new AIConversationTrace
            {
                SessionId = sessionId,
                CustomerMessage = customerMessage,
                PersonaSummary = personaSummary,
                LiveSystemSnapshot = liveSystemSnapshot,
                RetrievedKnowledgeJson = retrievedKnowledgeJson,
                GraphReasoningJson = liveSystemSnapshot,
                GuardResult = personaSummary,
                FinalAnswer = finalAnswer,
                ModelProvider = provider,
                PerformanceLog = string.Join(",", stageLogs),
                RuntimeName = "semantic-kernel",
                RuntimeVersion = "legacy"
            };

            _context.AIConversationTraces.Add(trace);
            await _context.SaveChangesAsync(cancellationToken);
            return trace;
        }

        private string ResolveProvider()
        {
            var provider = string.IsNullOrWhiteSpace(_aiOptions.Provider)
                ? "gemini"
                : _aiOptions.Provider.Trim().ToLowerInvariant();

            if (string.Equals(provider, "gemma4", StringComparison.OrdinalIgnoreCase))
            {
                return "gemini";
            }

            return provider;
        }

        private string BuildSystemPrompt(FinalSynthesizerPromptConfig config, ChatMode mode)
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine($"[THÔNG TIN HỆ THỐNG]\nHôm nay là: {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}. Khách hàng thường sẽ nói ngày tháng trong năm hiện tại ({DateTime.Now.Year}). Nếu khách nói 'ngày mai', 'hôm nay', 'mùng 10 tháng 6' thì phải dùng năm hiện tại để tính toán ra định dạng YYYY-MM-DD.");
            builder.AppendLine(config.BasePrompt);
            
            if (mode == ChatMode.PublicBooking)
            {
                builder.AppendLine(config.PublicBookingPrompt);
                builder.AppendLine($"Tính cách: {config.PublicBookingPersonality}");
            }

            builder.AppendLine("\n[QUY TẮC CƠ BẢN]");
            if (!string.IsNullOrWhiteSpace(config.Style)) builder.AppendLine($"- {config.Style}");
            if (!string.IsNullOrWhiteSpace(config.LanguageRule)) builder.AppendLine($"- {config.LanguageRule}");
            if (!string.IsNullOrWhiteSpace(config.DataTruthRule)) builder.AppendLine($"- {config.DataTruthRule}");
            if (!string.IsNullOrWhiteSpace(config.MissingInfoRule)) builder.AppendLine($"- {config.MissingInfoRule}");
            if (!string.IsNullOrWhiteSpace(config.BookingRule)) builder.AppendLine($"- {config.BookingRule}");

            builder.AppendLine("\n[HƯỚNG DẪN SỬ DỤNG HÀM/PLUGINS]");
            builder.AppendLine("- LUÔN LUÔN gọi các hàm này để lấy dữ liệu thực tế từ RAG và Graph của hệ thống trước khi trả lời. Tuyệt đối không tự suy đoán thông tin.");
            builder.AppendLine("- NẾU khách hàng YÊU CẦU ĐẶT PHÒNG NHƯNG CHƯA CUNG CẤP ĐỦ THÔNG TIN (chi nhánh, ngày nhận/trả, số khách), hãy CHỦ ĐỘNG ĐẶT CÂU HỎI để thu thập đủ thông tin. Không tự ý đoán chi nhánh.");
            builder.AppendLine("- Để tìm phòng trống, hãy gọi hàm 'BookingPlugin_CheckAvailability'. KHÔNG tự bịa phòng trống.");
            builder.AppendLine("- Nếu khách muốn xem KHUNG GIỜ của một phòng cụ thể để thuê THEO GIỜ, HÃY GỌI HÀM 'BookingPlugin_CheckHourlySlots'. TUYỆT ĐỐI KHÔNG TỰ BỊA RA KHUNG GIỜ.");
            builder.AppendLine("- Để tìm kiếm hoặc đề xuất phòng dựa trên mô tả nhu cầu, tiện ích, sở thích hoặc phong cách bằng ngôn ngữ tự nhiên, hãy gọi hàm 'SemanticSearchPlugin_SemanticSearchRooms'. Sau khi tìm thấy phòng, bạn LUÔN LUÔN phải gọi hàm 'BookingPlugin_ShowRoomCards' (truyền ID phòng) để hiển thị thẻ phòng trực quan lên màn hình cho khách.");
            builder.AppendLine("- Để tìm thông tin nội quy, chính sách chung của homestay, hãy gọi hàm 'KnowledgeGraphPlugin_SearchPolicies'.");
            builder.AppendLine("- Nếu khách hàng gặp sự cố, wifi, thiết bị tại phòng hoặc cần hướng dẫn check-in, hãy gọi hàm 'KnowledgeGraphPlugin_SearchRoomOperationManual'.");
            builder.AppendLine("- Để trả lời câu hỏi về địa điểm xung quanh chi nhánh, hãy gọi hàm 'KnowledgeGraphPlugin_SearchLocationGraph'.");
            builder.AppendLine("- Để tạo link thanh toán chốt phòng, hãy gọi hàm 'BookingPlugin_GenerateCheckoutLink'.");
            
            return builder.ToString();
        }

        private static string FirstText(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static string ToCustomerSafeReply(string configuredReply)
        {
            var text = (configuredReply ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return "Mình sẽ hỏi từng bước để lấy đúng thông tin còn thiếu. Bạn muốn đặt theo giờ hay theo ngày ạ?";
            }

            var looksLikeInternalRule =
                text.Contains("AvailableSlotOptions", StringComparison.OrdinalIgnoreCase)
                || text.Contains("UiBlock", StringComparison.OrdinalIgnoreCase)
                || text.Contains("fieldKey", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Không hỏi ngân sách", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Hỏi đúng thông tin", StringComparison.OrdinalIgnoreCase);

            return looksLikeInternalRule
                ? "Mình sẽ tư vấn theo đúng cấu hình đặt phòng. Bạn chọn hình thức đặt phòng trước nhé."
                : text;
        }

        private async Task<AIBrainChatResponse?> TryBuildPluginFirstResponseAsync(
            AIBrainChatRequest request,
            AIBookingSessionState sessionState,
            KnowledgeGraphPlugin knowledgePlugin,
            List<string> stageLogs,
            CancellationToken cancellationToken)
        {
            if (request.Mode != ChatMode.PublicBooking)
            {
                return null;
            }

            var normalized = (request.Message ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            if (IsLocationQuestion(normalized))
            {
                stageLogs.Add("retrieval");
                stageLogs.Add("composition");
                var branchAnswer = await TryBuildBranchLocationAnswerAsync(normalized, cancellationToken);
                if (!string.IsNullOrWhiteSpace(branchAnswer))
                {
                    return new AIBrainChatResponse
                    {
                        Answer = branchAnswer,
                        PersonaSummary = "branch-location-direct",
                        GuardResult = "db-direct",
                        ModelProvider = "branch-db-direct",
                        IsMock = false,
                        FormSchema = "[]",
                        BookingAction = "reply",
                        BookingState = sessionState,
                        UiBlocks = []
                    };
                }

                var raw = await knowledgePlugin.SearchLocationGraph(normalized);
                return new AIBrainChatResponse
                {
                    Answer = BuildLocationFallbackAnswer(raw),
                    PersonaSummary = "location-plugin",
                    GuardResult = "graph-direct",
                    ModelProvider = "knowledge-graph-direct",
                    IsMock = false,
                    FormSchema = "[]",
                    BookingAction = "reply",
                    BookingState = sessionState,
                    UiBlocks = []
                };
            }

            if (IsPolicyQuestion(normalized))
            {
                stageLogs.Add("retrieval");
                stageLogs.Add("composition");
                var raw = await knowledgePlugin.SearchPolicies(normalized);
                return new AIBrainChatResponse
                {
                    Answer = BuildPolicyFallbackAnswer(raw),
                    PersonaSummary = "policy-plugin",
                    GuardResult = "rag-direct",
                    ModelProvider = "knowledge-rag-direct",
                    IsMock = false,
                    FormSchema = "[]",
                    BookingAction = "reply",
                    BookingState = sessionState,
                    UiBlocks = []
                };
            }

            return null;
        }

        private static bool ShouldSkipStudioFirst(string message)
        {
            var lowered = (message ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lowered))
            {
                return false;
            }

            return Workflow.SemanticQueryHelper.IsSemanticQuery(lowered)
                || IsLocationQuestion(lowered)
                || IsPolicyQuestion(lowered);
        }

        private static bool IsLocationQuestion(string message)
        {
            var lowered = message.ToLowerInvariant();
            return lowered.Contains("địa chỉ")
                || lowered.Contains("dia chi")
                || lowered.Contains("ở đâu")
                || lowered.Contains("o dau")
                || lowered.Contains("gần")
                || lowered.Contains("gan")
                || lowered.Contains("quận")
                || lowered.Contains("quan")
                || lowered.Contains("chi nhánh")
                || lowered.Contains("chi nhanh");
        }

        private static bool IsPolicyQuestion(string message)
        {
            var lowered = message.ToLowerInvariant();
            return lowered.Contains("chính sách")
                || lowered.Contains("chinh sach")
                || lowered.Contains("hủy")
                || lowered.Contains("huy")
                || lowered.Contains("check-in")
                || lowered.Contains("check in")
                || lowered.Contains("check-out")
                || lowered.Contains("check out")
                || lowered.Contains("wifi")
                || lowered.Contains("mật khẩu")
                || lowered.Contains("mat khau")
                || lowered.Contains("nội quy")
                || lowered.Contains("noi quy");
        }

        private static string BuildLocationFallbackAnswer(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("Không tìm thấy", StringComparison.OrdinalIgnoreCase))
            {
                return "Mình chưa thấy dữ liệu địa điểm khớp hoàn toàn trong hệ thống. Bạn nói rõ chi nhánh hoặc khu vực cần hỏi, mình kiểm tra tiếp cho bạn nhé.";
            }

            var payload = ExtractJsonSegment(raw);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return "Mình đã kiểm tra dữ liệu chi nhánh nhưng chưa gom lại được câu trả lời gọn. Bạn nói rõ tên chi nhánh giúp mình nhé.";
            }

            try
            {
                using var doc = JsonDocument.Parse(payload);
                var nodes = doc.RootElement.TryGetProperty("Nodes", out var nodesEl) && nodesEl.ValueKind == JsonValueKind.Array
                    ? nodesEl.EnumerateArray()
                        .Select(node =>
                        {
                            var label = node.TryGetProperty("Label", out var labelEl) ? labelEl.GetString() : null;
                            var summary = node.TryGetProperty("Summary", out var summaryEl) ? summaryEl.GetString() : null;
                            return string.Join(": ", new[] { label, summary }.Where(value => !string.IsNullOrWhiteSpace(value)));
                        })
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .Take(3)
                        .ToList()
                    : [];

                if (nodes.Count > 0)
                {
                    return "Mình tìm được thông tin liên quan:\n- " + string.Join("\n- ", nodes);
                }
            }
            catch
            {
            }

            return "Mình đã kiểm tra dữ liệu địa điểm liên quan nhưng cần bạn nói rõ thêm tên chi nhánh để trả lời chính xác nhất.";
        }

        private async Task<string?> TryBuildBranchLocationAnswerAsync(string message, CancellationToken cancellationToken)
        {
            var normalized = message.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var branches = await _context.Branches
                .AsNoTracking()
                .OrderBy(branch => branch.Id)
                .Select(branch => new
                {
                    branch.Name,
                    branch.Address,
                    branch.MapUrl
                })
                .ToListAsync(cancellationToken);

            var expandedNormalized = ExpandLocationAliases(normalized);
            var tokens = expandedNormalized
                .Split(new[] { ' ', ',', '.', '-', '_', ':', ';', '/', '\\', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(token => token.Length >= 2 && token is not "dia" and not "chi" and not "địa" and not "chỉ" and not "chi" and not "nhanh" and not "nhánh")
                .ToArray();

            var matchedBranches = branches
                .Where(branch =>
                {
                    var haystack = ExpandLocationAliases($"{branch.Name} {branch.Address}".ToLowerInvariant());
                    return haystack.Contains(expandedNormalized)
                        || tokens.Any(token => haystack.Contains(token));
                })
                .Take(3)
                .ToList();

            if (matchedBranches.Count == 0)
            {
                return null;
            }

            return "Mình tìm thấy chi nhánh phù hợp:\n- " + string.Join("\n- ", matchedBranches.Select(branch =>
            {
                var mapText = string.IsNullOrWhiteSpace(branch.MapUrl) ? string.Empty : $" | Bản đồ: {branch.MapUrl}";
                return $"{branch.Name}: {branch.Address}{mapText}";
            }));
        }

        private static string ExpandLocationAliases(string text)
        {
            return text
                .Replace("q7", "quan 7 quận 7", StringComparison.OrdinalIgnoreCase)
                .Replace("q1", "quan 1 quận 1", StringComparison.OrdinalIgnoreCase)
                .Replace("q.", "quan ", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildPolicyFallbackAnswer(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("Không tìm thấy", StringComparison.OrdinalIgnoreCase))
            {
                return "Mình chưa thấy đúng chính sách này trong dữ liệu hiện tại. Bạn nói rõ hơn câu hỏi cần kiểm tra giúp mình nhé.";
            }

            var payload = ExtractJsonSegment(raw);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return "Mình đã tìm trong dữ liệu chính sách nhưng chưa gom lại được câu trả lời ngắn gọn. Bạn nói rõ nội dung cần kiểm tra giúp mình nhé.";
            }

            try
            {
                using var doc = JsonDocument.Parse(payload);
                var firstGroup = doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0
                    ? doc.RootElement[0]
                    : doc.RootElement;

                if (firstGroup.TryGetProperty("Matches", out var matchesEl) && matchesEl.ValueKind == JsonValueKind.Array)
                {
                    var lines = matchesEl.EnumerateArray()
                        .Select(match =>
                        {
                            var title = match.TryGetProperty("Title", out var titleEl) ? titleEl.GetString() : null;
                            var content = match.TryGetProperty("Content", out var contentEl) ? contentEl.GetString() : null;
                            var summary = string.IsNullOrWhiteSpace(content) ? title : $"{title}: {content}";
                            return summary?.Trim();
                        })
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .Take(2)
                        .ToList();

                    if (lines.Count > 0)
                    {
                        return string.Join("\n", lines);
                    }
                }
            }
            catch
            {
            }

            return "Mình đã kiểm tra dữ liệu chính sách nhưng cần bạn nói rõ hơn câu hỏi để trả lời chuẩn hơn.";
        }

        private static string? ExtractJsonSegment(string raw)
        {
            var start = raw.IndexOf('{');
            if (start < 0)
            {
                start = raw.IndexOf('[');
            }

            if (start < 0)
            {
                return null;
            }

            var endBrace = raw.LastIndexOf('}');
            var endBracket = raw.LastIndexOf(']');
            var end = Math.Max(endBrace, endBracket);
            if (end <= start)
            {
                return null;
            }

            return raw[start..(end + 1)];
        }
    }
}
