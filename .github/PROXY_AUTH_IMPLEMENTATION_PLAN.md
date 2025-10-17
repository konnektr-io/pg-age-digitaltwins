# Proxy & Auth0 Implementation Plan

## 🎯 Goals

1. **Single Frontend Deployment** - One frontend can connect to multiple backends
2. **Proxy-Based Routing** - Use proxy to add headers and route requests
3. **Auth0 Authentication** - Replace mock token with real Auth0 tokens
4. **Environment Selection** - UI to choose which backend to connect to

## 📋 Architecture Overview

```
┌─────────────────┐
│   Frontend      │
│   (React/Vite)  │
└────────┬────────┘
         │
         │ API Requests with x-adt-host header
         ▼
┌─────────────────┐
│   Proxy Layer   │  ← Adds Auth headers, routes based on x-adt-host
│  (Vite Dev/     │
│   Nginx Prod)   │
└────────┬────────┘
         │
         │ Forwards to appropriate backend
         ▼
┌─────────────────┐
│  Konnektr Graph │
│   API Service   │
└─────────────────┘
```

## 🚀 Phase 1: Same-Domain Setup (Quick Start)

### Step 1.1: Update Vite Config for Proxy

**File**: `vite.config.ts`

```typescript
import path from "path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react-swc";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    proxy: {
      // Proxy all API requests to the backend
      "/api": {
        target: process.env.VITE_API_BASE_URL || "http://localhost:5000",
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ""),
        configure: (proxy, options) => {
          proxy.on("proxyReq", (proxyReq, req, res) => {
            // Add x-adt-host header from frontend request
            const adtHost = req.headers["x-adt-host"];
            if (adtHost) {
              proxyReq.setHeader("x-adt-host", adtHost);
            }
            // For development, you can hardcode a default
            // proxyReq.setHeader("x-adt-host", "localhost");
          });
        },
      },
    },
  },
});
```

### Step 1.2: Update Environment Variables

**File**: `.env.development`

```bash
VITE_API_BASE_URL=http://localhost:5000
VITE_AUTH0_DOMAIN=auth.konnektr.io
VITE_AUTH0_CLIENT_ID=your-client-id
VITE_AUTH0_AUDIENCE=https://api.graph.konnektr.io
```

**File**: `.env.production`

```bash
# In production, API is on same domain via reverse proxy
VITE_API_BASE_URL=/api
VITE_AUTH0_DOMAIN=auth.konnektr.io
VITE_AUTH0_CLIENT_ID=your-client-id
VITE_AUTH0_AUDIENCE=https://api.graph.konnektr.io
```

## 🔐 Phase 2: Auth0 Integration

### Step 2.1: Install Auth0 Dependencies

```bash
cd src/frontend
pnpm add @auth0/auth0-react @auth0/auth0-spa-js
```

### Step 2.2: Create Auth0 Provider Setup

**File**: `src/auth/Auth0Provider.tsx`

```typescript
import { Auth0Provider as Auth0ProviderBase } from "@auth0/auth0-react";
import { useNavigate } from "react-router-dom";
import type { ReactNode } from "react";

interface Auth0ProviderProps {
  children: ReactNode;
}

export function Auth0Provider({ children }: Auth0ProviderProps) {
  const navigate = useNavigate();

  const onRedirectCallback = (appState?: { returnTo?: string }) => {
    navigate(appState?.returnTo || window.location.pathname);
  };

  return (
    <Auth0ProviderBase
      domain={import.meta.env.VITE_AUTH0_DOMAIN}
      clientId={import.meta.env.VITE_AUTH0_CLIENT_ID}
      authorizationParams={{
        redirect_uri: window.location.origin,
        audience: import.meta.env.VITE_AUTH0_AUDIENCE,
      }}
      onRedirectCallback={onRedirectCallback}
      useRefreshTokens={true}
      cacheLocation="localstorage"
    >
      {children}
    </Auth0ProviderBase>
  );
}
```

### Step 2.3: Create Auth0 Token Credential

**File**: `src/services/Auth0TokenCredential.ts`

