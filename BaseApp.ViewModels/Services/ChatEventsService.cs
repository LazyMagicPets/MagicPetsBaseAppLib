using LazyMagic.Client.Base;
using Microsoft.Extensions.Logging;
using System.Reactive.Linq;

namespace BaseApp.ViewModels;

public interface IChatEventsService : IDisposable
{
    Task<bool> InitializeAsync();
    Task<string?> SubscribeToChatAsync(string chatId);
    Task UnsubscribeFromChatAsync(string chatId);
    IObservable<ChatEventReceivedEventArgs> ChatEvents { get; }
    bool IsInitialized { get; }
}

public class ChatEventsService : IChatEventsService
{
    private readonly ILogger<ChatEventsService> _logger;
    private readonly ILzClientConfig _clientConfig;
    private readonly IAppSyncEventsWebSocketClient _webSocketClient;
    private readonly Dictionary<string, string> _chatSubscriptions = new();
    private bool _isInitialized;

    public IObservable<ChatEventReceivedEventArgs> ChatEvents => _webSocketClient.EventReceived;

    public bool IsInitialized => _isInitialized;

    public ChatEventsService(
        ILogger<ChatEventsService> logger,
        ILzClientConfig clientConfig,
        IAppSyncEventsWebSocketClient webSocketClient)
    {
        _logger = logger;
        _clientConfig = clientConfig;
        _webSocketClient = webSocketClient;
    }

    public async Task<bool> InitializeAsync()
    {
        if (_isInitialized)
        {
            _logger.LogWarning("ChatEventsService already initialized");
            return true;
        }

        try
        {
            // Get AppSync Events API URL for the current auth configuration
            var eventApiUrl = _clientConfig.GetCurrentEventsApiUrl();

            if (string.IsNullOrEmpty(eventApiUrl))
            {
                _logger.LogWarning("AppSync Events API URL not found for auth config: {AuthConfig}",
                    _clientConfig.SelectedAuthConfig);
                return false;
            }

            _logger.LogInformation("Initializing ChatEventsService with Event API: {Url} for auth: {AuthConfig}",
                eventApiUrl, _clientConfig.SelectedAuthConfig);

            var connected = await _webSocketClient.ConnectAsync(eventApiUrl);

            if (connected)
            {
                _isInitialized = true;
                _logger.LogInformation("ChatEventsService initialized successfully");
            }

            return connected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ChatEventsService");
            return false;
        }
    }

    public async Task<string?> SubscribeToChatAsync(string chatId)
    {
        if (!_isInitialized)
        {
            _logger.LogWarning("Cannot subscribe - ChatEventsService not initialized");
            return null;
        }

        if (_chatSubscriptions.ContainsKey(chatId))
        {
            _logger.LogWarning("Already subscribed to chat: {ChatId}", chatId);
            return _chatSubscriptions[chatId];
        }

        try
        {
            // Subscribe to the chat channel
            var channelPath = $"/chat/{chatId}";
            var subscriptionId = await _webSocketClient.SubscribeAsync(channelPath);

            _chatSubscriptions[chatId] = subscriptionId;
            _logger.LogInformation("Subscribed to chat: {ChatId} with subscription: {SubscriptionId}", chatId, subscriptionId);

            return subscriptionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to chat: {ChatId}", chatId);
            return null;
        }
    }

    public async Task UnsubscribeFromChatAsync(string chatId)
    {
        if (!_chatSubscriptions.TryGetValue(chatId, out var subscriptionId))
        {
            _logger.LogWarning("No active subscription for chat: {ChatId}", chatId);
            return;
        }

        try
        {
            await _webSocketClient.UnsubscribeAsync(subscriptionId);
            _chatSubscriptions.Remove(chatId);
            _logger.LogInformation("Unsubscribed from chat: {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from chat: {ChatId}", chatId);
        }
    }

    public void Dispose()
    {
        _webSocketClient.Dispose();
        _chatSubscriptions.Clear();
        _isInitialized = false;
    }
}
