# MCP Server OAuth 2.1 Authorization Setup

This document explains how the MCP Server integrates with the API Service to provide OAuth 2.1-compliant authorization while using the same tokens and permissions.

## Architecture Overview

The MCP Server acts as a **proxy/client** to the API Service:

```
User → MCP Client (e.g., VS Code) → MCP Server → API Service → Database
       [User Token]                  [Validates]   [Enforces]
                                     [Checks Perms]
```

### Key Benefits

1. **Same Token**: Users use the same Auth0 token for both MCP Server and API Service
2. **Unified Permissions**: Permission checking is centralized via the ApiPermissionProvider
3. **MCP Compliant**: Implements RFC 9728 (Protected Resource Metadata) and OAuth 2.1
4. **Flexible**: Can use direct database access OR API Service calls

## Configuration

### 1. Authentication Setup

```json
{
  "Authentication": {
    "Enabled": true,
    "Authority": "https://YOUR_DOMAIN.auth0.com",
    "Issuer": "https://YOUR_DOMAIN.auth0.com/",
    "Audience": "https://your-mcp-server.com",
    "MetadataAddress": "https://YOUR_DOMAIN.auth0.com/.well-known/openid-configuration"
  }
}
```

### 2. Authorization Setup

Choose between two permission providers:

#### Option A: Claims-Based (Default)
Permissions are embedded in the JWT token as claims:

```json
{
  "Authorization": {
    "Enabled": true,
    "Provider": "Claims",
    "PermissionsClaimName": "permissions",
    "RequiredScopes": ["mcp:tools"]
  }
}
```

#### Option B: API-Based (Recommended for Production)
Permissions are fetched from the Control Plane API:

```json
{
  "Authorization": {
    "Enabled": true,
    "Provider": "Api",
    "PermissionsClaimName": "permissions",
    "RequiredScopes": ["mcp:tools"],
    "ScopesSupported": ["mcp:tools", "mcp:resources"],
    "ApiProvider": {
      "BaseUrl": "https://api.ktrlplane.konnektr.io",
      "CheckEndpoint": "/api/v1/permissions/check",
      "ResourceName": "digitaltwins",
      "CacheExpirationMinutes": 5,
      "TimeoutSeconds": 10,
      "TokenEndpoint": "https://YOUR_DOMAIN.auth0.com/oauth/token",
      "Audience": "https://api.ktrlplane.konnektr.io",
      "ClientId": "YOUR_MCP_SERVER_CLIENT_ID",
      "ClientSecret": "YOUR_MCP_SERVER_CLIENT_SECRET"
    }
  }
}
```

**Note:** When using API provider, the MCP Server uses M2M (Machine-to-Machine) credentials to authenticate with the Control Plane API to check user permissions.

### 3. API Service Configuration (Optional)

If you want MCP tools to call the API Service instead of direct database access:

```json
{
  "ApiService": {
    "BaseUrl": "https://your-graph.api.konnektr.io",
    "TimeoutSeconds": 30
  }
}
```

### 4. MCP Server Metadata

```json
{
  "MCP": {
    "ServerName": "Konnektr Graph MCP Server",
    "Version": "1.0.0",
    "ResourceServerUrl": "https://your-mcp-server.com"
  }
}
```

## Auth0 Setup

### 1. Create API (Audience)

In your Auth0 dashboard:

1. Navigate to **Applications → APIs**
2. Click **Create API**
3. Name: `Konnektr Graph MCP Server`
4. Identifier: `https://your-mcp-server.com` (this is your audience)
5. Signing Algorithm: `RS256`

### 2. Configure Scopes

In your API settings, add these scopes:

- `mcp:tools` - Access to MCP tools
- `mcp:resources` - Access to MCP resources (future use)
- `mcp:prompts` - Access to MCP prompts (future use)

### 3. Configure Permissions

If using API-based permissions, set up permissions in your permission system that align with your resources:

- `digitaltwins/read` - Read digital twins
- `digitaltwins/write` - Create/update digital twins
- `digitaltwins/delete` - Delete digital twins
- `models/read` - Read models
- `models/write` - Create/update models
- `query/read` - Execute queries

### 4. Create M2M Application (for API Permission Provider)

If using `Provider: "Api"`, create a Machine-to-Machine application:

1. Navigate to **Applications → Applications**
2. Click **Create Application**
3. Name: `MCP Server Permission Checker`
4. Type: **Machine to Machine Application**
5. Authorize it to call your Control Plane API
6. Grant necessary scopes (e.g., `read:permissions`)
7. Copy the **Client ID** and **Client Secret**

### 5. Create User Application

For end users to authenticate:

1. Navigate to **Applications → Applications**
2. Click **Create Application**
3. Name: `Konnektr Platform`
4. Type: **Single Page Application** or **Native**
5. Configure **Allowed Callback URLs**, **Allowed Logout URLs**, etc.
6. In **APIs** tab, authorize this app to access your MCP Server API
7. Ensure required scopes are granted

## OAuth 2.1 Flow

### 1. Discovery

MCP clients discover authorization endpoints via the Protected Resource Metadata:

```bash
GET https://your-mcp-server.com/.well-known/oauth-protected-resource
```

Response:
```json
{
  "resource": "https://your-mcp-server.com",
  "authorization_servers": ["https://YOUR_DOMAIN.auth0.com"],
  "scopes_supported": ["mcp:tools", "mcp:resources"],
  "bearer_methods_supported": ["header"]
}
```

### 2. Authorization

1. User opens MCP client (e.g., VS Code)
2. Client redirects user to Auth0 for login
3. User authenticates and grants scopes
4. Auth0 returns authorization code
5. Client exchanges code for access token
6. Client stores token securely

