import type { DtdlInterface } from "./DtdlInterface";

export interface DigitalTwinsModelData {
    /** A language map that contains the localized display names as specified in the model definition. */
    displayName?: {
        [propertyName: string]: string;
    };
    /** A language map that contains the localized descriptions as specified in the model definition. */
    description?: {
        [propertyName: string]: string;
    };
    /** The id of the model as specified in the model definition. */
    id: string;
    /** The time the model was uploaded to the service. */
    uploadTime?: Date;
    /** Indicates if the model is decommissioned. Decommissioned models cannot be referenced by newly created digital twins. */
    decommissioned?: boolean;
    /** The model definition. */
    model?: DtdlInterface;
}
