using System.Security.Claims;
using System.Text.Encodings.Web;
using AiIssueTracker.Api.Common.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AiIssueTracker.Api.Common.Auth;

public class BffAuthenticationOptions : AuthenticationSchemeOptions;

public class BffAuthenticationHandler(
    IOptionsMonitor<BffAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<BffAuthOptions> bffOptions)
    : AuthenticationHandler<BffAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "BffAuth";
    public const string SecretHeader = "X-Bff-Secret";
    public const string UserIdHeader = "X-User-Id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var sharedSecret = bffOptions.CurrentValue.SharedSecret;
        if (string.IsNullOrWhiteSpace(sharedSecret))
        {
            return Task.FromResult(AuthenticateResult.Fail("BFF shared secret is not configured."));
        }

        if (!Request.Headers.TryGetValue(SecretHeader, out var providedSecret) ||
            !CryptographicEquals(providedSecret.ToString(), sharedSecret))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid BFF secret."));
        }

        var claims = new List<Claim>();
        if (Request.Headers.TryGetValue(UserIdHeader, out var encodedId) &&
            !string.IsNullOrWhiteSpace(encodedId))
        {
            if (!IdEncoding.TryDecode(encodedId.ToString(), out var userId))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid user id header."));
            }

            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
            claims.Add(new Claim("encoded_id", encodedId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }
        return diff == 0;
    }
}
