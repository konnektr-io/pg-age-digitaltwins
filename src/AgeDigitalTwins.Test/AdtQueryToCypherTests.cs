using System.Runtime.InteropServices;

namespace AgeDigitalTwins.Test;

public class AdtQueryToCypherTests
{
    [Theory]
    [InlineData("SELECT * FROM DIGITALTWINS", "MATCH (T:Twin) RETURN *")]
    [InlineData("SELECT T FROM DIGITALTWINS T", "MATCH (T:Twin) RETURN T")]
    [InlineData("SELECT * FROM RELATIONSHIPS", "MATCH (:Twin)-[R]->(:Twin) RETURN *")]
    [InlineData(
        "SELECT T.name FROM DIGITALTWINS T WHERE T.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'",
        "MATCH (T:Twin) WHERE T['$metadata']['$model'] = 'dtmi:com:adt:dtsample:room;1' RETURN T.name")]
    [InlineData(
        "SELECT TOP(1) T FROM DIGITALTWINS T WHERE T.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'",
        "MATCH (T:Twin) WHERE T['$metadata']['$model'] = 'dtmi:com:adt:dtsample:room;1' RETURN T LIMIT 1")]
    [InlineData("SELECT COUNT() FROM DIGITALTWINS", "MATCH (T:Twin) RETURN COUNT(*)")]
    [InlineData(
        "SELECT T,R FROM DIGITALTWINS MATCH (current)-[R]->(T) WHERE current.$dtId='root'",
        "MATCH (current:Twin)-[R]->(T:Twin) WHERE current['$dtId']='root' RETURN T,R")]
    [InlineData(
        "SELECT B, R FROM DIGITALTWINS DT JOIN B RELATED DT.has R WHERE DT.$dtId = 'root2'",
        "MATCH (DT:Twin)-[R:has]->(B:Twin) WHERE DT['$dtId'] = 'root2' RETURN B, R")]
    [InlineData(
        "SELECT LightBulb FROM DIGITALTWINS Room JOIN LightPanel RELATED Room.contains JOIN LightBulb RELATED LightPanel.contains WHERE Room.$dtId IN ['room1', 'room2']",
        "MATCH (Room:Twin)-[:contains]->(LightPanel:Twin),(LightPanel:Twin)-[:contains]->(LightBulb:Twin) WHERE Room['$dtId'] IN ['room1', 'room2'] RETURN LightBulb")]
    [InlineData(
        "SELECT LightBulb FROM DIGITALTWINS Building JOIN Floor RELATED Building.contains JOIN Room RELATED Floor.contains JOIN LightPanel RELATED Room.contains JOIN LightBulbRow RELATED LightPanel.contains JOIN LightBulb RELATED LightBulbRow.contains WHERE Building.$dtId = 'Building1'",
        "MATCH (Building:Twin)-[:contains]->(Floor:Twin),(Floor:Twin)-[:contains]->(Room:Twin),(Room:Twin)-[:contains]->(LightPanel:Twin),(LightPanel:Twin)-[:contains]->(LightBulbRow:Twin),(LightBulbRow:Twin)-[:contains]->(LightBulb:Twin) WHERE Building['$dtId'] = 'Building1' RETURN LightBulb")]
    [InlineData(
        "SELECT r, t FROM DIGITALTWINS\n      MATCH (s)<-[r]-(t)\n      WHERE s.$dtId = 'root3'",
        "MATCH (s:Twin)<-[r]-(t:Twin) WHERE s['$dtId'] = 'root3' RETURN r, t")]
    public void ConvertAdtQueryToCypher_ReturnsExpectedCypher(string adtQuery, string expectedCypher)
    {
        var actualCypher = AdtQueryHelpers.ConvertAdtQueryToCypher(adtQuery);
        Assert.Equal(expectedCypher, actualCypher);
    }

}