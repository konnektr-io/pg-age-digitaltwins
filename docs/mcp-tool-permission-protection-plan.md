# MCP Tool Permission Protection - Implementation Plan

## Goal

Protect individual MCP tools with the same permission system used by API Service endpoints, allowing fine-grained control over what tools users can access.

## Current State

- ✅ OAuth 2.1 authentication working (JWT Bearer tokens from Auth0)
- ✅ Middleware checks for "mcp:tools" scope globally
- ✅ Middleware checks for "mcp/tools" permission (ResourceType.McpTools)
- ❌ Individual tools don't enforce specific permissions yet

## Options for Implementation

### Option 1: Attribute-Based (Recommended)

Create a custom attribute that can be applied to MCP tool methods:

```csharp
[RequireMcpPermission(ResourceType.DigitalTwins, PermissionAction.Read)]
[McpTool("get_digital_twin")]
public async Task<DigitalTwinResponse> GetDigitalTwin(string id)
{
    // Implementation
}
```

**Pros:**
- Declarative and clear
- Consistent with API Service pattern
- Easy to see permissions at a glance
- Can be validated at compile-time

**Cons:**
- Requires intercepting tool execution
- MCP library may not support attribute-based authorization

### Option 2: Manual Checks in Tool Methods

Add permission checks at the start of each tool method:

```csharp
[McpTool("get_digital_twin")]
public async Task<DigitalTwinResponse> GetDigitalTwin(string id)
{
    await _authService.RequirePermissionAsync(
        ResourceType.DigitalTwins, 
        PermissionAction.Read
    );
    
    // Implementation
}
```

**Pros:**
- Simple and straightforward
- Works with any MCP library
- Full control over authorization logic

**Cons:**
- Must remember to add to every tool
- Boilerplate code in each method
- Easy to forget

### Option 3: Wrapper/Proxy Pattern

Create a wrapper that checks permissions before calling the actual tool:

```csharp
public class AuthorizedDigitalTwinsTools
{
    private readonly DigitalTwinsTools _innerTools;
    private readonly IPermissionService _authService;
    
    [McpTool("get_digital_twin")]
    public async Task<DigitalTwinResponse> GetDigitalTwin(string id)
    {
        await _authService.RequirePermissionAsync(
            ResourceType.DigitalTwins, 
            PermissionAction.Read
        );
        return await _innerTools.GetDigitalTwin(id);
    }
}
```

**Pros:**
- Separates authorization from business logic
- Can be auto-generated
- Clear separation of concerns

**Cons:**
- More code to maintain
- Duplicate method signatures

## Recommended Approach: Option 2 + Helper Service

Use manual checks with a helper service to minimize boilerplate:

### Step 1: Create Permission Check Service

```csharp
public interface IMcpAuthorizationService
{
    /// <summary>
    /// Checks if the current user has the specified permission.
    /// </summary>
    Task RequirePermissionAsync(
        ResourceType resource, 
        PermissionAction action,
        CancellationToken cancellationToken = default
    );
    
    /// <summary>
    /// Checks if the current user has any of the specified permissions.
    /// </summary>
    Task RequireAnyPermissionAsync(
        IEnumerable<Permission> permissions,
        CancellationToken cancellationToken = default
    );
}

public class McpAuthorizationService : IMcpAuthorizationService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPermissionProvider _permissionProvider;
    private readonly ILogger<McpAuthorizationService> _logger;
    
    public async Task RequirePermissionAsync(
        ResourceType resource, 
        PermissionAction action,
        CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedException("Authentication required");
        }
        
        var userPermissions = await _permissionProvider.GetPermissionsAsync(
            httpContext.User, 
            cancellationToken
        );
        
        var required = new Permission(resource, action);
        if (!userPermissions.Any(p => p.Grants(required)))
        {
            _logger.LogWarning(
                "User {User} denied access to tool requiring permission {Permission}",
                httpContext.User.FindFirst("sub")?.Value ?? "Unknown",
                required
            );
            throw new ForbiddenException($"Required permission: {required}");
        }
    }
}
```

### Step 2: Update Tool Classes

```csharp
public class DigitalTwinsTools
{
    private readonly AgeDigitalTwinsClient _client;
    private readonly IMcpAuthorizationService _auth;
    private readonly ILogger<DigitalTwinsTools> _logger;
    
    public DigitalTwinsTools(
        AgeDigitalTwinsClient client,
        IMcpAuthorizationService auth,
        ILogger<DigitalTwinsTools> logger)
    {
        _client = client;
        _auth = auth;
        _logger = logger;
    }
    
    [McpTool("get_digital_twin")]
    [Description("Retrieves a digital twin by ID")]
    public async Task<string> GetDigitalTwin(
        [Description("The ID of the digital twin")] string id)
    {
        // Check permission before execution
        await _auth.RequirePermissionAsync(
            ResourceType.DigitalTwins, 
            PermissionAction.Read
        );
        
        _logger.LogInformation("Fetching digital twin: {Id}", id);
        var twin = await _client.GetDigitalTwinAsync(id);
        return JsonSerializer.Serialize(twin, new JsonSerializerOptions { WriteIndented = true });
    }
    
    [McpTool("delete_digital_twin")]
    [Description("Deletes a digital twin by ID")]
    public async Task<string> DeleteDigitalTwin(
        [Description("The ID of the digital twin")] string id)
    {
        // Check permission before execution
        await _auth.RequirePermissionAsync(
            ResourceType.DigitalTwins, 
            PermissionAction.Delete
        );
        
        _logger.LogWarning("Deleting digital twin: {Id}", id);
        await _client.DeleteDigitalTwinAsync(id);
        return $"Successfully deleted digital twin: {id}";
    }
}
```

