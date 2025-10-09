namespace BaseApp.ViewModels;

/// <summary>
/// This looks redundant but its not. Each app may have multiple libraries that 
/// provide SessionViewModels. We reference each of those here, in this case 
/// we only have one because the SnapsApp only references the SetsCmp.ViewModels library.
/// </summary>
public interface IBaseAppSessionViewModel :
    ILzSessionViewModel
{
    PetsViewModel PetsViewModel { get; set; }
    CategoriesViewModel CategoriesViewModel { get; set; }
    TagsViewModel TagsViewModel { get; set; }
    ChatsViewModel ChatsViewModel { get; set; }
}
