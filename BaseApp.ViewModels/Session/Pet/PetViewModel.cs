namespace BaseApp.ViewModels;

using LazyMagic.Client.FactoryGenerator; // do not put in global using. Causes runtime error.
using StoreModule;
using PublicModule;

[Factory]
public class PetViewModel : LzItemViewModel<Pet, PetModel>
{
    public PetViewModel(
        [FactoryInject] ILoggerFactory loggerFactory,
        [FactoryInject] IPublicModuleClient publicApi,
        [FactoryInject] IStoreModuleClient? AppApi,
        ILzParentViewModel parentViewModel,
        Pet pet,
        bool? isLoaded = null
        ) : base(loggerFactory, pet, model: null, isLoaded) 
    {
        ParentViewModel = parentViewModel;
        if(publicApi != null)
            _DTOReadAsync = publicApi.PublicModuleGetPetByIdAsync;

        if(AppApi != null)
        {
            _DTOCreateAsync = AppApi.StoreModuleAddPetAsync;
            _DTOUpdateAsync = AppApi.StoreModuleUpdatePetAsync;
            _DTODeleteAsync = AppApi.StoreModuleDeletePetAsync;
        }
    }
    public override string Id => Data?.Id ?? string.Empty;
    public override long UpdatedAt => Data?.UpdateUtcTick ?? long.MaxValue;

 }
