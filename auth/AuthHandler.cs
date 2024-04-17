using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class AuthOptions : AuthenticationSchemeOptions { }

public class AuthHandler(
    IOptionsMonitor<AuthOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    ITokenValidator tokenValidator)
    : AuthenticationHandler<AuthOptions>(options, loggerFactory, encoder)
{
    private readonly ITokenValidator tokenValidator = tokenValidator;

    private readonly ILogger<AuthHandler> logger = loggerFactory.CreateLogger<AuthHandler>();

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            this.logger.LogDebug("starting authentication check...");

            // extract bearer token from header
            var header = Request.Headers.Authorization;
            var token = header.FirstOrDefault()?.Replace("Bearer ", "", StringComparison.InvariantCultureIgnoreCase);
            if (token is null)
            {
                this.logger.LogInformation("no bearer token was provided.");
                return AuthenticateResult.NoResult();
            }

            // validate the token
            this.logger.LogDebug("attempting to validate the incoming bearer token...");
            var jwt = await this.tokenValidator.ValidateToken(token);
            this.logger.LogInformation("the incoming bearer token was successfully validated.");

            // we don't care about any claims for this use-case
            var claims = new List<Claim>();

            // build the identity, principal, and ticket
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            // success
            this.logger.LogInformation("authentication was successful.");
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception e)
        {
            this.logger.LogWarning(e, "authentication exception...");
            return AuthenticateResult.Fail(e);
        }
    }
}