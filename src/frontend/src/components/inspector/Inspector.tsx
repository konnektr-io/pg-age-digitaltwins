import { useState } from "react";
import {
  Search,
  FileText,
  Database,
  GitBranch,
  FileCode2,
  X,
} from "lucide-react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";

import { TwinInspector } from "./TwinInspector";
import { RelationshipInspector } from "./RelationshipInspector";
import { ModelInspector } from "./ModelInspector";
import { useInspectorStore } from "@/stores/inspectorStore";

export function Inspector() {
  const selectedItem = useInspectorStore((state) => state.selectedItem);
  const clearSelection = useInspectorStore((state) => state.clearSelection);
  const [searchQuery, setSearchQuery] = useState("");

  if (!selectedItem) {
    return (
      <div className="h-full w-full flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b">
          <h2 className="font-semibold text-sm">Inspector</h2>
        </div>

        {/* Empty State */}
        <div className="flex-1 flex items-center justify-center p-6">
          <div className="text-center space-y-3">
            <Search className="w-12 h-12 mx-auto text-muted-foreground/50" />
            <div className="space-y-1">
              <div className="font-medium text-sm">No item selected</div>
              <div className="text-xs text-muted-foreground max-w-48">
                Click on a digital twin, relationship, or model in the query
                results or model sidebar to inspect it
              </div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  const getIcon = () => {
    switch (selectedItem.type) {
      case "twin":
        return <Database className="w-4 h-4" />;
      case "relationship":
        return <GitBranch className="w-4 h-4" />;
      case "model":
        return <FileCode2 className="w-4 h-4" />;
      default:
        return <FileText className="w-4 h-4" />;
    }
  };

  const getTypeLabel = () => {
    switch (selectedItem.type) {
      case "twin":
        return "Digital Twin";
      case "relationship":
        return "Relationship";
      case "model":
        return "Model";
      default:
        return "Item";
    }
  };

  return (
    <div className="h-full w-full flex flex-col bg-card border-l">
      {/* Header */}
      <div className="flex items-center justify-between p-4 border-b">
        <div className="flex items-center gap-2 min-w-0 flex-1">
          <h2 className="font-semibold text-sm">Inspector</h2>
          <div className="flex items-center gap-2 min-w-0">
            {getIcon()}
            <Badge variant="secondary" className="text-xs">
              {getTypeLabel()}
            </Badge>
          </div>
        </div>
        <Button
          variant="ghost"
          size="sm"
          onClick={clearSelection}
          className="h-6 w-6 p-0"
        >
          <X className="w-3 h-3" />
        </Button>
      </div>

      {/* Search */}
      <div className="p-4 border-b">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <Input
            placeholder="Search properties..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="pl-9 h-8 text-sm"
          />
        </div>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-hidden">
        <div className="h-full overflow-y-auto p-4">
          {selectedItem.type === "twin" && (
            <TwinInspector twinId={selectedItem.id} />
          )}
          {selectedItem.type === "relationship" && (
            <RelationshipInspector relationshipId={selectedItem.id} />
          )}
          {selectedItem.type === "model" && (
            <ModelInspector modelId={selectedItem.id} />
          )}
        </div>
      </div>
    </div>
  );
}
