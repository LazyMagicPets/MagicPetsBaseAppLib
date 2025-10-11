namespace BaseApp.ViewModels;

using LazyMagic.Client.FactoryGenerator;
using System.Reactive.Linq;

[Factory]
public class ChatsViewModel : LzItemsViewModel<ChatViewModel, Chat, ChatModel>
{
    private readonly IChatModuleClient? _chatApi;
    private readonly IChatEventsService? _chatEventsService;
    private IDisposable? _chatEventsSubscription;

    public ChatsViewModel(
        [FactoryInject] ILoggerFactory loggerFactory,
        [FactoryInject] IChatModuleClient? chatApi,
        [FactoryInject] IChatViewModelFactory chatViewModelFactory,
        [FactoryInject] IChatEventsService? chatEventsService
        ) : base(loggerFactory)
    {
        ChatViewModelFactory = chatViewModelFactory;
        _chatApi = chatApi;
        _chatEventsService = chatEventsService;

        if (chatApi != null)
        {
            _DTOReadListAsync = async () =>
            {
                var result = await chatApi.ChatModuleListChatsAsync();
                return result?.ToList() ?? new List<Chat>();
            };
        }

    }

    private async Task InitializeChatEventsAsync()
    {
        if (_chatEventsService == null)
            return;

        if (_chatEventsService.IsInitialized)
            return;

        try
        {
            var initialized = await _chatEventsService.InitializeAsync();

            if (initialized)
            {
                // Subscribe to chat events and route them to the appropriate ChatViewModel
                _chatEventsSubscription = _chatEventsService.ChatEvents
                    .Subscribe(eventArgs =>
                    {
                        if (ViewModels.TryGetValue(eventArgs.ChatId, out var chatViewModel))
                        {
                            // Let the ChatViewModel handle its own events via its observable
                            _logger.LogDebug("Routing event {EventType} to ChatViewModel {ChatId}",
                                eventArgs.EventType, eventArgs.ChatId);
                        }
                    });

                _logger.LogInformation("ChatsViewModel: Chat events service initialized");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize chat events service");
        }
    }

    public IChatViewModelFactory? ChatViewModelFactory { get; init; }

    public override (ChatViewModel, string) NewViewModel(Chat dto)
        => (ChatViewModelFactory!.Create(this, dto), string.Empty);

    public override async Task<(bool, string)> ReadAsync(bool forceload = false)
        => await base.ReadAsync(forceload);

    public async Task<(bool, string)> CreateNewChatAsync(string title = "New Chat")
    {
        if (_chatApi == null)
            return (false, "Chat API not available");

        try
        {
            var chatId = Guid.NewGuid().ToString();
            var newChat = new Chat
            {
                Id = chatId,
                ChatId = chatId,
                Summary = title,
                Status = ChatStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow,
                CreateUtcTick = DateTime.UtcNow.Ticks,
                UpdateUtcTick = DateTime.UtcNow.Ticks
            };

            var result = await _chatApi.ChatModuleAddChatAsync(newChat);

            if (result != null)
            {
                // Reload chats to include the new one
                await ReadAsync(forceload: true);

                // Set as current
                if (ViewModels.ContainsKey(result.Id))
                {
                    CurrentViewModel = ViewModels[result.Id];
                }

                return (true, string.Empty);
            }

            return (false, "Failed to create chat");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public override void Dispose()
    {
        _chatEventsSubscription?.Dispose();
        base.Dispose();
    }
}
