# Simplified Authentication Architecture (Connection-Based)

**Date**: November 1, 2025  
**Status**: Proposed (based on user feedback)  
**Complexity**: Minimal (token-per-connection approach)

---

## 🎯 Design Goals (Revised)

Based on feedback, the authentication architecture has been simplified:

1. **✅ Minimal Implementation**: Focus on getting tokens for API calls
2. **✅ Connection-Based**: Each connection specifies its own auth provider
3. **✅ No Global Context**: No need for React context providers
4. **✅ User-Configured MSAL**: Users provide their own client IDs/tenants
5. **✅ Fast to Implement**: Simpler than original proposal

---

## 🏗️ Architecture

### Connection Model (Extended)

```typescript
// src/stores/connectionStore.ts
export interface Connection {
  id: string;
  name: string;
  adtHost: string;
  description?: string;

  // NEW: Authentication configuration
  authProvider: "msal" | "auth0" | "none";
  authConfig?: AuthConfig;
}

export interface AuthConfig {
  // For MSAL (Azure Digital Twins)
  clientId?: string;
  tenantId?: string;
  scopes?: string[]; // Default: ["https://digitaltwins.azure.net/.default"]

  // For Auth0 (Konnektr hosted/self-hosted)
  domain?: string;
  clientId?: string; // Reuse clientId field
  audience?: string;

  // For both
  redirectUri?: string; // Defaults to window.location.origin
}
```

### Token Credential Factory

```typescript
// src/services/tokenCredentialFactory.ts
import type { TokenCredential } from "@azure/core-auth";
import type { Connection } from "@/stores/connectionStore";

/**
 * Creates a TokenCredential for the Azure SDK based on connection auth provider
 */
export function createTokenCredential(connection: Connection): TokenCredential {
  if (!connection.authConfig) {
    throw new Error(`No auth config for connection ${connection.name}`);
  }

  switch (connection.authProvider) {
    case "msal":
      return createMsalCredential(connection.authConfig);
    case "auth0":
      return createAuth0Credential(connection.authConfig);
    case "none":
      return createNoAuthCredential();
    default:
      throw new Error(`Unknown auth provider: ${connection.authProvider}`);
  }
}

// MSAL implementation
function createMsalCredential(config: AuthConfig): TokenCredential {
  // Use @azure/msal-browser with PKCE flow
  const msalConfig = {
    auth: {
      clientId: config.clientId!,
      authority: `https://login.microsoftonline.com/${config.tenantId}`,
      redirectUri: config.redirectUri || window.location.origin,
    },
    cache: {
      cacheLocation: "localStorage",
    },
  };

  const pca = new PublicClientApplication(msalConfig);

  return {
    async getToken(scopes: string | string[]) {
      const scopesArray = Array.isArray(scopes) ? scopes : [scopes];
      const request = {
        scopes: config.scopes || scopesArray,
        redirectUri: config.redirectUri || window.location.origin,
      };

      try {
        // Try silent token acquisition first
        const response = await pca.acquireTokenSilent(request);
        return {
          token: response.accessToken,
          expiresOnTimestamp: response.expiresOn.getTime(),
        };
      } catch (error) {
        // Fall back to interactive login
        const response = await pca.acquireTokenPopup(request);
        return {
          token: response.accessToken,
          expiresOnTimestamp: response.expiresOn.getTime(),
        };
      }
    },
  };
}

// Auth0 implementation (existing)
function createAuth0Credential(config: AuthConfig): TokenCredential {
  // Use existing Auth0TokenCredential implementation
  // This requires Auth0 SDK to be initialized somewhere
  // (could be lazy-initialized per connection)
  return new Auth0TokenCredential(/* pass getAccessTokenSilently */);
}

// No-auth implementation (for testing)
function createNoAuthCredential(): TokenCredential {
  return {
    async getToken() {
      return {
        token: "no-auth-token",
        expiresOnTimestamp: Date.now() + 3600000,
      };
    },
  };
}
```

### Updated Digital Twins Client Factory

```typescript
// src/services/digitalTwinsClientFactory.ts
import { DigitalTwinsClient } from "@azure/digital-twins-core";
import type { Connection } from "@/stores/connectionStore";
import { createTokenCredential } from "./tokenCredentialFactory";

