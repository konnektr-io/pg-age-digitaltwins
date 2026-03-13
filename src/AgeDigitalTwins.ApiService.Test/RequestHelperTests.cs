using System.Security.Claims;
using AgeDigitalTwins.ApiService.Helpers;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AgeDigitalTwins.ApiService.Test;

[Trait("Category", "Unit")]
public class RequestHelperTests
{
    [Fact]
    public void ParseUserId_NameIdentifierClaim_ReturnsUserId()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-from-name-identifier")],
            "TestAuth"
        );
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };

        // Act
        var result = RequestHelper.ParseUserId(httpContext);

        // Assert
        Assert.Equal("user-from-name-identifier", result);
    }

    [Fact]
    public void ParseUserId_SubClaim_ReturnsUserId()
    {
        // Arrange - only "sub" claim present (no NameIdentifier)
        var identity = new ClaimsIdentity(
            [new Claim("sub", "user-from-sub-claim")],
            "TestAuth"
        );
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };

        // Act
        var result = RequestHelper.ParseUserId(httpContext);

        // Assert
        Assert.Equal("user-from-sub-claim", result);
    }

    [Fact]
    public void ParseUserId_NameIdentifierTakesPrecedenceOverSub()
    {
        // Arrange - both claims present; NameIdentifier should win
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "preferred-id"),
                new Claim("sub", "fallback-id"),
            ],
            "TestAuth"
        );
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };

        // Act
        var result = RequestHelper.ParseUserId(httpContext);

        // Assert
        Assert.Equal("preferred-id", result);
    }

    [Fact]
    public void ParseUserId_NoClaims_ReturnsNull()
    {
        // Arrange - authenticated but no relevant claims
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "some-user")],
            "TestAuth"
        );
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };

        // Act
        var result = RequestHelper.ParseUserId(httpContext);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseUserId_UnauthenticatedUser_ReturnsNull()
    {
        // Arrange - no identity at all
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal() };

        // Act
        var result = RequestHelper.ParseUserId(httpContext);

        // Assert
        Assert.Null(result);
    }
}
