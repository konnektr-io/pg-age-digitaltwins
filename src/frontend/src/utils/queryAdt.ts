import type { QueryResult } from "@azure/digital-twins-core";
import type { BasicDigitalTwin } from "@/types/BasicDigitalTwin";
import type { BasicRelationship } from "@/types/BasicRelationship";

export interface QueryResponseData {
  /** All twins extracted from query */
  twins: BasicDigitalTwin[];
  /** All relationships extracted from query */
  relationships: BasicRelationship[];
  /** Returned count (in case of a COUNT query) or total number of twins */
  count: number;
  /** Objects in the query that aren not a twin nor a relationship */
  other: unknown[];
  /** The raw query response */
  data: Record<string, unknown>[];
}

/** Extract all twins and relationships from a query response */
export const getDataFromQueryResponse = (
  response: QueryResult["value"]
): QueryResponseData => {
  const result: QueryResponseData = {
    twins: [],
    relationships: [],
    count: 0,
    other: [],
    data: response ? [...response] : [],
  };
  if (!response || response.length === 0) return result;

  const list = [...response];

  for (const current of list) {
    if (current.$dtId) {
      if (!result.twins.some((x) => x.$dtId === current.$dtId))
        result.twins.push(current as BasicDigitalTwin);
      continue;
    } else if (current.$relationshipId) {
      if (
        !result.relationships.some(
          (x) =>
            x.$sourceId === current.$sourceId &&
            x.$relationshipId === current.$relationshipId
        )
      )
        result.relationships.push(current as BasicRelationship);
      continue;
    } else if (typeof current.COUNT === "number" && !isNaN(current.COUNT)) {
      result.count = current.COUNT;
      continue;
    }

    for (const k of Object.keys(current)) {
      const v = current[k];
      if (Array.isArray(v)) {
        v.forEach((x) => list.push(x));
      } else if (
        typeof v === "object" &&
        v !== null &&
        Object.getOwnPropertyNames(v).length > 0
      ) {
        list.push(v as Record<string, unknown>);
      } else {
        result.other.push(v);
      }
    }
  }
  if (!result.count && result.twins.length > 0)
    result.count = result.twins.length;

  return result;
};
