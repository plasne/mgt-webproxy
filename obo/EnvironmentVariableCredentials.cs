public class EnvironmentVariableCredentials() : ICredentials
{
    public (string clientId, string clientSecret) GetForTenant(string tenantId)
    {
        var clientId = NetBricks.Config.GetOnce($"{tenantId}_CLIENT_ID");
        var clientSecret = NetBricks.Config.GetOnce($"{tenantId}_CLIENT_SECRET");

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new HttpException(403, "You are not authorized to use this application.");
        }

        return (clientId, clientSecret);
    }
}