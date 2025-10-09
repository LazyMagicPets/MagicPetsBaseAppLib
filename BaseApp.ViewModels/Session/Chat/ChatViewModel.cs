namespace BaseApp.ViewModels;

using LazyMagic.Client.FactoryGenerator;
using System.Timers;

[Factory]
public class ChatViewModel : LzItemViewModel<Chat, ChatModel>
{
    private readonly IChatModuleClient? _chatApi;
    private readonly IChatMessagesViewModelFactory? _chatMessagesViewModelFactory;
    private System.Timers.Timer? _pollingTimer;
    private int _lastMessageCount = 0;

    public ChatViewModel(
        [FactoryInject] ILoggerFactory loggerFactory,
        [FactoryInject] IChatModuleClient? chatApi,
        [FactoryInject] IChatMessagesViewModelFactory? chatMessagesViewModelFactory,
        ILzParentViewModel parentViewModel,
        Chat chat,
        bool? isLoaded = null
        ) : base(loggerFactory, chat, model: null, isLoaded)
    {
        ParentViewModel = parentViewModel;
        _chatApi = chatApi;
        _chatMessagesViewModelFactory = chatMessagesViewModelFactory;

        if (chatApi != null)
        {
            _DTOReadAsync = chatApi.ChatModuleGetChatByIdAsync;
            _DTOCreateAsync = chatApi.ChatModuleAddChatAsync;
            _DTOUpdateAsync = (chat) => chatApi.ChatModuleUpdateChatAsync(chat);
            _DTODeleteAsync = chatApi.ChatModuleDeleteChatAsync;
        }

        // Initialize messages ViewModel
        if (_chatMessagesViewModelFactory != null && !string.IsNullOrEmpty(chat?.Id))
        {
            MessagesViewModel = _chatMessagesViewModelFactory.Create();
            MessagesViewModel.ChatId = chat.Id;
        }
    }

    public void StartPolling()
    {
        if (_pollingTimer == null)
        {
            _pollingTimer = new System.Timers.Timer(2000); // Poll every 2 seconds
            _pollingTimer.Elapsed += async (sender, e) => await PollForNewMessages();
            _pollingTimer.AutoReset = true;
            _pollingTimer.Start();
        }
    }

    public void StopPolling()
    {
        if (_pollingTimer != null)
        {
            _pollingTimer.Stop();
            _pollingTimer.Dispose();
            _pollingTimer = null;
        }
    }

    private async Task PollForNewMessages()
    {
        if (MessagesViewModel == null) return;

        try
        {
            await MessagesViewModel.ReadAsync(forceload: true);

            // Track message count to detect new messages
            var currentCount = MessagesViewModel.ViewModels.Count;
            if (currentCount != _lastMessageCount)
            {
                _lastMessageCount = currentCount;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error polling for new messages");
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

            if (result != null && MessagesViewModel != null)
            {
                // Reload messages to get both user message and assistant response
                await MessagesViewModel.ReadAsync(forceload: true);
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
