using System.Security.Claims;
using AgeDigitalTwins.ApiService.Helpers;
using AgeDigitalTwins.ApiService.Middleware;
using Microsoft.AspNetCore.Http;

namespace AgeDigitalTwins.ApiService.Test.Middleware;

[Trait("Category", "Unit")]
public class UserIdHeaderMiddlewareTests
{
    [Fact]
    public async Task WhenHeaderPresent_CreatesSyntheticIdentityWithCorrectClaims()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Id"] = "b2c-user-123";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "managed-identity")],
            "Bearer"
        ));

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new UserIdHeaderMiddleware(next, "X-User-Id", false);

        await middleware.InvokeAsync(context);

        Assert.Equal("b2c-user-123", context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("b2c-user-123", context.User.FindFirst("sub")?.Value);
        Assert.Equal("b2c-user-123", context.User.FindFirst(ClaimTypes.Name)?.Value);
    }

    [Fact]
    public async Task WhenHeaderPresent_OriginalIdentityPreservedAsSecondary()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Id"] = "b2c-user-123";
        var originalIdentity = new ClaimsIdentity(
            [new Claim("some-original-claim", "original-value")],
            "Bearer"
        );
        context.User = new ClaimsPrincipal(originalIdentity);

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new UserIdHeaderMiddleware(next, "X-User-Id", false);

        await middleware.InvokeAsync(context);

        Assert.Equal(2, context.User.Identities.Count());
        var originalFound = context.User.Identities.Any(i =>
            i.FindFirst("some-original-claim")?.Value == "original-value"
        );
        Assert.True(originalFound, "Original identity should be preserved as secondary identity");
    }

    [Fact]
    public async Task WhenHeaderPresent_PermissionClaimsAreCopiedToSynthetic()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Id"] = "b2c-user-123";
        var originalIdentity = new ClaimsIdentity(
            [
                new Claim("permissions", "digitaltwins/read"),
                new Claim("permissions", "digitaltwins/write"),
                new Claim("roles", "admin"),
                new Claim("scp", "user_impersonation"),
                new Claim("some-other-claim", "should-not-be-copied"),
            ],
            "Bearer"
        );
        context.User = new ClaimsPrincipal(originalIdentity);

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new UserIdHeaderMiddleware(next, "X-User-Id", false);

        await middleware.InvokeAsync(context);

        // Synthetic identity should have the copied claims
        var syntheticIdentity = context.User.Identities.First();
        Assert.Equal("UserIdHeader", syntheticIdentity.AuthenticationType);

        var permissionClaims = syntheticIdentity.FindAll("permissions").Select(c => c.Value).ToList();
        Assert.Contains("digitaltwins/read", permissionClaims);
        Assert.Contains("digitaltwins/write", permissionClaims);

        var roleClaims = syntheticIdentity.FindAll("roles").Select(c => c.Value).ToList();
        Assert.Contains("admin", roleClaims);

        var scpClaim = syntheticIdentity.FindFirst("scp")?.Value;
        Assert.Equal("user_impersonation", scpClaim);

        // Non-copied claim should remain only in the original identity
        Assert.Null(syntheticIdentity.FindFirst("some-other-claim"));
    }

    [Fact]
    public async Task WhenHeaderAbsentAndRequired_Returns401()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "managed-identity")],
            "Bearer"
        ));

        bool nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new UserIdHeaderMiddleware(next, "X-User-Id", true);

        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task WhenHeaderAbsentAndNotRequired_NoChangeToUser()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "managed-identity")],
            "Bearer"
        ));

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new UserIdHeaderMiddleware(next, "X-User-Id", false);

        await middleware.InvokeAsync(context);

        Assert.Equal("managed-identity", context.User.FindFirst("sub")?.Value);
        Assert.Single(context.User.Identities);
    }

    [Fact]
    public async Task WhenHeaderNameEmpty_HeaderIsIgnored()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Id"] = "b2c-user-123";
        var originalIdentity = new ClaimsIdentity(
            [new Claim("sub", "managed-identity")],
            "Bearer"
        );
        context.User = new ClaimsPrincipal(originalIdentity);

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new UserIdHeaderMiddleware(next, "", false);

        await middleware.InvokeAsync(context);

        Assert.Equal("managed-identity", context.User.FindFirst("sub")?.Value);
        Assert.Single(context.User.Identities);
    }

    [Fact]
    public async Task WhenHeaderPresent_RequestHelperParseUserIdReturnsHeaderValue()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-User-Id"] = "b2c-user-456";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "managed-identity")],
            "Bearer"
        ));

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new UserIdHeaderMiddleware(next, "X-User-Id", false);

        await middleware.InvokeAsync(context);

        var result = RequestHelper.ParseUserId(context);
        Assert.Equal("b2c-user-456", result);
    }

    [Fact]
    public async Task WhenHeaderAbsentAndRequired_ResponseBodyIsEmpty()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "managed-identity")],
            "Bearer"
        ));

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new UserIdHeaderMiddleware(next, "X-User-Id", true);

        await middleware.InvokeAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }
}
