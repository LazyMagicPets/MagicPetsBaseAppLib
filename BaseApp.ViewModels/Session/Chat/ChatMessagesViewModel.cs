namespace BaseApp.ViewModels;

using LazyMagic.Client.FactoryGenerator;

[Factory]
public class ChatMessagesViewModel : LzItemsViewModel<ChatMessageViewModel, ChatMessage, ChatMessageModel>
{
    public ChatMessagesViewModel(
        [FactoryInject] ILoggerFactory loggerFactory,
        [FactoryInject] IChatModuleClient? chatApi,
        [FactoryInject] IChatMessageViewModelFactory chatMessageViewModelFactory
        ) : base(loggerFactory)
    {
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
    public string ChatId { get; set; } = string.Empty;

    public override (ChatMessageViewModel, string) NewViewModel(ChatMessage dto)
        => (ChatMessageViewModelFactory!.Create(this, dto), string.Empty);

    public override async Task<(bool, string)> ReadAsync(bool forceload = false)
        => await base.ReadAsync(forceload);
}
