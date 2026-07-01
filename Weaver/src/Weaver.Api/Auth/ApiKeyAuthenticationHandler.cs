using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Weaver.Infrastructure.Auth;
using Weaver.Infrastructure.Persistence;

namespace Weaver.Api.Auth;

/// <summary>
/// Authenticates requests bearing a personal API key (Authorization: Bearer wvr_...) instead of a
/// JWT -- the server-to-server alternative for scripts/cron jobs that can't run an interactive
/// login flow. Selected instead of the JWT handler by the "Smart" policy scheme in Program.cs based
/// on the token's shape, so both work interchangeably against every endpoint that requires auth.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";

    private readonly WeaverDbContext _db;
    private readonly IApiKeyService _apiKeyService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        WeaverDbContext db,
        IApiKeyService apiKeyService)
        : base(options, logger, encoder)
    {
        _db = db;
        _apiKeyService = apiKeyService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var authHeader = authHeaderValues.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (!_apiKeyService.LooksLikeApiKey(token))
        {
            return AuthenticateResult.NoResult();
        }

        var hash = _apiKeyService.Hash(token);
        var apiKey = await _db.ApiKeys.FirstOrDefaultAsync(k => k.HashedKey == hash);
        if (apiKey is null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        if (apiKey.ExpiresAt is not null && apiKey.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return AuthenticateResult.Fail("This API key has expired.");
        }

        apiKey.LastUsedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, apiKey.OwnerUserId.ToString()) };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