```typescript
import type { GetTokenOptions } from "@auth0/auth0-spa-js";

/**
 * Token credential interface compatible with Azure Digital Twins SDK
 */
interface AccessToken {
  token: string;
  expiresOnTimestamp: number;
}

interface TokenCredential {
  getToken(
    scopes: string | string[],
    options?: Record<string, unknown>
  ): Promise<AccessToken | null>;
}

/**
 * Auth0-based token credential for Azure Digital Twins SDK
 * Implements the TokenCredential interface expected by the Azure SDK
 */
export class Auth0TokenCredential implements TokenCredential {
  constructor(
    private getAccessTokenSilently: (
      options?: GetTokenOptions
    ) => Promise<string>
  ) {}

  async getToken(
    _scopes: string | string[],
    _options?: Record<string, unknown>
  ): Promise<AccessToken | null> {
    try {
      const token = await this.getAccessTokenSilently({
        authorizationParams: {
          audience: import.meta.env.VITE_AUTH0_AUDIENCE,
        },
      });

      if (!token) {
        return null;
      }

      // Parse JWT to get expiration time
      const payload = JSON.parse(atob(token.split(".")[1]));
      const expiresOnTimestamp = payload.exp * 1000; // Convert to milliseconds

      return {
        token,
        expiresOnTimestamp,
      };
    } catch (error) {
      console.error("Failed to get Auth0 token:", error);
      return null;
    }
  }
}
```

### Step 2.4: Update Digital Twins Client Factory

**File**: `src/services/digitalTwinsClientFactory.ts`

```typescript
import { DigitalTwinsClient } from "@azure/digital-twins-core";
import type {
  PipelineRequest,
  PipelineResponse,
  SendRequest,
  PipelinePolicy,
} from "@azure/core-rest-pipeline";
import { Auth0TokenCredential } from "./Auth0TokenCredential";
import type { GetTokenOptions } from "@auth0/auth0-spa-js";

/**
 * Token credential interface compatible with Azure Digital Twins SDK
 */
interface AccessToken {
  token: string;
  expiresOnTimestamp: number;
}

interface TokenCredential {
  getToken(
    scopes: string | string[],
    options?: Record<string, unknown>
  ): Promise<AccessToken | null>;
}

/**
 * Pipeline policy that rewrites URLs and adds custom headers
 */
const createCustomProxyPolicy = (
  adtHost: string,
  pathRewrite: (path: string) => string
): PipelinePolicy => {
  return {
    name: "customProxyPolicy",
    sendRequest: async (
      request: PipelineRequest,
      next: SendRequest
    ): Promise<PipelineResponse> => {
      // Add x-adt-host header to route to correct backend
      request.headers.set("x-adt-host", adtHost);

      // Rewrite URL to use local proxy
      const url = new URL(request.url);
      const baseUrl = new URL(window.location.origin);
      url.host = baseUrl.host;
      url.pathname = pathRewrite(url.pathname);
      url.protocol = baseUrl.protocol;
      request.url = url.toString();

      return next(request);
    },
  };
};

/** Digital Twins Clients cache object */
const digitalTwinsClients: { [environmentId: string]: DigitalTwinsClient } = {};

/**
 * Digital Twins client factory that returns cached Digital Twins Client
 * @param environmentId - Unique identifier for the environment
 * @param adtHost - The ADT host to connect to (used in x-adt-host header)
 * @param tokenCredential - The token credential for authentication
 */
export const digitalTwinsClientFactory = (
  environmentId: string,
  adtHost: string,
  tokenCredential: TokenCredential
): DigitalTwinsClient => {
  const cacheKey = `${environmentId}-${adtHost}`;

  if (!digitalTwinsClients[cacheKey]) {
    // Find all consecutive forward slashes (two or more)
    const _pathRegex = /(\/){2,}/g;
    const _pathRewrite = (path: string) => {
      const apiBasePath = import.meta.env.VITE_API_BASE_PATH || "/api";
      return `${apiBasePath}${path}`.replace(_pathRegex, "/");
    };

    const customPolicy = createCustomProxyPolicy(adtHost, _pathRewrite);

    digitalTwinsClients[cacheKey] = new DigitalTwinsClient(
      // Use a dummy URL since we're rewriting it anyway
      `https://${adtHost}/`,
      tokenCredential,
      {
        allowInsecureConnection: window.location.hostname === "localhost",
        additionalPolicies: [{ policy: customPolicy, position: "perCall" }],
      }
    );
  }

  return digitalTwinsClients[cacheKey];
};

