using System.Security.Claims;
using AgeDigitalTwins.ApiService.Authorization;
using AgeDigitalTwins.ApiService.Authorization.Models;
using AgeDigitalTwins.ApiService.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgeDigitalTwins.ApiService.Test.Authorization;

/// <summary>
/// Tests for the PermissionService class.
/// </summary>
public class PermissionServiceTests
{
    private readonly Mock<IPermissionProvider> _providerMock;
    private readonly Mock<ILogger<PermissionService>> _loggerMock;
    private readonly PermissionService _service;

    public PermissionServiceTests()
    {
        _providerMock = new Mock<IPermissionProvider>();
        _loggerMock = new Mock<ILogger<PermissionService>>();
        _service = new PermissionService(_providerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void GetUserPermissions_CallsProvider()
    {
        // Arrange
        var user = new ClaimsPrincipal();
        var expectedPermissions = new List<Permission>
        {
            new(ResourceType.DigitalTwins, PermissionAction.Read),
        }.AsReadOnly();

        _providerMock
            .Setup(p => p.GetPermissionsAsync(user, default))
            .ReturnsAsync(expectedPermissions);

        // Act
        var permissions = _service.GetUserPermissions(user);

        // Assert
        Assert.Equal(expectedPermissions, permissions);
        _providerMock.Verify(p => p.GetPermissionsAsync(user, default), Times.Once);
    }

    [Fact]
    public void HasPermission_UserHasExactPermission_ReturnsTrue()
    {
        // Arrange
        var user = new ClaimsPrincipal();
        var userPermissions = new List<Permission>
        {
            new(ResourceType.DigitalTwins, PermissionAction.Read),
        }.AsReadOnly();

        _providerMock
            .Setup(p => p.GetPermissionsAsync(user, default))
            .ReturnsAsync(userPermissions);

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
        var user = new ClaimsPrincipal();
        var userPermissions = new List<Permission>
        {
            new(ResourceType.DigitalTwins, PermissionAction.Wildcard),
        }.AsReadOnly();

        _providerMock
            .Setup(p => p.GetPermissionsAsync(user, default))
            .ReturnsAsync(userPermissions);

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
        var user = new ClaimsPrincipal();
        var userPermissions = new List<Permission>
        {
            new(ResourceType.DigitalTwins, PermissionAction.Read),
        }.AsReadOnly();

        _providerMock
            .Setup(p => p.GetPermissionsAsync(user, default))
            .ReturnsAsync(userPermissions);

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
        var user = new ClaimsPrincipal();
        var userPermissions = new List<Permission>
        {
            new(ResourceType.DigitalTwins, PermissionAction.Read),
        }.AsReadOnly();

        _providerMock
            .Setup(p => p.GetPermissionsAsync(user, default))
            .ReturnsAsync(userPermissions);

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
        var user = new ClaimsPrincipal();
        var userPermissions = new List<Permission>
        {
            new(ResourceType.DigitalTwins, PermissionAction.Read),
        }.AsReadOnly();

        _providerMock
            .Setup(p => p.GetPermissionsAsync(user, default))
            .ReturnsAsync(userPermissions);

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
        var user = new ClaimsPrincipal();

        // Act
        var result = _service.HasAnyPermission(user);

        // Assert
        Assert.True(result);
    }
}
