using LazyMagic.Client.Base;

namespace BaseApp.ViewModels;

public static class LzClientConfigExtensions
{
    /// <summary>
    /// Get the AppSync Events API URL for the currently selected auth configuration
    /// </summary>
    public static string? GetCurrentEventsApiUrl(this ILzClientConfig clientConfig)
    {
        // Get the current auth config name
        var authConfig = clientConfig.SelectedAuthConfig;

        if (string.IsNullOrEmpty(authConfig))
            return null;

        // Look up the corresponding Events API configuration
        // EventsApis dictionary keys should match AuthConfigs keys
        if (clientConfig.EventsApis?.TryGetValue(authConfig, out var eventsApiConfig) == true)
        {
            // Try to get the URL from the events API config
            var eventsApiUrl = eventsApiConfig?["url"]?.ToString();

            if (!string.IsNullOrEmpty(eventsApiUrl))
            {
                return eventsApiUrl;
            }

            // Fallback: try "apiUrl" property
            eventsApiUrl = eventsApiConfig?["apiUrl"]?.ToString();

            if (!string.IsNullOrEmpty(eventsApiUrl))
            {
                return eventsApiUrl;
            }
        }

        return null;
    }
}
