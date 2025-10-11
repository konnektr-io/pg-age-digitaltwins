import type { DtdlMapSchema, DtdlObjectSchema, DtdlSchema } from './Dtdl'
import type { DtdlLocalizableString } from './DtdlLocalizableString'

export interface DtdlProperty {
  '@id'?: string
  /**
   * Type should be 'Property', with or without adjunct type.
   * Support for other adjunct types depends on the used extensions
   */
  '@type': 'Property' | (string[] & { 0: 'Property' })
  name: string
  comment?: string
  displayName?: DtdlLocalizableString
  description?: DtdlLocalizableString
  schema: DtdlSchema
  unit?: string
  writable?: boolean
  /** Part of Overriding extension */
  overrides?: string
  /** Part of Annotation extension */
  annotates?: string
}

export interface DtdlObjectProperty extends DtdlProperty {
  schema: DtdlObjectSchema
}

export interface DtdlMapProperty extends DtdlProperty {
  schema: DtdlMapSchema
}
