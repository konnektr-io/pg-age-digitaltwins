using System.Text.Json;
using Npgsql.Age;
using Npgsql.Age.Types;

namespace AgeDigitalTwins.Test;

public class QueryConversionTests
{
    [Fact]
    public void ConvertAgtypeToObject_Vertex_ExtractsProperties()
    {
        var agtype = new Agtype(
            """{"$type":"vertex","id":1,"label":"Twin","properties":{"$dtId":"twin1","name":"Test"}}"""
        );
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        var dict = Assert.IsType<Dictionary<string, object?>>(value);
        Assert.Equal("twin1", dict["$dtId"]);
        Assert.Equal("Test", dict["name"]);
        Assert.Equal(2, count);
    }

    [Fact]
    public void ConvertAgtypeToObject_Vertex_ExtractsPropertiesWithMetadata()
    {
        var agtype = new Agtype(
            """{"$type":"vertex","id":1,"label":"Twin","properties":{"$dtId":"room1","$metadata":{"$model":"dtmi:com:example:Room;1"},"name":"Room 1","temperature":22.5}}"""
        );
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        var dict = Assert.IsType<Dictionary<string, object?>>(value);
        Assert.Equal("room1", dict["$dtId"]);
        Assert.Equal("Room 1", dict["name"]);
        Assert.Equal(22.5, dict["temperature"]);
        var metadata = Assert.IsType<Dictionary<string, object?>>(dict["$metadata"]);
        Assert.Equal("dtmi:com:example:Room;1", metadata["$model"]);
        Assert.Equal(4, count);
    }

    [Fact]
    public void ConvertAgtypeToObject_Edge_ExtractsProperties()
    {
        var agtype = new Agtype(
            """{"$type":"edge","id":1,"label":"rel_has_sensors","startid":1,"endid":2,"properties":{"$relationshipId":"rel1","$sourceId":"room1","$targetId":"sensor1","$relationshipName":"rel_has_sensors"}}"""
        );
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        var dict = Assert.IsType<Dictionary<string, object?>>(value);
        Assert.Equal("rel1", dict["$relationshipId"]);
        Assert.Equal("room1", dict["$sourceId"]);
        Assert.Equal("sensor1", dict["$targetId"]);
        Assert.Equal("rel_has_sensors", dict["$relationshipName"]);
        Assert.Equal(4, count);
    }

    [Fact]
    public void ConvertAgtypeToObject_ArrayOfVertices_ExtractsEachProperties()
    {
        var agtype = new Agtype(
            """[{"$type":"vertex","id":1,"label":"Twin","properties":{"$dtId":"sensor1","temperature":25.0}},{"$type":"vertex","id":2,"label":"Twin","properties":{"$dtId":"sensor2","temperature":30.0}}]"""
        );
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        var list = Assert.IsType<List<object?>>(value);
        Assert.Equal(2, list.Count);

        var first = Assert.IsType<Dictionary<string, object?>>(list[0]);
        Assert.Equal("sensor1", first["$dtId"]);
        Assert.Equal(25.0, first["temperature"]);

        var second = Assert.IsType<Dictionary<string, object?>>(list[1]);
        Assert.Equal("sensor2", second["$dtId"]);
        Assert.Equal(30.0, second["temperature"]);
    }

    [Fact]
    public void ConvertAgtypeToObject_ArrayOfMaps_ReturnsMapsAsIs()
    {
        var agtype = new Agtype(
            """[{"$dtId":"sensor1","temperature":25.0},{"$dtId":"sensor2","temperature":30.0}]"""
        );
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        var list = Assert.IsType<List<object?>>(value);
        Assert.Equal(2, list.Count);

        var first = Assert.IsType<Dictionary<string, object?>>(list[0]);
        Assert.Equal("sensor1", first["$dtId"]);
        Assert.Equal(25.0, first["temperature"]);
    }

