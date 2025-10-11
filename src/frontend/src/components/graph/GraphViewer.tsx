import { useEffect, useRef } from "react";
import Graph from "graphology";
import Sigma from "sigma";
import type { BasicDigitalTwin, BasicRelationship } from "@/types";

interface GraphViewerProps {
  twins: BasicDigitalTwin[];
  relationships: BasicRelationship[];
  onNodeClick?: (twinId: string) => void;
}

// Function to get color by model type
const getColorByModelType = (modelType: string): string => {
  const colors: Record<string, string> = {
    Building: "#3b82f6",
    Room: "#10b981",
    Sensor: "#f59e0b",
    Device: "#ef4444",
    Person: "#8b5cf6",
  };
  return colors[modelType] || "#6b7280";
};

export function GraphViewer({
  twins,
  relationships,
  onNodeClick,
}: GraphViewerProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const sigmaRef = useRef<Sigma | null>(null);

  useEffect(() => {
    if (!containerRef.current) return;

    // Create a new graph
    const graph = new Graph();

    // Add nodes (digital twins)
    twins.forEach((twin) => {
      const modelType = twin.$metadata?.$model || "Unknown";
      graph.addNode(twin.$dtId, {
        x: Math.random() * 100,
        y: Math.random() * 100,
        size: 10,
        label: twin.$dtId,
        color: getColorByModelType(modelType),
        modelType,
        twin,
      });
    });

    // Add edges (relationships)
    relationships.forEach((rel) => {
      if (graph.hasNode(rel.$sourceId) && graph.hasNode(rel.$targetId)) {
        try {
          graph.addEdgeWithKey(
            rel.$relationshipId,
            rel.$sourceId,
            rel.$targetId,
            {
              label: rel.$relationshipName || "relationship",
              color: "#666",
              size: 2,
              relationship: rel,
            }
          );
        } catch (error) {
          // Edge might already exist, skip
          console.warn("Could not add edge:", error);
        }
      }
    });

    // Create Sigma instance
    const sigma = new Sigma(graph, containerRef.current, {
      nodeReducer: (_node, data) => ({
        ...data,
        size: data.size || 10,
        color: data.color || "#999",
      }),
      edgeReducer: (_edge, data) => ({
        ...data,
        color: data.color || "#ccc",
        size: data.size || 1,
      }),
    });

    sigmaRef.current = sigma;

    // Add click handler
    sigma.on("clickNode", ({ node }) => {
      const nodeData = graph.getNodeAttributes(node);
      if (onNodeClick && nodeData.twin) {
        onNodeClick(nodeData.twin.$dtId);
      }
    });

    // Simple layout: spread nodes in a circle
    if (twins.length > 0) {
      const angleStep = (2 * Math.PI) / twins.length;
      graph.forEachNode((node: string) => {
        const index = twins.findIndex((t) => t.$dtId === node);
        if (index >= 0 && containerRef.current) {
          const angle = index * angleStep;
          const radius =
            Math.min(
              containerRef.current.clientWidth,
              containerRef.current.clientHeight
            ) * 0.3;
          const centerX = containerRef.current.clientWidth / 2;
          const centerY = containerRef.current.clientHeight / 2;

          graph.setNodeAttribute(node, "x", centerX + Math.cos(angle) * radius);
          graph.setNodeAttribute(node, "y", centerY + Math.sin(angle) * radius);
        }
      });
    }

    // Cleanup function
    return () => {
      if (sigmaRef.current) {
        sigmaRef.current.kill();
        sigmaRef.current = null;
      }
    };
  }, [twins, relationships, onNodeClick]);

  return (
    <div
      ref={containerRef}
      className="w-full h-full min-h-[400px] bg-background border rounded"
      style={{ position: "relative" }}
    />
  );
}
