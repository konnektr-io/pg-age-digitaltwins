namespace AgeDigitalTwins.Test;

public class AdtQueryToCypherTests
{
    [Theory]
    [InlineData("SELECT T FROM DIGITALTWINS T", "MATCH (T:Twin) RETURN T")]
    [InlineData("SELECT * FROM DIGITALTWINS", "MATCH (T:Twin) RETURN *")]
    [InlineData("SELECT * FROM RELATIONSHIPS", "MATCH (:Twin)-[R]->(:Twin) RETURN *")]
    [InlineData(
        "SELECT T.name FROM DIGITALTWINS T WHERE T.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'",
        "MATCH (T:Twin) WHERE T['$metadata']['$model'] = 'dtmi:com:adt:dtsample:room;1' RETURN T.name"
    )]
    [InlineData(
        "SELECT * FROM DIGITALTWINS WHERE name = 'foo'",
        "MATCH (T:Twin) WHERE T.name = 'foo' RETURN *"
    )]
    [InlineData(
        "SELECT * FROM DIGITALTWINS WHERE diameter > 2.5",
        "MATCH (T:Twin) WHERE T.diameter > 2.5 RETURN *"
    )]
    [InlineData(
        "SELECT * FROM DIGITALTWINS WHERE $metadata.$model='dtmi:com:adt:dtsample:room;1'",
        "MATCH (T:Twin) WHERE T['$metadata']['$model']='dtmi:com:adt:dtsample:room;1' RETURN *"
    )]
    [InlineData(
        "SELECT * FROM DIGITALTWINS WHERE IS_OF_MODEL('dtmi:com:adt:dtsample:room;1')",
        "MATCH (T:Twin) WHERE testgraph.is_of_model(T,'dtmi:com:adt:dtsample:room;1') RETURN *"
    )]
    [InlineData(
        "SELECT * FROM DIGITALTWINS WHERE STARTS_WITH(name, 'foo')",
        "MATCH (T:Twin) WHERE STARTS_WITH(T.name, 'foo') RETURN *"
    )]
    [InlineData(
        "SELECT $dtId, name FROM DIGITALTWINS WHERE STARTS_WITH(name, 'foo')",
        "MATCH (T:Twin) WHERE STARTS_WITH(T.name, 'foo') RETURN T['$dtId'], T.name"
    )]
    [InlineData(
        "SELECT * FROM DIGITALTWINS WHERE IS_OF_MODEL('dtmi:com:adt:dtsample:room;1') AND name = 'foo'",
        "MATCH (T:Twin) WHERE testgraph.is_of_model(T,'dtmi:com:adt:dtsample:room;1') AND T.name = 'foo' RETURN *"
    )]
    [InlineData(
        "SELECT T FROM DIGITALTWINS T WHERE IS_OF_MODEL(T,'dtmi:com:adt:dtsample:room;1') AND T.name = 'foo'",
        "MATCH (T:Twin) WHERE testgraph.is_of_model(T,'dtmi:com:adt:dtsample:room;1') AND T.name = 'foo' RETURN T"
    )]
    [InlineData(
        "SELECT * FROM RELATIONSHIPS WHERE $sourceId = 'root'",
        "MATCH (:Twin)-[R]->(:Twin) WHERE R['$sourceId'] = 'root' RETURN *"
    )]
    [InlineData(
        "SELECT TOP(1) T FROM DIGITALTWINS T WHERE T.$metadata.$model = 'dtmi:com:adt:dtsample:room;1'",
        "MATCH (T:Twin) WHERE T['$metadata']['$model'] = 'dtmi:com:adt:dtsample:room;1' RETURN T LIMIT 1"
    )]
    [InlineData(
        "SELECT TOP(1) FROM DIGITALTWINS WHERE $metadata.$model = 'dtmi:com:adt:dtsample:room;1'",
        "MATCH (T:Twin) WHERE T['$metadata']['$model'] = 'dtmi:com:adt:dtsample:room;1' RETURN * LIMIT 1"
    )]
    [InlineData("SELECT COUNT() FROM DIGITALTWINS", "MATCH (T:Twin) RETURN COUNT(*)")]
    [InlineData(
        "SELECT T,R FROM DIGITALTWINS MATCH (current)-[R]->(T) WHERE current.$dtId='root'",
        "MATCH (current:Twin)-[R]->(T:Twin) WHERE current['$dtId']='root' RETURN T,R"
    )]
    [InlineData(
        "SELECT T FROM DIGITALTWINS MATCH (E)-[]->(T) WHERE T.$dtId = 'abc' AND E.$dtId != 'def'",
        "MATCH (E:Twin)-[]->(T:Twin) WHERE T['$dtId'] = 'abc' AND NOT (E['$dtId'] = 'def') RETURN T"
    )]
    [InlineData(
        "SELECT T FROM DIGITALTWINS MATCH (E)-[]->(T) WHERE T.$dtId != 'abc' AND E.$dtId != 'def'",
        "MATCH (E:Twin)-[]->(T:Twin) WHERE NOT (T['$dtId'] = 'abc') AND NOT (E['$dtId'] = 'def') RETURN T"
    )]
    [InlineData(
        "SELECT T FROM DIGITALTWINS MATCH (E)-[]->(T) WHERE T.size != 1 AND E.$dtId != 'def'",
        "MATCH (E:Twin)-[]->(T:Twin) WHERE NOT (T.size = 1) AND NOT (E['$dtId'] = 'def') RETURN T"
    )]
    [InlineData(
        "SELECT B, R FROM DIGITALTWINS DT JOIN B RELATED DT.has R WHERE DT.$dtId = 'root2'",
        "MATCH (DT:Twin)-[R:has]->(B:Twin) WHERE DT['$dtId'] = 'root2' RETURN B, R"
    )]
    [InlineData(
        "SELECT B, R FROM DIGITALTWINS MATCH (T)-[R:hasBlob|hasModel]->(B) WHERE T.$dtId = 'root3'",
        // AGE currently doesn't support the pipe operator
        // See https://github.com/apache/age/issues/1714
        // There is an open PR to support this syntax https://github.com/apache/age/pull/2082
        // Until then we need to use a workaround and generate something like this
        "MATCH (T:Twin)-[R]->(B:Twin) WHERE (label(R) = 'hasBlob' OR label(R) = 'hasModel') AND (T['$dtId'] = 'root3') RETURN B, R"
    )]
    [InlineData(
        "SELECT B, R FROM DIGITALTWINS MATCH (T)-[R:hasBlob|hasModel]->(B)-[R2:has]->(T2) WHERE T.$dtId = 'root3'",
        // AGE currently doesn't support the pipe operator
        // See https://github.com/apache/age/issues/1714
        // There is an open PR to support this syntax https://github.com/apache/age/pull/2082
        // Until then we need to use a workaround and generate something like this
        "MATCH (T:Twin)-[R]->(B:Twin)-[R2]->(T2:Twin) WHERE (label(R) = 'hasBlob' OR label(R) = 'hasModel') AND (label(R2) = 'has') AND (T['$dtId'] = 'root3') RETURN B, R"
    )]
    [InlineData(
        "SELECT LightBulb FROM DIGITALTWINS Room JOIN LightPanel RELATED Room.contains JOIN LightBulb RELATED LightPanel.contains WHERE Room.$dtId IN ['room1', 'room2']",
        "MATCH (Room:Twin)-[:contains]->(LightPanel:Twin),(LightPanel:Twin)-[:contains]->(LightBulb:Twin) WHERE Room['$dtId'] IN ['room1', 'room2'] RETURN LightBulb"
    )]
    [InlineData(
        "SELECT LightBulb FROM DIGITALTWINS Building JOIN Floor RELATED Building.contains JOIN Room RELATED Floor.contains JOIN LightPanel RELATED Room.contains JOIN LightBulbRow RELATED LightPanel.contains JOIN LightBulb RELATED LightBulbRow.contains WHERE Building.$dtId = 'Building1'",
        "MATCH (Building:Twin)-[:contains]->(Floor:Twin),(Floor:Twin)-[:contains]->(Room:Twin),(Room:Twin)-[:contains]->(LightPanel:Twin),(LightPanel:Twin)-[:contains]->(LightBulbRow:Twin),(LightBulbRow:Twin)-[:contains]->(LightBulb:Twin) WHERE Building['$dtId'] = 'Building1' RETURN LightBulb"
    )]
    [InlineData(
        "SELECT r, t FROM DIGITALTWINS\n      MATCH (s)<-[r]-(t)\n      WHERE s.$dtId = 'root3'",
        "MATCH (s:Twin)<-[r]-(t:Twin) WHERE s['$dtId'] = 'root3' RETURN r, t"
    )]
    [InlineData(
        "SELECT * FROM RELATIONSHIPS WHERE $sourceId = 'source' AND $targetId = 'target'",
        "MATCH (:Twin)-[R]->(:Twin) WHERE R['$sourceId'] = 'source' AND R['$targetId'] = 'target' RETURN *"
    )]
    [InlineData(
        "SELECT * FROM relationships WHERE $sourceId = 'source' AND $targetId = 'target'",
        "MATCH (:Twin)-[R]->(:Twin) WHERE R['$sourceId'] = 'source' AND R['$targetId'] = 'target' RETURN *"
    )]
    [InlineData(
        "SELECT R.$sourceId, R.$targetId FROM relationships R WHERE R.$sourceId IN ['s1','s2','s3'] AND R.$relationshipName = 'nextActivity'",
        "MATCH (:Twin)-[R]->(:Twin) WHERE R['$sourceId'] IN ['s1','s2','s3'] AND R['$relationshipName'] = 'nextActivity' RETURN R['$sourceId'], R['$targetId']"
    )]
    [InlineData(
        "SELECT $sourceId, $targetId FROM relationships WHERE $sourceId IN ['s1','s2','s3'] AND $relationshipName = 'nextActivity'",
        "MATCH (:Twin)-[R]->(:Twin) WHERE R['$sourceId'] IN ['s1','s2','s3'] AND R['$relationshipName'] = 'nextActivity' RETURN R['$sourceId'], R['$targetId']"
    )]
    [InlineData(
        "SELECT TOP(50) FROM DIGITALTWINS WHERE (CONTAINS(email,'test') OR CONTAINS(name,'test') OR CONTAINS($dtId,'test')) AND (IS_OF_MODEL('dtmi:com:example:identity:User;1') OR IS_OF_MODEL('dtmi:com:example:identity:Invite;1'))",
        "MATCH (T:Twin) WHERE (T.email CONTAINS 'test' OR T.name CONTAINS 'test' OR T['$dtId'] CONTAINS 'test') AND (testgraph.is_of_model(T,'dtmi:com:example:identity:User;1') OR testgraph.is_of_model(T,'dtmi:com:example:identity:Invite;1')) RETURN * LIMIT 50"
    )]
    [InlineData(
        "SELECT TOP (50) twin FROM DIGITALTWINS twin WHERE CONTAINS(twin.$dtId,'test') OR CONTAINS(twin.name,'test') OR CONTAINS(twin.displayName,'test') OR CONTAINS(twin.tag,'test') OR CONTAINS(twin.label,'test')",
        "MATCH (twin:Twin) WHERE twin['$dtId'] CONTAINS 'test' OR twin.name CONTAINS 'test' OR twin.displayName CONTAINS 'test' OR twin.tag CONTAINS 'test' OR twin.label CONTAINS 'test' RETURN twin LIMIT 50"
    )]
    [InlineData(
        "SELECT * FROM DIGITALTWINS WHERE IS_NUMBER(Capacity)",
        "MATCH (T:Twin) WHERE ((toFloat(T.Capacity) IS NOT NULL OR toInteger(T.Capacity) IS NOT NULL) AND NOT (toString(T.Capacity) = T.Capacity)) RETURN *"
    )]
    [InlineData(
        "SELECT * FROM DIGITALTWINS WHERE IS_NUMBER(Capacity) AND Capacity != 0",
        "MATCH (T:Twin) WHERE ((toFloat(T.Capacity) IS NOT NULL OR toInteger(T.Capacity) IS NOT NULL) AND NOT (toString(T.Capacity) = T.Capacity)) AND NOT (T.Capacity = 0) RETURN *"
    )]
    [InlineData(
        "SELECT T FROM DIGITALTWINS T WHERE IS_NUMBER(T.temperature) AND T.temperature > 20.5",
        "MATCH (T:Twin) WHERE ((toFloat(T.temperature) IS NOT NULL OR toInteger(T.temperature) IS NOT NULL) AND NOT (toString(T.temperature) = T.temperature)) AND T.temperature > 20.5 RETURN T"
    )]
    [InlineData(
        "SELECT TOP(1) FROM digitaltwins WHERE ($dtId IN ['00000-0000-0000-00000','test@example.com'] OR email = 'test@example.com') AND $metadata.$model = 'dtmi:com:arcadis:identity:Invite;1'",
        "MATCH (T:Twin) WHERE (T['$dtId'] IN ['00000-0000-0000-00000','test@example.com'] OR T.email = 'test@example.com') AND T['$metadata']['$model'] = 'dtmi:com:arcadis:identity:Invite;1' RETURN * LIMIT 1"
    )]
    public void ConvertAdtQueryToCypher_ReturnsExpectedCypher(
        string adtQuery,
        string expectedCypher
    )
    {
        var actualCypher = AdtQueryHelpers.ConvertAdtQueryToCypher(adtQuery, "testgraph");
        Assert.Equal(expectedCypher, actualCypher);
    }
}
