// @ts-nocheck - Zustand type inference issues with strict mode
import { create } from "zustand";
import { subscribeWithSelector } from "zustand/middleware";
import type { DigitalTwinsClient } from "@azure/digital-twins-core";
import type {
  DigitalTwinsModelDataExtended,
  DtdlInterface,
  DigitalTwinsModelData,
} from "@/types";
import { digitalTwinsClientFactory } from "@/services/digitalTwinsClientFactory";
import { useConnectionStore } from "./connectionStore";

/**
 * Helper to get initialized Digital Twins client
 * Throws error if connection is not configured
 *
 * Note: This function is now async to support MSAL initialization
 */
const getClient = async (): Promise<DigitalTwinsClient> => {
  const { getCurrentConnection, isConnected } = useConnectionStore.getState();
  const connection = getCurrentConnection();
  if (!connection || !isConnected) {
    throw new Error(
      "Not connected to Digital Twins instance. Please configure connection."
    );
  }

  // Use the new connection-based factory with auth support
  return await digitalTwinsClientFactory(connection);
};

export interface ModelsState {
  // State
  models: DigitalTwinsModelDataExtended[];
  selectedModelId: string | null;
  isLoading: boolean;
  error: string | null;
  filter: {
    searchTerm?: string;
    modelType?: string;
    isValid?: boolean;
  };
  validation: {
    [modelId: string]: {
      isValid: boolean;
      errors: string[];
      warnings: string[];
    };
  };

  // Actions - Models
  loadModels: () => Promise<void>;
  getModelById: (modelId: string) => Promise<DigitalTwinsModelData>;
  uploadModel: (model: DtdlInterface) => Promise<string>;
  uploadModels: (models: DtdlInterface[]) => Promise<DigitalTwinsModelData[]>;
  updateModel: (modelId: string, model: DtdlInterface) => Promise<void>;
  deleteModel: (modelId: string) => Promise<void>;
  decommissionModel: (modelId: string) => Promise<void>;
  getModel: (modelId: string) => DigitalTwinsModelDataExtended | undefined;
  getModelsByType: (modelType: string) => DigitalTwinsModelDataExtended[];

  // Actions - Validation
  validateModel: (modelId: string) => Promise<boolean>;
  validateAllModels: () => Promise<void>;
  getDependencies: (modelId: string) => string[];
  getDependents: (modelId: string) => string[];
  getInheritanceChain: (modelId: string) => string[];

  // Actions - Selection & Filtering
  selectModel: (modelId: string | null) => void;
  setFilter: (filter: Partial<ModelsState["filter"]>) => void;
  clearFilter: () => void;
  getFilteredModels: () => DigitalTwinsModelDataExtended[];

  // Actions - Relationships
  getRelatedModels: (modelId: string) => {
    extends: string[];
    components: string[];
    relationships: string[];
  };

  // Actions - Utility
  clearError: () => void;
  reset: () => void;
}

/**
 * Convert DigitalTwinsModelData to Extended format
 */
const toExtendedModel = (
  modelData: DigitalTwinsModelData
): DigitalTwinsModelDataExtended => {
  return {
    ...modelData,
    model: modelData.model as DtdlInterface,
  };
};

