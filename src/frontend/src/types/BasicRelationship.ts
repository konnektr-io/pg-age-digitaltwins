/** Basic Digital Twin data model with id and model */
export interface BasicRelationship {
  /** The unique Id of the relationship. */
  $relationshipId: string
  /** The name of the relationship, which defines the type of link (e.g. Contains). */
  $relationshipName: string
  /** The unique Id of the source digital twin. */
  $sourceId: string
  /** The unique Id of the target digital twin. */
  $targetId: string
  /** Additional, custom properties defined in the DTDL model. */
  [x: string]: unknown
}
