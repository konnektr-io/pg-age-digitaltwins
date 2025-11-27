using System.Security.Claims;
using AgeDigitalTwins.ApiService.Authorization;
using AgeDigitalTwins.ApiService.Authorization.Models;
using AgeDigitalTwins.ApiService.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AgeDigitalTwins.ApiService.Test.Authorization;

/// <summary>
/// Tests for the ClaimsPermissionProvider class.
/// </summary>
public class ClaimsPermissionProviderTests
{
    private readonly Mock<ILogger<ClaimsPermissionProvider>> _loggerMock;
    private readonly ClaimsPermissionProvider _provider;

    public ClaimsPermissionProviderTests()
    {
        _loggerMock = new Mock<ILogger<ClaimsPermissionProvider>>();
        var options = Options.Create(
            new AuthorizationOptions { PermissionsClaimName = "permissions" }
        );
        _provider = new ClaimsPermissionProvider(options, _loggerMock.Object);
    }

    [Fact]
    public async Task GetPermissionsAsync_UnauthenticatedUser_ReturnsEmpty()
    {
        // Arrange
        var user = new ClaimsPrincipal();

        // Act
        var permissions = await _provider.GetPermissionsAsync(user, default);

        // Assert
        Assert.Empty(permissions);
    }

    [Fact]
    public async Task GetPermissionsAsync_NoPermissionClaims_ReturnsEmpty()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "testuser") },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);

        // Act
        var permissions = await _provider.GetPermissionsAsync(user, default);

        // Assert
        Assert.Empty(permissions);
    }

    [Fact]
    public async Task GetPermissionsAsync_ValidPermissionClaims_ReturnsPermissions()
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
        var permissions = await _provider.GetPermissionsAsync(user, default);

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
    public async Task GetPermissionsAsync_CustomClaimName_ReturnsPermissions()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ClaimsPermissionProvider>>();
        var options = Options.Create(
            new AuthorizationOptions { PermissionsClaimName = "custom_permissions" }
        );
        var provider = new ClaimsPermissionProvider(options, loggerMock.Object);

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("custom_permissions", "digitaltwins/read"),
                new Claim("permissions", "models/write"), // Wrong claim name
            },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);

        // Act
        var permissions = await provider.GetPermissionsAsync(user, default);

        // Assert
        Assert.Single(permissions);
        Assert.Contains(
            permissions,
            p => p.Resource == ResourceType.DigitalTwins && p.Action == PermissionAction.Read
        );
    }

    [Fact]
    public async Task GetPermissionsAsync_WildcardPermissions_ReturnsPermissions()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            new[] { new Claim("permissions", "digitaltwins/*") },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);

        // Act
        var permissions = await _provider.GetPermissionsAsync(user, default);

        // Assert
        Assert.Single(permissions);
        Assert.Contains(
            permissions,
            p => p.Resource == ResourceType.DigitalTwins && p.Action == PermissionAction.Wildcard
        );
    }

    [Fact]
    public async Task GetPermissionsAsync_DuplicatePermissions_ReturnsDistinct()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("permissions", "digitaltwins/read"),
                new Claim("permissions", "digitaltwins/read"), // Duplicate
            },
            "TestAuth"
        );
        var user = new ClaimsPrincipal(identity);

        // Act
        var permissions = await _provider.GetPermissionsAsync(user, default);

        // Assert
        Assert.Single(permissions);
    }
}
