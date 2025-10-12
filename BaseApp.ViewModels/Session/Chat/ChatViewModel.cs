namespace BaseApp.ViewModels;

using LazyMagic.Client.FactoryGenerator;

[Factory]
public class ChatViewModel : LzItemViewModel<Chat, ChatModel>
{
    private readonly IChatModuleClient? _chatApi;
    private readonly IChatMessagesViewModelFactory? _chatMessagesViewModelFactory;

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
            _DTOUpdateAsync = async (chat) => await chatApi.ChatModuleUpdateChatAsync(chat);
            _DTODeleteAsync = chatApi.ChatModuleDeleteChatAsync;
        }

        // Initialize messages ViewModel
        // ChatMessagesViewModel will auto-subscribe to events when ChatId is set
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
            // MessagesViewModel handles all event subscriptions and updates

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
