namespace AgeDigitalTwins.Test
{
    public class SampleData
    {
        public const string DtdlRoom =
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
                        ""@type"": ""Property"",
                        ""name"": ""name"",
                        ""description"": {
                            ""en"": ""The name of the room\n\nThis is a multiline description.""
                        },
                        ""schema"": ""string""
                    },
                    {
                        ""@type"": ""Property"",
                        ""name"": ""description"",
                        ""description"": {
                            ""en"": ""The description of description has a few weird 👽 '/\\characters.""
                        },
                        ""schema"": ""string""
                    },
                    {
                        ""@type"": ""Property"",
                        ""name"": ""temperature"",
                        ""schema"": ""double""
                    },
                    {
                        ""@type"": [""Property"", ""Humidity""],
                        ""name"": ""humidity"",
                        ""schema"": ""double"",
                        ""unit"": ""gramPerCubicMetre""
                    },
                    {
                        ""@type"": ""Property"",
                        ""name"": ""dimensions"",
                        ""schema"": {
                            ""@type"": ""Object"",
                            ""fields"": [
                                {
                                    ""name"": ""length"",
                                    ""schema"": ""double""
                                },
                                {
                                    ""name"": ""width"",
                                    ""schema"": ""double""
                                },
                                {
                                    ""name"": ""height"",
                                    ""schema"": ""double""
                                }
                            ]
                        }
                    },
                    {
                        ""@type"": ""Relationship"",
                        ""@id"": ""dtmi:com:adt:dtsample:room:rel_has_sensors;1"",
                        ""name"": ""rel_has_sensors"",
                        ""displayName"": ""Room has sensors""
                    }
                ]
            }";

        public const string DtdlTemperatureSensor =
            @"{
                ""@id"": ""dtmi:com:adt:dtsample:tempsensor;1"",
                ""@type"": ""Interface"",
                ""@context"": [
                    ""dtmi:dtdl:context;3"",
                    ""dtmi:dtdl:extension:quantitativeTypes;1""
                ],
                ""displayName"": ""Temperature Sensor"",
                ""contents"": [
                    {
                        ""@type"": ""Property"",
                        ""name"": ""name"",
                        ""schema"": ""string""
                    },
                    {
                        ""@type"": ""Property"",
                        ""name"": ""temperature"",
                        ""schema"": ""double""
                    }
                ]
            }";

        public const string TwinRoom1 =
            @"{
                ""$dtId"": ""room1"",
                ""$metadata"": {
                    ""$model"": ""dtmi:com:adt:dtsample:room;1""
                },
                ""name"": ""Room 1"",
                ""description"": ""This is a room."",
                ""temperature"": 22.5,
                ""humidity"": 0.5,
                ""dimensions"": {
                    ""length"": 5.0,
                    ""width"": 4.0,
                    ""height"": 3.0
                }
            }";

        public const string TwinTemperatureSensor1 =
            @"{
                ""$dtId"": ""sensor1"",
                ""$metadata"": {
                    ""$model"": ""dtmi:com:adt:dtsample:tempsensor;1""
                },
                ""name"": ""Temperature Sensor 1"",
                ""temperature"": 22.5
            }";

        public const string DtdlCelestialBody =
            @"{
                ""@context"": ""dtmi:dtdl:context;3"",
                ""@id"": ""dtmi:com:contoso:CelestialBody;1"",
                ""@type"": ""Interface"",
                ""displayName"": ""Celestial body"",
                ""contents"": [
                    {
                        ""@type"": ""Property"",
                        ""name"": ""name"",
                        ""schema"": ""string""
                    },
                    {
                        ""@type"": ""Property"",
                        ""name"": ""mass"",
                        ""schema"": ""double""
                    },
                    {
                        ""@type"": ""Property"",
                        ""name"": ""temperature"",
                        ""schema"": ""double""
                    }
                ]
            }";

        public const string DtdlPlanet =
            @"{
                ""@context"": ""dtmi:dtdl:context;3"",
                ""@id"": ""dtmi:com:contoso:Planet;1"",
                ""@type"": ""Interface"",
                ""displayName"": ""Planet"",
                ""extends"": ""dtmi:com:contoso:CelestialBody;1"",
                ""contents"": [
                    {
                        ""@type"": ""Relationship"",
                        ""name"": ""satellites"",
                        ""target"": ""dtmi:com:contoso:Moon;1"",
                        ""properties"": [
                            {
                                ""@type"": ""Property"",
                                ""name"": ""Distance"",
                                ""schema"": ""double""
                            }
                        ]
                    },
                    {
                        ""@type"": ""Component"",
                        ""name"": ""deepestCrater"",
                        ""schema"": ""dtmi:com:contoso:Crater;1""
                    },
                    {
                        ""@type"": ""Property"",
                        ""name"": ""hasLife"",
                        ""schema"": ""boolean""
                    }
                ]
            }";

        public const string DtdlMoon =
            @"{
                ""@context"": ""dtmi:dtdl:context;3"",
                ""@id"": ""dtmi:com:contoso:Moon;1"",
                ""@type"": ""Interface"",
                ""displayName"": ""Moon"",
                ""extends"": ""dtmi:com:contoso:CelestialBody;1""
            }";

        public const string DtdlHabitablePlanet =
            @"{
                ""@context"": ""dtmi:dtdl:context;3"",
                ""@id"": ""dtmi:com:contoso:HabitablePlanet;1"",
                ""@type"": ""Interface"",
                ""displayName"": ""Habitable Planet"",
                ""extends"": ""dtmi:com:contoso:Planet;1""
            }";

        public const string TwinPlanetEarth =
            @"{
                ""$dtId"": ""earth"",
                ""$metadata"": {
                    ""$model"": ""dtmi:com:contoso:Planet;1""
                },
                ""name"": ""Earth""
            }";

        public const string DtdlCrater =
            @"{
                ""@context"": ""dtmi:dtdl:context;3"",
                ""@id"": ""dtmi:com:contoso:Crater;1"",
                ""@type"": ""Interface"",
                ""displayName"": ""Crater"",
                ""contents"": [
                    {
                        ""@type"": ""Property"",
                        ""name"": ""diameter"",
                        ""schema"": ""double""
                    }
                ]
            }";

        public const string TwinCrater =
            @"{
                ""$dtId"": ""crater1"",
                ""$metadata"": {
                    ""$model"": ""dtmi:com:contoso:Crater;1""
                },
                ""diameter"": 100
            }";
    }
}
