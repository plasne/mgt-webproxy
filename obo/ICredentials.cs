public interface ICredentials
{
    (string clientId, string clientSecret) GetForTenant(string tenantId);
}