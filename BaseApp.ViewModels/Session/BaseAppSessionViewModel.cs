namespace BaseApp.ViewModels;
using LazyMagic.Client.FactoryGenerator; // do not put in global using. Causes runtime error.
public abstract class BaseAppSessionViewModel : 
    LzSessionViewModel, 
    IBaseAppSessionViewModel
{
    public BaseAppSessionViewModel(
        ILoggerFactory loggerFactory,
        IConnectivityService connectivityService,
        ILzMessages messages,
        IPetsViewModelFactory petsViewModelFactory, // transient
        ICategoriesViewModelFactory categoriesViewModelFactory, // transient
        ITagsViewModelFactory tagsViewModelFactory, // transient
        IChatsViewModelFactory chatsViewModelFactory // transient

        ) : base(loggerFactory, connectivityService, messages)
    {
        // Create the data sets manipuated under this session. Note that we could just
        // inject these, but don't, in case we want to pass parameters to their constructors.
        // at some point in the future.
        PetsViewModel = petsViewModelFactory?.Create() ?? throw new ArgumentNullException(nameof(petsViewModelFactory));
        CategoriesViewModel = categoriesViewModelFactory?.Create() ?? throw new ArgumentNullException(nameof(categoriesViewModelFactory));
        TagsViewModel = tagsViewModelFactory?.Create() ?? throw new ArgumentNullException(nameof(tagsViewModelFactory));
        ChatsViewModel = chatsViewModelFactory?.Create() ?? throw new ArgumentNullException(nameof(chatsViewModelFactory));
    }

    public TenantConfigViewModel? TenantConfigViewModel { get; init; }
    public PetsViewModel PetsViewModel { get; set; }
    public CategoriesViewModel CategoriesViewModel { get; set; }
    public TagsViewModel TagsViewModel { get; set; }
    public ChatsViewModel ChatsViewModel { get; set; }

}
