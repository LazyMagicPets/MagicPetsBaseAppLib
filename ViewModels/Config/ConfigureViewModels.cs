using LazyMagic.Shared;
using TenantApi;

namespace ViewModels;

public static class ConfigureViewModels
{
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {

        ViewModelsRegisterFactories.ViewModelsRegister(services); // Register Factory Classes


        // Register the ClientSDK 
        services.AddScoped<ITenantApi> (serviceProvider =>
        {
            var lzHost = serviceProvider.GetRequiredService<ILzHost>();
            var authenticationHandler = serviceProvider.GetRequiredService<IAuthenticationHandler>();
            var handler = authenticationHandler.CreateHandler();
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(lzHost.GetApiUrl("")) // LocalApiUrl or RemoteApiUrl depending on UseLocalhostApi property
            };
            var api = new TenantApi.TenantApi(httpClient);
            return api;
        });

        // Register the modules used from the Client SDK.
        services.AddScoped<IPublicModuleClient>(provider => provider.GetRequiredService<ITenantApi>());
        services.AddScoped<IConsumerModuleClient>(provider => provider.GetRequiredService<ITenantApi>());
        services.AddScoped<IStoreModuleClient>(provider => provider.GetRequiredService<ITenantApi>());
        services.AddScoped<IChatModuleClient>(provider => provider.GetRequiredService<ITenantApi>());

        services.AddScoped<ISessionViewModel, SessionViewModel>();
        services.AddTransient<IBaseAppSessionViewModel>(sp => sp.GetRequiredService<ISessionViewModel>());

        services.AddBaseAppViewModels();
           
        return services;
    }
}

