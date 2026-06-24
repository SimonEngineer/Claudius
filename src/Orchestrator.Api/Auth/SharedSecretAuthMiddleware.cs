using Microsoft.Extensions.Primitives;

namespace Orchestrator.Api.Auth;

/// <summary>
/// Minimal auth for the API/dashboard: if Auth:SharedSecret is configured, every request must
/// present it (as an X-Api-Key header, an Authorization: Bearer header, or an access_token query
/// string value -- the last two so the SignalR JS client's accessTokenFactory works across all
/// transports, including WebSockets, which can't set custom headers in the browser).
/// Disabled entirely (no-op) when the secret is unset, which is the default for the
/// single-user/localhost setup this project assumes out of the box.
/// </summary>
public class SharedSecretAuthMiddleware(RequestDelegate next, IConfiguration configuration)
{
    // Health checks need to stay unauthenticated for infra probes; swagger and the Hangfire
    // dashboard are browser-visited directly (no way to attach a custom header) and the
    // dashboard already enforces its own localhost-only check.
    private static readonly string[] ExemptPathPrefixes = ["/api/health", "/swagger", "/hangfire"];

    public async Task InvokeAsync(HttpContext context)
    {
        var secret = configuration["Auth:SharedSecret"];
        if (string.IsNullOrEmpty(secret) || IsExempt(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (!HasValidSecret(context.Request, secret))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    private static bool IsExempt(PathString path) =>
        ExemptPathPrefixes.Any(prefix => path.StartsWithSegments(prefix));

    private static bool HasValidSecret(HttpRequest request, string secret)
    {
        if (request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader) && Matches(apiKeyHeader, secret))
        {
            return true;
        }

        if (request.Headers.TryGetValue("Authorization", out var authHeader) &&
            authHeader.ToString() is var auth && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
            auth["Bearer ".Length..] == secret)
        {
            return true;
        }

        return request.Query.TryGetValue("access_token", out var queryToken) && Matches(queryToken, secret);
    }

    private static bool Matches(StringValues value, string secret) => value.ToString() == secret;
}
