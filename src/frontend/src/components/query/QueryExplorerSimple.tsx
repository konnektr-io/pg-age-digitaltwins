import { useState } from "react";
import { Panel, PanelGroup, PanelResizeHandle } from "react-resizable-panels";
import { QueryResults } from "./QueryResults";
import { Button } from "@/components/ui/button";
import { Play, Save, History, Settings2 } from "lucide-react";
import { useQueryStore } from "@/stores/queryStore";
import { Textarea } from "@/components/ui/textarea";

export function QueryExplorer() {
  const {
    currentQuery,
    setCurrentQuery,
    executeQuery,
    isExecuting: isLoading,
    queryResults,
    queryError,
  } = useQueryStore();
  const [localQuery, setLocalQuery] = useState(currentQuery);

  const handleExecute = () => {
    setCurrentQuery(localQuery);
    executeQuery(localQuery);
  };

  return (
    <div className="h-full flex flex-col">
      {/* Toolbar */}
      <div className="border-b p-3 bg-muted/30">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Button
              onClick={handleExecute}
              disabled={isLoading}
              className="gap-2"
            >
              <Play className="w-4 h-4" />
              {isLoading ? "Running..." : "Run Query"}
            </Button>
            <Button variant="outline" size="sm" className="gap-2">
              <Save className="w-4 h-4" />
              Save
            </Button>
            <Button variant="outline" size="sm" className="gap-2">
              <History className="w-4 h-4" />
              History
            </Button>
          </div>
          <Button variant="ghost" size="sm">
            <Settings2 className="w-4 h-4" />
          </Button>
        </div>
      </div>

      {/* Main Content */}
      <div className="flex-1 overflow-hidden">
        <PanelGroup direction="horizontal" className="h-full">
          {/* Query Editor */}
          <Panel defaultSize={40} minSize={25} maxSize={60}>
            <div className="h-full flex flex-col">
              <div className="border-b p-2 bg-muted/20">
                <h3 className="text-sm font-medium">Query Editor</h3>
              </div>
              <div className="flex-1 p-4">
                <Textarea
                  value={localQuery}
                  onChange={(e) => setLocalQuery(e.target.value)}
                  placeholder="Enter your Digital Twins query here..."
                  className="h-full font-mono text-sm resize-none"
                />
              </div>
            </div>
          </Panel>

          <PanelResizeHandle className="w-1 bg-border hover:bg-border/80 transition-colors data-[resize-handle-active]:bg-primary" />

          {/* Results */}
          <Panel defaultSize={60} minSize={40}>
            <div className="h-full">
              <QueryResults
                results={queryResults}
                error={queryError}
                isLoading={isLoading}
              />
            </div>
          </Panel>
        </PanelGroup>
      </div>
    </div>
  );
}
