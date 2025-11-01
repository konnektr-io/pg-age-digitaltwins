/**
 * Utility functions to analyze query result data structures
 * and determine the best table view mode
 */

/**
 * Data complexity levels
 */
export type DataComplexity = "simple" | "nested" | "complex";

/**
 * Table view mode recommendations
 */
export type TableViewMode = "simple" | "grouped" | "flat" | "expandable";

/**
 * Result of data structure analysis
 */
export interface DataStructureInfo {
  hasNestedEntities: boolean;
  entityColumns: string[];
  complexity: DataComplexity;
  recommendedView: TableViewMode;
  totalColumns: number;
  hasDeepNesting: boolean;
}

/**
 * Check if a value is a digital twin entity (has $dtId)
 */
function isEntity(value: unknown): boolean {
  return (
    typeof value === "object" &&
    value !== null &&
    "$dtId" in value &&
    typeof (value as Record<string, unknown>).$dtId === "string"
  );
}

/**
 * Get all keys from first result that contain entity objects
 */
export function getEntityColumns(results: unknown[] | null): string[] {
  if (!results || results.length === 0) return [];

  const firstRow = results[0];
  if (typeof firstRow !== "object" || firstRow === null) return [];

  const row = firstRow as Record<string, unknown>;
  return Object.keys(row).filter((key) => isEntity(row[key]));
}

/**
 * Count total number of columns including nested properties
 */
function getTotalColumnCount(results: unknown[] | null): number {
  if (!results || results.length === 0) return 0;

  const firstRow = results[0];
  if (typeof firstRow !== "object" || firstRow === null) return 0;

  const row = firstRow as Record<string, unknown>;
  let count = 0;

  for (const value of Object.values(row)) {
    if (isEntity(value) && typeof value === "object" && value !== null) {
      // Count properties of the entity
      count += Object.keys(value).length;
    } else {
      count += 1;
    }
  }

  return count;
}

/**
 * Check if results have deeply nested structures (3+ levels)
 */
function hasDeepNesting(results: unknown[] | null, maxDepth = 3): boolean {
  if (!results || results.length === 0) return false;

  function checkDepth(obj: unknown, depth: number): boolean {
    if (depth >= maxDepth) return true;
    if (typeof obj !== "object" || obj === null) return false;

    const record = obj as Record<string, unknown>;
    for (const value of Object.values(record)) {
      if (typeof value === "object" && value !== null) {
        if (checkDepth(value, depth + 1)) return true;
      }
    }
    return false;
  }

  // Check first few results for performance
  const sampleSize = Math.min(3, results.length);
  for (let i = 0; i < sampleSize; i++) {
    if (checkDepth(results[i], 0)) return true;
  }

  return false;
}

/**
 * Determine data complexity based on structure
 */
function determineComplexity(
  hasEntities: boolean,
  entityCount: number,
  totalColumns: number,
  deepNesting: boolean
): DataComplexity {
  if (!hasEntities && totalColumns <= 5) return "simple";
  if (deepNesting || totalColumns > 20) return "complex";
  if (hasEntities && entityCount > 0) return "nested";
  return "simple";
}

/**
 * Recommend the best table view mode based on data structure
 */
function recommendViewMode(
  complexity: DataComplexity,
  entityCount: number,
  totalColumns: number
): TableViewMode {
  switch (complexity) {
    case "simple":
      return "simple";

    case "nested":
      if (entityCount === 1) {
        // Single entity column - flat view works well
        return "flat";
      }
      if (entityCount <= 3 && totalColumns <= 15) {
        // Multiple entities but manageable - grouped view
        return "grouped";
      }
      // Many entities or columns - expandable for better organization
      return "expandable";

    case "complex":
      // Complex data benefits from expandable rows
      return "expandable";

    default:
      return "simple";
  }
}

/**
 * Analyze query results data structure
 * Returns information about nesting, complexity, and recommended view mode
 *
 * @param results - Query results to analyze
 * @returns Data structure analysis information
 *
 * @example
 * const info = analyzeDataStructure(results);
 * console.log(info.recommendedView); // "grouped", "flat", or "expandable"
 */
export function analyzeDataStructure(
  results: unknown[] | null
): DataStructureInfo {
  const entityColumns = getEntityColumns(results);
  const hasNestedEntities = entityColumns.length > 0;
  const totalColumns = getTotalColumnCount(results);
  const deepNesting = hasDeepNesting(results);

  const complexity = determineComplexity(
    hasNestedEntities,
    entityColumns.length,
    totalColumns,
    deepNesting
  );

  const recommendedView = recommendViewMode(
    complexity,
    entityColumns.length,
    totalColumns
  );

  return {
    hasNestedEntities,
    entityColumns,
    complexity,
    recommendedView,
    totalColumns,
    hasDeepNesting: deepNesting,
  };
}

/**
 * Get properties from an entity object, excluding metadata
 */
export function getEntityProperties(entity: unknown): Array<[string, unknown]> {
  if (typeof entity !== "object" || entity === null) return [];

  const entries = Object.entries(entity);
  return entries.filter(([key]) => key !== "$metadata");
}

/**
 * Extract entity type from its metadata model
 */
export function getEntityType(entity: unknown): string {
  if (
    typeof entity === "object" &&
    entity !== null &&
    "$metadata" in entity &&
    typeof entity.$metadata === "object" &&
    entity.$metadata !== null &&
    "$model" in entity.$metadata
  ) {
    const model = String(entity.$metadata.$model);
    const parts = model.split(":");
    return parts[parts.length - 1]?.split(";")[0] || "Entity";
  }
  return "Entity";
}
