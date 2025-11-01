// @ts-nocheck - Zustand type inference issues with strict mode
import { create } from "zustand";
import { subscribeWithSelector } from "zustand/middleware";
import type { DigitalTwinsClient } from "@azure/digital-twins-core";
import type { Operation } from "fast-json-patch";
import type { BasicDigitalTwin, BasicRelationship } from "@/types";
import { digitalTwinsClientFactory } from "@/services/digitalTwinsClientFactory";
import { useConnectionStore } from "./connectionStore";
import {
  REL_TYPE_ALL,
  REL_TYPE_INCOMING,
  REL_TYPE_OUTGOING,
  QUERY_ALL_TWINS,
} from "@/utils/constants";
import {
  getDataFromQueryResponse,
  type QueryResponseData,
} from "@/utils/queryAdt";
/**
 * Helper to get initialized Digital Twins client
 * Throws error if connection is not configured
 *
 * Note: This function is now async to support MSAL initialization
 */
const getClient = async (): Promise<DigitalTwinsClient> => {
  const { getCurrentConnection, isConnected } = useConnectionStore.getState();
  const connection = getCurrentConnection();
  if (!connection || !isConnected) {
    throw new Error(
      "Not connected to Digital Twins instance. Please configure connection."
    );
  }

  // Use the new connection-based factory with auth support
  return await digitalTwinsClientFactory(connection);
};

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
  getTwinById: (twinId: string) => Promise<BasicDigitalTwin>;
  getTwinsByModel: (modelId: string) => BasicDigitalTwin[];
  queryTwins: (query: string) => Promise<QueryResponseData>;

  // Actions - Relationships
  loadRelationships: (twinId?: string) => Promise<void>;
  createRelationship: (relationship: BasicRelationship) => Promise<string>;
  updateRelationship: (
    sourceTwinId: string,
    relationshipId: string,
    updates: Operation[]
  ) => Promise<void>;
  deleteRelationship: (
    sourceTwinId: string,
    relationshipId: string
  ) => Promise<void>;
  getRelationship: (
    sourceTwinId: string,
    relationshipId: string
  ) => Promise<BasicRelationship>;
  getRelationshipsForTwin: (twinId: string) => {
    outgoing: BasicRelationship[];
    incoming: BasicRelationship[];
  };
  queryRelationships: (
    twinId: string,
    type?: string
  ) => Promise<BasicRelationship[]>;

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
        const queryResult = await get().queryTwins(QUERY_ALL_TWINS);

        set({
          twins: queryResult.twins,
          relationships: queryResult.relationships,
          isLoading: false,
        });
      } catch (error) {
        console.error("Error loading twins:", error);
        let errorMessage = "Failed to load twins";

        if (error instanceof Error) {
          errorMessage = error.message;
        } else if (typeof error === "object" && error !== null) {
          // Handle API error responses
          const apiError = error as any;
          if (apiError.statusCode) {
            errorMessage = `API Error ${apiError.statusCode}: ${
              apiError.message || "Unknown error"
            }`;
          } else if (apiError.message) {
            errorMessage = apiError.message;
          }
        }

        set({
          error: errorMessage,
          isLoading: false,
        });
      }
    },

    createTwin: async (twinData) => {
      set({ isLoading: true, error: null });
      try {
        const client = await getClient();

        // Generate a unique ID if not provided
        const twinId =
          twinData.$dtId ||
          `twin-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;

        const response = await client.upsertDigitalTwin(
          twinId,
          JSON.stringify(twinData)
        );

        const newTwin = response as BasicDigitalTwin;

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
        const client = await getClient();

        // Convert updates to JSON Patch operations
        const patch: Operation[] = Object.entries(updates).map(
          ([key, value]) => ({
            op: "replace" as const,
            path: `/${key}`,
            value,
          })
        );

        await client.updateDigitalTwin(
          twinId,
          patch as unknown as Array<Record<string, unknown>>
        );

        // Refresh the twin from the server
        const updatedTwin = await get().getTwinById(twinId);

        set((state) => ({
          twins: state.twins.map((twin) =>
            twin.$dtId === twinId ? updatedTwin : twin
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
        const client = await getClient();

        // Delete all relationships first
        const rels = await get().queryRelationships(twinId, REL_TYPE_ALL);
        for (const rel of rels) {
          await client.deleteRelationship(rel.$sourceId, rel.$relationshipId);
        }

        // Delete the twin
        await client.deleteDigitalTwin(twinId);

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

    getTwinById: async (twinId) => {
      const client = await getClient();
      const response = await client.getDigitalTwin(twinId);
      return response as BasicDigitalTwin;
    },

    getTwinsByModel: (modelId) => {
      return get().twins.filter((twin) => twin.$metadata.$model === modelId);
    },

    queryTwins: async (query) => {
      const client = await getClient();
      const result: QueryResponseData = {
        twins: [],
        relationships: [],
        count: 0,
        other: [],
        data: [],
      };

      const queryResult = client.queryTwins(query);
      for await (const page of queryResult.byPage()) {
        const data = getDataFromQueryResponse(page.value);
        result.twins.push(...data.twins);
        result.relationships.push(...data.relationships);
        result.other.push(...data.other);
        result.data.push(...data.data);
        result.count = data.count || result.twins.length;
      }

      return result;
    },

    // Relationships actions
    loadRelationships: async (twinId) => {
      set({ isLoading: true, error: null });
      try {
        if (twinId) {
          const rels = await get().queryRelationships(twinId, REL_TYPE_ALL);
          set({ relationships: rels, isLoading: false });
        } else {
          // Load all relationships via query
          const queryResult = await get().queryTwins(
            "SELECT * FROM RELATIONSHIPS"
          );
          set({ relationships: queryResult.relationships, isLoading: false });
        }
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
        const client = await getClient();

        const {
          $sourceId,
          $targetId,
          $relationshipName,
          $relationshipId,
          ...properties
        } = relationshipData;

        const relationshipId =
          $relationshipId ||
          `rel-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;

        const response = await client.upsertRelationship(
          $sourceId,
          relationshipId,
          {
            $relationshipName,
            $targetId,
            ...properties,
          }
        );

        const newRelationship = response as BasicRelationship;

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

    updateRelationship: async (sourceTwinId, relationshipId, patch) => {
      set({ isLoading: true, error: null });
      try {
        const client = await getClient();

        await client.updateRelationship(
          sourceTwinId,
          relationshipId,
          patch as unknown as Array<Record<string, unknown>>
        );

        // Refresh the relationship from the server
        const updatedRel = await get().getRelationship(
          sourceTwinId,
          relationshipId
        );

        set((state) => ({
          relationships: state.relationships.map((rel) =>
            rel.$relationshipId === relationshipId &&
            rel.$sourceId === sourceTwinId
              ? updatedRel
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

    deleteRelationship: async (sourceTwinId, relationshipId) => {
      set({ isLoading: true, error: null });
      try {
        const client = await getClient();
        await client.deleteRelationship(sourceTwinId, relationshipId);

        set((state) => ({
          relationships: state.relationships.filter(
            (rel) =>
              !(
                rel.$relationshipId === relationshipId &&
                rel.$sourceId === sourceTwinId
              )
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

    getRelationship: async (sourceTwinId, relationshipId) => {
      const client = await getClient();
      const response = await client.getRelationship(
        sourceTwinId,
        relationshipId
      );
      return response as BasicRelationship;
    },

    queryRelationships: async (twinId, type = REL_TYPE_OUTGOING) => {
      const client = await getClient();
      const list: BasicRelationship[] = [];

      const query =
        type === REL_TYPE_ALL
          ? `SELECT * FROM RELATIONSHIPS WHERE $targetId = '${twinId}' OR $sourceId = '${twinId}'`
          : type === REL_TYPE_INCOMING
          ? `SELECT * FROM RELATIONSHIPS WHERE $targetId = '${twinId}'`
          : `SELECT * FROM RELATIONSHIPS WHERE $sourceId = '${twinId}'`;

      const queryResult = client.queryTwins(query).byPage();
      for await (const page of queryResult) {
        if (page.value) {
          list.push(...(page.value as BasicRelationship[]));
        }
      }

      return list;
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
              const twinValue = twin[key as keyof BasicDigitalTwin];
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
        await get().updateTwin(twinId, { [propertyName]: value });
        set({ isLoading: false });
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
        await get().updateTwin(twinId, { [componentName]: componentData });
        set({ isLoading: false });
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

// Subscribe to connection changes to auto-reload twins
useConnectionStore.subscribe(
  (state) => state.currentConnectionId,
  (currentConnectionId, previousConnectionId) => {
    // Only reload if connection actually changed and we have a connection
    if (currentConnectionId && currentConnectionId !== previousConnectionId) {
      console.log("Connection changed, reloading twins...");
      const { loadTwins } = useDigitalTwinsStore.getState();
      loadTwins();
    }
  }
);
