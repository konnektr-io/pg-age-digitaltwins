import { Database, Settings } from "lucide-react";
import { Button } from "@/components/ui/button";

import { useWorkspaceStore } from "@/stores/workspaceStore";
import { ConnectionSelector } from "@/components/ConnectionSelector";
import { ConnectionStatus } from "@/components/ConnectionStatus";

export function AppHeader() {
  const { mainView, setMainView } = useWorkspaceStore();

  return (
    <header className="h-14 border-b border-border bg-card flex items-center justify-between px-4">
      <div className="flex items-center gap-4">
        {/* Logo and Title - Main Brand */}
        <div className="flex items-center gap-2">
          <Database className="w-5 h-5 text-secondary" />
          <span className="font-semibold text-foreground text-lg">
            Konnektr Graph
          </span>
        </div>

        <div className="h-6 w-px bg-border" />

        {/* Connection Selector */}
        <ConnectionSelector />
      </div>

      {/* View Switcher */}
      <div className="flex items-center gap-2">
        <div className="flex gap-1 p-1 bg-muted rounded-md mr-2">
          <Button
            variant={mainView === "query" ? "default" : "ghost"}
            size="sm"
            className="px-3 py-1.5 text-xs"
            onClick={() => setMainView("query")}
          >
            Query Explorer
          </Button>
          <Button
            variant={mainView === "models" ? "default" : "ghost"}
            size="sm"
            className="px-3 py-1.5 text-xs"
            onClick={() => setMainView("models")}
          >
            Model Graph
          </Button>
        </div>

        {/* Settings Button */}
        <Button variant="ghost" size="sm" className="p-2">
          <Settings className="w-4 h-4" />
        </Button>
      </div>

      {/* Connection Status Indicator */}
      <ConnectionStatus />
    </header>
  );
}
