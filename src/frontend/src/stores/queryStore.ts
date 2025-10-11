import { create } from "zustand";
import { persist } from "zustand/middleware";

export interface QueryTab {
  id: string;
  name: string;
  query: string;
  saved: boolean;
  language: "sql" | "cypher";
}

export interface QueryHistoryItem {
  query: string;
  timestamp: number;
  executionTime?: number;
  resultCount?: number;
}

export interface QueryState {
  // Tab management
  tabs: QueryTab[];
  activeTabId: string | null;

  // Current query state
  currentQuery: string;
  isExecuting: boolean;
  queryResults: any[] | null;
  queryError: string | null;

  // Query history
  queryHistory: QueryHistoryItem[];
  showHistory: boolean;

  // Results view state
  resultView: "table" | "graph";

  // Actions
  addTab: (tab?: Partial<QueryTab>) => string;
  removeTab: (tabId: string) => void;
  updateTab: (tabId: string, updates: Partial<QueryTab>) => void;
  setActiveTab: (tabId: string) => void;
  addToHistory: (item: QueryHistoryItem) => void;
  setResultView: (view: "table" | "graph") => void;
  clearHistory: () => void;

  // New actions for query execution
  setCurrentQuery: (query: string) => void;
  executeQuery: (query: string) => Promise<void>;
  setShowHistory: (show: boolean) => void;
}

const generateTabId = () => Date.now().toString();

const createDefaultTab = (overrides?: Partial<QueryTab>): QueryTab => ({
  id: generateTabId(),
  name: "Untitled",
  query: "-- Cypher Query (Ctrl+Enter to run)\nMATCH (n) RETURN n LIMIT 10",
  saved: true,
  language: "cypher",
  ...overrides,
});

export const useQueryStore = create<QueryState>()(
  persist(
    (set, get) => ({
      // Initial state
      tabs: [createDefaultTab({ name: "Query 1" })],
      activeTabId: null,

      // Current query state
      currentQuery: "MATCH (n) RETURN n LIMIT 10",
      isExecuting: false,
      queryResults: null,
      queryError: null,

      // Query history
      queryHistory: [],
      showHistory: false,

      // Results view state
      resultView: "table",

      // Actions
      addTab: (tabOverrides) => {
        const newTab = createDefaultTab({
          name: `Query ${get().tabs.length + 1}`,
          ...tabOverrides,
        });

        set((state) => ({
          tabs: [...state.tabs, newTab],
          activeTabId: newTab.id,
        }));

        return newTab.id;
      },

      removeTab: (tabId) => {
        set((state) => {
          const newTabs = state.tabs.filter((tab) => tab.id !== tabId);

          // If we're removing the active tab, select another one
          let newActiveTabId = state.activeTabId;
          if (state.activeTabId === tabId) {
            if (newTabs.length > 0) {
              // Try to select the next tab, or the previous one if it was the last
              const removedIndex = state.tabs.findIndex(
                (tab) => tab.id === tabId
              );
              const nextIndex = Math.min(removedIndex, newTabs.length - 1);
              newActiveTabId = newTabs[nextIndex]?.id || null;
            } else {
              newActiveTabId = null;
            }
          }

          return {
            tabs: newTabs,
            activeTabId: newActiveTabId,
          };
        });
      },

      updateTab: (tabId, updates) => {
        set((state) => ({
          tabs: state.tabs.map((tab) =>
            tab.id === tabId ? { ...tab, ...updates, saved: false } : tab
          ),
        }));
      },

      setActiveTab: (tabId) => {
        set({ activeTabId: tabId });
      },

      addToHistory: (item) => {
        set((state) => ({
          queryHistory: [
            item,
            ...state.queryHistory.slice(0, 49), // Keep last 50 queries
          ],
        }));
      },

      setResultView: (view) => set({ resultView: view }),

      clearHistory: () => set({ queryHistory: [] }),

      // New action methods
      setCurrentQuery: (query) => set({ currentQuery: query }),

      executeQuery: async (query) => {
        set({ isExecuting: true, queryError: null });

        try {
          // Mock API call - replace with actual GraphQL/REST call
          const executionTime = 800 + Math.random() * 400;
          await new Promise((resolve) => setTimeout(resolve, executionTime));

          // Import mock data dynamically to avoid circular dependencies
          const { mockQueryResults, mockDigitalTwins, formatTwinForDisplay } =
            await import("@/mocks/digitalTwinData");

          // Simple query routing based on query content
          let results: any[] = [];
          const queryLower = query.toLowerCase();

          if (queryLower.includes("building") && queryLower.includes("floor")) {
            // Relationship query
            results = mockQueryResults.twinRelationshipResults.map(
              (result) => ({
                ...result,
                b: result.b ? formatTwinForDisplay(result.b) : null,
                f: result.f ? formatTwinForDisplay(result.f) : null,
              })
            );
          } else if (queryLower.includes("building")) {
            // Building query
            results = mockQueryResults.singleTwins.map(formatTwinForDisplay);
          } else if (
            queryLower.includes("avg") ||
            queryLower.includes("count")
          ) {
            // Aggregation query
            results = mockQueryResults.aggregationResults;
          } else if (
            queryLower.includes("collect") ||
            queryLower.includes("*")
          ) {
            // Nested results query
            results = mockQueryResults.nestedResults.map((result) => ({
              ...result,
              b: result.b ? formatTwinForDisplay(result.b) : null,
              rooms: result.rooms?.map(formatTwinForDisplay) || [],
            }));
          } else {
            // Default: return all twins
            results = mockDigitalTwins.map(formatTwinForDisplay);
          }

          set({
            queryResults: results,
            isExecuting: false,
          });

          // Add to history
          get().addToHistory({
            query,
            timestamp: Date.now(),
            executionTime: Math.round(executionTime),
            resultCount: results.length,
          });
        } catch (error) {
          set({
            queryError:
              error instanceof Error ? error.message : "Unknown error occurred",
            isExecuting: false,
          });
        }
      },

      setShowHistory: (show) => set({ showHistory: show }),
    }),
    {
      name: "konnektr-query",
      // Don't persist query history to avoid bloating localStorage
      partialize: (state) => ({
        tabs: state.tabs,
        activeTabId: state.activeTabId,
        resultView: state.resultView,
      }),
    }
  )
);

// Initialize active tab if none is set
useQueryStore.getState().activeTabId =
  useQueryStore.getState().activeTabId ||
  useQueryStore.getState().tabs[0]?.id ||
  null;
