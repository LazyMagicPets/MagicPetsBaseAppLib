using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using LazyMagic.OIDC.Base;
using System.Reactive.Subjects;

namespace BaseApp.ViewModels;

public interface IAppSyncEventsWebSocketClient : IDisposable
{
    Task<bool> ConnectAsync(string eventApiUrl, CancellationToken cancellationToken = default);
    Task<string> SubscribeAsync(string channelPath, CancellationToken cancellationToken = default);
    Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    IObservable<ChatEventReceivedEventArgs> EventReceived { get; }
    bool IsConnected { get; }
}

public class ChatEventReceivedEventArgs : EventArgs
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public object? Data { get; set; }
    public string? DataType { get; set; }
}

public class AppSyncEventsWebSocketClient : IAppSyncEventsWebSocketClient
{
    private readonly ILogger<AppSyncEventsWebSocketClient> _logger;
    private readonly IOIDCService? _oidcService;
    private readonly Subject<ChatEventReceivedEventArgs> _eventReceivedSubject = new();
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private string? _authToken;
    private string? _httpHost;

    public IObservable<ChatEventReceivedEventArgs> EventReceived => _eventReceivedSubject;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public AppSyncEventsWebSocketClient(
        ILogger<AppSyncEventsWebSocketClient> logger,
        IOIDCService? oidcService = null)
    {
        _logger = logger;
        _oidcService = oidcService;
    }

    public async Task<bool> ConnectAsync(string eventApiUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            _webSocket = new ClientWebSocket();

            // Get access token from OIDC service
            _authToken = null;
            if (_oidcService != null)
            {
                _authToken = await _oidcService.GetAccessTokenAsync();
                if (!string.IsNullOrEmpty(_authToken))
                {
                    _logger.LogDebug("Got access token from OIDC service");
                }
                else
                {
                    _logger.LogWarning("No access token available from OIDC service");
                }
            }
            else
            {
                _logger.LogWarning("OIDC service not available - connecting without authentication");
            }

            // Store the HTTP host for use in subscribe messages
            _httpHost = new Uri(eventApiUrl).Host;

            // Build the WebSocket URL
            var wsUrl = BuildAppSyncWebSocketUrl(eventApiUrl);

            // AWS AppSync Events requires specific subprotocol
            _webSocket.Options.AddSubProtocol("aws-appsync-event-ws");

            // Add authentication via subprotocol header
            // Format: header-<base64url_encoded_auth_object>
            if (!string.IsNullOrEmpty(_authToken))
            {
                // Create auth header object with the HTTP endpoint host
                var authHeader = new
                {
                    Authorization = _authToken,
                    host = _httpHost
                };

                var authJson = JsonSerializer.Serialize(authHeader);
                var authBytes = Encoding.UTF8.GetBytes(authJson);
                var authBase64 = Convert.ToBase64String(authBytes)
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_'); // Base64URL encoding

                var subprotocol = $"header-{authBase64}";
                _webSocket.Options.AddSubProtocol(subprotocol);
                _logger.LogDebug("Added WebSocket subprotocol for authentication with host: {Host}", _httpHost);
            }

            _logger.LogInformation("Connecting to AppSync Events WebSocket: {Url}", wsUrl);

            await _webSocket.ConnectAsync(new Uri(wsUrl), cancellationToken);

            // Send connection_init message
            await SendMessageAsync(new
            {
                type = "connection_init"
            }, cancellationToken);

            // Start receiving messages
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);

