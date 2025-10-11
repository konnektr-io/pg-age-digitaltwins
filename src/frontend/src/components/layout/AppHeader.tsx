import { Database, Settings, ChevronDown } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useWorkspaceStore } from "@/stores/workspaceStore";
import { useConnectionStore } from "@/stores/connectionStore";

export function AppHeader() {
  const { mainView, setMainView } = useWorkspaceStore();
  const { isConnected, endpoint } = useConnectionStore();

  return (
    <header className="h-14 border-b border-border bg-card flex items-center justify-between px-4">
      <div className="flex items-center gap-4">
        {/* Logo and Title */}
        <div className="flex items-center gap-2">
          <Database className="w-5 h-5 text-secondary" />
          <span className="font-semibold text-foreground">Konnektr Graph</span>
        </div>

        <div className="h-6 w-px bg-border" />

        {/* Environment Selector */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="ghost"
              className="px-3 py-1.5 text-sm bg-muted hover:bg-muted/80 rounded-md flex items-center gap-2"
            >
              {endpoint ? new URL(endpoint).hostname : "No Environment"}
              <ChevronDown className="w-4 h-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start">
            <DropdownMenuItem>Configure Connection...</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
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
      <div className="flex items-center gap-2">
        <div
          className={`w-2 h-2 rounded-full ${
            isConnected ? "bg-green-500" : "bg-red-500"
          }`}
        />
        <span className="text-xs text-muted-foreground">
          {isConnected ? "Connected" : "Disconnected"}
        </span>
      </div>
    </header>
  );
}
