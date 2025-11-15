using System.Security.Claims;
using AgeDigitalTwins.ApiService.Authorization.Models;
using AgeDigitalTwins.ApiService.Configuration;
using AgeDigitalTwins.ApiService.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AgeDigitalTwins.ApiService.Test.Authorization;

/// <summary>
/// Tests for the PermissionService class.
/// </summary>
public class PermissionServiceTests
{
    private readonly Mock<ILogger<PermissionService>> _loggerMock;
    private readonly PermissionService _service;

    public PermissionServiceTests()
    {
        _loggerMock = new Mock<ILogger<PermissionService>>();
        var options = Options.Create(
            new AuthorizationOptions { PermissionsClaimName = "permissions" }
        );
        _service = new PermissionService(options, _loggerMock.Object);
    }

    [Fact]
    public void GetUserPermissions_UnauthenticatedUser_ReturnsEmpty()
    {
        // Arrange
        var user = new ClaimsPrincipal();

        // Act
        var permissions = _service.GetUserPermissions(user);

        // Assert
        Assert.Empty(permissions);
    }

    [Fact]
    public void GetUserPermissions_NoPermissionClaims_ReturnsEmpty()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "testuser") },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);

        // Act
        var permissions = _service.GetUserPermissions(user);

        // Assert
        Assert.Empty(permissions);
    }

    [Fact]
    public void GetUserPermissions_ValidPermissionClaims_ReturnsPermissions()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("permissions", "digitaltwins/read"),
                new Claim("permissions", "models/write"),
                new Claim("permissions", "invalid"), // Should be skipped
            },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);

        // Act
        var permissions = _service.GetUserPermissions(user);

        // Assert
        Assert.Equal(2, permissions.Count);
        Assert.Contains(
            permissions,
            p => p.Resource == ResourceType.DigitalTwins && p.Action == PermissionAction.Read
        );
        Assert.Contains(
            permissions,
            p => p.Resource == ResourceType.Models && p.Action == PermissionAction.Write
        );
    }

    [Fact]
    public void HasPermission_UserHasExactPermission_ReturnsTrue()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            new[] { new Claim("permissions", "digitaltwins/read") },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);
        var required = new Permission(ResourceType.DigitalTwins, PermissionAction.Read);

        // Act
        var result = _service.HasPermission(user, required);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasPermission_UserHasWildcardPermission_ReturnsTrue()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            new[] { new Claim("permissions", "digitaltwins/*") },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);
        var required = new Permission(ResourceType.DigitalTwins, PermissionAction.Read);

        // Act
        var result = _service.HasPermission(user, required);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasPermission_UserLacksPermission_ReturnsFalse()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            new[] { new Claim("permissions", "digitaltwins/read") },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);
        var required = new Permission(ResourceType.DigitalTwins, PermissionAction.Write);

        // Act
        var result = _service.HasPermission(user, required);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasAnyPermission_UserHasOneOfRequired_ReturnsTrue()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            new[] { new Claim("permissions", "digitaltwins/read") },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);
        var required1 = new Permission(ResourceType.DigitalTwins, PermissionAction.Write);
        var required2 = new Permission(ResourceType.DigitalTwins, PermissionAction.Read);

        // Act
        var result = _service.HasAnyPermission(user, required1, required2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasAnyPermission_UserHasNone_ReturnsFalse()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            new[] { new Claim("permissions", "digitaltwins/read") },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);
        var required1 = new Permission(ResourceType.Models, PermissionAction.Read);
        var required2 = new Permission(ResourceType.DigitalTwins, PermissionAction.Write);

        // Act
        var result = _service.HasAnyPermission(user, required1, required2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasAnyPermission_NoRequiredPermissions_ReturnsTrue()
    {
        // Arrange
        var identity = new ClaimsIdentity(new Claim[] { }, "TestAuth");
        var user = new ClaimsPrincipal(identity);

        // Act
        var result = _service.HasAnyPermission(user);

        // Assert
        Assert.True(result);
    }
}