/**
 * Clear cached clients (useful for logout or environment change)
 */
export const clearClientCache = () => {
  Object.keys(digitalTwinsClients).forEach((key) => {
    delete digitalTwinsClients[key];
  });
};
```

### Step 2.5: Create Environment Configuration Store

**File**: `src/stores/environmentStore.ts`

```typescript
import { create } from "zustand";
import { persist } from "zustand/middleware";

export interface Environment {
  id: string;
  name: string;
  adtHost: string;
  apiBaseUrl?: string; // Optional override for API base URL
  description?: string;
}

interface EnvironmentState {
  environments: Environment[];
  currentEnvironmentId: string | null;

  // Actions
  addEnvironment: (env: Environment) => void;
  removeEnvironment: (id: string) => void;
  updateEnvironment: (id: string, updates: Partial<Environment>) => void;
  setCurrentEnvironment: (id: string) => void;
  getCurrentEnvironment: () => Environment | null;
}

// Default environments
const defaultEnvironments: Environment[] = [
  {
    id: "localhost",
    name: "Local Development",
    adtHost: "localhost:5000",
    description: "Local development instance",
  },
  {
    id: "staging",
    name: "Staging",
    adtHost: "staging.api.graph.konnektr.io",
    description: "Staging environment",
  },
];

export const useEnvironmentStore = create<EnvironmentState>()(
  persist(
    (set, get) => ({
      environments: defaultEnvironments,
      currentEnvironmentId: defaultEnvironments[0].id,

      addEnvironment: (env) => {
        set((state) => ({
          environments: [...state.environments, env],
        }));
      },

      removeEnvironment: (id) => {
        set((state) => ({
          environments: state.environments.filter((e) => e.id !== id),
          currentEnvironmentId:
            state.currentEnvironmentId === id
              ? state.environments[0]?.id || null
              : state.currentEnvironmentId,
        }));
      },

      updateEnvironment: (id, updates) => {
        set((state) => ({
          environments: state.environments.map((e) =>
            e.id === id ? { ...e, ...updates } : e
          ),
        }));
      },

      setCurrentEnvironment: (id) => {
        const env = get().environments.find((e) => e.id === id);
        if (env) {
          set({ currentEnvironmentId: id });
        }
      },

      getCurrentEnvironment: () => {
        const state = get();
        return (
          state.environments.find((e) => e.id === state.currentEnvironmentId) ||
          null
        );
      },
    }),
    {
      name: "konnektr-environments",
    }
  )
);
```

### Step 2.6: Update Connection Store

**File**: `src/stores/connectionStore.ts`

```typescript
import { create } from "zustand";
import { persist } from "zustand/middleware";

interface ConnectionState {
  // Authentication
  isAuthenticated: boolean;

  // Connection status
  isConnected: boolean;
  lastConnectionAttempt: number | null;
  connectionError: string | null;

  // Actions
  setAuthenticated: (authenticated: boolean) => void;
  setConnected: (connected: boolean) => void;
  setConnectionError: (error: string | null) => void;
  updateLastConnectionAttempt: () => void;
  clearConnection: () => void;
}

export const useConnectionStore = create<ConnectionState>()(
  persist(
    (set) => ({
      // Initial state
      isAuthenticated: false,
      isConnected: false,
      lastConnectionAttempt: null,
      connectionError: null,

      // Actions
      setAuthenticated: (authenticated) =>
        set({ isAuthenticated: authenticated }),
      setConnected: (connected) =>
        set({
          isConnected: connected,
          connectionError: connected ? null : undefined,
        }),
      setConnectionError: (error) => set({ connectionError: error }),
      updateLastConnectionAttempt: () =>
        set({ lastConnectionAttempt: Date.now() }),
      clearConnection: () =>
        set({
          isConnected: false,
          connectionError: null,
        }),
    }),
    {
      name: "konnektr-connection",
      partialize: (state) => ({
        // Only persist connection state, not auth
        isConnected: state.isConnected,
      }),
    }
  )
);
```

### Step 2.7: Update Stores to Use Auth0 Token

**File**: `src/stores/digitalTwinsStore.ts` (Update getClient function)

```typescript
import { useAuth0 } from "@auth0/auth0-react";
import { Auth0TokenCredential } from "@/services/Auth0TokenCredential";
import { useEnvironmentStore } from "./environmentStore";
import { useConnectionStore } from "./connectionStore";
import { digitalTwinsClientFactory } from "@/services/digitalTwinsClientFactory";

