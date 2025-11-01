import { useState, useEffect } from "react";
import { Search, Plus, Upload, Layers, Trash2, Copy } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import { TreeView, type TreeDataItem } from "@/components/ui/tree-view";
import { Badge } from "@/components/ui/badge";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { useInspectorStore } from "@/stores/inspectorStore";
import { useModelsStore } from "@/stores/modelsStore";
import { useDigitalTwinsStore } from "@/stores/digitalTwinsStore";
import { getModelDisplayName } from "@/utils/dtdlHelpers";
import type { DigitalTwinsModelDataExtended } from "@/types";

/**
 * Build a tree structure from models based on 'extends' relationships
 */
function buildModelTree(
  models: DigitalTwinsModelDataExtended[],
  twins: any[],
  onDelete: (modelId: string) => void,
  onCreateTwin: (modelId: string) => void
): TreeDataItem[] {
  if (models.length === 0) return [];

  const modelMap = new Map<string, DigitalTwinsModelDataExtended>();
  const twinCountMap = new Map<string, number>();
  const childrenMap = new Map<string, Set<string>>();

  // Build maps
  models.forEach((model) => {
    modelMap.set(model.id, model);
    const count = twins.filter(
      (twin) => twin.$metadata?.$model === model.id
    ).length;
    twinCountMap.set(model.id, count);
  });

  // Build parent-child relationships
  models.forEach((model) => {
    const extendsValue = model.model?.extends;
    if (!extendsValue) return;

    const extendsList = Array.isArray(extendsValue)
      ? extendsValue
      : [extendsValue];

    extendsList.forEach((ext) => {
      const parentId = typeof ext === "string" ? ext : ext?.["@id"];
      if (parentId && modelMap.has(parentId)) {
        if (!childrenMap.has(parentId)) {
          childrenMap.set(parentId, new Set());
        }
        childrenMap.get(parentId)!.add(model.id);
      }
    });
  });

  // Find root models (models that don't extend anything or extend models not in the list)
  const rootModels = models.filter((model) => {
    const extendsValue = model.model?.extends;
    if (!extendsValue) return true;

    const extendsList = Array.isArray(extendsValue)
      ? extendsValue
      : [extendsValue];

    // Check if any parent exists in our model list
    const hasParentInList = extendsList.some((ext) => {
      const parentId = typeof ext === "string" ? ext : ext?.["@id"];
      return parentId && modelMap.has(parentId);
    });

    return !hasParentInList;
  });

  // Recursive function to build tree nodes
  function buildNode(model: DigitalTwinsModelDataExtended): TreeDataItem {
    const modelId = model.id;
    const displayName = getModelDisplayName(modelId);
    const count = twinCountMap.get(modelId) || 0;

    // Get children from the map
    const childIds = childrenMap.get(modelId);
    const children =
      childIds && childIds.size > 0
        ? Array.from(childIds)
            .map((childId) => modelMap.get(childId))
            .filter((m): m is DigitalTwinsModelDataExtended => m !== undefined)
            .map((childModel) => buildNode(childModel))
        : undefined;

    return {
      id: modelId,
      name: displayName,
      icon: Layers,
      children,
      actions: (
        <div
          className="flex items-center gap-1"
          onClick={(e) => e.stopPropagation()}
        >
          <Badge variant="secondary" className="text-xs">
            {count}
          </Badge>
          <div
            role="button"
            tabIndex={0}
            className="h-6 w-6 p-0 inline-flex items-center justify-center rounded-md hover:bg-accent hover:text-accent-foreground cursor-pointer"
            onClick={(e) => {
              e.stopPropagation();
              onCreateTwin(modelId);
            }}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                e.stopPropagation();
                onCreateTwin(modelId);
              }
            }}
            title="Create twin from this model"
          >
            <Copy className="w-3 h-3" />
          </div>
          <div
            role="button"
            tabIndex={0}
            className="h-6 w-6 p-0 inline-flex items-center justify-center rounded-md hover:bg-destructive/10 hover:text-destructive cursor-pointer text-destructive/70"
            onClick={(e) => {
              e.stopPropagation();
              onDelete(modelId);
            }}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                e.stopPropagation();
                onDelete(modelId);
              }
            }}
            title="Delete model"
          >
            <Trash2 className="w-3 h-3" />
          </div>
        </div>
      ),
    };
  }

  return rootModels.map((model) => buildNode(model));
}