export function createDigitalTwinsClient(
  connection: Connection
): DigitalTwinsClient {
  const tokenCredential = createTokenCredential(connection);
  const endpoint = `https://${connection.adtHost}`;

  return new DigitalTwinsClient(endpoint, tokenCredential);
}
```

### Updated Store Usage

```typescript
// src/stores/digitalTwinsStore.ts
const getClient = (): DigitalTwinsClient => {
  const { getCurrentConnection, isConnected } = useConnectionStore.getState();
  const connection = getCurrentConnection();

  if (!connection || !isConnected) {
    throw new Error("Not connected to Digital Twins instance.");
  }

  // Create client with connection-specific auth
  return createDigitalTwinsClient(connection);
};
```

---

## 🎨 User Experience

### Adding Azure ADT Connection

```tsx
// ConnectionSelector "Add Connection" dialog
<Dialog>
  <DialogContent>
    <h2>Add Azure Digital Twins Connection</h2>

    <Label>Connection Name</Label>
    <Input value={name} onChange={...} />

    <Label>ADT Host</Label>
    <Input value={adtHost} placeholder="myadt.api.weu.digitaltwins.azure.net" />

    <Label>Auth Provider</Label>
    <Select value={authProvider}>
      <SelectItem value="msal">Azure AD (MSAL)</SelectItem>
      <SelectItem value="auth0">Auth0 (Konnektr)</SelectItem>
      <SelectItem value="none">No Auth (Testing)</SelectItem>
    </Select>

    {authProvider === 'msal' && (
      <>
        <Label>Client ID</Label>
        <Input value={clientId} placeholder="your-app-registration-client-id" />

        <Label>Tenant ID</Label>
        <Input value={tenantId} placeholder="your-azure-tenant-id" />

        <Button variant="link" onClick={openMsalDocs}>
          How to create an App Registration
        </Button>
      </>
    )}

    {authProvider === 'auth0' && (
      <>
        <Label>Domain</Label>
        <Input value={domain} placeholder="auth.konnektr.io" />

        <Label>Client ID</Label>
        <Input value={clientId} />

        <Label>Audience</Label>
        <Input value={audience} placeholder="https://api.graph.konnektr.io" />
      </>
    )}

    <Button onClick={saveConnection}>Add Connection</Button>
  </DialogContent>
