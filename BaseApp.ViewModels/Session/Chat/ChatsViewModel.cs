namespace BaseApp.ViewModels;

using LazyMagic.Client.FactoryGenerator;

[Factory]
public class ChatsViewModel : LzItemsViewModel<ChatViewModel, Chat, ChatModel>
{
    private readonly IChatModuleClient? _chatApi;

    public ChatsViewModel(
        [FactoryInject] ILoggerFactory loggerFactory,
        [FactoryInject] IChatModuleClient? chatApi,
        [FactoryInject] IChatViewModelFactory chatViewModelFactory
        ) : base(loggerFactory)
    {
        ChatViewModelFactory = chatViewModelFactory;
        _chatApi = chatApi;

        if (chatApi != null)
        {
            _DTOReadListAsync = async () =>
            {
                var result = await chatApi.ChatModuleListChatsAsync();
                return result?.ToList() ?? new List<Chat>();
            };
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
}