export function ModelSidebar() {
  const [searchQuery, setSearchQuery] = useState("");
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [modelToDelete, setModelToDelete] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const selectedItem = useInspectorStore((state) => state.selectedItem);
  const selectItem = useInspectorStore((state) => state.selectItem);
  const {
    models,
    loadModels,
    deleteModel,
    isLoading: modelsLoading,
    error: modelsError,
  } = useModelsStore();
  const {
    twins,
    loadTwins,
    isLoading: twinsLoading,
    error: twinsError,
  } = useDigitalTwinsStore();

  // Load models and twins on mount
  useEffect(() => {
    loadModels();
    loadTwins();
  }, [loadModels, loadTwins]);

  const isLoading = modelsLoading || twinsLoading;
  const error = modelsError || twinsError;

  const handleModelSelect = (item: TreeDataItem | undefined) => {
    if (item) {
      selectItem({ type: "model", id: item.id });
    }
  };

  const handleDeleteModel = (modelId: string) => {
    setModelToDelete(modelId);
    setDeleteDialogOpen(true);
  };

  const confirmDelete = async () => {
    if (!modelToDelete) return;

    setIsDeleting(true);
    try {
      await deleteModel(modelToDelete);
      setDeleteDialogOpen(false);
      setModelToDelete(null);
    } catch (err) {
      console.error("Failed to delete model:", err);
    } finally {
      setIsDeleting(false);
    }
  };

  const handleCreateTwin = (modelId: string) => {
    // TODO: Implement create twin dialog
    console.log("Create twin for model:", modelId);
  };

  // Build tree structure from models
  const modelTree = buildModelTree(
    models,
    twins,
    handleDeleteModel,
    handleCreateTwin
  );

  // Filter tree recursively
  const filterTree = (items: TreeDataItem[], query: string): TreeDataItem[] => {
    if (!query) return items;

    const lowerQuery = query.toLowerCase();
    const filtered: TreeDataItem[] = [];

    for (const item of items) {
      const matchesName = item.name.toLowerCase().includes(lowerQuery);
      const matchesId = item.id.toLowerCase().includes(lowerQuery);
      const filteredChildren = item.children
        ? filterTree(item.children, query)
        : undefined;

      if (
        matchesName ||
        matchesId ||
        (filteredChildren && filteredChildren.length > 0)
      ) {
        filtered.push({
          ...item,
          children: filteredChildren,
        });
      }
    }

    return filtered;
  };

  const filteredTree = filterTree(modelTree, searchQuery);

  // Compute the selected model ID for the tree
  const treeSelectedId =
    selectedItem?.type === "model" ? selectedItem.id : null;

  return (
    <>
      <div className="flex flex-col h-full w-full bg-card border-r border-border">
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
        <div className="flex-1 overflow-hidden">
          <ScrollArea className="h-full">
            <div className="p-2">
              {/* Loading State */}
              {isLoading && !error && (
                <div className="text-center text-muted-foreground text-sm py-8">
                  <div className="flex flex-col items-center gap-2">
                    <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-primary"></div>
                    <span>Loading models...</span>
                  </div>
                </div>
              )}

              {/* Error State */}
              {error && !isLoading && (
                <div className="text-center text-sm py-8 px-4">
                  <div className="flex flex-col items-center gap-3">
                    <div className="text-destructive">
                      <Layers className="w-8 h-8 mx-auto mb-2" />
                      <div className="font-semibold">Failed to load models</div>
                    </div>
                    <div className="text-xs text-muted-foreground bg-muted p-2 rounded max-w-full break-words">
                      {error}
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => {
                        loadModels();
                        loadTwins();
                      }}
                      className="mt-2"
                    >
                      Retry
                    </Button>
                  </div>
                </div>
              )}

              {/* Model Tree */}
              {!isLoading && !error && (
                <>
                  {filteredTree.length > 0 && (
                    <TreeView
                      data={filteredTree}
                      className="w-full"
                      selectedItemId={treeSelectedId}
                      onSelectChange={handleModelSelect}
                    />
                  )}

                  {filteredTree.length === 0 && searchQuery && (
                    <div className="text-center text-muted-foreground text-sm py-8">
                      No models found matching "{searchQuery}"
                    </div>
                  )}

                  {filteredTree.length === 0 &&
                    !searchQuery &&
                    models.length === 0 && (
                      <div className="text-center text-muted-foreground text-sm py-8">
                        <Layers className="w-8 h-8 mx-auto mb-2 opacity-50" />
                        <div>No models found</div>
                        <div className="text-xs mt-1">
                          Upload a model to get started
                        </div>
                      </div>
                    )}
                </>
              )}
            </div>
          </ScrollArea>
        </div>
      </div>

      {/* Delete Confirmation Dialog */}
      <AlertDialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Model</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to delete model{" "}
              <code className="bg-muted px-1 py-0.5 rounded text-xs">
                {modelToDelete ? getModelDisplayName(modelToDelete) : ""}
              </code>
              ? This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isDeleting}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={confirmDelete}
              disabled={isDeleting}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {isDeleting ? "Deleting..." : "Delete"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
