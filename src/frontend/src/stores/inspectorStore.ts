import { create } from "zustand";

export interface InspectorItem {
  type: "twin" | "relationship" | "model";
  id: string;
  data?: any;
}

interface InspectorState {
  selectedItem: InspectorItem | null;
  selectItem: (item: InspectorItem) => void;
  clearSelection: () => void;
}

export const useInspectorStore = create<InspectorState>((set) => ({
  selectedItem: null,
  selectItem: (item) => set({ selectedItem: item }),
  clearSelection: () => set({ selectedItem: null }),
}));
