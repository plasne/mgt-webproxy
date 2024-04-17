using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Threading.Tasks;
using System;
using System.Collections.Concurrent;

public class AzureAdTokenValidator(ILogger<AzureAdTokenValidator> logger) : ITokenValidator
{
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> configManagers = [];

    private readonly ILogger<AzureAdTokenValidator> logger = logger;

    public async Task<JwtSecurityToken> ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();

        // extract the tenantId
        var preValidatedToken = handler.ReadJwtToken(token);
        if (!preValidatedToken.Payload.TryGetValue("tid", out var tid))
        {
            throw new Exception("tid could not be extracted from the token.");
        }
        var tenantId = tid.ToString();
        if (string.IsNullOrEmpty(tenantId))
        {
            throw new Exception("tid did not contain a valid tenant ID.");
        }

        // determine the valid issuers
        var issuers = new string[] {
            $"https://login.microsoftonline.com/{tenantId}/v2.0",
            $"https://sts.windows.net/{tenantId}/"
        };

        // get the configuration manager
        var configManager = this.configManagers.GetOrAdd(tenantId, useTenantId =>
        {
            this.logger.LogDebug(
                "creating new configuration manager for tenant {tid} for a total of {n} managed configurations.",
                useTenantId,
                this.configManagers.Count + 1);
            return new ConfigurationManager<OpenIdConnectConfiguration>(
                $"https://login.microsoftonline.com/{useTenantId}/v2.0/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever() { RequireHttps = true });
        });

        // get the OIDC configuration
        var config = await configManager.GetConfigurationAsync();

        // define the parameters to validate
        var validationParameters = new TokenValidationParameters
        {
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuer = true,
            ValidIssuers = issuers,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKeys = config.SigningKeys
        };

        // validate all previously defined parameters
        handler.ValidateToken(token, validationParameters, out SecurityToken validatedSecurityToken);
        var validatedJwt = validatedSecurityToken as JwtSecurityToken
            ?? throw new Exception("the bearer token was not a JWT security token.");

        return validatedJwt;
    }
}