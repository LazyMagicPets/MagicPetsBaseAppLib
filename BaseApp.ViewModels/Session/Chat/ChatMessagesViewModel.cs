namespace BaseApp.ViewModels;

using LazyMagic.Client.FactoryGenerator;
using System.Reactive.Linq;

[Factory]
public class ChatMessagesViewModel : LzItemsViewModel<ChatMessageViewModel, ChatMessage, ChatMessageModel>
{
    private readonly IChatModuleClient? _chatApi;
    private readonly IChatEventsService? _chatEventsService;
    private string? _subscriptionId;
    private IDisposable? _eventSubscription;
    private string _chatId = string.Empty;

    public ChatMessagesViewModel(
        [FactoryInject] ILoggerFactory loggerFactory,
        [FactoryInject] IChatModuleClient? chatApi,
        [FactoryInject] IChatMessageViewModelFactory chatMessageViewModelFactory,
        [FactoryInject] IChatEventsService? chatEventsService
        ) : base(loggerFactory)
    {
        _chatApi = chatApi;
        _chatEventsService = chatEventsService;
        ChatMessageViewModelFactory = chatMessageViewModelFactory;

        if (chatApi != null)
        {
            _DTOReadListAsync = async () =>
            {
                if (string.IsNullOrEmpty(ChatId))
                    return new List<ChatMessage>();

                var result = await chatApi.ChatModuleGetChatMessagesAsync(ChatId, null, null);
                return result?.ToList() ?? new List<ChatMessage>();
            };
        }
    }

    public IChatMessageViewModelFactory? ChatMessageViewModelFactory { get; init; }

    public string ChatId
    {
        get => _chatId;
        set
        {
            if (_chatId != value)
            {
                _chatId = value;

                // Auto-subscribe to events when ChatId is set
                if (!string.IsNullOrEmpty(_chatId))
                {
                    _ = InitializeEventsAsync();
                }
            }
        }
    }

    public override (ChatMessageViewModel, string) NewViewModel(ChatMessage dto)
        => (ChatMessageViewModelFactory!.Create(this, dto), string.Empty);

    public override async Task<(bool, string)> ReadAsync(bool forceload = false)
        => await base.ReadAsync(forceload);

    /// <summary>
    /// Initialize event subscriptions for this chat's messages.
    /// Called automatically when ChatId is set.
    /// </summary>
    private async Task InitializeEventsAsync()
    {
        if (_chatEventsService == null)
        {
            _logger.LogWarning("ChatEventsService not available for chat: {ChatId}", ChatId);
            return;
        }

        if (!string.IsNullOrEmpty(_subscriptionId))
        {
            _logger.LogDebug("Already subscribed to chat events for: {ChatId}", ChatId);
            return;
        }

        // Initialize the service if not already initialized
        if (!_chatEventsService.IsInitialized)
        {
            _logger.LogDebug("ChatEventsService not yet initialized, initializing now for chat: {ChatId}", ChatId);
            var initialized = await _chatEventsService.InitializeAsync();

            if (!initialized)
            {
                _logger.LogWarning("Failed to initialize ChatEventsService for chat: {ChatId}", ChatId);
                return;
            }
        }

        try
        {
            // Subscribe to the chat channel
            _subscriptionId = await _chatEventsService.SubscribeToChatAsync(ChatId);

            if (_subscriptionId != null)
            {
                // Subscribe to events for this specific chat using ReactiveUI
                _eventSubscription = _chatEventsService.ChatEvents
                    .Where(e => e.ChatId == ChatId)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(async eventArgs =>
                    {
                        await HandleChatEventAsync(eventArgs);
                    });

                _logger.LogInformation("ChatMessagesViewModel: Subscribed to events for chat: {ChatId}", ChatId);
            }
            else
            {
                _logger.LogWarning("Failed to subscribe to chat events for: {ChatId}", ChatId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to chat events for: {ChatId}", ChatId);
        }
    }

    private async Task HandleChatEventAsync(ChatEventReceivedEventArgs eventArgs)
    {
        switch (eventArgs.EventType)
        {
            case "Message_received":
                // User message received - reload to show it
                _logger.LogInformation("Reloading messages for chat {ChatId} due to user message", ChatId);
                await ReadAsync(forceload: true);
                break;

            case "Message_processing":
                // Assistant is starting to process - create placeholder message
                _logger.LogDebug("Assistant processing started for chat: {ChatId}", ChatId);
                await HandleProcessingStartedAsync(eventArgs);
                break;

            case "Message_streaming":
                // Streaming chunk received - update the message in real-time
                _logger.LogDebug("Streaming chunk for chat: {ChatId}", ChatId);
                HandleStreamingChunk(eventArgs);
                break;

            case "Message_completed":
                // Assistant message completed - finalize the message
                _logger.LogInformation("Assistant message completed for chat {ChatId}", ChatId);
                await HandleMessageCompletedAsync(eventArgs);
                break;

            case "Error_occurred":
                // Error handling - reload messages
                _logger.LogWarning("Error occurred for chat: {ChatId}", ChatId);
                await ReadAsync(forceload: true);
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
                ChatId = ChatId,
                Role = ChatMessageRole.Assistant,
                Content = string.Empty,  // Will be updated as chunks arrive
                Timestamp = DateTimeOffset.UtcNow
            };

            // Add the streaming message to the collection
            var (viewModel, error) = NewViewModel(streamingMessage);
            if (string.IsNullOrEmpty(error))
            {
                viewModel.IsStreaming = true;
                // Use ItemCreated callback to properly notify the collection
                await ItemCreated(viewModel, makeCurrentItem: false);
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
            var messageViewModel = ViewModels.Values
                .FirstOrDefault(m => m.Id == messageId);

            if (messageViewModel != null && messageViewModel.Data != null)
            {
                // Append the chunk to the existing content
                messageViewModel.Data.Content += chunk;
                IsChanged = true; // Notify UI of change
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

    private async Task HandleMessageCompletedAsync(ChatEventReceivedEventArgs eventArgs)
    {
        try
        {
            // Extract the completed message from the event data
            var data = eventArgs.Data as System.Text.Json.JsonElement?;
            if (!data.HasValue)
            {
                // Fallback: reload all messages
                await ReadAsync(forceload: true);
                return;
            }

            // Deserialize the ChatMessage from the data
            var completedMessage = System.Text.Json.JsonSerializer.Deserialize<ChatMessage>(
                data.Value.GetRawText(),
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (completedMessage == null)
            {
                await ReadAsync(forceload: true);
                return;
            }

            // Find the streaming message and mark it as complete
            var messageViewModel = ViewModels.Values
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
                IsChanged = true; // Notify UI of change
                _logger.LogInformation("Marked message {MessageId} as completed", completedMessage.MessageId);
            }
            else
            {
                // Message not found in collection - reload to ensure consistency
                _logger.LogWarning("Completed message not found in collection: {MessageId}, reloading", completedMessage.MessageId);
                await ReadAsync(forceload: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message completed event");
            // Fallback: reload messages
            await ReadAsync(forceload: true);
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
            _logger.LogInformation("Unsubscribing from chat events for: {ChatId}", ChatId);
            // Fire and forget - don't wait for unsubscribe
            _ = _chatEventsService.UnsubscribeFromChatAsync(ChatId);
            _subscriptionId = null;
        }

        base.Dispose();
    }
}
