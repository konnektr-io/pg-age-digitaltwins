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

        [JsonPropertyName("decommissioned")]
        public bool IsDecommissioned { get; }

        [JsonConstructor]
        public DigitalTwinsModelData(
            string id,
            JsonElement? dtdlModelJson,
            DateTimeOffset? uploadedOn,
            IReadOnlyDictionary<string, string> languageDisplayNames,
            IReadOnlyDictionary<string, string> languageDescriptions,
            bool isDecommissioned,
            string[]? bases
        )
        {
            Id = id;
            DtdlModelJson = dtdlModelJson;
            UploadedOn = uploadedOn;
            LanguageDisplayNames = languageDisplayNames;
            LanguageDescriptions = languageDescriptions;
            IsDecommissioned = isDecommissioned;
            Bases = bases ?? [];
        }

        public DigitalTwinsModelData(Dictionary<string, object?> modelData)
        {
            if (modelData.TryGetValue("id", out var idValue))
            {
                Id = idValue?.ToString()!;
            }
            else
            {
                throw new ArgumentException("Model data must contain an 'id' property.");
            }

            if (modelData.TryGetValue("model", out var modelValue))
            {
                DtdlModel = modelValue?.ToString();
            }

            if (
                modelData.TryGetValue("uploadTime", out var uploadTimeValue)
                && DateTimeOffset.TryParse(uploadTimeValue?.ToString(), out var parsedUploadTime)
            )
            {
                UploadedOn = parsedUploadTime;
            }
            else
            {
                UploadedOn = DateTimeOffset.MinValue;
            }

            if (modelData.TryGetValue("displayName", out var displayNameValue))
            {
                LanguageDisplayNames = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    displayNameValue?.ToString() ?? "{}"
                )!;
            }
            else
            {
                LanguageDisplayNames = new Dictionary<string, string>();
            }

            if (modelData.TryGetValue("description", out var descriptionValue))
            {
                LanguageDescriptions = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    descriptionValue?.ToString() ?? "{}"
                )!;
            }
            else
            {
                LanguageDescriptions = new Dictionary<string, string>();
            }

            if (
                modelData.TryGetValue("decommissioned", out var decommissionedValue)
                && bool.TryParse(decommissionedValue?.ToString(), out var parsedDecommissioned)
            )
            {
                IsDecommissioned = parsedDecommissioned;
            }
            else
            {
                IsDecommissioned = false;
            }

            if (modelData.TryGetValue("bases", out var basesValue))
            {
                Bases =
                    JsonSerializer.Deserialize<string[]>(basesValue?.ToString() ?? "[]")
                    ?? Array.Empty<string>();
            }
            else
            {
                Bases = Array.Empty<string>();
            }
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
        }
    }
}
