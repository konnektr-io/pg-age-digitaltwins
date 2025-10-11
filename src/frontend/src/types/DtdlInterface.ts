import type { DtdlTelemetry } from './DtdlTelemetry'
import type { DtdlLocalizableString } from './DtdlLocalizableString'
import type { DtdlComponent } from './DtdlComponent'
import type { DtdlRelationship } from './DtdlRelationship'
import type { DtdlProperty } from './DtdlProperty'

export type DtdlContent = DtdlProperty | DtdlRelationship | DtdlTelemetry | DtdlComponent

export interface DtdlInterface extends Record<string, unknown> {
  '@context'?: string[]
  '@id': string
  '@type': 'Interface' | (string[] & { 0: 'Interface' })
  contents?: DtdlContent[]
  comment?: string
  displayName?: DtdlLocalizableString
  description?: DtdlLocalizableString
  extends: string | string[]
  /** MQTT extension */
  telemetryTopic?: string
  /** MQTT extension */
  commandTopic?: string
  /** MQTT extension */
  payloadFormat?: string
}
