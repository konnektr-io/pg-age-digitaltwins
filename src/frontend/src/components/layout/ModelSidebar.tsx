import { useState } from "react";
import {
  Search,
  Plus,
  Upload,
  ChevronRight,
  ChevronDown,
  Layers,
  Database,
  X,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import { useWorkspaceStore } from "@/stores/workspaceStore";
import { useInspectorStore } from "@/stores/inspectorStore";

// Mock data for development - using real Digital Twin model structure
interface MockModel {
  id: string;
  name: string;
  displayName: string;
  count: number;
  expanded?: boolean;
  children?: MockModel[];
}

// Import real model data
import {
  mockModels as realModels,
  mockDigitalTwins,
  getModelDisplayName,
} from "@/mocks/digitalTwinData";

const mockModels: MockModel[] = realModels.map((model) => {
  let modelId = "";
  if (
    typeof model.model === "object" &&
    model.model !== null &&
    "@id" in model.model
  ) {
    const idVal = (model.model as Record<string, unknown>)["@id"];
    if (typeof idVal === "string") {
      modelId = idVal;
    }
  }
  return {
    id: modelId,
    name: modelId.split(":").pop()?.split(";")[0] || "Unknown",
    displayName: getModelDisplayName(modelId),
    count: mockDigitalTwins.filter((twin) => twin.$metadata.$model === modelId)
      .length,
    expanded: false,
    children: [],
  };
});

interface ModelTreeItemProps {
  model: MockModel;
  level?: number;
  onSelect: (modelId: string) => void;
  selectedId?: string;
}

function ModelTreeItem({
  model,
  level = 0,
  onSelect,
  selectedId,
}: ModelTreeItemProps) {
  const [expanded, setExpanded] = useState(
    "expanded" in model ? model.expanded : false
  );
  const hasChildren =
    "children" in model && model.children && model.children.length > 0;

  const isSelected = selectedId === model.id;

  return (
    <div>
      <div
        className={`
          flex items-center gap-1 px-2 py-1 rounded-md cursor-pointer hover:bg-muted/50
          ${isSelected ? "bg-secondary/20 text-secondary-foreground" : ""}
        `}
        style={{ paddingLeft: `${8 + level * 16}px` }}
        onClick={() => onSelect(model.id)}
      >
        {hasChildren ? (
          <Button
            variant="ghost"
            size="sm"
            className="p-0 h-4 w-4 hover:bg-transparent"
            onClick={(e) => {
              e.stopPropagation();
              setExpanded(!expanded);
            }}
          >
            {expanded ? (
              <ChevronDown className="w-3 h-3" />
            ) : (
              <ChevronRight className="w-3 h-3" />
            )}
          </Button>
        ) : (
          <div className="w-4" />
        )}

        <Layers className="w-4 h-4 text-muted-foreground flex-shrink-0" />

        <div className="flex-1 min-w-0">
          <span className="text-sm truncate block">
            {model.displayName || model.name}
          </span>
        </div>

        <span className="text-xs text-muted-foreground bg-muted px-1.5 py-0.5 rounded">
          {model.count}
        </span>
      </div>

      {hasChildren && expanded && model.children && (
        <div>
          {model.children.map((child) => (
            <ModelTreeItem
              key={child.id}
              model={child}
              level={level + 1}
              onSelect={onSelect}
              selectedId={selectedId}
            />
          ))}
        </div>
      )}
    </div>
  );
}

export function ModelSidebar() {
  const [searchQuery, setSearchQuery] = useState("");
  const { selectedItem, setSelectedItem, setShowLeftPanel } =
    useWorkspaceStore();
  const { selectItem } = useInspectorStore();

  const handleModelSelect = (modelId: string) => {
    setSelectedItem({ type: "model", id: modelId });
    selectItem({ type: "model", id: modelId });
  };

  const filteredModels = mockModels.filter(
    (model) =>
      model.displayName.toLowerCase().includes(searchQuery.toLowerCase()) ||
      model.name.toLowerCase().includes(searchQuery.toLowerCase())
  );

  return (
    <div className="flex flex-col h-full w-full bg-card border-r border-border">
      {/* Sidebar Header with Title */}
      <div className="border-b bg-background px-6 py-4">
        <div className="flex items-center justify-between h-10">
          <div className="flex items-center gap-2">
            <Database className="h-5 w-5 text-secondary" />
            <span className="font-semibold">Konnektr Graph</span>
          </div>
          <Button
            variant="ghost"
            size="sm"
            className="p-1.5"
            onClick={() => setShowLeftPanel(false)}
            title="Close Models Panel"
          >
            <X className="w-4 h-4" />
          </Button>
        </div>
      </div>

      {/* Models Section Header */}
      <div className="p-3 border-b border-border">
        <div className="flex items-center justify-between mb-2">
          <h2 className="font-semibold text-sm">Models</h2>
          <div className="flex gap-1">
            <Button variant="ghost" size="sm" className="p-1.5">
              <Plus className="w-3 h-3" />
            </Button>
            <Button variant="ghost" size="sm" className="p-1.5">
              <Upload className="w-3 h-3" />
            </Button>
          </div>
        </div>

        {/* Search */}
        <div className="relative">
          <Search className="w-4 h-4 absolute left-2 top-2 text-muted-foreground" />
          <Input
            type="text"
            placeholder="Search models..."
            className="pl-8 pr-3 py-1.5 text-sm"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
        </div>
      </div>

      {/* Model Tree */}
      <ScrollArea className="flex-1 p-2">
        <div className="space-y-1">
          {filteredModels.map((model) => (
            <ModelTreeItem
              key={model.id}
              model={model}
              onSelect={handleModelSelect}
              selectedId={
                selectedItem?.type === "model" ? selectedItem.id : undefined
              }
            />
          ))}
        </div>

        {filteredModels.length === 0 && searchQuery && (
          <div className="text-center text-muted-foreground text-sm py-8">
            No models found matching "{searchQuery}"
          </div>
        )}
      </ScrollArea>
    </div>
  );
}
