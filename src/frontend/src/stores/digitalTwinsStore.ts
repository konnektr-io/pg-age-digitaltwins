import { create } from "zustand";
import { subscribeWithSelector } from "zustand/middleware";
import type { BasicDigitalTwin, BasicRelationship } from "@/types";
import { mockDigitalTwins } from "@/mocks/digitalTwinData";

export interface DigitalTwinsState {
  // State
  twins: BasicDigitalTwin[];
  relationships: BasicRelationship[];
  selectedTwinId: string | null;
  isLoading: boolean;
  error: string | null;
  filter: {
    modelId?: string;
    searchTerm?: string;
    propertyFilters?: Record<string, unknown>;
  };

  // Actions - Twins
  loadTwins: () => Promise<void>;
  createTwin: (twin: BasicDigitalTwin) => Promise<string>;
  updateTwin: (
    twinId: string,
    updates: Partial<BasicDigitalTwin>
  ) => Promise<void>;
  deleteTwin: (twinId: string) => Promise<void>;
  getTwin: (twinId: string) => BasicDigitalTwin | undefined;
  getTwinsByModel: (modelId: string) => BasicDigitalTwin[];

  // Actions - Relationships
  loadRelationships: (twinId?: string) => Promise<void>;
  createRelationship: (relationship: BasicRelationship) => Promise<string>;
  updateRelationship: (
    relationshipId: string,
    updates: Partial<BasicRelationship>
  ) => Promise<void>;
  deleteRelationship: (relationshipId: string) => Promise<void>;
  getRelationshipsForTwin: (twinId: string) => {
    outgoing: BasicRelationship[];
    incoming: BasicRelationship[];
  };

  // Actions - Selection & Filtering
  selectTwin: (twinId: string | null) => void;
  setFilter: (filter: Partial<DigitalTwinsState["filter"]>) => void;
  clearFilter: () => void;
  getFilteredTwins: () => BasicDigitalTwin[];

  // Actions - Properties
  updateTwinProperty: (
    twinId: string,
    propertyName: string,
    value: unknown
  ) => Promise<void>;
  updateTwinComponent: (
    twinId: string,
    componentName: string,
    componentData: Record<string, unknown>
  ) => Promise<void>;

  // Actions - Utility
  clearError: () => void;
  reset: () => void;
}