/**
 * Helper to get initialized Digital Twins client
 * Throws error if not authenticated or environment not configured
 */
const getClient = (): DigitalTwinsClient => {
  const { isAuthenticated, getAccessTokenSilently } = useAuth0();
  const { isConnected } = useConnectionStore.getState();
  const currentEnv = useEnvironmentStore.getState().getCurrentEnvironment();

  if (!isAuthenticated) {
    throw new Error("Not authenticated. Please log in.");
  }

  if (!currentEnv) {
    throw new Error("No environment selected. Please select an environment.");
  }

  if (!isConnected) {
    throw new Error("Not connected to Digital Twins instance.");
  }

  const tokenCredential = new Auth0TokenCredential(getAccessTokenSilently);
  return digitalTwinsClientFactory(
    currentEnv.id,
    currentEnv.adtHost,
    tokenCredential
  );
};
```

### Step 2.8: Update App Entry Point

**File**: `src/main.tsx` or `src/App.tsx`

```typescript
import { Auth0Provider } from "@/auth/Auth0Provider";
import { BrowserRouter } from "react-router-dom";

root.render(
  <StrictMode>
    <BrowserRouter>
      <Auth0Provider>
        <App />
      </Auth0Provider>
    </BrowserRouter>
  </StrictMode>
);
```

### Step 2.9: Create Connection Status Component

**File**: `src/components/connection/ConnectionStatus.tsx`

```typescript
import { useAuth0 } from "@auth0/auth0-react";
import { useConnectionStore } from "@/stores/connectionStore";
import { useEnvironmentStore } from "@/stores/environmentStore";
import { Button } from "@/components/ui/button";

export function ConnectionStatus() {
  const { isAuthenticated, loginWithRedirect, logout } = useAuth0();
  const { isConnected } = useConnectionStore();
  const currentEnv = useEnvironmentStore((state) =>
    state.getCurrentEnvironment()
  );

  const getStatus = () => {
    if (!isAuthenticated)
      return { color: "bg-gray-500", text: "Not Logged In" };
    if (!currentEnv) return { color: "bg-yellow-500", text: "No Environment" };
    if (!isConnected) return { color: "bg-red-500", text: "Disconnected" };
    return { color: "bg-green-500", text: "Connected" };
  };

  const status = getStatus();

  return (
    <div className="flex items-center gap-3">
      <div className="flex items-center gap-2">
        <div className={`w-2 h-2 rounded-full ${status.color}`} />
        <span className="text-xs text-muted-foreground">{status.text}</span>
      </div>

      {!isAuthenticated ? (
        <Button size="sm" onClick={() => loginWithRedirect()}>
          Log In
        </Button>
      ) : (
        <Button size="sm" variant="outline" onClick={() => logout()}>
          Log Out
        </Button>
      )}
    </div>
  );
}
```

### Step 2.10: Update AppHeader with Environment Selector

**File**: `src/components/layout/AppHeader.tsx`

```typescript
import { Database, Settings, ChevronDown } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useWorkspaceStore } from "@/stores/workspaceStore";
import { useEnvironmentStore } from "@/stores/environmentStore";
import { ConnectionStatus } from "@/components/connection/ConnectionStatus";

