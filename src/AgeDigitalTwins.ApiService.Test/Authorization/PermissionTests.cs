using AgeDigitalTwins.ServiceDefaults.Authorization.Models;
using Xunit;

namespace AgeDigitalTwins.ApiService.Test.Authorization;

/// <summary>
/// Tests for the Permission class.
/// </summary>
public class PermissionTests
{
    [Theory]
    [InlineData(ResourceType.DigitalTwins, PermissionAction.Read, "digitaltwins/read")]
    [InlineData(ResourceType.DigitalTwins, PermissionAction.Write, "digitaltwins/write")]
    [InlineData(ResourceType.DigitalTwins, PermissionAction.Delete, "digitaltwins/delete")]
    [InlineData(ResourceType.DigitalTwins, PermissionAction.Wildcard, "digitaltwins/*")]
    [InlineData(
        ResourceType.Relationships,
        PermissionAction.Read,
        "digitaltwins/relationships/read"
    )]
    [InlineData(
        ResourceType.Relationships,
        PermissionAction.Write,
        "digitaltwins/relationships/write"
    )]
    [InlineData(ResourceType.Models, PermissionAction.Read, "models/read")]
    [InlineData(ResourceType.Query, PermissionAction.Action, "query/action")]
    [InlineData(ResourceType.JobsImports, PermissionAction.Read, "jobs/imports/read")]
    public void ToString_ReturnsCorrectFormat(
        ResourceType resource,
        PermissionAction action,
        string expectedString
    )
    {
        // Arrange
        var permission = new Permission(resource, action);

        // Act
        var result = permission.ToString();

        // Assert
        Assert.Equal(expectedString, result);
    }

    [Fact]
    public void Grants_ExactMatch_ReturnsTrue()
    {
        // Arrange
        var permission = new Permission(ResourceType.DigitalTwins, PermissionAction.Read);
        var required = new Permission(ResourceType.DigitalTwins, PermissionAction.Read);

        // Act
        var result = permission.Grants(required);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Grants_WildcardPermission_ReturnsTrue()
    {
        // Arrange
        var permission = new Permission(ResourceType.DigitalTwins, PermissionAction.Wildcard);
        var required = new Permission(ResourceType.DigitalTwins, PermissionAction.Read);

        // Act
        var result = permission.Grants(required);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Grants_DifferentResource_ReturnsFalse()
    {
        // Arrange
        var permission = new Permission(ResourceType.DigitalTwins, PermissionAction.Read);
        var required = new Permission(ResourceType.Models, PermissionAction.Read);

        // Act
        var result = permission.Grants(required);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Grants_DifferentAction_ReturnsFalse()
    {
        // Arrange
        var permission = new Permission(ResourceType.DigitalTwins, PermissionAction.Read);
        var required = new Permission(ResourceType.DigitalTwins, PermissionAction.Write);

        // Act
        var result = permission.Grants(required);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Equals_SameResourceAndAction_ReturnsTrue()
    {
        // Arrange
        var permission1 = new Permission(ResourceType.DigitalTwins, PermissionAction.Read);
        var permission2 = new Permission(ResourceType.DigitalTwins, PermissionAction.Read);

        // Act & Assert
        Assert.Equal(permission1, permission2);
        Assert.True(permission1 == permission2);
        Assert.False(permission1 != permission2);
    }

    [Fact]
    public void Equals_DifferentPermissions_ReturnsFalse()
    {
        // Arrange
        var permission1 = new Permission(ResourceType.DigitalTwins, PermissionAction.Read);
        var permission2 = new Permission(ResourceType.DigitalTwins, PermissionAction.Write);

        // Act & Assert
        Assert.NotEqual(permission1, permission2);
        Assert.False(permission1 == permission2);
        Assert.True(permission1 != permission2);
    }

    [Fact]
    public void GetHashCode_SamePermissions_ReturnsSameHash()
    {
        // Arrange
        var permission1 = new Permission(ResourceType.DigitalTwins, PermissionAction.Read);
        var permission2 = new Permission(ResourceType.DigitalTwins, PermissionAction.Read);

        // Act
        var hash1 = permission1.GetHashCode();
        var hash2 = permission2.GetHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }
}
