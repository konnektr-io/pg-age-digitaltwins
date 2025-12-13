using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgeDigitalTwins.Models
{
    public class DigitalTwinsModelData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// The model definition that conforms to Digital Twins Definition Language (DTDL).
        /// </summary>
        /// <remarks>
        /// <see href="https://docs.microsoft.com/en-us/azure/digital-twins/concepts-models">Understand twin models in Azure Digital Twins</see>.
        /// </remarks>
        [JsonPropertyName("model")]
        public JsonElement? DtdlModelJson { get; private set; }

        [JsonIgnore]
        public string? DtdlModel
        {
            get => DtdlModelJson?.GetRawText();
            set => DtdlModelJson = value is not null ? JsonDocument.Parse(value).RootElement : null;
        }

        /// <summary>
        /// The date and time the model was uploaded to the service.
        /// </summary>
        [JsonPropertyName("uploadTime")]
        public DateTimeOffset? UploadedOn { get; }

        /// <summary> A language dictionary that contains the localized display names as specified in the model definition. </summary>
        [JsonPropertyName("displayName")]
        public IReadOnlyDictionary<string, string> LanguageDisplayNames { get; }

        /// <summary> A language dictionary that contains the localized descriptions as specified in the model definition. </summary>
        [JsonPropertyName("description")]
        public IReadOnlyDictionary<string, string> LanguageDescriptions { get; }

        [JsonPropertyName("bases")]
        public string[] Bases { get; set; }

        /// <summary>
        /// Array of all descendant model IDs (models that extend this model, directly or indirectly).
        /// Precomputed at model creation time to optimize inheritance queries.
        /// </summary>
        [JsonPropertyName("descendants")]
        public string[]? Descendants { get; set; }

        [JsonPropertyName("decommissioned")]
        public bool IsDecommissioned { get; }

        [JsonPropertyName("embedding")]
        public double[]? Embedding { get; set; }

        [JsonConstructor]
        public DigitalTwinsModelData(
            string id,
            JsonElement? dtdlModelJson,
            DateTimeOffset? uploadedOn,
            IReadOnlyDictionary<string, string> languageDisplayNames,
            IReadOnlyDictionary<string, string> languageDescriptions,
            bool isDecommissioned,
            string[]? bases,
            string[]? descendants,
            double[]? embedding
        )
        {
            Id = id;
            DtdlModelJson = dtdlModelJson;
            UploadedOn = uploadedOn;
            LanguageDisplayNames = languageDisplayNames;
            LanguageDescriptions = languageDescriptions;
            IsDecommissioned = isDecommissioned;
            Bases = bases ?? [];
            Descendants = descendants;
            Embedding = embedding;
        }

        public DigitalTwinsModelData(Dictionary<string, object?> modelData)
        {
            Id =
                modelData.TryGetValue("id", out var idValue) && idValue is string idString
                    ? idString
                    : throw new ArgumentException("Model data must contain an 'id' property.");

            if (modelData.TryGetValue("model", out var modelValue))
            {
                if (modelValue is JsonElement modelElement)
                {
                    DtdlModelJson = modelElement;
                }
                else if (modelValue is string modelString)
                {
                    // Parse the string to a JsonElement
                    DtdlModel = modelString;
                }
            }

            UploadedOn =
                modelData.TryGetValue("uploadTime", out var uploadTimeValue)
                && uploadTimeValue is string uploadTimeString
                && DateTimeOffset.TryParse(uploadTimeString, out var parsedUploadTime)
                    ? parsedUploadTime
                    : DateTimeOffset.MinValue;

            LanguageDisplayNames =
                modelData.TryGetValue("displayName", out var displayNameValue)
                && displayNameValue is JsonElement displayNameElement
                    ? displayNameElement.ValueKind == JsonValueKind.Object
                        ? displayNameElement
                            .EnumerateObject()
                            .Select(property => new KeyValuePair<string, string>(
                                property.Name,
                                property.Value.GetString()!
                            ))
                            .ToDictionary(x => x.Key, x => x.Value)
                        : new Dictionary<string, string>()
                        {
                            { "en", displayNameElement.GetString()! },
                        }
                    : new Dictionary<string, string>();

            LanguageDescriptions =
                modelData.TryGetValue("description", out var descriptionValue)
                && descriptionValue is JsonElement descriptionElement
                    ? descriptionElement.ValueKind == JsonValueKind.Object
                        ? descriptionElement
                            .EnumerateObject()
                            .Select(property => new KeyValuePair<string, string>(
                                property.Name,
                                property.Value.GetString()!
                            ))
                            .ToDictionary(x => x.Key, x => x.Value)
                        : new Dictionary<string, string>()
                        {
                            { "en", descriptionElement.GetString()! },
                        }
                    : new Dictionary<string, string>();

            IsDecommissioned =
                modelData.TryGetValue("decommissioned", out var decommissionedValue)
                && decommissionedValue is bool decommissionedBool
                && decommissionedBool;

            Bases =
                modelData.TryGetValue("bases", out var basesValue)
                && basesValue is List<object> basesList
                    ? [.. basesList.Select(x => (string)x)]
                    : Array.Empty<string>();

            Descendants =
                modelData.TryGetValue("descendants", out var descendantsValue)
                && descendantsValue is List<object> descendantsList
                    ? [.. descendantsList.Select(x => (string)x)]
                    : null; // null indicates legacy model without precomputed descendants

            Embedding =
                modelData.TryGetValue("embedding", out var embeddingValue)
                && embeddingValue is List<object> embeddingList
                    ? [.. embeddingList.Select(x => Convert.ToDouble(x))]
                    :_ = modelData.TryGetValue("embedding", out var embeddingVectorValue) && embeddingVectorValue is double[] embeddingVector
                        ? embeddingVector
                        : null;
        }

        public DigitalTwinsModelData(string dtdlModel)
        {
            DtdlModel = dtdlModel;
            Id = ((JsonElement)DtdlModelJson!).GetProperty("@id")!.GetString()!;
            DtdlModel = dtdlModel;
            UploadedOn = DateTimeOffset.UtcNow;
            if (
                ((JsonElement)DtdlModelJson!).TryGetProperty(
                    "displayName",
                    out JsonElement displayName
                )
            )
            {
                if (displayName.ValueKind == JsonValueKind.Object)
                {
                    LanguageDisplayNames = displayName
                        .EnumerateObject()
                        .Select(property => new KeyValuePair<string, string>(
                            property.Name,
                            property.Value.GetString()!
                        ))
                        .ToDictionary(x => x.Key, x => x.Value);
                }
                else if (displayName.ValueKind == JsonValueKind.String)
                {
                    LanguageDisplayNames = new Dictionary<string, string>()
                    {
                        { "en", displayName.GetString()! },
                    };
                }
            }
            LanguageDisplayNames ??= new Dictionary<string, string>();
            if (
                ((JsonElement)DtdlModelJson!).TryGetProperty(
                    "description",
                    out JsonElement description
                )
            )
            {
                if (description.ValueKind == JsonValueKind.Object)
                {
                    LanguageDescriptions = description
                        .EnumerateObject()
                        .Select(property => new KeyValuePair<string, string>(
                            property.Name,
                            property.Value.GetString()!
                        ))
                        .ToDictionary(x => x.Key, x => x.Value);
                }
                else if (description.ValueKind == JsonValueKind.String)
                {
                    LanguageDescriptions = new Dictionary<string, string>()
                    {
                        { "en", description.GetString()! },
                    };
                }
            }
            LanguageDescriptions ??= new Dictionary<string, string>();
            IsDecommissioned = false;
            Bases = Array.Empty<string>();
            IsDecommissioned = false;
            Bases = Array.Empty<string>();
            Descendants = null; // Will be computed after model creation
            Embedding = null;
        }
    }
}
