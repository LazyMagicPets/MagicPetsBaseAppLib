namespace BaseApp.ViewModels;

using LazyMagic.Client.FactoryGenerator;

[Factory]
public class ChatMessageViewModel : LzItemViewModel<ChatMessage, ChatMessageModel>
{
    public ChatMessageViewModel(
        [FactoryInject] ILoggerFactory loggerFactory,
        ILzParentViewModel parentViewModel,
        ChatMessage chatMessage,
        bool? isLoaded = null
        ) : base(loggerFactory, chatMessage, model: null, isLoaded)
    {
        ParentViewModel = parentViewModel;
    }

    public override string Id => Data?.MessageId ?? string.Empty;
    public override long UpdatedAt => Data?.Timestamp.Ticks ?? long.MaxValue;

    public bool IsUserMessage => Data?.Role == ChatMessageRole.User;
    public bool IsAssistantMessage => Data?.Role == ChatMessageRole.Assistant;
    public bool IsStreaming { get; set; }
}
