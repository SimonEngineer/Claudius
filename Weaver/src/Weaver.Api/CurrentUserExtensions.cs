using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Weaver.Api;

public static class CurrentUserExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out var id) ? id : throw new InvalidOperationException("Request is missing a valid user id claim.");
    }
}
