
using ChatModule;

namespace BaseApp.ViewModels;

public static class ConfigureBaseAppViewModels
{
    public static IServiceCollection AddBaseAppViewModels(this IServiceCollection services)
    {
        //var assembly = typeof(ConfigureBaseAppViewModels).Assembly; // Uncomment if you need to use the assembly for something
        BaseAppViewModelsRegisterFactories.BaseAppViewModelsRegister(services); // Run generated registration code

        services.TryAddSingleton<ILzMessages, LzMessages>();
        services.TryAddSingleton<ILzClientConfig, LzClientConfig>();

        // Use of the various module clients is optional so we register them as singletons with null values.
        // Note we are using TryAdd so any existing registrations will not be overridden. It is expected that 
        // the application will register its own implementations of these interfaces if needed.
        services.TryAddScoped<IPublicModuleClient>(provider => null!);
        services.TryAddScoped<IConsumerModuleClient>(provider => null!);
        services.TryAddScoped<IStoreModuleClient>(provider => null!);
        services.TryAddScoped<IChatModuleClient>(provider => null!);    

        services.AddLazyMagicClientViewModels();

        return services;
    }
}
 