export function AppHeader() {
  const { mainView, setMainView } = useWorkspaceStore();
  const { environments, currentEnvironmentId, setCurrentEnvironment } =
    useEnvironmentStore();
  const currentEnv = useEnvironmentStore((state) =>
    state.getCurrentEnvironment()
  );

  return (
    <header className="h-14 border-b border-border bg-card flex items-center justify-between px-4">
      <div className="flex items-center gap-4">
        {/* Logo and Title */}
        <div className="flex items-center gap-2">
          <Database className="w-5 h-5 text-secondary" />
          <span className="font-semibold text-foreground">Konnektr Graph</span>
        </div>

        <div className="h-6 w-px bg-border" />

        {/* Environment Selector */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="ghost"
              className="px-3 py-1.5 text-sm bg-muted hover:bg-muted/80 rounded-md flex items-center gap-2"
            >
              {currentEnv ? currentEnv.name : "Select Environment"}
              <ChevronDown className="w-4 h-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start" className="w-64">
            {environments.map((env) => (
              <DropdownMenuItem
                key={env.id}
                onClick={() => setCurrentEnvironment(env.id)}
                className={currentEnvironmentId === env.id ? "bg-accent" : ""}
              >
                <div className="flex flex-col gap-1">
                  <span className="font-medium">{env.name}</span>
                  <span className="text-xs text-muted-foreground">
                    {env.adtHost}
                  </span>
                </div>
              </DropdownMenuItem>
            ))}
            <DropdownMenuSeparator />
            <DropdownMenuItem>Configure Environments...</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      {/* View Switcher */}
      <div className="flex items-center gap-2">
        <div className="flex gap-1 p-1 bg-muted rounded-md mr-2">
          <Button
            variant={mainView === "query" ? "default" : "ghost"}
            size="sm"
            className="px-3 py-1.5 text-xs"
            onClick={() => setMainView("query")}
          >
            Query Explorer
          </Button>
          <Button
            variant={mainView === "models" ? "default" : "ghost"}
            size="sm"
            className="px-3 py-1.5 text-xs"
            onClick={() => setMainView("models")}
          >
            Model Graph
          </Button>
        </div>

        {/* Settings Button */}
        <Button variant="ghost" size="sm" className="p-2">
          <Settings className="w-4 h-4" />
        </Button>
      </div>

      {/* Connection Status */}
      <ConnectionStatus />
    </header>
  );
}
```

## 🏗️ Phase 3: Production Proxy (Nginx/Envoy)

### Nginx Configuration Example

**File**: `nginx.conf`

```nginx
server {
    listen 80;
    server_name your-frontend-domain.com;

    # Frontend static files
    location / {
        root /usr/share/nginx/html;
        try_files $uri $uri/ /index.html;
    }

    # API Proxy
    location /api/ {
        # Extract x-adt-host header from request
        set $backend_host $http_x_adt_host;

        # Default backend if header not present
        if ($backend_host = "") {
            set $backend_host "default.api.graph.konnektr.io";
        }

        # Proxy to backend
        proxy_pass https://$backend_host/;
        proxy_set_header Host $backend_host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # Forward the x-adt-host header
        proxy_set_header x-adt-host $http_x_adt_host;

        # Auth headers are added by frontend Auth0 integration
        proxy_pass_header Authorization;

        # Rewrite path
        rewrite ^/api/(.*) /$1 break;
    }
}
```

## 📝 Summary

### Development Flow:

1. User logs in with Auth0
2. User selects environment from dropdown
3. Frontend makes API calls to `/api/*`
4. Vite proxy forwards to backend with `x-adt-host` header
5. Backend routes based on `x-adt-host`

### Production Flow:

1. User logs in with Auth0
2. User selects environment from dropdown
3. Frontend makes API calls to `/api/*`
4. Nginx proxy forwards to appropriate backend based on `x-adt-host`
5. Backend processes request

### Benefits:

- ✅ Single frontend deployment
- ✅ Connect to multiple backends
- ✅ No CORS issues
- ✅ Proper authentication with Auth0
- ✅ Environment selection UI
- ✅ Token caching and refresh

## 🔄 Next Steps

1. Implement Phase 1 (Proxy setup)
2. Implement Phase 2 (Auth0 integration)
3. Test with local backend
4. Deploy with Phase 3 (Nginx proxy) for production
5. Add environment management UI
6. Add connection testing/validation
