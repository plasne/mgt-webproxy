using System;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

public class KeyVaultCredentials(
    IConfig config,
    DefaultAzureCredential credential,
    ILogger<KeyVaultCredentials> logger)
    : ICredentials
{
    private readonly IConfig config = config;
    private readonly DefaultAzureCredential credential = credential;
    private readonly ILogger<KeyVaultCredentials> logger = logger;
    private readonly MemoryCache cache = new MemoryCache(new MemoryCacheOptions());

    public Task<(string clientId, string clientSecret)> GetForTenant(string tenantId)
    {
        return this.cache.GetOrCreateAsync(tenantId, async entry =>
        {
            this.logger.LogDebug("attempting to get CLIENT_ID and CLIENT_SECRET from {key}...", this.config.KEYVAULT_URL);

            var client = new SecretClient(new Uri(this.config.KEYVAULT_URL), this.credential);
            var clientIdTask = client.GetSecretAsync($"{tenantId}-CLIENT-ID");
            var clientSecretTask = client.GetSecretAsync($"{tenantId}-CLIENT-SECRET");
            await Task.WhenAll(clientIdTask, clientSecretTask);

            var clientId = clientIdTask.Result.Value.Value;
            var clientSecret = clientSecretTask.Result.Value.Value;

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new HttpException(403, "You are not authorized to use this application.");
            }

            this.logger.LogDebug("successfully obtained CLIENT_ID and CLIENT_SECRET from {key}.", this.config.KEYVAULT_URL);
            return (clientId, clientSecret);
        });
    }
}