            _logger.LogInformation("Connected to AppSync Events WebSocket");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to AppSync Events WebSocket");
            return false;
        }
    }

    public async Task<string> SubscribeAsync(string channelPath, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("WebSocket is not connected");

        var subscriptionId = Guid.NewGuid().ToString("N");

        _logger.LogInformation("Subscribing to channel: {Channel} with ID: {SubscriptionId}", channelPath, subscriptionId);

        // AWS AppSync Events requires authorization in each subscribe message
        var subscribeMessage = new
        {
            type = "subscribe",
            id = subscriptionId,
            channel = channelPath,
            authorization = !string.IsNullOrEmpty(_authToken) && !string.IsNullOrEmpty(_httpHost)
                ? new { Authorization = _authToken, host = _httpHost }
                : null
        };

        await SendMessageAsync(subscribeMessage, cancellationToken);

        return subscriptionId;
    }

    public async Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            return;

        _logger.LogInformation("Unsubscribing from subscription: {SubscriptionId}", subscriptionId);

        await SendMessageAsync(new
        {
            type = "unsubscribe",
            id = subscriptionId
        }, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _receiveCts?.Cancel();

            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", cancellationToken);
            }

            if (_receiveTask != null)
            {
                await _receiveTask;
            }

            _logger.LogInformation("Disconnected from AppSync Events WebSocket");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
        }
    }

    private string BuildAppSyncWebSocketUrl(string eventApiUrl)
    {
        _logger.LogDebug("Building AppSync WebSocket URL from eventApiUrl: {EventApiUrl}", eventApiUrl);

        // Convert HTTP URL to WebSocket URL and change subdomain
        // AWS AppSync Events uses different subdomains for HTTP vs WebSocket:
        // HTTP:      https://{api-id}.appsync-api.{region}.amazonaws.com
        // WebSocket: wss://{api-id}.appsync-realtime-api.{region}.amazonaws.com
        var wsUrl = eventApiUrl
            .Replace("https://", "wss://")
            .Replace("http://", "ws://")
            .Replace(".appsync-api.", ".appsync-realtime-api.");

        _logger.LogDebug("After protocol and domain conversion: {WsUrl}", wsUrl);

        // Ensure the URL has the correct path for AppSync Events API WebSocket
        // The path should be: /event/realtime
        if (!wsUrl.EndsWith("/event/realtime", StringComparison.OrdinalIgnoreCase))
        {
            var originalWsUrl = wsUrl;

            // If URL already ends with /event, just add /realtime
            if (wsUrl.EndsWith("/event", StringComparison.OrdinalIgnoreCase))
            {
                wsUrl = wsUrl + "/realtime";
            }
            // If URL has no path or different path, add /event/realtime
            else
            {
                wsUrl = wsUrl.TrimEnd('/') + "/event/realtime";
            }

            _logger.LogDebug("Added path suffix. Before: {Before}, After: {After}", originalWsUrl, wsUrl);
        }

        _logger.LogInformation("Final WebSocket URL: {Url}", wsUrl);

        return wsUrl;
    }

    private async Task SendMessageAsync(object message, CancellationToken cancellationToken)
    {
        if (_webSocket == null || !IsConnected)
            throw new InvalidOperationException("WebSocket is not connected");

        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);

        _logger.LogDebug("Sending message: {Message}", json);

        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 4];

        try
        {
            while (_webSocket != null && IsConnected && !cancellationToken.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("WebSocket closed by server");
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _logger.LogDebug("Received message: {Message}", message);

                ProcessMessage(message);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Receive loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in receive loop");
        }
    }

    private void ProcessMessage(string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeElement))
                return;

            var messageType = typeElement.GetString();

            switch (messageType)
            {
                case "connection_ack":
                    _logger.LogInformation("Connection acknowledged");
                    break;

                case "subscribe_success":
                    if (root.TryGetProperty("id", out var subId))
                    {
                        _logger.LogInformation("Subscription successful: {SubscriptionId}", subId.GetString());
                    }
                    break;

                case "subscribe_error":
                    if (root.TryGetProperty("id", out var errorSubId))
                    {
                        _logger.LogError("Subscription error: {SubscriptionId}", errorSubId.GetString());
                    }
                    break;

                case "error":
                    if (root.TryGetProperty("errors", out var errors))
                    {
                        _logger.LogError("WebSocket error: {Errors}", errors.GetRawText());
                    }
                    break;

                case "data":
                    HandleDataMessage(root);
                    break;

                case "ka":
                    // Keep-alive message, no action needed
                    break;

                default:
                    _logger.LogWarning("Unknown message type: {Type}", messageType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message: {Message}", message);
        }
    }

    private void HandleDataMessage(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("id", out var idElement))
                return;

            var subscriptionId = idElement.GetString() ?? string.Empty;

            if (!root.TryGetProperty("event", out var eventElement))
                return;

            // The event field comes as a JSON string, not an object, so we need to parse it
            JsonElement eventData;
            if (eventElement.ValueKind == JsonValueKind.String)
            {
                var eventJson = eventElement.GetString();
                if (string.IsNullOrEmpty(eventJson))
                    return;

                using var eventDoc = JsonDocument.Parse(eventJson);
                eventData = eventDoc.RootElement.Clone(); // Clone so it survives the using block
            }
            else
            {
                eventData = eventElement;
            }

            // Parse the event data
            var chatId = eventData.TryGetProperty("chatId", out var chatIdProp) ? chatIdProp.GetString() ?? string.Empty : string.Empty;
            var eventType = eventData.TryGetProperty("eventType", out var eventTypeProp) ? eventTypeProp.GetString() ?? string.Empty : string.Empty;
            var timestamp = eventData.TryGetProperty("timestamp", out var timestampProp) ? timestampProp.GetDateTimeOffset() : DateTimeOffset.UtcNow;
            var dataType = eventData.TryGetProperty("dataType", out var dataTypeProp) ? dataTypeProp.GetString() : null;
            object? data = eventData.TryGetProperty("data", out var dataProp) ? JsonSerializer.Deserialize<object>(dataProp.GetRawText()) : null;

            var eventArgs = new ChatEventReceivedEventArgs
            {
                SubscriptionId = subscriptionId,
                ChatId = chatId,
                EventType = eventType,
                Timestamp = timestamp,
                Data = data,
                DataType = dataType
            };

            _logger.LogInformation("Received chat event: {EventType} (DataType: {DataType}) for chat: {ChatId}",
                eventType, dataType ?? "null", chatId);

            _eventReceivedSubject.OnNext(eventArgs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling data message");
        }
    }

    public void Dispose()
    {
        _eventReceivedSubject.OnCompleted();
        _eventReceivedSubject.Dispose();
        DisconnectAsync().GetAwaiter().GetResult();
        _webSocket?.Dispose();
        _receiveCts?.Dispose();
    }
}
