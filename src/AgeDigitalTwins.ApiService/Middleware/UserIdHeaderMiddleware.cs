using System.Security.Claims;

namespace AgeDigitalTwins.ApiService.Middleware;

public class UserIdHeaderMiddleware
{
    private static readonly string[] _copiedClaimTypes = ["permissions", "roles", "role", "scp"];

    private readonly RequestDelegate _next;
    private readonly string _headerName;
    private readonly bool _required;

    public UserIdHeaderMiddleware(RequestDelegate next, string headerName, bool required)
    {
        _next = next;
        _headerName = headerName;
        _required = required;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(_headerName, out var headerValue)
            && !string.IsNullOrEmpty(headerValue))
        {
            var userId = headerValue.ToString();

            var syntheticIdentity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim("sub", userId),
                new Claim(ClaimTypes.Name, userId),
            ],
            "UserIdHeader"
            );

            var originalIdentities = context.User?.Identities.ToArray() ?? [];
            foreach (var original in originalIdentities)
            {
                foreach (var claim in original.Claims)
                {
                    if (_copiedClaimTypes.Contains(claim.Type))
                    {
                        syntheticIdentity.AddClaim(claim);
                    }
                }
            }

            var allIdentities = new List<ClaimsIdentity> { syntheticIdentity };
            allIdentities.AddRange(originalIdentities);
            context.User = new ClaimsPrincipal(allIdentities);
        }
        else if (_required)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }
}
