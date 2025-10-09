using LazyMagic.Client.FactoryGenerator; // do not put in global using. Causes runtime error.
namespace ViewModels;
/// <summary>do it
/// The SessionViewModel is the root viewModel for a user session.
/// This class maintains the "state" of the use session, which includes 
/// the data (in this case the sets).
/// </summary>
[Factory]
public class SessionViewModel : BaseAppSessionViewModel, ISessionViewModel
{
    public SessionViewModel(
        [FactoryInject] ILoggerFactory loggerFactory, // singleton
        [FactoryInject] IConnectivityService connectivityService, // singleton
        [FactoryInject] ILzMessages messages, // singleton
        [FactoryInject] IPetsViewModelFactory petsViewModelFactory, // transient
        [FactoryInject] ICategoriesViewModelFactory categoriesViewModelFactory, // transient
        [FactoryInject] ITagsViewModelFactory tagsViewModelFactory, // transient
        [FactoryInject] IChatsViewModelFactory chatsViewModelFactory, // transient
        [FactoryInject] ITenantApi api // scoped
        ) : base(loggerFactory, connectivityService, messages,
                petsViewModelFactory, categoriesViewModelFactory, tagsViewModelFactory, chatsViewModelFactory )
    {
        try
        {
            // Note: ITenantApi is resolved when IHostApi is resolved. This makes dynamice binding of the API methods possible
            // in libraries like the BaseApp.ViewModels. In the app, use the ITenantApi interface to access methods 
            // without the overhead of the dynamic binding.
            _api = api ?? throw new ArgumentNullException(nameof(api), "StoreApi cannot be null");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating SessionsViewModel: {ex.Message}");
            throw;
        }
    }

    private ITenantApi _api;
    public override async Task InitAsync()
    {
        try
        {
            //var fingerPrint = JsonConvert.SerializeObject(sessionsViewModel!.BrowserFingerprint);

            //PublicSchema.Fingerprint newFingerprint = JsonConvert.DeserializeObject<PublicSchema.Fingerprint>(fingerPrint) ?? new PublicSchema.Fingerprint();

            //newFingerprint.Id = Guid.NewGuid().ToString();
            //await _api.PublicModuleFingerprintCreateAsync(newFingerprint);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error Calling PublicApi: {ex.Message}");
        }
    }
}
