// @ts-nocheck - Zustand type inference issues with strict mode
import { create } from "zustand";
import { persist } from "zustand/middleware";

/**
 * Authentication provider types supported by Graph Explorer
 */
export type AuthProvider = "msal" | "auth0" | "none";

/**
 * Authentication configuration for a connection
 */
export interface AuthConfig {
  // For MSAL (Azure Digital Twins)
  clientId?: string;
  tenantId?: string;
  scopes?: string[]; // Default: ["https://digitaltwins.azure.net/.default"]

  // For Auth0 (Konnektr hosted/self-hosted)
  // Note: clientId is reused for both MSAL and Auth0
  domain?: string;
  audience?: string;

  // Common
  redirectUri?: string; // Defaults to window.location.origin
}

/**
 * Digital Twins connection with authentication configuration
 */
export interface Connection {
  id: string;
  name: string;
  adtHost: string;
  description?: string;

  // Authentication
  authProvider: AuthProvider;
  authConfig?: AuthConfig;
}

interface ConnectionState {
  connections: Connection[];
  currentConnectionId: string | null;
  isConnected: boolean;
  dismissedBanners: Set<string>; // Track dismissed connection banners by connection ID

  // Actions
  addConnection: (conn: Connection) => void;
  removeConnection: (id: string) => void;
  updateConnection: (id: string, updates: Partial<Connection>) => void;
  setCurrentConnection: (id: string) => Promise<void>;
  getCurrentConnection: () => Connection | null;
  setIsConnected: (connected: boolean) => void;
  testConnection: (id: string) => Promise<boolean>;
  dismissBanner: (connectionId: string) => void;
  isBannerDismissed: (connectionId: string) => boolean;
}

const defaultConnections: Connection[] = [
  {
    id: "localhost",
    name: "Local Development",
    adtHost: "localhost:5000",
    description: "Local development instance",
    authProvider: "none",
  },
];

export const useConnectionStore = create<ConnectionState>()(
  persist(
    (set, get) => ({
      connections: defaultConnections,
      currentConnectionId: defaultConnections[0].id,
      isConnected: false,
      dismissedBanners: new Set<string>(),

      addConnection: (conn) => {
        set((state) => ({
          connections: [...state.connections, conn],
        }));
      },

      removeConnection: (id) => {
        set((state) => ({
          connections: state.connections.filter((c) => c.id !== id),
          currentConnectionId:
            state.currentConnectionId === id
              ? state.connections[0]?.id || null
              : state.currentConnectionId,
        }));
      },

      updateConnection: (id, updates) => {
        set((state) => ({
          connections: state.connections.map((c) =>
            c.id === id ? { ...c, ...updates } : c
          ),
        }));
      },

      setCurrentConnection: async (id) => {
        const conn = get().connections.find((c) => c.id === id);
        if (conn) {
          set({ currentConnectionId: id, isConnected: true });

          // For MSAL connections, just initialize the credential (don't call getToken yet)
          // This ensures MSAL is ready and handles any redirect responses
          if (conn.authProvider === "msal") {
            try {
              // Import here to avoid circular dependencies
              const { getTokenCredential } = await import("@/services/auth");
              // Just initialize - this handles redirects and sets up MSAL
              // Actual token will be acquired when first API call is made
              await getTokenCredential(conn);
              console.log("MSAL credential initialized and ready");
            } catch (error) {
              console.warn("Auth initialization:", error);
              // Don't fail the connection selection, just log the warning
              // The actual API calls will trigger login if needed
            }
          }
        }
      },

      getCurrentConnection: () => {
        const state = get();
        return (
          state.connections.find((c) => c.id === state.currentConnectionId) ||
          null
        );
      },

      setIsConnected: (connected) => {
        set({ isConnected: connected });
      },

      testConnection: async (id) => {
        const conn = get().connections.find((c) => c.id === id);
        if (!conn) {
          console.error(`Connection ${id} not found`);
          return false;
        }

        try {
          // Import here to avoid circular dependencies
          const { digitalTwinsClientFactory } = await import(
            "@/services/digitalTwinsClientFactory"
          );

          // Try to create a client - this will trigger auth if needed
          const client = await digitalTwinsClientFactory(conn);

          // Try a simple API call to verify connection works
          // Just list models with limit 1 to test connectivity
          const models = client.listModels({ includeModelDefinition: false });
          await models.next(); // Get first result

          return true;
        } catch (error) {
          console.error("Connection test failed:", error);
          return false;
        }
      },

      dismissBanner: (connectionId) => {
        set((state) => ({
          dismissedBanners: new Set(state.dismissedBanners).add(connectionId),
        }));
      },

      isBannerDismissed: (connectionId) => {
        return get().dismissedBanners.has(connectionId);
      },
    }),
    {
      name: "konnektr-connections",
      // Custom storage to handle Set serialization
      storage: {
        getItem: (name) => {
          const str = localStorage.getItem(name);
          if (!str) return null;
          const parsed = JSON.parse(str);
          // Convert dismissedBanners array back to Set
          if (parsed.state?.dismissedBanners) {
            parsed.state.dismissedBanners = new Set(
              parsed.state.dismissedBanners
            );
          }
          return parsed;
        },
        setItem: (name, value) => {
          const parsed = JSON.parse(value);
          // Convert dismissedBanners Set to array for storage
          if (parsed.state?.dismissedBanners) {
            parsed.state.dismissedBanners = Array.from(
              parsed.state.dismissedBanners
            );
          }
          localStorage.setItem(name, JSON.stringify(parsed));
        },
        removeItem: (name) => localStorage.removeItem(name),
      },
    }
  )
);

/**
 * Validates a connection's authentication configuration
 * @param connection The connection to validate
 * @returns Validation error message, or null if valid
 */
export function validateConnectionAuth(connection: Connection): string | null {
  const { authProvider, authConfig } = connection;

  if (authProvider === "none") {
    return null; // No auth required
  }

  if (!authConfig) {
    return `Authentication configuration required for ${authProvider}`;
  }

  if (authProvider === "msal") {
    if (!authConfig.clientId) {
      return "MSAL requires a Client ID (Azure App Registration)";
    }
    if (!authConfig.tenantId) {
      return "MSAL requires a Tenant ID";
    }
    // Scopes are optional, will default to ["https://digitaltwins.azure.net/.default"]
  }

  if (authProvider === "auth0") {
    if (!authConfig.clientId) {
      return "Auth0 requires a Client ID";
    }
    if (!authConfig.domain) {
      return "Auth0 requires a Domain";
    }
    if (!authConfig.audience) {
      return "Auth0 requires an Audience";
    }
  }

  return null;
}
