import { useWorkspaceStore } from "@/stores/workspaceStore";
import { QueryExplorer } from "@/components/query/QueryExplorer";

export function MainContent() {
  const { mainView } = useWorkspaceStore();

  if (mainView === "query") {
    return <QueryExplorer />;
  }

  return (
    <div className="flex-1 flex items-center justify-center bg-background">
      <div className="text-center">
        <h2 className="text-2xl font-semibold mb-2">Model Graph</h2>
        <p className="text-muted-foreground">
          DTDL model visualization will be implemented in Phase 5
        </p>
      </div>
    </div>
  );
}
