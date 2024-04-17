public class LocalCredentials(IConfig config) : ICredentials
{
    private readonly IConfig config = config;

    public (string clientId, string clientSecret) GetForTenant(string tenantId)
    {
        return (this.config.CLIENT_ID, this.config.CLIENT_SECRET);
    }
}