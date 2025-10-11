import type { DtdlProperty } from './DtdlProperty'
import type { DtdlLocalizableString } from './DtdlLocalizableString'

export interface DtdlRelationship {
  '@id'?: string
  /** Type should be 'Property', with or without semantic type. Optional to allow using it as field in object */
  '@type': 'Relationship'
  name: string
  comment?: string
  displayName?: DtdlLocalizableString
  description?: DtdlLocalizableString
  target: string
  properties: DtdlProperty[]
}
