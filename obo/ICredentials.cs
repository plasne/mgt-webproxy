using System.Threading.Tasks;

public interface ICredentials
{
    Task<(string clientId, string clientSecret)> GetForTenant(string tenantId);
}