export const useModelsStore = create<ModelsState>()(
  subscribeWithSelector((set, get) => ({
    // Initial state
    models: [],
    selectedModelId: null,
    isLoading: false,
    error: null,
    filter: {},
    validation: {},

    // Models actions
    loadModels: async () => {
      set({ isLoading: true, error: null });
      try {
        const client = await getClient();
        const list: DigitalTwinsModelDataExtended[] = [];

        const models = client.listModels({
          dependenciesFor: [],
          includeModelDefinition: true,
        });

        for await (const model of models) {
          list.push(toExtendedModel(model as DigitalTwinsModelData));
        }

        set({ models: list, isLoading: false });

        // Auto-validate models after loading
        get().validateAllModels();
      } catch (error) {
        console.error("Error loading models:", error);
        let errorMessage = "Failed to load models";

        if (error instanceof Error) {
          errorMessage = error.message;
        } else if (typeof error === "object" && error !== null) {
          // Handle API error responses
          const apiError = error as any;
          if (apiError.statusCode) {
            errorMessage = `API Error ${apiError.statusCode}: ${
              apiError.message || "Unknown error"
            }`;
          } else if (apiError.message) {
            errorMessage = apiError.message;
          }
        }

        set({
          error: errorMessage,
          isLoading: false,
        });
      }
    },

    getModelById: async (modelId) => {
      const client = await getClient();
      const model = await client.getModel(modelId, {
        includeModelDefinition: true,
      });
      return model as DigitalTwinsModelData;
    },

    uploadModel: async (model) => {
      set({ isLoading: true, error: null });
      try {
        const modelId = model["@id"];
        if (!modelId) {
          throw new Error("Model must have an @id property");
        }

        // Check if model already exists
        const existingModel = get().models.find((m) => m.id === modelId);
        if (existingModel) {
          throw new Error(`Model with ID ${modelId} already exists`);
        }

        const client = await getClient();
        const result = await client.createModels([model]);
        const newModelData = toExtendedModel(
          result[0] as DigitalTwinsModelData
        );

        set((state) => ({
          models: [...state.models, newModelData],
          isLoading: false,
        }));

        // Validate the newly uploaded model
        await get().validateModel(modelId);

        return modelId;
      } catch (error) {
        set({
          error:
            error instanceof Error ? error.message : "Failed to upload model",
          isLoading: false,
        });
        throw error;
      }
    },

    uploadModels: async (models) => {
      set({ isLoading: true, error: null });
      try {
        const client = await getClient();
        const result = await client.createModels(models);
        const modelDataList = result.map((m) =>
          toExtendedModel(m as DigitalTwinsModelData)
        );

        set((state) => ({
          models: [...state.models, ...modelDataList],
          isLoading: false,
        }));

        // Validate all newly uploaded models
        for (const modelData of modelDataList) {
          await get().validateModel(modelData.id);
        }

        return result as DigitalTwinsModelData[];
      } catch (error) {
        set({
          error:
            error instanceof Error ? error.message : "Failed to upload models",
          isLoading: false,
        });
        throw error;
      }
    },

    updateModel: async (modelId, model) => {
      set({ isLoading: true, error: null });
      try {
        // In Azure Digital Twins, models are immutable once created
        // To update a model, you need to delete the old one and create a new one
        // This should only be done if no twins are using the model

        const client = await getClient();

        // Delete the old model
        await client.deleteModel(modelId);

        // Create the new model
        const result = await client.createModels([model]);
        const updatedModelData = toExtendedModel(
          result[0] as DigitalTwinsModelData
        );

        set((state) => ({
          models: state.models.map((m) =>
            m.id === modelId ? updatedModelData : m
          ),
          isLoading: false,
        }));

        // Validate the updated model
        await get().validateModel(modelId);
      } catch (error) {
        set({
          error:
            error instanceof Error ? error.message : "Failed to update model",
          isLoading: false,
        });
        throw error;
      }
    },

    deleteModel: async (modelId) => {
      set({ isLoading: true, error: null });
      try {
        const client = await getClient();
        await client.deleteModel(modelId);

        set((state) => ({
          models: state.models.filter((m) => m.id !== modelId),
          selectedModelId:
            state.selectedModelId === modelId ? null : state.selectedModelId,
          validation: Object.fromEntries(
            Object.entries(state.validation).filter(([id]) => id !== modelId)
          ),
          isLoading: false,
        }));
      } catch (error) {
        set({
          error:
            error instanceof Error ? error.message : "Failed to delete model",
          isLoading: false,
        });
        throw error;
      }
    },

    decommissionModel: async (modelId) => {
      set({ isLoading: true, error: null });
      try {
        const client = await getClient();
        await client.decomissionModel(modelId);

        set((state) => ({
          models: state.models.map((m) =>
            m.id === modelId ? { ...m, decommissioned: true } : m
          ),
          isLoading: false,
        }));
      } catch (error) {
        set({
          error:
            error instanceof Error
              ? error.message
              : "Failed to decommission model",
          isLoading: false,
        });
        throw error;
      }
    },

    getModel: (modelId) => {
      return get().models.find((m) => m.id === modelId);
    },

    getModelsByType: (modelType) => {
      return get().models.filter((m) => m.model["@type"] === modelType);
    },

    // Validation actions
    validateModel: async (modelId) => {
      try {
        const model = get().getModel(modelId);
        if (!model) {
          throw new Error(`Model ${modelId} not found`);
        }

        // TODO: Implement proper DTDL validation using DTDLParser
        // For now, perform basic validation
        const errors: string[] = [];
        const warnings: string[] = [];

        // Check required fields
        if (!model.model["@id"]) {
          errors.push("Model must have an @id");
        }
        if (!model.model["@type"]) {
          errors.push("Model must have an @type");
        }

        // Check @type is valid
        const validTypes = [
          "Interface",
          "Relationship",
          "Property",
          "Component",
          "Telemetry",
          "Command",
        ];
        const types = Array.isArray(model.model["@type"])
          ? model.model["@type"]
          : [model.model["@type"]];
        if (!types.some((t) => validTypes.includes(t))) {
          errors.push(
            `Invalid @type. Must be one of: ${validTypes.join(", ")}`
          );
        }

        // Check extends references exist
        if (model.model.extends) {
          const extendsList = Array.isArray(model.model.extends)
            ? model.model.extends
            : [model.model.extends];

          for (const extendedModel of extendsList) {
            const extendedId =
              typeof extendedModel === "string"
                ? extendedModel
                : extendedModel["@id"];
            if (!get().getModel(extendedId)) {
              warnings.push(
                `Extended model ${extendedId} not found in current model list`
              );
            }
          }
        }

        // Check component schemas exist
        if (model.model.contents) {
          for (const content of model.model.contents) {
            if (content["@type"] === "Component" && content.schema) {
              const schemaId =
                typeof content.schema === "string"
                  ? content.schema
                  : content.schema["@id"];
              if (schemaId && !get().getModel(schemaId)) {
                warnings.push(
                  `Component schema ${schemaId} not found in current model list`
                );
              }
            }
          }
        }

        const isValid = errors.length === 0;

        set((state) => ({
          validation: {
            ...state.validation,
            [modelId]: {
              isValid,
              errors,
              warnings,
            },
          },
        }));

        return isValid;
      } catch (error) {
        set((state) => ({
          validation: {
            ...state.validation,
            [modelId]: {
              isValid: false,
              errors: [
                error instanceof Error ? error.message : "Validation failed",
              ],
              warnings: [],
            },
          },
        }));
        return false;
      }
    },

    validateAllModels: async () => {
      const models = get().models;
      const validationPromises = models.map((model) =>
        get().validateModel(model.id)
      );
      await Promise.all(validationPromises);
    },

    getDependencies: (modelId) => {
      const model = get().getModel(modelId);
      if (!model) return [];

      const dependencies: string[] = [];

      // Add extends dependencies
      if (model.model.extends) {
        const extendsList = Array.isArray(model.model.extends)
          ? model.model.extends
          : [model.model.extends];

        extendsList.forEach((ext) => {
          const id = typeof ext === "string" ? ext : ext["@id"];
          if (id) dependencies.push(id);
        });
      }

      // Add component schema dependencies
      if (model.model.contents) {
        model.model.contents.forEach((content) => {
          if (content["@type"] === "Component" && content.schema) {
            const schemaId =
              typeof content.schema === "string"
                ? content.schema
                : content.schema["@id"];
            if (schemaId) dependencies.push(schemaId);
          }
        });
      }

      return [...new Set(dependencies)]; // Remove duplicates
    },

    getDependents: (modelId) => {
      const allModels = get().models;
      const dependents: string[] = [];

      allModels.forEach((model) => {
        const deps = get().getDependencies(model.id);
        if (deps.includes(modelId)) {
          dependents.push(model.id);
        }
      });

      return dependents;
    },

    getInheritanceChain: (modelId) => {
      const chain: string[] = [];
      let currentModelId = modelId;

      while (currentModelId) {
        if (chain.includes(currentModelId)) {
          // Circular dependency detected
          break;
        }
        chain.push(currentModelId);

        const model = get().getModel(currentModelId);
        if (!model?.model.extends) {
          break;
        }

        // For simplicity, take the first extended model if multiple
        const extends_ = Array.isArray(model.model.extends)
          ? model.model.extends[0]
          : model.model.extends;
        currentModelId = typeof extends_ === "string" ? extends_ : "";
      }

      return chain;
    },

    // Selection & Filtering actions
    selectModel: (modelId) => {
      set({ selectedModelId: modelId });
    },

    setFilter: (newFilter) => {
      set((state) => ({
        filter: { ...state.filter, ...newFilter },
      }));
    },

    clearFilter: () => {
      set({ filter: {} });
    },

    getFilteredModels: () => {
      const { models, filter, validation } = get();
      let filtered = models;

      if (filter.searchTerm) {
        const searchTerm = filter.searchTerm.toLowerCase();
        filtered = filtered.filter((model) => {
          const id = model.id.toLowerCase();
          const displayName = model.displayName?.en?.toLowerCase() || "";
          const description = model.description?.en?.toLowerCase() || "";

          return (
            id.includes(searchTerm) ||
            displayName.includes(searchTerm) ||
            description.includes(searchTerm)
          );
        });
      }

      if (filter.modelType) {
        filtered = filtered.filter(
          (m) => m.model["@type"] === filter.modelType
        );
      }

      if (filter.isValid !== undefined) {
        filtered = filtered.filter((m) => {
          const v = validation[m.id];
          return v ? v.isValid === filter.isValid : false;
        });
      }

      return filtered;
    },

    // Relationships actions
    getRelatedModels: (modelId) => {
      const model = get().getModel(modelId);
      if (!model) return { extends: [], components: [], relationships: [] };

      const extends_: string[] = [];
      const components: string[] = [];
      const relationships: string[] = [];

      // Get extends
      if (model.model.extends) {
        const extendsList = Array.isArray(model.model.extends)
          ? model.model.extends
          : [model.model.extends];

        extendsList.forEach((ext) => {
          const id = typeof ext === "string" ? ext : ext["@id"];
          if (id) extends_.push(id);
        });
      }

      // Get components and relationships from contents
      if (model.model.contents) {
        model.model.contents.forEach((content) => {
          if (content["@type"] === "Component" && content.schema) {
            const schemaId =
              typeof content.schema === "string"
                ? content.schema
                : content.schema["@id"];
            if (schemaId) components.push(schemaId);
          } else if (content["@type"] === "Relationship" && content.target) {
            relationships.push(content.target);
          }
        });
      }

      return {
        extends: [...new Set(extends_)],
        components: [...new Set(components)],
        relationships: [...new Set(relationships)],
      };
    },

    // Utility actions
    clearError: () => {
      set({ error: null });
    },

    reset: () => {
      set({
        models: [],
        selectedModelId: null,
        isLoading: false,
        error: null,
        filter: {},
        validation: {},
      });
    },
  }))
);

// Subscribe to connection changes to auto-reload models
useConnectionStore.subscribe(
  (state) => state.currentConnectionId,
  (currentConnectionId, previousConnectionId) => {
    // Only reload if connection actually changed and we have a connection
    if (currentConnectionId && currentConnectionId !== previousConnectionId) {
      console.log("Connection changed, reloading models...");
      const { loadModels } = useModelsStore.getState();
      loadModels();
    }
  }
);