### Step 3: Register in Program.cs

```csharp
// Add authorization service
if (enableAuthorization)
{
    builder.Services.AddScoped<IMcpAuthorizationService, McpAuthorizationService>();
}
else
{
    // Provide a no-op implementation when authorization is disabled
    builder.Services.AddScoped<IMcpAuthorizationService, NoOpMcpAuthorizationService>();
}
```

### Step 4: Custom Exceptions

```csharp
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
```

## Implementation Steps

1. **Create Services folder** in MCPServerSSE project
2. **Create IMcpAuthorizationService.cs** interface
3. **Create McpAuthorizationService.cs** implementation
4. **Create NoOpMcpAuthorizationService.cs** for when auth is disabled
5. **Create custom exception types** (UnauthorizedException, ForbiddenException)
6. **Update Program.cs** to register the service
7. **Update DigitalTwinsTools.cs** to inject and use the service
8. **Add error handling** in MCP endpoint to catch and format exceptions
9. **Add tests** for authorization service and tool protection
10. **Update documentation** to explain permission requirements for each tool

## Permission Mapping

| Tool | Resource Type | Permission Action | Permission String |
|------|--------------|-------------------|-------------------|
| get_digital_twin | DigitalTwins | Read | digitaltwins/read |
| list_digital_twins | DigitalTwins | Read | digitaltwins/read |
| create_digital_twin | DigitalTwins | Write | digitaltwins/write |
| update_digital_twin | DigitalTwins | Write | digitaltwins/write |
| delete_digital_twin | DigitalTwins | Delete | digitaltwins/delete |
| query_digital_twins | Query | Read | query/read |
| get_relationship | Relationships | Read | digitaltwins/relationships/read |
| create_relationship | Relationships | Write | digitaltwins/relationships/write |
| delete_relationship | Relationships | Delete | digitaltwins/relationships/delete |

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task GetDigitalTwin_WithoutPermission_ThrowsForbidden()
{
    // Arrange
    var mockAuth = new Mock<IMcpAuthorizationService>();
    mockAuth.Setup(x => x.RequirePermissionAsync(
        ResourceType.DigitalTwins,
        PermissionAction.Read,
        It.IsAny<CancellationToken>()
    )).ThrowsAsync(new ForbiddenException("Required permission: digitaltwins/read"));
    
    var tools = new DigitalTwinsTools(mockClient, mockAuth.Object, mockLogger);
    
    // Act & Assert
    await Assert.ThrowsAsync<ForbiddenException>(
        () => tools.GetDigitalTwin("twin-1")
    );
}

[Fact]
public async Task GetDigitalTwin_WithPermission_ReturnsData()
{
    // Arrange
    var mockAuth = new Mock<IMcpAuthorizationService>();
    // Setup to not throw
    
    var tools = new DigitalTwinsTools(mockClient, mockAuth.Object, mockLogger);
    
    // Act
    var result = await tools.GetDigitalTwin("twin-1");
    
    // Assert
    Assert.NotNull(result);
    mockAuth.Verify(x => x.RequirePermissionAsync(
        ResourceType.DigitalTwins,
        PermissionAction.Read,
        It.IsAny<CancellationToken>()
    ), Times.Once);
}
```

### Integration Tests

```csharp
[Fact]
public async Task MCP_Tool_WithValidToken_Succeeds()
{
    // Arrange
    var token = await GetValidJwtTokenAsync("digitaltwins/read");
    var client = CreateMcpClient(token);
    
    // Act
    var response = await client.CallToolAsync("get_digital_twin", new { id = "twin-1" });
    
    // Assert
    Assert.True(response.IsSuccess);
}

[Fact]
public async Task MCP_Tool_WithoutPermission_Returns403()
{
    // Arrange
    var token = await GetValidJwtTokenAsync(); // No permissions
    var client = CreateMcpClient(token);
    
    // Act
    var response = await client.CallToolAsync("get_digital_twin", new { id = "twin-1" });
    
    // Assert
    Assert.Equal(403, response.StatusCode);
    Assert.Contains("insufficient_permissions", response.Error);
}
```

## Error Responses

When a tool is called without proper permissions, the MCP server should return:

```json
{
  "error": {
    "code": "insufficient_permissions",
    "message": "Required permission: digitaltwins/read",
    "details": {
      "required_permission": "digitaltwins/read",
      "user_permissions": ["query/read"],
      "resource_metadata": "https://your-server/.well-known/oauth-protected-resource"
    }
  }
}
```

## Future Enhancements

1. **Permission Caching** - Cache user permissions for better performance
2. **Audit Logging** - Log all permission checks and denials
3. **Rate Limiting** - Per-tool rate limits based on permissions
4. **Dynamic Permissions** - Load tool permissions from configuration
5. **Permission Hierarchy** - "digitaltwins/*" grants all digitaltwins permissions

## Questions & Decisions

### Q: Should we check permissions in middleware or in tools?

**Decision:** Both. Middleware checks for global "mcp/tools" permission, tools check for specific resource/action permissions.

### Q: What if authorization is disabled?

**Decision:** Provide NoOpMcpAuthorizationService that allows all operations.

### Q: Should we support wildcards in tool permissions?

**Decision:** Yes, Permission.Grants() already supports wildcards (e.g., "digitaltwins/*").

---

**Status:** Ready to Implement  
**Estimated Effort:** 4-6 hours  
**Priority:** High (required for production security)
