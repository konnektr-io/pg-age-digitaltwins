import { create } from "zustand";
import { persist } from "zustand/middleware";

export type MainView = "query" | "models";

interface WorkspaceState {
  // View state
  mainView: MainView;
  showLeftPanel: boolean;
  showRightPanel: boolean;

  // Panel sizes (for react-resizable-panels)
  leftPanelSize: number;
  rightPanelSize: number;

  // Actions
  setMainView: (view: MainView) => void;
  setShowLeftPanel: (show: boolean) => void;
  setShowRightPanel: (show: boolean) => void;
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
      leftPanelSize: 20, // percentage
      rightPanelSize: 25, // percentage

      // Actions
      setMainView: (view: MainView) => set({ mainView: view }),
      setShowLeftPanel: (show: boolean) => set({ showLeftPanel: show }),
      setShowRightPanel: (show: boolean) => set({ showRightPanel: show }),
      setPanelSize: (panel: "left" | "right", size: number) =>
        set((state: WorkspaceState) => ({
          ...state,
          [panel === "left" ? "leftPanelSize" : "rightPanelSize"]: size,
        })),
      toggleLeftPanel: () =>
        set((state: WorkspaceState) => ({
          showLeftPanel: !state.showLeftPanel,
        })),
      toggleRightPanel: () =>
        set((state: WorkspaceState) => ({
          showRightPanel: !state.showRightPanel,
        })),
    }),
    {
      name: "konnektr-workspace",
      // Only persist layout preferences, not selections
      partialize: (state: WorkspaceState) => ({
        mainView: state.mainView,
        showLeftPanel: state.showLeftPanel,
        showRightPanel: state.showRightPanel,
        leftPanelSize: state.leftPanelSize,
        rightPanelSize: state.rightPanelSize,
      }),
    }
  )
);
