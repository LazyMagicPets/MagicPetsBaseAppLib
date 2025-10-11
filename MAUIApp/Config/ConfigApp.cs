namespace MAUIApp;

public static class ConfigApp
{
    public static IServiceCollection AddApp(this IServiceCollection services)
    {
        // EventsApiUrl is now dynamically loaded from the /config endpoint by LzClientConfig.ReadAuthConfigAsync
        // and accessed via ILzClientConfig.GetCurrentEventsApiUrl()

        services.AddBlazorUI(); // The BlazorUI folder contains the BlazorUI namespace

        services.AddViewModels(); // The BlazorTest.ViewModels project contains the ViewModels namespace

        return services;
    }

}

