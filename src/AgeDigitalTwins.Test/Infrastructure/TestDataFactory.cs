using AgeDigitalTwins.Models;

namespace AgeDigitalTwins.Test.Infrastructure;

/// <summary>
/// Factory class for creating consistent test data across all job tests.
/// </summary>
public static class TestDataFactory
{
    /// <summary>
    /// Factory methods for creating DTDL models.
    /// </summary>
    public static class Models
    {
        /// <summary>
        /// Creates a simple DTDL model with a single string property.
        /// </summary>
        public static string CreateSimpleModel(string? id = null)
        {
            id ??= $"dtmi:example:TestModel{Guid.NewGuid():N};1";

            return $$"""
                {
                    "@id": "{{id}}",
                    "@type": "Interface",
                    "@context": "dtmi:dtdl:context;2",
                    "contents": [
                        {
                            "@type": "Property",
                            "name": "testProperty",
                            "schema": "string"
                        }
                    ]
                }
                """;
        }

        /// <summary>
        /// Creates a DTDL model with a relationship to another model.
        /// </summary>
        public static string CreateModelWithRelationship(string? id = null, string? targetId = null)
        {
            id ??= $"dtmi:example:ModelWithRel{Guid.NewGuid():N};1";
            targetId ??= $"dtmi:example:TargetModel{Guid.NewGuid():N};1";

            return $$"""
                {
                    "@id": "{{id}}",
                    "@type": "Interface",
                    "@context": "dtmi:dtdl:context;2",
                    "contents": [
                        {
                            "@type": "Property",
                            "name": "testProperty",
                            "schema": "string"
                        },
                        {
                            "@type": "Relationship",
                            "name": "relatesTo",
                            "target": "{{targetId}}"
                        }
                    ]
                }
                """;
        }

        /// <summary>
        /// Creates a set of related DTDL models.
        /// </summary>
        public static string[] CreateModelSet(int count = 2)
        {
            var models = new string[count];
            for (int i = 0; i < count; i++)
            {
                models[i] = CreateSimpleModel($"dtmi:example:TestModel{i + 1};1");
            }
            return models;
        }
    }

    /// <summary>
    /// Factory methods for creating digital twins.
    /// </summary>
    public static class Twins
    {
        /// <summary>
        /// Creates a simple twin object with test properties.
        /// </summary>
        public static object CreateSimpleTwin(string modelId)
        {
            return new { testProperty = $"test-value-{Guid.NewGuid():N}" };
        }

        /// <summary>
        /// Creates a twin JSON string with proper structure.
        /// </summary>
        public static string CreateTwinJson(
            string twinId,
            string modelId,
            object? properties = null
        )
        {
            properties ??= CreateSimpleTwin(modelId);

            return $$"""
                {
                    "$dtId": "{{twinId}}",
                    "$metadata": { "$model": "{{modelId}}" },
                    "testProperty": "{{((dynamic)properties).testProperty}}"
                }
                """;
        }
    }

    /// <summary>
    /// Factory methods for creating import job test data.
    /// </summary>
    public static class ImportData
    {
        /// <summary>
        /// Creates valid ND-JSON import data with header, models, twins, and relationships.
        /// </summary>
        public static string CreateValidNdJson()
        {
            return """
                {"Section": "Header"}
                {"fileVersion": "1.0.0", "author": "test", "organization": "test"}
                {"Section": "Models"}
                {"@id":"dtmi:example:Model1;1","@type":"Interface","@context":"dtmi:dtdl:context;2","contents":[{"@type":"Property","name":"prop1","schema":"string"},{"@type":"Relationship","name":"relatesTo","target":"dtmi:example:Model2;1"}]}
                {"@id":"dtmi:example:Model2;1","@type":"Interface","@context":"dtmi:dtdl:context;2","contents":[{"@type":"Property","name":"prop2","schema":"string"}]}
                {"Section": "Twins"}
                {"$dtId":"twin1","$metadata":{"$model":"dtmi:example:Model1;1"},"prop1":"value1"}
                {"$dtId":"twin2","$metadata":{"$model":"dtmi:example:Model2;1"},"prop2":"value2"}
                {"Section": "Relationships"}
                {"$sourceId":"twin1","$relationshipId":"rel1","$targetId":"twin2","$relationshipName":"relatesTo"}
                """;
        }

        /// <summary>
        /// Creates ND-JSON data with intentional errors for testing error handling.
        /// </summary>
        public static string CreateInvalidNdJson()
        {
            return """
                {"Section": "Header"}
                {"fileVersion": "1.0.0", "author": "test", "organization": "test"}
                {"Section": "Models"}
                {"@id":"dtmi:example:ValidModel;1","@type":"Interface","@context":"dtmi:dtdl:context;2"}
                {"Section": "Twins"}
                {"$dtId":"validTwin","$metadata":{"$model":"dtmi:example:ValidModel;1"}}
                {"$dtId":"invalidTwin","$metadata":{"$model":"dtmi:example:NonExistentModel;1"}}
                """;
        }

        /// <summary>
        /// Creates minimal header-only ND-JSON data.
        /// </summary>
        public static string CreateMinimalHeader()
        {
            return """
                {"Section": "Header"}
                {"fileVersion": "1.0.0", "author": "test", "organization": "test"}
                """;
        }

        /// <summary>
        /// Creates ND-JSON data with only models section.
        /// </summary>
        public static string CreateModelsOnly()
        {
            return """
                {"Section": "Header"}
                {"fileVersion": "1.0.0", "author": "test", "organization": "test"}
                {"Section": "Models"}
                {"@id":"dtmi:example:Model1;1","@type":"Interface","@context":"dtmi:dtdl:context;2"}
                {"@id":"dtmi:example:Model2;1","@type":"Interface","@context":"dtmi:dtdl:context;2"}
                """;
        }
    }
}