    [Fact]
    public void ConvertAgtypeToObject_EmptyArray_ReturnsEmptyList()
    {
        var agtype = new Agtype("""[]""");
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        var list = Assert.IsType<List<object?>>(value);
        Assert.Empty(list);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ConvertAgtypeToObject_Map_ReturnsDictionary()
    {
        var agtype = new Agtype("""{"$dtId":"twin1","name":"Test"}""");
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        var dict = Assert.IsType<Dictionary<string, object?>>(value);
        Assert.Equal("twin1", dict["$dtId"]);
        Assert.Equal("Test", dict["name"]);
        Assert.Equal(2, count);
    }

    [Fact]
    public void ConvertAgtypeToObject_NestedMap_ReturnsNestedDictionary()
    {
        var agtype = new Agtype(
            """{"name":"Test","metadata":{"key":"value","nested":{"inner":42}}}"""
        );
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        var dict = Assert.IsType<Dictionary<string, object?>>(value);
        Assert.Equal("Test", dict["name"]);
        var metadata = Assert.IsType<Dictionary<string, object?>>(dict["metadata"]);
        Assert.Equal("value", metadata["key"]);
        var nested = Assert.IsType<Dictionary<string, object?>>(metadata["nested"]);
        Assert.Equal(42, nested["inner"]);
    }

    [Fact]
    public void ConvertAgtypeToObject_String_ReturnsString()
    {
        var agtype = new Agtype("\"hello\"");
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        Assert.Equal("hello", value);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ConvertAgtypeToObject_StringBoolean_ReturnsBool()
    {
        var agtype = new Agtype("\"true\"");
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        Assert.Equal(true, value);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ConvertAgtypeToObject_StringFalseBoolean_ReturnsBool()
    {
        var agtype = new Agtype("\"false\"");
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        Assert.Equal(false, value);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ConvertAgtypeToObject_Integer_ReturnsInt()
    {
        var agtype = new Agtype("42");
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        Assert.Equal(42, value);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ConvertAgtypeToObject_Double_ReturnsDouble()
    {
        var agtype = new Agtype("3.14");
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        Assert.Equal(3.14, value);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ConvertAgtypeToObject_True_ReturnsBool()
    {
        var agtype = new Agtype("true");
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        Assert.Equal(true, value);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ConvertAgtypeToObject_False_ReturnsBool()
    {
        var agtype = new Agtype("false");
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        Assert.Equal(false, value);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ConvertAgtypeToObject_Null_ReturnsNull()
    {
        var agtype = new Agtype("null");
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        Assert.Null(value);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ConvertAgtypeToObject_Path_PassesThroughAsMap()
    {
        var agtype = new Agtype(
            """{"$type":"path","segments":[]}"""
        );
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        var dict = Assert.IsType<Dictionary<string, object?>>(value);
        Assert.Equal("path", dict["$type"]);
        var segments = Assert.IsType<List<object?>>(dict["segments"]);
        Assert.Empty(segments);
    }

    [Fact]
    public void ConvertAgtypeToObject_MixedArray_HandlesEachElementCorrectly()
    {
        var agtype = new Agtype(
            """[42,"hello",true,{"$type":"vertex","id":1,"label":"Twin","properties":{"$dtId":"twin1"}},null]"""
        );
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        var list = Assert.IsType<List<object?>>(value);
        Assert.Equal(5, list.Count);
        Assert.Equal(42, list[0]);
        Assert.Equal("hello", list[1]);
        Assert.Equal(true, list[2]);
        Assert.IsType<Dictionary<string, object?>>(list[3]);
        Assert.Null(list[4]);
    }

    [Fact]
    public void ConvertAgtypeToObject_TwinWithNestedArrays_ReturnsCorrectStructure()
    {
        var agtype = new Agtype(
            """{"$type":"vertex","id":1,"label":"Twin","properties":{"$dtId":"twin1","tags":["a","b","c"],"counts":[1,2,3]}}"""
        );
        var (value, count) = AgeDigitalTwinsClient.ConvertAgtypeToObject(agtype);
        var dict = Assert.IsType<Dictionary<string, object?>>(value);
        Assert.Equal("twin1", dict["$dtId"]);

        var tags = Assert.IsType<List<object?>>(dict["tags"]);
        Assert.Equal(3, tags.Count);
        Assert.Equal("a", tags[0]);
        Assert.Equal("b", tags[1]);
        Assert.Equal("c", tags[2]);

        var counts = Assert.IsType<List<object?>>(dict["counts"]);
        Assert.Equal(3, counts.Count);
        Assert.Equal(1, counts[0]);
        Assert.Equal(2, counts[1]);
        Assert.Equal(3, counts[2]);
    }
}
