import type { BasicDigitalTwin, BasicRelationship } from "@/types";

/**
 * Result of transforming query results into graph-compatible format
 */
export interface TransformedGraphData {
  twins: BasicDigitalTwin[];
  relationships: BasicRelationship[];
  hasGraphData: boolean;
}

/**
 * Type guard to check if an object is a BasicDigitalTwin
 * Must have $dtId and $metadata properties
 */
export function isBasicDigitalTwin(obj: unknown): obj is BasicDigitalTwin {
  if (typeof obj !== "object" || obj === null) {
    return false;
  }

  const candidate = obj as Record<string, unknown>;
  return (
    typeof candidate.$dtId === "string" &&
    typeof candidate.$metadata === "object" &&
    candidate.$metadata !== null
  );
}

/**
 * Type guard to check if an object is a BasicRelationship
 * Must have $relationshipId, $sourceId, and $targetId properties
 */
export function isBasicRelationship(obj: unknown): obj is BasicRelationship {
  if (typeof obj !== "object" || obj === null) {
    return false;
  }

  const candidate = obj as Record<string, unknown>;
  return (
    typeof candidate.$relationshipId === "string" &&
    typeof candidate.$sourceId === "string" &&
    typeof candidate.$targetId === "string"
  );
}

/**
 * Extract twins and relationships from a single result object
 * Handles nested objects by recursively searching for twins/relationships
 */
function extractFromObject(
  obj: Record<string, unknown>,
  twins: Set<BasicDigitalTwin>,
  relationships: Set<BasicRelationship>
): void {
  // Check if the object itself is a twin or relationship
  if (isBasicDigitalTwin(obj)) {
    twins.add(obj);
  } else if (isBasicRelationship(obj)) {
    relationships.add(obj);
  }

  // Recursively check nested objects
  for (const value of Object.values(obj)) {
    if (typeof value === "object" && value !== null) {
      if (Array.isArray(value)) {
        for (const item of value) {
          if (typeof item === "object" && item !== null) {
            extractFromObject(
              item as Record<string, unknown>,
              twins,
              relationships
            );
          }
        }
      } else {
        extractFromObject(
          value as Record<string, unknown>,
          twins,
          relationships
        );
      }
    }
  }
}

/**
 * Transform query results into graph-compatible format
 * Detects and extracts BasicDigitalTwin and BasicRelationship objects from:
 * - Flat result arrays (each result is a twin or relationship)
 * - Nested result objects (twins/relationships are nested properties)
 * - Mixed structures (combination of both)
 *
 * @param results - Query results to transform (can be any structure)
 * @returns Transformed data with twins, relationships, and hasGraphData flag
 *
 * @example
 * // Flat results
 * const results = [
 *   { $dtId: "twin1", $metadata: {...} },
 *   { $relationshipId: "rel1", $sourceId: "twin1", $targetId: "twin2" }
 * ];
 * const { twins, relationships, hasGraphData } = transformResultsToGraph(results);
 *
 * @example
 * // Nested results
 * const results = [
 *   {
 *     building: { $dtId: "building1", $metadata: {...} },
 *     room: { $dtId: "room1", $metadata: {...} },
 *     relationship: { $relationshipId: "rel1", $sourceId: "building1", $targetId: "room1" }
 *   }
 * ];
 * const { twins, relationships, hasGraphData } = transformResultsToGraph(results);
 */
export function transformResultsToGraph(
  results: unknown[] | null
): TransformedGraphData {
  const twinsSet = new Set<BasicDigitalTwin>();
  const relationshipsSet = new Set<BasicRelationship>();

  // Handle null or empty results
  if (!results || results.length === 0) {
    return {
      twins: [],
      relationships: [],
      hasGraphData: false,
    };
  }

  // Process each result
  for (const result of results) {
    if (typeof result === "object" && result !== null) {
      extractFromObject(
        result as Record<string, unknown>,
        twinsSet,
        relationshipsSet
      );
    }
  }

  const twins = Array.from(twinsSet);
  const relationships = Array.from(relationshipsSet);

  return {
    twins,
    relationships,
    hasGraphData: twins.length > 0 || relationships.length > 0,
  };
}

/**
 * Check if query results contain graph-compatible data
 * without performing the full transformation
 *
 * @param results - Query results to check
 * @returns True if results contain at least one twin or relationship
 */
export function hasGraphData(results: unknown[] | null): boolean {
  if (!results || results.length === 0) {
    return false;
  }

  // Quick check: look at first few results
  const sampleSize = Math.min(5, results.length);
  for (let i = 0; i < sampleSize; i++) {
    const result = results[i];
    if (typeof result === "object" && result !== null) {
      const obj = result as Record<string, unknown>;

      // Check if it's a twin or relationship at top level
      if (isBasicDigitalTwin(obj) || isBasicRelationship(obj)) {
        return true;
      }

      // Check nested properties
      for (const value of Object.values(obj)) {
        if (typeof value === "object" && value !== null) {
          if (isBasicDigitalTwin(value) || isBasicRelationship(value)) {
            return true;
          }
        }
      }
    }
  }

  return false;
}
