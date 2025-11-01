import { create } from "zustand";
import { persist } from "zustand/middleware";
import { digitalTwinsClientFactory } from "@/services/digitalTwinsClientFactory";
import { useConnectionStore } from "./connectionStore";
import { formatTwinForDisplay } from "@/utils/dtdlHelpers";

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
        const startTime = Date.now();

        try {
          // Get current connection
          const { getCurrentConnection, isConnected } =
            useConnectionStore.getState();
          const connection = getCurrentConnection();

          if (!connection || !isConnected) {
            throw new Error(
              "Not connected to Digital Twins instance. Please configure connection."
            );
          }

          // Get authenticated client
          const client = await digitalTwinsClientFactory(connection);

          // Execute query using Azure SDK
          const queryResult = client.queryTwins(query);
          const results: any[] = [];

          // Iterate through paginated results
          for await (const item of queryResult) {
            // Format twin data for display if it has the structure of a digital twin
            const formattedItem =
              typeof item === "object" && item !== null && "$dtId" in item
                ? formatTwinForDisplay(item as any)
                : item;
            results.push(formattedItem);
          }

          const executionTime = Date.now() - startTime;

          set({
            queryResults: results,
            isExecuting: false,
          });

          // Add to history
          get().addToHistory({
            query,
            timestamp: Date.now(),
            executionTime,
            resultCount: results.length,
          });
        } catch (error) {
          const executionTime = Date.now() - startTime;

          let errorMessage = "Unknown error occurred";
          if (error instanceof Error) {
            errorMessage = error.message;
          } else if (typeof error === "string") {
            errorMessage = error;
          }

          set({
            queryError: errorMessage,
            isExecuting: false,
            queryResults: null,
          });

          // Still add to history even on error
          get().addToHistory({
            query,
            timestamp: Date.now(),
            executionTime,
            resultCount: 0,
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
