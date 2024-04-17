using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class Ext
{
    public static bool TryGetString(this JwtPayload payload, string key, out string value)
    {
        value = string.Empty;

        if (!payload.TryGetValue(key, out var element))
        {
            return false;
        }

        string? possible = element.ToString();
        if (string.IsNullOrEmpty(possible))
        {
            return false;
        }

        value = possible;
        return true;
    }

    public static bool TryGetInt(this JwtPayload payload, string key, out int value)
    {
        value = 0;

        if (!payload.TryGetValue(key, out var element))
        {
            return false;
        }

        if (!int.TryParse(element.ToString(), out int possible))
        {
            return false;
        }

        value = possible;
        return true;
    }

    public static void AddDefaultAzureCredential(this IServiceCollection services)
    {
        services.AddSingleton(
            provider =>
            {
                // get the list of credential options
                var config = provider.GetService<IConfig>();
                string[] include = (config?.INCLUDE_CREDENTIAL_TYPES.Length > 0)
                    ? config.INCLUDE_CREDENTIAL_TYPES :
                    string.Equals(Config.ASPNETCORE_ENVIRONMENT, "Development", StringComparison.InvariantCultureIgnoreCase)
                        ? ["azcli", "env"]
                        : ["env", "mi"];

                // log
                var factory = provider.GetService<ILoggerFactory>();
                var logger = factory?.CreateLogger("AddDefaultAzureCredential");
                logger?.LogDebug("azure authentication will include: {list}", string.Join(", ", include));

                // build the credential object
                return new DefaultAzureCredential(
                    new DefaultAzureCredentialOptions()
                    {
                        ExcludeEnvironmentCredential = !include.Contains("env"),
                        ExcludeManagedIdentityCredential = !include.Contains("mi"),
                        ExcludeSharedTokenCacheCredential = !include.Contains("token"),
                        ExcludeVisualStudioCredential = !include.Contains("vs"),
                        ExcludeVisualStudioCodeCredential = !include.Contains("vscode"),
                        ExcludeAzureCliCredential = !include.Contains("azcli"),
                        ExcludeInteractiveBrowserCredential = !include.Contains("browser"),
                    });
            });
    }
}