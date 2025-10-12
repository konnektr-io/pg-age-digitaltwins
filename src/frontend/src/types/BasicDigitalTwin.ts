export interface DigitalTwinPropertyMetadata {
  lastUpdateTime?: string;
  sourceTime?: string;
  /** Desired value for IoT PnP devcies */
  desiredValue?: string | number | boolean;
  desiredVersion?: number;
  ackVersion?: number;
  ackCode?: number;
  ackDescription?: string;
}

/** Basic Digital Twin data model with id and model */
export interface BasicDigitalTwin {
  /** The unique Id of the digital twin in a digital twins instance. */
  $dtId: string
  /** Information about the model a digital twin conforms to. */
  $metadata: {
    /** The Id of the model that the digital twin or component is modeled by. */
    $model: string
    /** Metadata about changes on properties on the digital twin. The key will be the property name, and the value is the metadata. */
    [x: string]: string | DigitalTwinPropertyMetadata
  }
  /** Properties and components as defined in the contents section of the DTDL definition of the twin. */
  [x: string]: unknown
}

/** Basic Digital Twin Component data model with id and model */
export interface BasicDigitalTwinComponent {
  /** Information about the model a digital twin conforms to. */
  $metadata: {
    /** Metadata about changes on properties on the digital twin. The key will be the property name, and the value is the metadata. */
    [x: string]: DigitalTwinPropertyMetadata
  }
  /** Properties and components as defined in the contents section of the DTDL definition of the twin. */
  [x: string]: unknown
}
