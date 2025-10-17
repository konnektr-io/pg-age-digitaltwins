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
