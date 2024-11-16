
namespace AgeDigitalTwins.Tests
{
    public class SampleData
    {
        public const string DtdlSample =
            @"{
                ""@id"": ""dtmi:com:adt:dtsample:room;1"",
                ""@type"": ""Interface"",
                ""@context"": [
                    ""dtmi:dtdl:context;3"",
                    ""dtmi:dtdl:extension:quantitativeTypes;1""
                ],
                ""displayName"": ""Room"",
                ""contents"": [
                    {
                        ""@type"": [""Property"", ""Humidity""],
                        ""name"": ""humidity"",
                        ""schema"": ""double"",
                        ""unit"": ""gramPerCubicMetre""
                    },
                    {
                        ""@type"": ""Relationship"",
                        ""@id"": ""dtmi:com:adt:dtsample:room:rel_has_sensors;1"",
                        ""name"": ""rel_has_sensors"",
                        ""displayName"": ""Room has sensors""
                    }
                ]
            }";

    }
}