export const useDigitalTwinsStore = create<DigitalTwinsState>()(
  subscribeWithSelector((set, get) => ({
    // Initial state
    twins: [],
    relationships: [],
    selectedTwinId: null,
    isLoading: false,
    error: null,
    filter: {},

    // Twins actions
    loadTwins: async () => {
      set({ isLoading: true, error: null });
      try {
        // TODO: Replace with actual API call
        await new Promise((resolve) => setTimeout(resolve, 100)); // Simulate network delay
        set({ twins: [...mockDigitalTwins], isLoading: false });
      } catch (error) {
        set({
          error:
            error instanceof Error ? error.message : "Failed to load twins",
          isLoading: false,
        });
      }
    },

    createTwin: async (twinData) => {
      set({ isLoading: true, error: null });
      try {
        // Generate a unique ID
        const twinId = `twin-${Date.now()}-${Math.random()
          .toString(36)
          .substr(2, 9)}`;
        const newTwin: BasicDigitalTwin = {
          ...twinData,
          $dtId: twinId,
        };

        // TODO: Replace with actual API call
        await new Promise((resolve) => setTimeout(resolve, 100));

        set((state) => ({
          twins: [...state.twins, newTwin],
          isLoading: false,
        }));

        return twinId;
      } catch (error) {
        set({
          error:
            error instanceof Error ? error.message : "Failed to create twin",
          isLoading: false,
        });
        throw error;
      }
    },

    updateTwin: async (twinId, updates) => {
      set({ isLoading: true, error: null });
      try {
        // TODO: Replace with actual API call
        await new Promise((resolve) => setTimeout(resolve, 100));

        set((state) => ({
          twins: state.twins.map((twin) =>
            twin.$dtId === twinId ? { ...twin, ...updates } : twin
          ),
          isLoading: false,
        }));
      } catch (error) {
        set({
          error:
            error instanceof Error ? error.message : "Failed to update twin",
          isLoading: false,
        });
        throw error;
      }
    },

    deleteTwin: async (twinId) => {
      set({ isLoading: true, error: null });
      try {
        // TODO: Replace with actual API call
        await new Promise((resolve) => setTimeout(resolve, 100));

        set((state) => ({
          twins: state.twins.filter((twin) => twin.$dtId !== twinId),
          relationships: state.relationships.filter(
            (rel) => rel.$sourceId !== twinId && rel.$targetId !== twinId
          ),
          selectedTwinId:
            state.selectedTwinId === twinId ? null : state.selectedTwinId,
          isLoading: false,
        }));
      } catch (error) {
        set({
          error:
            error instanceof Error ? error.message : "Failed to delete twin",
          isLoading: false,
        });
        throw error;
      }
    },

    getTwin: (twinId) => {
      return get().twins.find((twin) => twin.$dtId === twinId);
    },

    getTwinsByModel: (modelId) => {
      return get().twins.filter((twin) => twin.$metadata.$model === modelId);
    },

    // Relationships actions
    loadRelationships: async (twinId) => {
      set({ isLoading: true, error: null });
      try {
        // TODO: Replace with actual API call
        await new Promise((resolve) => setTimeout(resolve, 100));

        // Mock relationships data - in real implementation, would filter by twinId if provided
        const mockRelationships: BasicRelationship[] = [
          {
            $relationshipId: "rel-001",
            $relationshipName: "contains",
            $sourceId: "building-001",
            $targetId: "floor-001-01",
          },
          {
            $relationshipId: "rel-002",
            $relationshipName: "contains",
            $sourceId: "floor-001-01",
            $targetId: "room-001-01-001",
          },
        ];

        set({
          relationships: twinId
            ? mockRelationships.filter(
                (rel) => rel.$sourceId === twinId || rel.$targetId === twinId
              )
            : mockRelationships,
          isLoading: false,
        });
      } catch (error) {
        set({
          error:
            error instanceof Error
              ? error.message
              : "Failed to load relationships",
          isLoading: false,
        });
      }
    },

    createRelationship: async (relationshipData) => {
      set({ isLoading: true, error: null });
      try {
        const relationshipId = `rel-${Date.now()}-${Math.random()
          .toString(36)
          .substr(2, 9)}`;
        const newRelationship: BasicRelationship = {
          ...relationshipData,
          $relationshipId: relationshipId,
        };

        // TODO: Replace with actual API call
        await new Promise((resolve) => setTimeout(resolve, 100));

        set((state) => ({
          relationships: [...state.relationships, newRelationship],
          isLoading: false,
        }));

        return relationshipId;
      } catch (error) {
        set({
          error:
            error instanceof Error
              ? error.message
              : "Failed to create relationship",
          isLoading: false,
        });
        throw error;
      }
    },

    updateRelationship: async (relationshipId, updates) => {
      set({ isLoading: true, error: null });
      try {
        // TODO: Replace with actual API call
        await new Promise((resolve) => setTimeout(resolve, 100));

        set((state) => ({
          relationships: state.relationships.map((rel) =>
            rel.$relationshipId === relationshipId
              ? { ...rel, ...updates }
              : rel
          ),
          isLoading: false,
        }));
      } catch (error) {
        set({
          error:
            error instanceof Error
              ? error.message
              : "Failed to update relationship",
          isLoading: false,
        });
        throw error;
      }
    },

    deleteRelationship: async (relationshipId) => {
      set({ isLoading: true, error: null });
      try {
        // TODO: Replace with actual API call
        await new Promise((resolve) => setTimeout(resolve, 100));

        set((state) => ({
          relationships: state.relationships.filter(
            (rel) => rel.$relationshipId !== relationshipId
          ),
          isLoading: false,
        }));
      } catch (error) {
        set({
          error:
            error instanceof Error
              ? error.message
              : "Failed to delete relationship",
          isLoading: false,
        });
        throw error;
      }
    },

    getRelationshipsForTwin: (twinId) => {
      const relationships = get().relationships;
      return {
        outgoing: relationships.filter((rel) => rel.$sourceId === twinId),
        incoming: relationships.filter((rel) => rel.$targetId === twinId),
      };
    },

    // Selection & Filtering actions
    selectTwin: (twinId) => {
      set({ selectedTwinId: twinId });
    },

    setFilter: (newFilter) => {
      set((state) => ({
        filter: { ...state.filter, ...newFilter },
      }));
    },

    clearFilter: () => {
      set({ filter: {} });
    },

    getFilteredTwins: () => {
      const { twins, filter } = get();
      let filtered = twins;

      if (filter.modelId) {
        filtered = filtered.filter(
          (twin) => twin.$metadata.$model === filter.modelId
        );
      }

      if (filter.searchTerm) {
        const searchTerm = filter.searchTerm.toLowerCase();
        filtered = filtered.filter(
          (twin) =>
            twin.$dtId.toLowerCase().includes(searchTerm) ||
            Object.values(twin).some(
              (value) =>
                typeof value === "string" &&
                value.toLowerCase().includes(searchTerm)
            )
        );
      }

      if (filter.propertyFilters) {
        filtered = filtered.filter((twin) => {
          return Object.entries(filter.propertyFilters!).every(
            ([key, value]) => {
              const twinValue = twin[key];
              if (value === null || value === undefined) {
                return twinValue === null || twinValue === undefined;
              }
              return twinValue === value;
            }
          );
        });
      }

      return filtered;
    },

    // Property actions
    updateTwinProperty: async (twinId, propertyName, value) => {
      set({ isLoading: true, error: null });
      try {
        // TODO: Replace with actual API call
        await new Promise((resolve) => setTimeout(resolve, 100));

        set((state) => ({
          twins: state.twins.map((twin) =>
            twin.$dtId === twinId
              ? {
                  ...twin,
                  [propertyName]: value,
                  $metadata: {
                    ...twin.$metadata,
                    [propertyName]: {
                      ...((twin.$metadata[propertyName] as any) || {}),
                      lastUpdateTime: new Date().toISOString(),
                    },
                  },
                }
              : twin
          ),
          isLoading: false,
        }));
      } catch (error) {
        set({
          error:
            error instanceof Error
              ? error.message
              : "Failed to update property",
          isLoading: false,
        });
        throw error;
      }
    },

    updateTwinComponent: async (twinId, componentName, componentData) => {
      set({ isLoading: true, error: null });
      try {
        // TODO: Replace with actual API call
        await new Promise((resolve) => setTimeout(resolve, 100));

        set((state) => ({
          twins: state.twins.map((twin) =>
            twin.$dtId === twinId
              ? {
                  ...twin,
                  [componentName]: componentData,
                  $metadata: {
                    ...twin.$metadata,
                    [componentName]: {
                      ...((twin.$metadata[componentName] as any) || {}),
                      lastUpdateTime: new Date().toISOString(),
                    },
                  },
                }
              : twin
          ),
          isLoading: false,
        }));
      } catch (error) {
        set({
          error:
            error instanceof Error
              ? error.message
              : "Failed to update component",
          isLoading: false,
        });
        throw error;
      }
    },

    // Utility actions
    clearError: () => {
      set({ error: null });
    },

    reset: () => {
      set({
        twins: [],
        relationships: [],
        selectedTwinId: null,
        isLoading: false,
        error: null,
        filter: {},
      });
    },
  }))
);
