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
        public JsonElement DtdlModelJson { get; private set; }

        [JsonIgnore]
        public string DtdlModel
        {
            get => DtdlModelJson.GetRawText();
            set => DtdlModelJson = JsonDocument.Parse(value).RootElement;
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
            JsonElement dtdlModelJson,
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
            Id = modelData["id"]?.ToString()!;
            DtdlModel = modelData["model"]?.ToString()!;
            UploadedOn = DateTimeOffset.Parse(modelData["uploadTime"]?.ToString()!);
            LanguageDisplayNames = JsonSerializer.Deserialize<Dictionary<string, string>>(
                modelData["displayName"]?.ToString()!
            )!;
            LanguageDescriptions = JsonSerializer.Deserialize<Dictionary<string, string>>(
                modelData["description"]?.ToString()!
            )!;
            IsDecommissioned = bool.Parse(modelData["decommissioned"]?.ToString()!);
            Bases =
                JsonSerializer.Deserialize<string[]>(modelData["bases"]?.ToString()!)
                ?? Array.Empty<string>();
        }

        public DigitalTwinsModelData(string dtdlModel)
        {
            DtdlModel = dtdlModel;
            Id = DtdlModelJson.GetProperty("@id")!.GetString()!;
            DtdlModel = dtdlModel;
            UploadedOn = DateTimeOffset.UtcNow;
            if (DtdlModelJson.TryGetProperty("displayName", out JsonElement displayName))
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
            if (DtdlModelJson.TryGetProperty("description", out JsonElement description))
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
