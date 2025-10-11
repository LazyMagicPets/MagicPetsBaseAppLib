namespace BaseApp.ViewModels;

using LazyMagic.Client.FactoryGenerator;
using System.Reactive.Linq;

[Factory]
public class ChatViewModel : LzItemViewModel<Chat, ChatModel>
{
    private readonly IChatModuleClient? _chatApi;
    private readonly IChatMessagesViewModelFactory? _chatMessagesViewModelFactory;
    private readonly IChatEventsService? _chatEventsService;
    private string? _subscriptionId;
    private IDisposable? _eventSubscription;

    public ChatViewModel(
        [FactoryInject] ILoggerFactory loggerFactory,
        [FactoryInject] IChatModuleClient? chatApi,
        [FactoryInject] IChatMessagesViewModelFactory? chatMessagesViewModelFactory,
        [FactoryInject] IChatEventsService? chatEventsService,
        ILzParentViewModel parentViewModel,
        Chat chat,
        bool? isLoaded = null
        ) : base(loggerFactory, chat, model: null, isLoaded)
    {
        ParentViewModel = parentViewModel;
        _chatApi = chatApi;
        _chatMessagesViewModelFactory = chatMessagesViewModelFactory;
        _chatEventsService = chatEventsService;

        if (chatApi != null)
        {
            _DTOReadAsync = chatApi.ChatModuleGetChatByIdAsync;
            _DTOCreateAsync = chatApi.ChatModuleAddChatAsync;
            _DTOUpdateAsync = async (chat) => await chatApi.ChatModuleUpdateChatAsync(chat);
            _DTODeleteAsync = chatApi.ChatModuleDeleteChatAsync;
        }

        // Initialize messages ViewModel
        if (_chatMessagesViewModelFactory != null && !string.IsNullOrEmpty(chat?.Id))
        {
            MessagesViewModel = _chatMessagesViewModelFactory.Create();
            MessagesViewModel.ChatId = chat.Id;
        }
    }

    public override string Id => Data?.Id ?? string.Empty;
    public override long UpdatedAt => Data?.UpdateUtcTick ?? long.MaxValue;

    public ChatMessagesViewModel? MessagesViewModel { get; private set; }

    public async Task<(bool, string)> SendMessageAsync(string messageContent)
    {
        if (_chatApi == null || string.IsNullOrEmpty(Id))
            return (false, "Chat API not available");

        try
        {
            var chatMessage = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                ChatId = Id,
                Role = ChatMessageRole.User,
                Content = messageContent,
                Timestamp = DateTimeOffset.UtcNow
            };

            var result = await _chatApi.ChatModuleAddChatMessageAsync(Id, chatMessage);

            // Don't reload immediately - let events handle updates
            // If events aren't working, we'll add fallback polling later

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Initialize event subscriptions for this chat.
    /// Called automatically when ViewModel is loaded (override LoadAsync to call this).
    /// </summary>
    public async Task InitializeEventsAsync()
    {
        if (_chatEventsService == null)
        {
            _logger.LogWarning("ChatEventsService not available for chat: {ChatId}", Id);
            return;
        }

        if (!string.IsNullOrEmpty(_subscriptionId))
        {
            _logger.LogDebug("Already subscribed to chat events for: {ChatId}", Id);
            return;
        }

        // Initialize the service if not already initialized
        // This handles race condition if ChatPage calls this before ChatsViewModel finishes initialization
        if (!_chatEventsService.IsInitialized)
        {
            _logger.LogDebug("ChatEventsService not yet initialized, initializing now for chat: {ChatId}", Id);
            var initialized = await _chatEventsService.InitializeAsync();

            if (!initialized)
            {
                _logger.LogWarning("Failed to initialize ChatEventsService for chat: {ChatId}", Id);
                return;
            }
        }

        try
        {
            // Subscribe to the chat channel
            _subscriptionId = await _chatEventsService.SubscribeToChatAsync(Id);

            if (_subscriptionId != null)
            {
                // Subscribe to events for this specific chat using ReactiveUI
                _eventSubscription = _chatEventsService.ChatEvents
                    .Where(e => e.ChatId == Id)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(async eventArgs =>
                    {
                        await HandleChatEventAsync(eventArgs);
                    });

                _logger.LogInformation("ChatViewModel: Subscribed to events for chat: {ChatId}", Id);
            }
            else
            {
                _logger.LogWarning("Failed to subscribe to chat events for: {ChatId}", Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to chat events for: {ChatId}", Id);
        }
    }

    private async Task HandleChatEventAsync(ChatEventReceivedEventArgs eventArgs)
    {
        if (MessagesViewModel == null)
            return;

        switch (eventArgs.EventType)
        {
            case "Message_received":
            case "Message_completed":
                // Reload messages when new messages arrive
                _logger.LogInformation("Reloading messages for chat {ChatId} due to event: {EventType}", Id, eventArgs.EventType);
                await MessagesViewModel.ReadAsync(forceload: true);
                break;

            case "Message_streaming":
                // Could update a "typing indicator" here
                _logger.LogDebug("Streaming message for chat: {ChatId}", Id);
                break;

            case "Error_occurred":
                // Error handling could be added here
                _logger.LogWarning("Error occurred for chat: {ChatId}", Id);
                break;
        }
    }

    /// <summary>
    /// Unsubscribe from chat events when ViewModel is disposed
    /// </summary>
    public override void Dispose()
    {
        _eventSubscription?.Dispose();

        if (!string.IsNullOrEmpty(_subscriptionId) && _chatEventsService != null)
        {
            _logger.LogInformation("Unsubscribing from chat events for: {ChatId}", Id);
            // Fire and forget - don't wait for unsubscribe
            _ = _chatEventsService.UnsubscribeFromChatAsync(Id);
            _subscriptionId = null;
        }

        base.Dispose();
    }
}
