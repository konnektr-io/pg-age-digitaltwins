import type { DigitalTwinsModelData } from '@azure/digital-twins-core'
import type { DtdlInterface, DtdlProperty, DtdlRelationship, DtdlTelemetry, DtdlComponent } from '.'

/** A model definition and metadata for that model. */
export interface DigitalTwinsModelDataExtended extends DigitalTwinsModelData {
  /** The DTDL model */
  model: DtdlInterface
  /** All properties (including inherited) for this model */
  properties?: DtdlProperty[]
  /** All relationships (including inherited) for this model */
  relationships?: DtdlRelationship[]
  /** All components (including inherited) for this model */
  components?: DtdlComponent[]
  /** All telemetries (including inherited) for this model */
  telemetries?: DtdlTelemetry[]
  /** All base model ids that this model inherits from */
  bases?: string[]
  /** Child models that inherit directly from this model (used for ModelTree) */
  children?: DigitalTwinsModelDataExtended[]
  /** If the model is hidden, it is abstract and shouldn't be instantiated */
  hidden?: boolean
  /** Whether the model is selectable (used for ModelTree) */
  selectable?: boolean
  [key: string]: unknown
}