</Dialog>
```

### Switching Connections

When user switches connections:

1. ConnectionStore updates `currentConnectionId`
2. Next API call creates new client with new connection's auth
3. Token is acquired automatically (MSAL popup if needed, Auth0 silent)
4. No global state to reset

---

## 📦 Dependencies

### Required npm Packages

```json
{
  "dependencies": {
    "@azure/msal-browser": "^3.x", // For MSAL PKCE flow
    "@auth0/auth0-react": "^2.x", // For Auth0 (already installed)
    "@azure/digital-twins-core": "^1.x", // Already installed
    "@azure/core-auth": "^1.x" // TokenCredential interface
  }
}
```

---

## 🚀 Implementation Plan

### Phase 6.2a: Extend Connection Model (2 hours)

**Tasks**:

1. Update `Connection` interface in `connectionStore.ts`
2. Add `authProvider` and `authConfig` fields
3. Update default connections
4. Update `ConnectionSelector` UI to show auth type
5. Add validation for auth config

**Files**:

- `src/stores/connectionStore.ts`
- `src/components/ConnectionSelector.tsx`

---

### Phase 6.2b: Implement Token Credential Factory (3 hours)

**Tasks**:

1. Create `src/services/tokenCredentialFactory.ts`
2. Implement MSAL credential with PKCE
3. Update Auth0 credential to accept config
4. Implement no-auth credential for testing
5. Add error handling and token refresh

**Files**:

- `src/services/tokenCredentialFactory.ts`
- `src/services/MsalTokenCredential.ts` (new)
- `src/services/Auth0TokenCredential.ts` (update)
- `src/services/NoAuthCredential.ts` (new)

---

### Phase 6.2c: Update Client Factory (1 hour)

**Tasks**:

1. Update `digitalTwinsClientFactory.ts`
2. Accept `Connection` instead of separate params
3. Use `createTokenCredential()`
4. Update all store usage

**Files**:

- `src/services/digitalTwinsClientFactory.ts`
- `src/stores/digitalTwinsStore.ts`
- `src/stores/modelsStore.ts`
- `src/hooks/useDigitalTwinsClient.ts`

---

### Phase 6.2d: Enhanced Connection Dialog (2 hours)

**Tasks**:

1. Add auth provider selector
2. Conditional fields based on provider
3. Validation for MSAL config
4. Link to documentation
5. Save connection with auth config

**Files**:

- `src/components/ConnectionSelector.tsx`
- Add new component: `src/components/AddConnectionDialog.tsx`

---

### Phase 6.2e: Documentation (1 hour)

**Tasks**:

1. Create MSAL setup guide
2. Document redirect URI configuration
3. Add troubleshooting section
4. Update README

**Files**:

- `docs/authentication/msal-setup.md` (new)
- `docs/authentication/auth0-setup.md` (new)
- `README.md` (update)

---

## ⏱️ Total Effort

- **Phase 6.2a**: 2 hours
- **Phase 6.2b**: 3 hours
- **Phase 6.2c**: 1 hour
- **Phase 6.2d**: 2 hours
- **Phase 6.2e**: 1 hour

**Total: ~9 hours (1-1.5 days)**

---

## ✅ Advantages of This Approach

1. **Simpler**: No React context, no global auth state
2. **Flexible**: Each connection independent
3. **User-Controlled**: Users provide their MSAL app registrations
4. **Testable**: Easy to mock with 'none' provider
5. **Fast**: Minimal code changes
6. **Scalable**: Easy to add more providers later

---

## 🔒 Security Considerations

### MSAL (Azure AD)

- ✅ PKCE flow (no client secret needed)
- ✅ Tokens stored in localStorage (MSAL default)
- ✅ Automatic token refresh
- ✅ User controls app registration

### Auth0

- ✅ PKCE flow
- ✅ Tokens in localStorage
- ✅ Automatic token refresh via refresh tokens
- ✅ Audience validation

### Best Practices

- ⚠️ Validate redirect URIs
- ⚠️ Implement token expiration handling
- ⚠️ Log auth errors for troubleshooting
- ⚠️ Support logout per connection

---

## 📝 Example Connection Configurations

### Azure Digital Twins (MSAL)

```json
{
  "id": "azure-adt-dev",
  "name": "Azure ADT Development",
  "adtHost": "adt-digitaltwins-develop-dev-001.api.weu.digitaltwins.azure.net",
  "authProvider": "msal",
  "authConfig": {
    "clientId": "your-app-registration-id",
    "tenantId": "your-tenant-id",
    "scopes": ["https://digitaltwins.azure.net/.default"],
    "redirectUri": "http://localhost:5173"
  }
}
```

### Konnektr Hosted (Auth0)

```json
{
  "id": "konnektr-prod",
  "name": "Konnektr Production",
  "adtHost": "graph-api.konnektr.io",
  "authProvider": "auth0",
  "authConfig": {
    "domain": "auth.konnektr.io",
    "clientId": "konnektr-graph-explorer-client-id",
    "audience": "https://api.graph.konnektr.io",
    "redirectUri": "https://explorer.konnektr.io"
  }
}
```

### Local Development (No Auth)

```json
{
  "id": "local-dev",
  "name": "Local Development",
  "adtHost": "localhost:5000",
  "authProvider": "none"
}
```

---

**Status**: Ready to implement  
**Next Step**: Begin Phase 6.2a (Extend Connection Model)
