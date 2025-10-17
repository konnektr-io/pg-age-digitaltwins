import { create } from "zustand";
import { persist } from "zustand/middleware";

export interface Connection {
  id: string;
  name: string;
  adtHost: string;
  description?: string;
}

interface ConnectionState {
  connections: Connection[];
  currentConnectionId: string | null;
  isConnected: boolean;

  // Actions
  addConnection: (conn: Connection) => void;
  removeConnection: (id: string) => void;
  updateConnection: (id: string, updates: Partial<Connection>) => void;
  setCurrentConnection: (id: string) => void;
  getCurrentConnection: () => Connection | null;
  setIsConnected: (connected: boolean) => void;
}

const defaultConnections: Connection[] = [
  {
    id: "localhost",
    name: "Local Development",
    adtHost: "localhost:5000",
    description: "Local development instance",
  },
];

export const useConnectionStore = create<ConnectionState>()(
  persist(
    (set, get) => ({
      connections: defaultConnections,
      currentConnectionId: defaultConnections[0].id,
      isConnected: false,

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

      setCurrentConnection: (id) => {
        const conn = get().connections.find((c) => c.id === id);
        if (conn) {
          set({ currentConnectionId: id });
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
    }),
    {
      name: "konnektr-connections",
    }
  )
);
