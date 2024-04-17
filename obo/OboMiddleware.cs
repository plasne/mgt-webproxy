using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class OboMiddleware(
    RequestDelegate next,
    IConfig config,
    IHttpClientFactory httpClientFactory,
    ICredentials credentials,
    ILogger<OboMiddleware> logger,
    ICache? cache = null)
{
    private readonly RequestDelegate next = next;
    private readonly IConfig config = config;
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly ICredentials credentials = credentials;
    private readonly ILogger<OboMiddleware> logger = logger;
    private readonly ICache? cache = cache;

    private async Task<string> GetOboTokenFromCache(
        HttpContext context,
        string tenantId,
        string origToken,
        JwtSecurityToken unvalJwt)
    {
        // authenticate the user
        var authResult = await context.AuthenticateAsync("multi-auth");
        if (!authResult.Succeeded)
        {
            throw new HttpException(401, "The provided Bearer Token could not be validated.");
        }

        // ensure there is an oid
        if (!unvalJwt.Payload.TryGetString("oid", out var oid))
        {
            throw new HttpException(403, "You must provide a Bearer Token that contains the oid element.");
        }

        // ensure there is an aud
        if (!unvalJwt.Payload.TryGetString("oid", out var aud))
        {
            throw new HttpException(403, "You must provide a Bearer Token that contains the aud element.");
        }

        // get or set from cache
        var key = $"{tenantId}:{oid}";
        this.logger.LogDebug("attempting to get OBO token from cache with key {key}...", key);
        var cacheEntry = await this.cache!.GetOrSetAsync(
            key,
            entry =>
            {
                // when getting from cache, ensure the aud matches
                if (entry.OrigAud != aud)
                {
                    throw new HttpException(403, "The aud in the Bearer Token does not appear to be valid.");
                }

                this.logger.LogInformation("successfully obtained OBO token from cache.");
                return Task.CompletedTask;
            },
            async () =>
            {
                // get from azure if it is not cached
                var oboToken = await GetOboTokenFromAzure(tenantId, origToken);
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(oboToken);
                jwt.Payload.TryGetInt("exp", out int exp); // if it was invalid, it will be 0
                var expiry = DateTimeOffset.FromUnixTimeSeconds(exp);
                return new CacheEntry(oboToken, aud, expiry);
            });

        return cacheEntry.OboToken;
    }

    private async Task<string> GetOboTokenFromAzure(string tenantId, string origToken)
    {
        this.logger.LogDebug("attempting to get OBO token from Azure...");

        var (clientId, clientSecret) = await this.credentials.GetForTenant(tenantId);
        string url = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        var collection = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new("client_id", clientId),
                new("client_secret", clientSecret),
                new("assertion", origToken),
                new("scope", this.config.SCOPE),
                new("requested_token_use", "on_behalf_of")
            };
        using var content = new FormUrlEncodedContent(collection);
        request.Content = content;

        using var client = this.httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request);

        var responseContent = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"{response.StatusCode}: {responseContent}");
        }

        using var doc = JsonDocument.Parse(responseContent);
        if (!doc.RootElement.TryGetProperty("access_token", out var accessTokenElement))
        {
            throw new Exception("no access_token was not found in the OBO token response.");
        }

        var accessToken = accessTokenElement.GetString()
            ?? throw new Exception("the access token found in OBO token was empty.");

        this.logger.LogInformation("successfully obtained OBO token from Azure.");
        return accessToken;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // ensure the user has a token
            var authHeader = context.Request.Headers.Authorization;
            var origToken = authHeader.FirstOrDefault()?.Replace("Bearer ", "", StringComparison.InvariantCultureIgnoreCase);
            if (origToken is null)
            {
                throw new HttpException(401, "You must provide a Bearer Token in the Authorization header.");
            }

            // decode the JWT
            var handler = new JwtSecurityTokenHandler();
            var unvalJwt = handler.ReadJwtToken(origToken);

            // get the tenant id
            if (!unvalJwt.Payload.TryGetString("tid", out var tenantId))
            {
                throw new HttpException(403, "You must provide a Bearer Token that contains the tid element.");
            }

            // get the obo token
            var oboToken = this.cache is not null
                ? await GetOboTokenFromCache(context, tenantId, origToken, unvalJwt)
                : await GetOboTokenFromAzure(tenantId, origToken);

            // set the obo token
            context.Items.Add("obo-token", oboToken);
        }
        catch (HttpException ex)
        {
            this.logger.LogError(ex, "exception in obo middleware...");
            context.Response.StatusCode = ex.StatusCode;
            await context.Response.WriteAsync(ex.Message);
            return;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "exception in obo middleware...");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync("Internal server error.");
            return;
        }

        // next
        await this.next(context);
    }
}