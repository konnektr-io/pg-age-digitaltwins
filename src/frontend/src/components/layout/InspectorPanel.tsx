import { useWorkspaceStore } from "@/stores/workspaceStore";
import { Inspector } from "../inspector/Inspector";

export function InspectorPanel() {
  const { showRightPanel } = useWorkspaceStore();

  if (!showRightPanel) {
    return null;
  }

  return (
    <div className="flex flex-col h-full w-full bg-card border-l border-border">
      <Inspector />
    </div>
  );
}
