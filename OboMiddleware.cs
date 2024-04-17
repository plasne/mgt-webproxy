using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class OboMiddleware(
    RequestDelegate next,
    IHttpClientFactory httpClientFactory,
    ICredentials credentials,
    ILogger<OboMiddleware> logger)
{
    private readonly RequestDelegate next = next;
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly ICredentials credentials = credentials;
    private readonly ILogger<OboMiddleware> logger = logger;

    private string? GetTenantId(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        if (jwtToken.Payload.TryGetValue("tid", out var tid))
        {
            return tid?.ToString();
        }

        return null;
    }

    private async Task<string> GetOnBehalfOfToken(string tenantId, string token)
    {
        var (clientId, clientSecret) = this.credentials.GetForTenant(tenantId);
        string url = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        var collection = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new("client_id", clientId),
                new("client_secret", clientSecret),
                new("assertion", token),
                new("scope", "https://graph.microsoft.com/chat.read"),
                new("requested_token_use", "on_behalf_of")
            };
        using var content = new FormUrlEncodedContent(collection);
        request.Content = content;

        using var client = this.httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseContent);
        if (doc.RootElement.TryGetProperty("access_token", out var accessTokenElement))
        {
            var accessToken = accessTokenElement.GetString()
                ?? throw new Exception("the access token found in OBO token was empty.");
            return accessToken;
        }

        throw new Exception("no access token found in OBO token.");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // ensure the user has a token
            string? authHeader = context.Request.Headers["Authorization"];
            if (authHeader is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("You must provide a Bearer Token in the Authorization header.");
                return;
            }
            var origToken = authHeader.Replace("Bearer ", "", StringComparison.InvariantCultureIgnoreCase);

            // get the customer data
            var tenantId = this.GetTenantId(origToken);
            if (string.IsNullOrEmpty(tenantId))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("The Bearer Token must contain a tid (Tenant ID).");
                return;
            }

            // get the on-behalf-of token
            this.logger.LogDebug("attempting to get OBO token...");
            var oboToken = await this.GetOnBehalfOfToken(tenantId, origToken);
            this.logger.LogInformation("successfully obtained OBO token.");

            // add to the context
            context.Items.Add("obo-token", oboToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex.Message);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync("Internal server error.");
            return;
        }

        // next
        await this.next(context);
    }
}