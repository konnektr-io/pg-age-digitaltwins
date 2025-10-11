import { create } from "zustand";
import { persist } from "zustand/middleware";

interface ConnectionState {
  // Connection settings
  endpoint: string;
  authToken: string;
  isConnected: boolean;

  // Connection status
  lastConnectionAttempt: number | null;
  connectionError: string | null;

  // Actions
  setEndpoint: (endpoint: string) => void;
  setAuthToken: (token: string) => void;
  setConnected: (connected: boolean) => void;
  setConnectionError: (error: string | null) => void;
  updateLastConnectionAttempt: () => void;
  clearConnection: () => void;
}

export const useConnectionStore = create<ConnectionState>()(
  persist(
    (set) => ({
      // Initial state
      endpoint: "",
      authToken: "",
      isConnected: false,
      lastConnectionAttempt: null,
      connectionError: null,

      // Actions
      setEndpoint: (endpoint) => set({ endpoint }),
      setAuthToken: (token) => set({ authToken: token }),
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
          endpoint: "",
          authToken: "",
          isConnected: false,
          connectionError: null,
        }),
    }),
    {
      name: "konnektr-connection",
      // Don't persist sensitive auth token in production
      partialize: (state) => ({
        endpoint: state.endpoint,
        // authToken: state.authToken, // Enable this for development convenience
      }),
    }
  )
);
