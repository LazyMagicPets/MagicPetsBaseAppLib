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
                // User message received - reload to show it
                _logger.LogInformation("Reloading messages for chat {ChatId} due to user message", Id);
                await MessagesViewModel.ReadAsync(forceload: true);
                break;

            case "Message_processing":
                // Assistant is starting to process - could show a "thinking" indicator
                _logger.LogDebug("Assistant processing started for chat: {ChatId}", Id);
                await HandleProcessingStartedAsync(eventArgs);
                break;

            case "Message_streaming":
                // Streaming chunk received - update the message in real-time
                _logger.LogDebug("Streaming chunk for chat: {ChatId}", Id);
                HandleStreamingChunk(eventArgs);
                break;

            case "Message_completed":
                // Assistant message completed - finalize the message
                _logger.LogInformation("Assistant message completed for chat {ChatId}", Id);
                await HandleMessageCompleted(eventArgs);
                break;

            case "Error_occurred":
                // Error handling could be added here
                _logger.LogWarning("Error occurred for chat: {ChatId}", Id);
                await MessagesViewModel.ReadAsync(forceload: true);
                break;
        }
    }

    private async Task HandleProcessingStartedAsync(ChatEventReceivedEventArgs eventArgs)
    {
        try
        {
            // Extract MessageId from the event data
            var data = eventArgs.Data as System.Text.Json.JsonElement?;
            if (!data.HasValue)
                return;

            var messageId = data.Value.TryGetProperty("MessageId", out var msgIdProp)
                ? msgIdProp.GetString()
                : null;

            if (string.IsNullOrEmpty(messageId))
                return;

            // Create a placeholder assistant message for streaming
            var streamingMessage = new ChatMessage
            {
                MessageId = messageId,
                ChatId = Id,
                Role = ChatMessageRole.Assistant,
                Content = string.Empty,  // Will be updated as chunks arrive
                Timestamp = DateTimeOffset.UtcNow
            };

            // Add the streaming message to the collection
            var (viewModel, error) = MessagesViewModel!.NewViewModel(streamingMessage);
            if (string.IsNullOrEmpty(error))
            {
                viewModel.IsStreaming = true;
                // Use ItemCreated callback to properly notify the collection
                await MessagesViewModel.ItemCreated(viewModel, makeCurrentItem: false);
                _logger.LogDebug("Created streaming message placeholder: {MessageId}", messageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling processing started event");
        }
    }

    private void HandleStreamingChunk(ChatEventReceivedEventArgs eventArgs)
    {
        try
        {
            // Extract MessageId and Chunk from the event data
            var data = eventArgs.Data as System.Text.Json.JsonElement?;
            if (!data.HasValue)
                return;

            var messageId = data.Value.TryGetProperty("MessageId", out var msgIdProp)
                ? msgIdProp.GetString()
                : null;
            var chunk = data.Value.TryGetProperty("Chunk", out var chunkProp)
                ? chunkProp.GetString()
                : null;

            if (string.IsNullOrEmpty(messageId) || string.IsNullOrEmpty(chunk))
                return;

            // Find the streaming message in the collection
            var messageViewModel = MessagesViewModel!.ViewModels.Values
                .FirstOrDefault(m => m.Id == messageId);

            if (messageViewModel != null && messageViewModel.Data != null)
            {
                // Append the chunk to the existing content
                messageViewModel.Data.Content += chunk;
                MessagesViewModel!.IsChanged = true; // Notify UI of change
                _logger.LogDebug("Appended chunk to message {MessageId}: {ChunkLength} chars", messageId, chunk.Length);
            }
            else
            {
                _logger.LogWarning("Streaming message not found: {MessageId}", messageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling streaming chunk");
        }
    }

    private async Task HandleMessageCompleted(ChatEventReceivedEventArgs eventArgs)
    {
        try
        {
            // Extract the completed message from the event data
            var data = eventArgs.Data as System.Text.Json.JsonElement?;
            if (!data.HasValue)
            {
                // Fallback: reload all messages
                await MessagesViewModel!.ReadAsync(forceload: true);
                return;
            }

            // Deserialize the ChatMessage from the data
            var completedMessage = System.Text.Json.JsonSerializer.Deserialize<ChatMessage>(
                data.Value.GetRawText(),
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (completedMessage == null)
            {
                await MessagesViewModel!.ReadAsync(forceload: true);
                return;
            }

            // Find the streaming message and mark it as complete
            var messageViewModel = MessagesViewModel!.ViewModels.Values
                .FirstOrDefault(m => m.Id == completedMessage.MessageId);

            if (messageViewModel != null)
            {
                messageViewModel.IsStreaming = false;
                // Update with the final message data (includes timestamp, etc.)
                if (messageViewModel.Data != null)
                {
                    messageViewModel.Data.MessageId = completedMessage.MessageId;
                    messageViewModel.Data.ChatId = completedMessage.ChatId;
                    messageViewModel.Data.Role = completedMessage.Role;
                    messageViewModel.Data.Content = completedMessage.Content;
                    messageViewModel.Data.Timestamp = completedMessage.Timestamp;
                }
                MessagesViewModel!.IsChanged = true; // Notify UI of change
                _logger.LogInformation("Marked message {MessageId} as completed", completedMessage.MessageId);
            }
            else
            {
                // Message not found in collection - reload to ensure consistency
                _logger.LogWarning("Completed message not found in collection: {MessageId}, reloading", completedMessage.MessageId);
                await MessagesViewModel.ReadAsync(forceload: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message completed event");
            // Fallback: reload messages
            await MessagesViewModel!.ReadAsync(forceload: true);
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
