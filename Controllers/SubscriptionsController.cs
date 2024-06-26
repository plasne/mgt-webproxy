using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionsController(
    IHttpClientFactory httpClientFactory,
    ILogger<SubscriptionsController> logger)
    : ControllerBase
{
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly ILogger<SubscriptionsController> logger = logger;

    private string GetOboToken()
    {
        if (HttpContext.Items.TryGetValue("obo-token", out object? obj))
        {
            var oboToken = obj as string;
            if (oboToken is not null)
            {
                return oboToken;
            }
        }

        throw new HttpException(403, "Forbidden - a bearer token that can be exchanged for an on-behalf-of token to Microsoft Graph must be presented.");
    }

    private static async Task<dynamic> CreateSubscription(HttpClient client, string payload, string accessToken)
    {
        string url = "https://graph.microsoft.com/v1.0/subscriptions";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Content = content;

        using var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new HttpException(400, responseContent);
        }
        else if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"{response.StatusCode}: {responseContent}");
        }

        return JsonConvert.DeserializeObject<dynamic>(responseContent);
    }

    private static async Task<dynamic> RenewSubscription(HttpClient client, string subscriptionId, string payload, string accessToken)
    {
        string url = $"https://graph.microsoft.com/v1.0/subscriptions/{subscriptionId}";
        using var request = new HttpRequestMessage(HttpMethod.Patch, url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Content = content;

        using var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new HttpException(400, responseContent);
        }
        else if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"{response.StatusCode}: {responseContent}");
        }

        return JsonConvert.DeserializeObject<dynamic>(responseContent);
    }

    private static async Task<dynamic> Negotiate(HttpClient client, string notificationUrl, string accessToken)
    {
        var url = notificationUrl
            .Replace("websockets:", "")
            .Replace("/1.0/", "/beta/")
            .Replace("?groupid=", "/negotiate?groupid=")
            .Replace("sessionid=default", $"sessionid={Guid.NewGuid()}")
            + "&negotiateVersion=1";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

        using var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"{response.StatusCode}: {responseContent}");
        }

        return JsonConvert.DeserializeObject<dynamic>(responseContent);
    }

    [HttpPatch("{subscriptionId}")]
    public async Task<IActionResult> Patch([FromRoute] string subscriptionId)
    {
        try
        {
            // get the obo token
            var oboToken = this.GetOboToken();

            // create the subscription
            this.logger.LogDebug("attempting to renew subscription...");
            using var httpClient = this.httpClientFactory.CreateClient();
            string requestBody = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var subscription = await RenewSubscription(httpClient, subscriptionId, requestBody, oboToken);
            this.logger.LogInformation("successfully renewed subscription.");

            // return similar payload to create, but without negotate
            var response = new { subscription };
            return new ContentResult
            {
                Content = JsonConvert.SerializeObject(response),
                ContentType = "application/json",
                StatusCode = 200
            };
        }
        catch (HttpException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "renew subscription exception...");
            return StatusCode(500, "Internal server error.");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create()
    {
        try
        {
            // get the obo token
            var oboToken = this.GetOboToken();

            // create the subscription
            this.logger.LogDebug("attempting to create subscription...");
            using var httpClient = this.httpClientFactory.CreateClient();
            string requestBody = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var subscription = await CreateSubscription(httpClient, requestBody, oboToken);
            this.logger.LogInformation("successfully created subscription.");

            // negotiate with the notification endpoint
            this.logger.LogDebug("attempting to negotiate Signal-R endpoint...");
            var negotiate = await Negotiate(httpClient, (string)subscription.notificationUrl, oboToken);
            this.logger.LogInformation("successfully negotiated Signal-R endpoint.");

            // return the full payload
            var response = new { subscription, negotiate };
            return new ContentResult
            {
                Content = JsonConvert.SerializeObject(response),
                ContentType = "application/json",
                StatusCode = 200
            };
        }
        catch (HttpException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "create subscription exception...");
            return StatusCode(500, "Internal server error.");
        }
    }
}
