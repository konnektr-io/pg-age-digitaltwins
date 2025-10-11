import { create } from "zustand";
import { persist } from "zustand/middleware";

export type MainView = "query" | "models";
export type SelectedItemType = "twin" | "model";

export interface SelectedItem {
  type: SelectedItemType;
  id: string;
}

interface WorkspaceState {
  // View state
  mainView: MainView;
  showLeftPanel: boolean;
  showRightPanel: boolean;

  // Selection state
  selectedItem: SelectedItem | null;

  // Panel sizes (for react-resizable-panels)
  leftPanelSize: number;
  rightPanelSize: number;

  // Actions
  setMainView: (view: MainView) => void;
  setShowLeftPanel: (show: boolean) => void;
  setShowRightPanel: (show: boolean) => void;
  setSelectedItem: (item: SelectedItem | null) => void;
  setPanelSize: (panel: "left" | "right", size: number) => void;
  toggleLeftPanel: () => void;
  toggleRightPanel: () => void;
}

export const useWorkspaceStore = create<WorkspaceState>()(
  persist(
    (set) => ({
      // Initial state
      mainView: "query",
      showLeftPanel: true,
      showRightPanel: true,
      selectedItem: null,
      leftPanelSize: 20, // percentage
      rightPanelSize: 25, // percentage

      // Actions
      setMainView: (view) => set({ mainView: view }),
      setShowLeftPanel: (show) => set({ showLeftPanel: show }),
      setShowRightPanel: (show) => set({ showRightPanel: show }),
      setSelectedItem: (item) => set({ selectedItem: item }),
      setPanelSize: (panel, size) =>
        set((state) => ({
          ...state,
          [panel === "left" ? "leftPanelSize" : "rightPanelSize"]: size,
        })),
      toggleLeftPanel: () =>
        set((state) => ({ showLeftPanel: !state.showLeftPanel })),
      toggleRightPanel: () =>
        set((state) => ({ showRightPanel: !state.showRightPanel })),
    }),
    {
      name: "konnektr-workspace",
      // Only persist layout preferences, not selections
      partialize: (state) => ({
        mainView: state.mainView,
        showLeftPanel: state.showLeftPanel,
        showRightPanel: state.showRightPanel,
        leftPanelSize: state.leftPanelSize,
        rightPanelSize: state.rightPanelSize,
      }),
    }
  )
);