### 3. API Calls

Every MCP request includes the token:

```http
POST https://your-mcp-server.com/mcp
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...
```

### 4. Token Validation

The MCP Server:

1. Validates JWT signature against Auth0 JWKS
2. Checks audience matches MCP server URL
3. Checks issuer is the configured Auth0 tenant
4. Checks required scopes are present
5. Extracts user identity from `sub` claim

### 5. Permission Checking

Depending on configuration:

**Claims-based:**
- Extracts `permissions` claim from JWT
- Parses permission strings (e.g., `digitaltwins/read`)

**API-based:**
- Extracts `sub` claim (user ID)
- Calls Control Plane API with M2M token
- Gets user's permissions for the resource
- Caches result for 5 minutes (configurable)

### 6. Tool Execution

- MCP Server checks if user has required permission for the tool
- If authorized, executes tool (either direct DB or API Service call)
- Returns result to MCP client

## Testing

### 1. Test OAuth Metadata Endpoint

```bash
curl https://your-mcp-server.com/.well-known/oauth-protected-resource
```

### 2. Test Unauthorized Access

```bash
curl -X POST https://your-mcp-server.com/mcp
```

Should return:
```json
{
  "error": "unauthorized",
  "error_description": "Authentication required. Please provide a valid Bearer token.",
  "resource_metadata": "https://your-mcp-server.com/.well-known/oauth-protected-resource"
}
```

### 3. Test with Token

```bash
# Get a token from Auth0
TOKEN="your-access-token"

# Test MCP endpoint
curl -X POST https://your-mcp-server.com/mcp \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/list","id":1}'
```

### 4. Test with VS Code

1. Install the MCP extension
2. Add server configuration:

```json
{
  "my-secure-mcp-server": {
    "url": "https://your-mcp-server.com",
    "type": "http"
  }
}
```

3. Connect - VS Code will handle OAuth flow automatically
4. Try using MCP tools in chat

## Security Considerations

### Token Security

- ✅ **HTTPS Required**: Never use HTTP in production (except localhost dev)
- ✅ **Short-lived Tokens**: Configure Auth0 for 1-hour token expiration
- ✅ **Token Storage**: MCP clients must store tokens securely
- ✅ **Token Refresh**: Use refresh tokens for long-lived sessions

### Audience Validation

- ✅ **Strict Checking**: Always validate `aud` claim matches your MCP server URL
- ✅ **No Wildcards**: Don't accept generic audiences like `api://default`
- ✅ **Per-Instance**: Use unique audience per MCP server instance if multi-tenant

### Permission Checking

- ✅ **Fail Closed**: Deny access if permission check fails
- ✅ **Cache Wisely**: Balance performance vs freshness (5 min default)
- ✅ **Audit Logs**: Log all permission checks and denials

### Secrets Management

- ❌ **Never Commit**: Don't put client secrets in source control
- ✅ **Environment Variables**: Use environment variables or
- ✅ **Secret Managers**: Use Azure Key Vault, AWS Secrets Manager, etc.

## Troubleshooting

### "Unauthorized" Error

- Check token is included in `Authorization: Bearer <token>` header
- Verify token hasn't expired
- Check audience claim matches MCP server URL
- Verify issuer matches configured Auth0 tenant

### "Insufficient Scope" Error

- Check token contains required scope (`mcp:tools`)
- Verify application is authorized for the MCP Server API in Auth0
- Check scope was granted during user consent

### Permission Denied

- Verify user has required permissions in your permission system
- Check permission cache (try after 5 minutes)
- Verify API permission provider configuration if using `Provider: "Api"`
- Check M2M credentials if using API provider

### API Service Connection Failed

- Verify `ApiService:BaseUrl` is correct
- Check network connectivity
- Verify API Service is running and healthy
- Check token is being passed through correctly

## Migration from Current Setup

### Current State

- ✅ Basic JWT authentication
- ❌ No authorization/permissions
- ❌ No OAuth metadata endpoint
- ❌ No scope validation
- ❌ No MCP-compliant error responses

### Migration Steps

1. **Deploy with Authorization Disabled** (Week 1)
   ```json
   {
     "Authentication": {"Enabled": true},
     "Authorization": {"Enabled": false}
   }
   ```
   - Verify all clients still work
   - Monitor logs for issues

2. **Enable Claims-Based Authorization** (Week 2)
   ```json
   {
     "Authorization": {
       "Enabled": true,
       "Provider": "Claims"
     }
   }
   ```
   - Update Auth0 to include permission claims
   - Test with a single user
   - Gradually roll out to more users

3. **Switch to API-Based Authorization** (Week 3+)
   ```json
   {
     "Authorization": {
       "Enabled": true,
       "Provider": "Api"
     }
   }
   ```
   - Configure M2M credentials
   - Test permission checking
   - Monitor performance and cache hit rates
   - Roll out to production

## Best Practices

1. **Use API-Based Permissions** for production (centralized, dynamic)
2. **Enable Caching** to reduce latency and API calls
3. **Monitor Token Expiration** and implement refresh flows
4. **Log Security Events** for audit and debugging
5. **Use HTTPS Everywhere** (except local development)
6. **Rotate Secrets Regularly** (M2M client secrets)
7. **Review Permissions Regularly** and follow principle of least privilege
8. **Test OAuth Flow End-to-End** before going live

## Additional Resources

- [MCP Authorization Specification](https://modelcontextprotocol.io/docs/tutorials/security/authorization)
- [RFC 9728: Protected Resource Metadata](https://datatracker.ietf.org/doc/html/rfc9728)
- [OAuth 2.1](https://oauth.net/2.1/)
- [Auth0 Documentation](https://auth0.com/docs)
