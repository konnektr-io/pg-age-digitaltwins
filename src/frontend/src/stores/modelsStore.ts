import { create } from "zustand";
import { subscribeWithSelector } from "zustand/middleware";
import type { DigitalTwinsModelDataExtended, DtdlInterface } from "@/types";
import { mockModels } from "@/mocks/digitalTwinData";

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
  uploadModel: (model: DtdlInterface) => Promise<string>;
  updateModel: (modelId: string, model: DtdlInterface) => Promise<void>;
  deleteModel: (modelId: string) => Promise<void>;
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
        // TODO: Replace with actual API call
        await new Promise((resolve) => setTimeout(resolve, 100)); // Simulate network delay
        set({ models: [...mockModels], isLoading: false });

        // Auto-validate models after loading
        get().validateAllModels();
      } catch (error) {
        set({
          error:
            error instanceof Error ? error.message : "Failed to load models",
          isLoading: false,
        });
      }
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

        const newModelData: DigitalTwinsModelDataExtended = {
          id: modelId,
          model: model,
          displayName:
            typeof model.displayName === "string"
              ? { en: model.displayName }
              : typeof model.displayName === "object" && model.displayName
              ? model.displayName
              : undefined,
          description:
            typeof model.description === "string"
              ? { en: model.description }
              : typeof model.description === "object" && model.description
              ? model.description
              : undefined,
          uploadTime: new Date(),
          decommissioned: false,
        };

        // TODO: Replace with actual API call
        await new Promise((resolve) => setTimeout(resolve, 100));

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

    updateModel: async (modelId, model) => {
      set({ isLoading: true, error: null });
      try {
        // TODO: Replace with actual API call
        await new Promise((resolve) => setTimeout(resolve, 100));

        set((state) => ({
          models: state.models.map((m) =>
            m.id === modelId
              ? {
                  ...m,
                  model,
                  displayName:
                    typeof model.displayName === "string"
                      ? { en: model.displayName }
                      : typeof model.displayName === "object" &&
                        model.displayName
                      ? model.displayName
                      : m.displayName,
                  description:
                    typeof model.description === "string"
                      ? { en: model.description }
                      : typeof model.description === "object" &&
                        model.description
                      ? model.description
                      : m.description,
                  uploadTime: new Date(),
                }
              : m
          ),
          isLoading: false,
        }));

        // Re-validate after update
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
        // Check for dependents before deleting
        const dependents = get().getDependents(modelId);
        if (dependents.length > 0) {
          throw new Error(
            `Cannot delete model ${modelId}. It is referenced by: ${dependents.join(
              ", "
            )}`
          );
        }

        // TODO: Replace with actual API call
        await new Promise((resolve) => setTimeout(resolve, 100));

        set((state) => ({
          models: state.models.filter((m) => m.id !== modelId),
          selectedModelId:
            state.selectedModelId === modelId ? null : state.selectedModelId,
          validation: Object.fromEntries(
            Object.entries(state.validation).filter(([id]) => id !== modelId)
          ),
          isLoading: false,
        }));

        // Re-validate all models after deletion (dependencies might be affected)
        get().validateAllModels();
      } catch (error) {
        set({
          error:
            error instanceof Error ? error.message : "Failed to delete model",
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
          set((state) => ({
            validation: {
              ...state.validation,
              [modelId]: {
                isValid: false,
                errors: ["Model not found"],
                warnings: [],
              },
            },
          }));
          return false;
        }

        // TODO: Replace with actual DTDL validation
        await new Promise((resolve) => setTimeout(resolve, 50));

        // Mock validation logic
        const errors: string[] = [];
        const warnings: string[] = [];

        // Check basic structure
        if (!model.model["@id"]) {
          errors.push("Model must have an @id property");
        }
        if (!model.model["@type"]) {
          errors.push("Model must have an @type property");
        }

        // Check extends references
        if (model.model.extends) {
          const extendsList = Array.isArray(model.model.extends)
            ? model.model.extends
            : [model.model.extends];
          extendsList.forEach((extendedModel) => {
            if (
              typeof extendedModel === "string" &&
              !get().getModel(extendedModel)
            ) {
              errors.push(`Extended model not found: ${extendedModel}`);
            }
          });
        }

        // Check component references
        if (model.model.contents) {
          model.model.contents.forEach((content) => {
            if (content["@type"] === "Component" && content.schema) {
              if (
                typeof content.schema === "string" &&
                !get().getModel(content.schema)
              ) {
                warnings.push(`Component schema not found: ${content.schema}`);
              }
            }
          });
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
        extendsList.forEach((extendedModel) => {
          if (typeof extendedModel === "string") {
            dependencies.push(extendedModel);
          }
        });
      }

      // Add component schema dependencies
      if (model.model.contents) {
        model.model.contents.forEach((content) => {
          if (
            content["@type"] === "Component" &&
            content.schema &&
            typeof content.schema === "string"
          ) {
            dependencies.push(content.schema);
          }
        });
      }

      return [...new Set(dependencies)]; // Remove duplicates
    },

    getDependents: (modelId) => {
      const allModels = get().models;
      const dependents: string[] = [];

      allModels.forEach((model) => {
        const dependencies = get().getDependencies(model.id);
        if (dependencies.includes(modelId)) {
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
          // Circular reference detected
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
          // Search in ID
          if (model.id.toLowerCase().includes(searchTerm)) return true;

          // Search in display names (language map)
          if (model.displayName) {
            const displayNameMatches = Object.values(model.displayName).some(
              (value) => value.toLowerCase().includes(searchTerm)
            );
            if (displayNameMatches) return true;
          }

          // Search in descriptions (language map)
          if (model.description) {
            const descriptionMatches = Object.values(model.description).some(
              (value) => value.toLowerCase().includes(searchTerm)
            );
            if (descriptionMatches) return true;
          }

          return false;
        });
      }

      if (filter.modelType) {
        filtered = filtered.filter(
          (model) => model.model["@type"] === filter.modelType
        );
      }

      if (filter.isValid !== undefined) {
        filtered = filtered.filter((model) => {
          const modelValidation = validation[model.id];
          return modelValidation
            ? modelValidation.isValid === filter.isValid
            : false;
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
        extendsList.forEach((extendedModel) => {
          if (typeof extendedModel === "string") {
            extends_.push(extendedModel);
          }
        });
      }

      // Get components and relationships from contents
      if (model.model.contents) {
        model.model.contents.forEach((content) => {
          if (
            content["@type"] === "Component" &&
            content.schema &&
            typeof content.schema === "string"
          ) {
            components.push(content.schema);
          } else if (
            content["@type"] === "Relationship" &&
            content.target &&
            typeof content.target === "string"
          ) {
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
